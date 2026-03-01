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

## Solution Structure

| Project | Framework | Role |
|---|---|---|
| `ArgusTransfer` | net10.0 | Protocol, Routing, Serialization, Named-pipe client + server host |
| `ArgusTransfer.Tests` | net10.0 | Tests for ArgusTransfer |

### Key Directories

```
ArgusTransfer/
├── Client/          – ArgusClient
├── Extensions/      – DI extension methods
├── Protocol/        – ArgusMessage, ArgusRequest, ArgusResponse, ArgusVerb, ArgusStatusCode
├── Routing/         – ArgusRouter, route templates, modules
├── Serialization/   – Request/response readers and writers
└── Server/          – ArgusPipeHostBackgroundService, ArgusPipeHostOptions
```

## Architecture

### IPC Protocol (ARGUS/1.0)

Text-based request/response wire format transmitted over named pipes.

- **Request line**: `{VERB} {ROUTE} ARGUS/1.0`
- **Response status line**: `ARGUS/1.0 {StatusCode} {ReasonPhrase}`
- **Standard headers**: `X-Correlation-Token`, `X-Timestamp`, `Content-Length`, `Content-Type`
- **Verbs** (`ArgusVerb`): GET, POST, PUT, PATCH, HEAD, DELETE
- **Status codes** (`ArgusStatusCode`): 200 Ok, 201 Created, 400 BadRequest, 404 NotFound, 500 InternalServerError

### Routing

`ArgusRouter` matches verb + route template and dispatches to an `ArgusHandlerDelegate`. Supports literal segments, `{param}` parameters, and `{param:Guid}` constrained parameters (case-insensitive matching on literals). Modules implement `IArgusModule.AddRoutes(IArgusRouteBuilder)` to register endpoints.

### Serialization

`ArgusRequestWriter`/`ArgusRequestReader` and `ArgusResponseWriter`/`ArgusResponseReader` in `/Serialization/`. Each provides a synchronous string API and a `StreamWriter`/`StreamReader` overload for pipe I/O.

### Client

`ArgusClient` creates a `NamedPipeClientStream` per request, serializes via `ArgusRequestWriter`, reads the response via `ArgusResponseReader`.

### Server

`ArgusPipeHostBackgroundService` extends `BackgroundService`, listens on a named pipe, deserializes incoming requests, and routes them through `ArgusRouter`.

### DI Integration

- `services.AddArgusModules()` — scans the calling assembly for `IArgusModule` implementations, registers them as transient, and registers `ArgusRouter` as a singleton that wires up all module routes.
- `services.AddArgusPipeHost(Action<ArgusPipeHostOptions>?)` — registers `ArgusPipeHostBackgroundService` as a hosted service with optional configuration.

## Git Conventions

- **No co-author trailer**: Do not add a `Co-Authored-By` line for Claude in commit messages.

## Code Conventions

- **language**: C#, no using python
- **Explicit usings**: `ImplicitUsings` is disabled — all `using` directives must be written explicitly.
- **LangVersion**: `14.0`
- **Nullable**: `enable` in `ArgusTransfer`, `disable` in `ArgusTransfer.Tests`
- **XML doc comments**: required on all public types and members.
- **License header**: every `.cs` file starts with the Apache-2.0 copyright block.

## Testing Conventions

- Test class naming: `{ClassName}TestFixture`
- Test method naming: `Verify_that_{scenario_in_snake_case}`
- Test convention: use Nunit with `Assert.That` syntax
