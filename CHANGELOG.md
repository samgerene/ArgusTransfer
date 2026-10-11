# Changelog

All notable changes to ArgusTransfer are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Numbers such as [#67](https://github.com/samgerene/ArgusTransfer/issues/67) refer to the issue or pull request of the change.

## [Unreleased]

### Added

- `ArgusProblemDetails`: a structured error response format in the style of RFC 7807 Problem Details ([#29](https://github.com/samgerene/ArgusTransfer/issues/29)).
- `ArgusRetryPolicy`: optional automatic retry of transient named pipe failures in `ArgusClient` ([#30](https://github.com/samgerene/ArgusTransfer/issues/30)).
- Global exception handling middleware, `ArgusExceptionHandlerMiddleware` ([#31](https://github.com/samgerene/ArgusTransfer/issues/31)).
- `Content-Encoding` support for compressed request and response bodies (gzip) ([#34](https://github.com/samgerene/ArgusTransfer/issues/34)).
- `Authorization` header convention for process identity ([#35](https://github.com/samgerene/ArgusTransfer/issues/35)).
- Built-in authentication middleware with pluggable handlers ([#36](https://github.com/samgerene/ArgusTransfer/issues/36)).
- `ArgusPipeHostOptions.MaxRequestHeaderSize`: a limit on the size of the header block ([#64](https://github.com/samgerene/ArgusTransfer/issues/64)).
- `ArgusPipeHostOptions.RequestReadTimeout` and `MaxConcurrentConnections`: a request read timeout and a connection limit for the pipe host ([#65](https://github.com/samgerene/ArgusTransfer/issues/65)).
- `ArgusClient.MaxResponseBodySize`: an optional maximum response body size on the client ([#76](https://github.com/samgerene/ArgusTransfer/issues/76)).
- `ArgusPipeHostOptions.ResponseWriteTimeout`, so a client that does not read its response cannot hold a connection ([#85](https://github.com/samgerene/ArgusTransfer/issues/85)).

### Changed

- **Behavior change:** `ArgusPipeHostOptions` validates its values when they are set. A host configured with a zero or negative limit, a request timeout shorter than one millisecond, a negative `ShutdownDrainTimeout`, a timeout longer than about 49.7 days or an empty `PipeName` now fails at startup with an exception that names the option, instead of failing later. A negative `MaxRequestBodySize` is rejected; use `0` for no limit. Use `Timeout.InfiniteTimeSpan` for no timeout ([#67](https://github.com/samgerene/ArgusTransfer/issues/67)).
- The host answers only request-format violations with 400, through `ArgusProtocolException`; other failures are server faults ([#63](https://github.com/samgerene/ArgusTransfer/issues/63)).
- Routes are percent-encoded on the wire, and header names and values are validated ([#69](https://github.com/samgerene/ArgusTransfer/issues/69)).
- Route templates are validated at registration, so a mistyped template fails at startup ([#73](https://github.com/samgerene/ArgusTransfer/issues/73)).
- Route templates are parsed and middleware pipelines composed once, instead of for every request ([#74](https://github.com/samgerene/ArgusTransfer/issues/74)).
- The response serializer is negotiated from the `Accept` header ([#75](https://github.com/samgerene/ArgusTransfer/issues/75)).
- The host's built-in 400, 406 and 503 responses are problem details ([#77](https://github.com/samgerene/ArgusTransfer/issues/77)).
- `ArgusClient.SendAsync` leaves the caller's request unchanged and validates it ([#79](https://github.com/samgerene/ArgusTransfer/issues/79)).
- The protocol version in request and status lines is validated ([#81](https://github.com/samgerene/ArgusTransfer/issues/81)).
- Documented why `IArgusClient` is `IDisposable` and what `ArgusClient.Dispose` does ([#82](https://github.com/samgerene/ArgusTransfer/issues/82)).
- The README is restructured into a landing page with a verified quick start ([#61](https://github.com/samgerene/ArgusTransfer/issues/61)).
- The sample project demonstrates the current feature set: JSON with a `Content-Type`, problem details for invalid input (a malformed body returns 400 instead of 500), the exception handler, authentication, a retry policy and compression. The tests run it end to end ([#57](https://github.com/samgerene/ArgusTransfer/issues/57)).
- Updated the dependencies.

### Fixed

- Byte-safe message framing for non-ASCII and binary bodies ([#33](https://github.com/samgerene/ArgusTransfer/issues/33)).
- A pipeline that completes without a response is answered with a generic 500 instead of a closed connection ([#62](https://github.com/samgerene/ArgusTransfer/issues/62)).
- The client reads the response while writing the request, so an early rejection by the host does not deadlock ([#68](https://github.com/samgerene/ArgusTransfer/issues/68)).
- The pipe accept loop keeps running after an unexpected exception ([#70](https://github.com/samgerene/ArgusTransfer/issues/70)).
- Shutdown resources are disposed only after all requests have finished ([#71](https://github.com/samgerene/ArgusTransfer/issues/71)).
- `AddArgusModules` can no longer scan the wrong assembly ([#72](https://github.com/samgerene/ArgusTransfer/issues/72)).
- A null query parameter value is written as an empty value ([#78](https://github.com/samgerene/ArgusTransfer/issues/78)).
- `ArgusLoggingMiddleware` logs a completion entry when the pipeline throws ([#80](https://github.com/samgerene/ArgusTransfer/issues/80)).
- Corrected stale file headers and the "Argus Health" wording ([#83](https://github.com/samgerene/ArgusTransfer/issues/83)).

## [1.2.1] - 2026-04-26

### Fixed

- The default pipe ACL also grants the account that runs the service `FullControl`, so subsequent pipe instances can be created. Authenticated Users alone lack `CreateNewInstance`, which broke the host loop after the first connection.

## [1.2.0] - 2026-04-26

### Added

- `ArgusPipeHostOptions.PipeSecurity` for cross-session named-pipe access. The default ACL grants Authenticated Users `ReadWrite | Synchronize`, so clients in a user session can connect to a pipe owned by a service that runs as LocalSystem.

## [1.1.0] - 2026-04-26

### Added

- `EnsureSuccessStatusCode` helper and `EnsureSuccessAsync` client overloads ([#23](https://github.com/samgerene/ArgusTransfer/issues/23)).

### Changed

- Updated `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` to version 10.0.7.

## [1.0.2] - 2026-03-28

### Changed

- `ArgusTransportExtensions` registers `ArgusPipeHostBackgroundService` with a factory that resolves `IArgusBodySerializerRegistry` first and falls back to `IArgusBodySerializer`.

## [1.0.1] - 2026-03-28

### Security

- Hardened chunked transfer encoding against denial of service: chunk size parsing uses `TryParse` with a non-negative check, so a malicious peer cannot trigger excessive memory allocation.
- Parsed `Content-Length` headers are checked to be non-negative in both the request and the response deserializer.

### Fixed

- `ArgusClient.SendAsync` throws `ObjectDisposedException` after disposal, to fail fast.
- Resource leak in `ArgusClient.SendAsync`: the `StreamWriter` and `StreamReader` are now disposed, with `leaveOpen: true`.

### Changed

- Performance: header parsing uses `AsSpan().Trim().ToString()` instead of `Substring().Trim()`, to reduce intermediate string allocations.
- Performance: `ArgusBodySerializerRegistry` uses `TryGetValue` instead of a `ContainsKey` and indexer double lookup.
- Performance: chunked encoding uses the `ReadAsync(Memory<byte>)` overload instead of the inexact `Read(byte[], int, int)`.
- Performance: the sample's `SampleClientService` caches its `JsonSerializerOptions` instance instead of allocating one per call.
- Introduced `ArgusHeaderNames` constants (`Accept`, `ContentType`, `ContentLength`, `CorrelationToken`, `Timestamp`), replacing all string literals.
- Reordered the `IArgusClient` and `ArgusClient` method overloads to be adjacent by name.
- Reordered `ArgusResponseSerializer` so all `Write` overloads are grouped, followed by all `WriteAsync` overloads.
- Refactored `GuidExtensions.FromShortGuidArray` to use a private iterator method.
- Refactored `ArgusClient.SendAsync` to use `requestSerializer.WriteAsync`.

## [1.0.0] - 2026-03-28

### Added

- `IArgusClient` interface.
- Query parameters.
- `Accept` header as a first-class citizen on `ArgusRequest` ([#1](https://github.com/samgerene/ArgusTransfer/issues/1)).
- Content negotiation with `IArgusBodySerializerRegistry` ([#1](https://github.com/samgerene/ArgusTransfer/issues/1)).
- Timeout per request ([#4](https://github.com/samgerene/ArgusTransfer/issues/4)).
- Request timeout on the server ([#14](https://github.com/samgerene/ArgusTransfer/issues/14)).
- Status codes 401 and 403 ([#8](https://github.com/samgerene/ArgusTransfer/issues/8)).
- Status codes 409, 501 and 503 ([#9](https://github.com/samgerene/ArgusTransfer/issues/9), [#10](https://github.com/samgerene/ArgusTransfer/issues/10)).
- Status codes 204 and 422 ([#13](https://github.com/samgerene/ArgusTransfer/issues/13), [#12](https://github.com/samgerene/ArgusTransfer/issues/12)).
- The `HEAD` verb, which returns no body ([#24](https://github.com/samgerene/ArgusTransfer/issues/24)).
- Maximum body size checking ([#15](https://github.com/samgerene/ArgusTransfer/issues/15)).
- Graceful shutdown that drains in-flight requests ([#16](https://github.com/samgerene/ArgusTransfer/issues/16)).
- Concurrency gating ([#17](https://github.com/samgerene/ArgusTransfer/issues/17)).
- Streaming body support for large payloads ([#7](https://github.com/samgerene/ArgusTransfer/issues/7)).

## [0.2.0] - 2026-03-15

### Added

- `ArgusContext`.

## [0.1.0] - 2026-03-02

### Added

- Initial version.

[Unreleased]: https://github.com/samgerene/ArgusTransfer/compare/1.2.1...development
[1.2.1]: https://github.com/samgerene/ArgusTransfer/compare/1.2.0...1.2.1
[1.2.0]: https://github.com/samgerene/ArgusTransfer/compare/1.1.0...1.2.0
[1.1.0]: https://github.com/samgerene/ArgusTransfer/compare/1.0.2...1.1.0
[1.0.2]: https://github.com/samgerene/ArgusTransfer/compare/1.0.1...1.0.2
[1.0.1]: https://github.com/samgerene/ArgusTransfer/compare/1.0.0...1.0.1
[1.0.0]: https://github.com/samgerene/ArgusTransfer/compare/0.2.0...1.0.0
[0.2.0]: https://github.com/samgerene/ArgusTransfer/compare/0.1.0...0.2.0
[0.1.0]: https://github.com/samgerene/ArgusTransfer/releases/tag/0.1.0
