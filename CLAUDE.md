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
├── Client/          – ArgusClient
├── Extensions/      – DI extension methods
├── Middleware/      – Middleware implementations such as ArgusLoggingMiddleware
├── Protocol/        – ArgusMessage, ArgusRequest, ArgusResponse, ArgusVerb, ArgusStatusCode, ArgusHeaderNames
├── Routing/         – ArgusRouter, route templates, modules
├── Serialization/   – Request/response serializers, body serializer registry, chunked encoding
└── Server/          – ArgusPipeHostBackgroundService, ArgusPipeHostOptions
```

## Architecture

### IPC Protocol (ARGUS/1.0)

Text-based request/response wire format transmitted over named pipes.

- **Request line**: `{VERB} {ROUTE} ARGUS/1.0`
- **Response status line**: `ARGUS/1.0 {StatusCode} {ReasonPhrase}`
- **Standard headers**: `X-Correlation-Token`, `X-Timestamp`, `Content-Length`, `Content-Type`
- **Verbs** (`ArgusVerb`): GET, POST, PUT, PATCH, HEAD, DELETE
- **Status codes** (`ArgusStatusCode`): 200 Ok, 201 Created, 204 NoContent, 400 BadRequest, 401 Unauthorized, 403 Forbidden, 404 NotFound, 406 NotAcceptable, 409 Conflict, 422 UnprocessableEntity, 500 InternalServerError, 501 NotImplemented, 503 ServiceUnavailable

### Routing

`ArgusRouter` matches verb + route template and dispatches to an `ArgusHandlerDelegate`. Supports literal segments, `{param}` parameters, `{param:Guid}` and `{param:ShortGuid}` constrained parameters (case-insensitive matching on literals). Modules implement `IArgusModule.AddRoutes(IArgusRouteBuilder)` to register endpoints.

### Serialization

`ArgusRequestSerializer` and `ArgusResponseSerializer` in `/Serialization/`. Each provides a synchronous string API and a `StreamWriter`/`StreamReader` overload for pipe I/O. `IArgusBodySerializer` and `IArgusBodySerializerRegistry` support content-type-based serializer resolution. `ArgusChunkedEncoding` handles chunked transfer encoding for streaming bodies.

### Client

`ArgusClient` creates a `NamedPipeClientStream` per request, serializes via `ArgusRequestSerializer`, reads the response via `ArgusResponseSerializer`. Provides typed convenience methods (`GetAsync`, `PostAsync`, `PutAsync`, `PatchAsync`, `DeleteAsync`, `HeadAsync`) with optional query parameters, per-request timeouts, and streaming body support.

### Server

`ArgusPipeHostBackgroundService` extends `BackgroundService`, listens on a named pipe, deserializes incoming requests, and routes them through `ArgusRouter`.

### DI Integration

- `services.AddArgusModules()` — scans the calling assembly for `IArgusModule` implementations, registers them as transient, and registers `ArgusRouter` as a singleton that wires up all module routes.
- `services.AddArgusPipeHost(Action<ArgusPipeHostOptions>?)` — registers `ArgusPipeHostBackgroundService` as a hosted service with optional configuration.
- `services.AddArgusClient(string pipeName, TimeSpan? defaultTimeout)` — registers `IArgusClient` as transient, using `IArgusBodySerializerRegistry` if available.
- `services.AddArgusPlainTextProtocol()` — registers `PlainTextArgusBodySerializer`, the enumerable `IArgusBodySerializer`, and `IArgusBodySerializerRegistry`.
- `services.AddArgusBodySerializer<T>()` — registers an additional `IArgusBodySerializer` implementation (deduplicated via `TryAddEnumerable`).

## Git Conventions

- **No co-author trailer**: Do not add a `Co-Authored-By` line for Claude in commit messages.

## Code Conventions

- **language**: C#, no using python
- **Explicit usings**: `ImplicitUsings` is disabled — all `using` directives must be written explicitly.
- **LangVersion**: `14.0`
- **Nullable**: `disable` in both `ArgusTransfer` and `ArgusTransfer.Tests`
- **XML doc comments**: required on all public types and members.
- **License header**: every `.cs` file starts with the Apache-2.0 copyright block.

## Testing Conventions

- Test class naming: `{ClassName}TestFixture`
- Test method naming: `Verify_that_{scenario_in_snake_case}`
- Test convention: use Nunit with `Assert.That` syntax
