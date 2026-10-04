# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ArgusTransfer is a framework that provides routing for named pipes inspired by the HTTP 1.1 request/response protocol. It defines a text-based wire format (ARGUS/1.0), a verb+route routing engine, request/response serialization, and a named-pipe client and server host that integrates with `Microsoft.Extensions.Hosting`.

## Commands

```bash
# Build
dotnet build ArgusTransfer.sln

# Run all tests
dotnet test ArgusTransfer.sln

# Run tests with coverage (as CI does)
dotnet test ArgusTransfer.sln --no-restore --no-build --verbosity normal /p:CollectCoverage=true /p:CoverletOutput="../CoverageResults/" /p:MergeWith="../CoverageResults/coverage.json" /p:CoverletOutputFormat="opencover,json"

# Run a single test by name filter
dotnet test ArgusTransfer.Tests --filter "FullyQualifiedName~Verify_that_HealthEndPoint_can_be_read"

```

## Build & Verification Workflow

After making code changes, follow this verification sequence before considering work complete:

1. **Build and inspect warnings**: Run `dotnet build ArgusTransfer.sln`. Inspect the output for all warnings (CSxxxx, CAxxxx, IDExxxx). Fix all warnings before proceeding.
2. **Run tests**: Run `dotnet test ArgusTransfer.sln`. All tests must pass.
3. **Format check**: Run `dotnet format ArgusTransfer.sln --verify-no-changes`. Fix any formatting violations reported.
4. **Final strict build**: Run `dotnet build ArgusTransfer.sln -warnaserror` as a final pass. The build must succeed with zero warnings and zero errors.

## Solution Structure

| Project | Framework | Role |
|---|---|---|
| `ArgusTransfer` | net10.0 | Protocol, Routing, Serialization, Named-pipe client + server host |
| `ArgusTransfer.Tests` | net10.0 | Tests for ArgusTransfer |
| `ArgusTransfer.Sample` | net10.0 | A sample ArgusModule and Client to demonstrate its use |

### Key Directories

```
ArgusTransfer/
├── Authentication/  – IArgusAuthenticationHandler, ArgusAuthenticationResult/Status/Options, endpoint extensions
├── Client/          – ArgusClient
├── Extensions/      – DI extension methods
├── Middleware/      – Middleware implementations: ArgusLoggingMiddleware, ArgusExceptionHandlerMiddleware (+ options), ArgusAuthenticationMiddleware
├── Protocol/        – ArgusMessage, ArgusRequest, ArgusResponse, ArgusVerb, ArgusStatusCode, ArgusHeaderNames, ArgusProblemDetails
├── Routing/         – ArgusRouter, route templates, modules
├── Serialization/   – Request/response serializers, body serializer registry, chunked encoding
└── Server/          – ArgusPipeHostBackgroundService, ArgusPipeHostOptions
```

## Architecture

### IPC Protocol (ARGUS/1.0)

Text-based request/response wire format transmitted over named pipes.

- **Request line**: `{VERB} {ROUTE} ARGUS/1.0`, exactly three space-separated parts. The route is percent-encoded by the internal `ArgusRouteEncoding` (path: `%`, space and control characters; a query embedded in the route: space and control characters only) and decoded on parse (never `%2F`), so the server sees exactly the client's `Route` and CR/LF in a route cannot inject headers
- **Header validation**: `ArgusWireFormat.AppendStandardHeaders` rejects header names that are not visible-ASCII tokens without `:` and values containing CR, LF or NUL (`InvalidOperationException`); the host checks a handler's response with `GetHeaderError` before writing and answers invalid headers with the generic 500
- **Response status line**: `ARGUS/1.0 {StatusCode} {ReasonPhrase}`
- **Standard headers**: `X-Correlation-Token`, `X-Timestamp`, `Content-Length`, `Content-Type`, `Accept`, `Authorization`, `Content-Encoding`, `Accept-Encoding`
- **Accept negotiation**: `ArgusRequest` sets no `Accept` by default (any type). The host negotiates the response serializer with the internal `ArgusContentNegotiation.SelectSerializer(registry, accept)`: comma-separated media ranges with `q` values (`q=0` excludes; `type/*`, `*/*` and `*` supported; other parameters ignored; malformed ranges skipped; no valid range = no preference = `DefaultSerializer`); a serializer's quality comes from the most specific matching range; ties: more specific range, then header order, then the default serializer. 406 only when nothing is acceptable. The negotiated content type is passed to `ArgusResponseSerializer.WriteAsync(stream, response, accept)`, whose accept overloads use the same negotiation (falling back to the default serializer) and set `Content-Type`, but serialize a response that already has a `Content-Type` with that type's serializer. `IArgusBodySerializerRegistry.GetSerializers()` (default interface implementation returning only `DefaultSerializer`) lets wildcards see all registered serializers; exact types also resolve through `TryGetSerializer`.
- **Authorization**: `Authorization: {scheme} {parameter}` (e.g. `Bearer token123`). Like `Accept`, it lives in `ArgusMessage.Headers` (so all serializer paths round-trip it) with convenience members on `ArgusRequest`: `Authorization` (raw), `AuthorizationScheme` / `AuthorizationParameter` (parsed at the first white space) and `SetAuthorization(scheme, parameter)`. Values with CR/LF are rejected to prevent header injection. Never log it.
- **Verbs** (`ArgusVerb`): GET, POST, PUT, PATCH, HEAD, DELETE
- **Status codes** (`ArgusStatusCode`): 200 Ok, 201 Created, 204 NoContent, 400 BadRequest, 401 Unauthorized, 403 Forbidden, 404 NotFound, 406 NotAcceptable, 409 Conflict, 422 UnprocessableEntity, 500 InternalServerError, 501 NotImplemented, 503 ServiceUnavailable
- **Problem details** (`ArgusProblemDetails`): RFC 7807-style structured error body (`type`, `title`, `status`, `detail`, `instance`, plus `Extensions` written as top-level JSON members via `System.Text.Json`), sent with `Content-Type: application/problem+json`. Static factories (`BadRequest`, `NotFound`, …) return a ready `ArgusResponse`; `ArgusContext.Problem(...)` sets the response and uses the correlation token as `instance`; `ArgusProblemDetails.TryRead(response, out ...)` parses it on the client. The pipe host emits its built-in errors (400 unreadable request, 406 nothing acceptable, 503 concurrency limit / request timeout, 500) as problem details via the internal `ArgusPipeHostBackgroundService.CreateProblemResponse` (correlation token as `instance` and response token when the request was read; 400 has no instance; no body for HEAD); the 503 details are the public constants `ConcurrencyLimitDetail` and `RequestTimeoutDetail`. The router's 404/501 have no body.

### Routing

`ArgusRouter` matches verb + route template and dispatches to an `ArgusHandlerDelegate`. Supports literal segments, `{param}` parameters, `{param:Guid}` and `{param:ShortGuid}` constrained parameters (case-insensitive matching on literals). Modules implement `IArgusModule.AddRoutes(IArgusRouteBuilder)` to register endpoints. `Map*` parses and validates the template once, at registration (`ArgusRouteTemplateParser.Parse` → internal `ArgusRouteTemplate`, constraints resolved then): an unknown or empty constraint name or a parameter without a name throws `ArgumentException`, a `null` template or handler `ArgumentNullException`, so a mistyped template fails at startup rather than during request matching. `RouteAsync` splits the request route once and matches it against the parsed templates (route values allocated only on a match). Each endpoint caches its composed middleware pipeline (`ArgusRouteEndpoint.CachedPipeline`), tagged with the router's global and the endpoint's middleware versions; `UseMiddleware` and `WithMiddleware` (via `ArgusRouteEndpoint.AddMiddleware`) bump a version so the next request rebuilds it, and the versions are read before building so a pipeline built while middleware is added is never cached as current.

### Serialization

`ArgusRequestSerializer` and `ArgusResponseSerializer` in `/Serialization/`. Each provides a synchronous string API, `StreamWriter`/`StreamReader` overloads, and byte-safe `Stream` overloads (`WriteAsync(Stream, …)`, `ReadAsync(Stream, …)`) that `ArgusClient` and the pipe host use for pipe I/O.

`Content-Length` and chunk sizes are **byte** counts. The `Stream` overloads read header lines and exact-length bodies at the byte level via the internal `ArgusWireReader`, and copy streamed bodies as raw bytes (internal byte-level `ArgusChunkedEncoding.WriteChunkedAsync(Stream, Stream)` / `ReadChunkedAsync(ArgusWireReader)`), so binary bodies survive; a truncated body throws `EndOfStreamException`. The `StreamWriter` overloads flush and write bytes to `BaseStream`. The `StreamReader`/string readers count `Content-Length` in UTF-8 bytes (internal `ArgusTextBodyReader`), but their chunked path and the public text `ArgusChunkedEncoding` methods decode chunk data as UTF-8 and only round-trip valid UTF-8 text. Always use the `Stream` overloads for transport code.

Shared request/response wire-format logic lives in the internal `ArgusWireFormat`; `IArgusMessageSource` (implemented by `ArgusWireReader` and `ArgusTextMessageSource`) gives the `Stream` and `StreamReader` readers one read path.

**Compression** (`Content-Encoding`): `IArgusContentEncoding` (built-in `GZipArgusContentEncoding`) and `ArgusCompressionOptions` (`Enabled`, `MinimumBodySize`, `PreferredEncoding`, `Encodings`), exposed as `ArgusClient.Compression` and `ArgusPipeHostOptions.Compression`. The serializers' `Stream` paths are header-driven via their `ContentEncodings` property: a message with a `Content-Encoding` header is compressed on write (streamed bodies on the fly through the internal `ArgusChunkedWriteStream`) and decompressed on read, after which the header is removed; the text-based paths throw `InvalidOperationException` for such messages. Negotiation lives in the internal `ArgusCompression`: the client (when enabled) adds `Accept-Encoding` and, above the threshold, `Content-Encoding`; the host always decodes, applies `MaxRequestBodySize` to the decompressed size as well, and compresses responses only when enabled and accepted. Non-streamed messages are written in a single `Write` because the host's pipe is unbuffered: a server that rejects a request after its headers never reads a separately written body. `IArgusBodySerializer` and `IArgusBodySerializerRegistry` support content-type-based serializer resolution. `ArgusChunkedEncoding` handles chunked transfer encoding for streaming bodies.

### Client

`ArgusClient` creates a `NamedPipeClientStream` (with `PipeOptions.Asynchronous`) per request, serializes via `ArgusRequestSerializer`, reads the response via `ArgusResponseSerializer`. It reads the response **while** writing the request (`ExchangeAsync`): the host may answer early -- e.g. a 400 for an oversized header block or body -- without consuming the rest of the request, and since the host pipe is unbuffered, writing first and reading afterwards would block both sides until the timeout. On an early response the remaining write is cancelled; if the write fails after the server answered, the response still wins. Provides typed convenience methods (`GetAsync`, `PostAsync`, `PutAsync`, `PatchAsync`, `DeleteAsync`, `HeadAsync`) with optional query parameters, per-request timeouts, and streaming body support.

`ArgusClient.MaxResponseBodySize` (default 0 = unlimited; negative throws) is passed to `ArgusResponseSerializer.ReadAsync(stream, maxBodySize, token)` (overloads with `maxBodySize` added next to the original two-parameter ones for binary compatibility), so the shared wire-format path rejects a `Content-Length` above the limit before reading, stops chunked bodies at the limit, and limits the decompressed size, each with `ArgusProtocolException`.

Optional retry (`ArgusClient.RetryPolicy`, `null` = disabled) via `ArgusRetryPolicy` (`MaxRetries`, `InitialDelay`, `MaxDelay`, `RetryBackoffStrategy` Fixed/Linear/Exponential, `ShouldRetry` predicate defaulting to `IOException`). Implemented in-house rather than with Polly to avoid forcing a dependency on library consumers. Rules: failures before the request is written are always retryable; after it is written only idempotent verbs (GET/HEAD/PUT/DELETE) are retried unless `RetryNonIdempotentRequests` is set; a streamed body is only retried after writing if seekable (rewound); `TimeoutException`/`OperationCanceledException` are never retried; the timeout is a budget for the whole call including retries. Retries are logged as warnings through `ArgusClient.Logger`. `ArgusResponseSerializer.ReadAsync` throws `EndOfStreamException` (an `IOException`) when the connection closes before a status line, so a dropped connection is distinguishable from a malformed response (`FormatException`).

### Server

`ArgusPipeHostBackgroundService` extends `BackgroundService`, listens on a named pipe, deserializes incoming requests, and routes them through `ArgusRouter`.

Exception handling is layered: if routing throws (anything except `OperationCanceledException`, which maps to 503 on timeout), the host logs it and returns a generic 500 `ArgusProblemDetails` response (no exception details) instead of dropping the connection; dropping it would make clients with a retry policy re-execute the request. `ArgusExceptionHandlerMiddleware` customizes this inside the pipeline (`IncludeExceptionDetails` for development) and rethrows cancellation of `ArgusContext.RequestAborted` so timeouts still reach the host. Both build the response via the internal `ArgusExceptionHandlerMiddleware.CreateErrorResponse`. A pipeline that completes without setting `ArgusContext.Response` is logged as an error and also answered with the generic 500 (never a closed connection).

Connection limits: `ArgusPipeHostOptions.MaxConcurrentConnections` (default 100) is enforced by a connection semaphore acquired *after* the next pipe instance is created and *before* `WaitForConnectionAsync`, so at the limit new connections are not read (no 503 before the request is read, which would deadlock on the unbuffered pipe). The next instance must exist while waiting: on Linux/macOS (Unix domain sockets) the listening socket is shared by all instances of a pipe name and closes when the last one is disposed, which would reset clients queued in the backlog. Clients at the limit either wait in `ConnectAsync` or connect immediately (backlog on Unix; the ready instance on Windows) and wait to be read -- tests must not assume a connect timeout; `MaxConcurrentRequests` still limits processing. `RequestReadTimeout` (default 30 s) bounds receiving the complete request; on expiry the host logs a warning and closes the connection. `StopAsync` drains requests for `ShutdownDrainTimeout`, then cancels them and waits `CancellationGracePeriod` (internal, default 5 s); the drain token source and both semaphores are disposed only when every request task has finished -- otherwise disposal is skipped with a warning, so a handler that ignores cancellation can still release them. The accept loop (`AcceptConnectionAsync`) survives pipe errors: any exception other than cancellation on shutdown is logged and retried with exponential backoff (`AcceptRetryInitialDelay`, internal, default 1 s, doubling up to 30 s, reset after a successful accept), so a transient error never stops the host.

Header limits: the serializers' `MaxHeaderSize` (default 32 KB; host sets it from `ArgusPipeHostOptions.MaxRequestHeaderSize`) bounds the request/status line plus all header lines via the internal `ArgusHeaderBudget`; `ArgusWireReader.ReadLineAsync(maxLength, …)` stops buffering as soon as a line exceeds its limit (`ArgusLineTooLongException`, an internal `ArgusProtocolException`). Chunk-size lines on the byte path are capped at 1 KB. The string reader is not limited.

Request rejection: readers throw `ArgusProtocolException` (derives from `InvalidOperationException`) for violations by the received message -- body or decompressed size over the limit, unsupported or corrupt `Content-Encoding`. The host answers only `ArgusProtocolException` and `FormatException` (malformed request) with 400 and the message; any other failure is a server fault (500, or a logged closed connection once the response has started). A response whose `Content-Encoding` the host cannot produce is replaced by the generic 500 before writing. Writing-side and configuration errors (unsupported encoding on an outgoing message, text overloads with a compressed body, unknown preferred encoding) stay plain `InvalidOperationException`.

Authentication: `ArgusAuthenticationMiddleware` calls the singleton `IArgusAuthenticationHandler` for every routed request, sets `ArgusContext.User` (a `ClaimsPrincipal`) on success, and short-circuits endpoints that require authentication with a 401 (`NoResult`/`Failure`, or a `null` result) or 403 (`Forbidden`) problem-details response. Requirement = `ArgusAuthenticationOptions.RequireAuthentication` (default `true`), overridden per endpoint by the `"authorize"` metadata (`.RequireAuthentication()` / `.AllowAnonymous()`); an unparsable metadata value requires authentication. Global middleware order built by `AddArgusModules()`: exception handler → authentication → module middleware → endpoint middleware.

### DI Integration

- `services.AddArgusModules()` — scans the calling assembly for `IArgusModule` implementations, registers them as transient, and registers `ArgusRouter` as a singleton that wires up all module routes. Marked `[MethodImpl(MethodImplOptions.NoInlining)]` so `Assembly.GetCallingAssembly()` cannot resolve to the wrong assembly when the JIT would inline it.
- `services.AddArgusModules(params Assembly[] assemblies)` — the same, but scans the given assemblies explicitly (modules in other assemblies). Module types are registered with `TryAddEnumerable` and the router with `TryAddSingleton`, so repeated or combined calls register each module and the router once.
- `services.AddArgusPipeHost(Action<ArgusPipeHostOptions>?)` — registers `ArgusPipeHostBackgroundService` as a hosted service with optional configuration.
- `services.AddArgusClient(string pipeName, TimeSpan? defaultTimeout)` — registers `IArgusClient` as transient, using `IArgusBodySerializerRegistry` if available.
- `services.AddArgusClient(string pipeName, Action<ArgusClient> configure)` — same, configuring each created client (timeout, retry policy, compression, …).
- `services.AddArgusClient(string pipeName, TimeSpan? defaultTimeout, Action<ArgusRetryPolicy> configureRetry)` — same, plus a configured `ArgusRetryPolicy` and `ILogger<ArgusClient>` when logging is registered (separate overload to keep the original signature binary compatible).
- `services.AddArgusPlainTextProtocol()` — registers `PlainTextArgusBodySerializer`, the enumerable `IArgusBodySerializer`, and `IArgusBodySerializerRegistry`.
- `services.AddArgusBodySerializer<T>()` — registers an additional `IArgusBodySerializer` implementation (deduplicated via `TryAddEnumerable`).
- `services.AddArgusAuthentication<THandler>(Action<ArgusAuthenticationOptions>?)` — registers the handler (singleton, first registration wins) and `ArgusAuthenticationMiddleware`; the router adds it right after the exception handler.
- `services.AddArgusExceptionHandler(Action<ArgusExceptionHandlerOptions>?)` — registers `ArgusExceptionHandlerMiddleware` as a singleton; the `ArgusRouter` factory in `AddArgusModules()` adds it as the outermost global middleware before modules run `AddRoutes`, independent of registration order.

## Documentation Conventions

- **README.md is a landing page**, and also the NuGet package readme (`PackageReadmeFile`): pitch, why/when to use, feature list, install, a quick start, documentation links, contributing, SBOM, license. Do **not** add detailed feature sections to it.
- **Document features in the wiki** (`ArgusTransfer.wiki` repository, pushed to `samgerene/ArgusTransfer.wiki`): add or update the relevant page and the sidebar. A new feature gets at most a one-line entry in the README feature list, linking to its wiki page.
- Links in the README must be absolute URLs (relative links break on nuget.org).
- The README quick start must compile and run: verify it in a scratch console project (`dotnet new console`, project reference to `ArgusTransfer`, package `Microsoft.Extensions.Hosting`) whenever it or the APIs it uses change.

## Git Conventions

- **No co-author trailer**: Do not add a `Co-Authored-By` line for Claude in commit messages.

## Code Conventions

- **language**: C#, no using python
- **Explicit usings**: `ImplicitUsings` is disabled — all `using` directives must be written explicitly.
- **LangVersion**: `14.0`
- **Nullable**: `disable` in both `ArgusTransfer` and `ArgusTransfer.Tests`
- **XML doc comments**: required on all public types and members. **Never use `/// <inheritdoc />`** — always write the full `<summary>`, `<param>`, `<returns>`, and `<exception>` blocks on the implementation, even when they duplicate the interface.
- **License header**: every `.cs` file starts with the Apache-2.0 copyright block.
- **Logging in catch clauses**: always pass the caught exception to the logger (`logger.LogWarning(ex, "...")`), never log only a message (Sonar S6667).
- **No assignments inside conditions**: read the next value before the loop and at the end of its body instead of `while ((x = Read()) != null)` (Sonar S1121).
- **Don't update a `for` loop's counter in its body**: use a `while` loop with an explicit position when the step varies (Sonar S127).
- **Properties must not copy collections**: expose a method (e.g. `GetActiveRequestTasks()`) instead of a property that returns a new array or list on each access (Sonar S2365).

## Testing Conventions

- Test class naming: `{ClassName}TestFixture`
- Test method naming: `Verify_that_{scenario_in_snake_case}`
- Test convention: use Nunit with `Assert.That` syntax
