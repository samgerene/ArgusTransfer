![ArgusTransfer](https://raw.githubusercontent.com/samgerene/ArgusTransfer/development/argus-transfer.png)

# ArgusTransfer

**HTTP-style request/response routing over named pipes for .NET.**

[![NuGet](https://img.shields.io/nuget/v/ArgusTransfer.svg)](https://www.nuget.org/packages/ArgusTransfer)
[![Build Status](https://github.com/samgerene/ArgusTransfer/actions/workflows/CodeQuality.yml/badge.svg?branch=development)](https://github.com/samgerene/ArgusTransfer/actions/workflows/CodeQuality.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=coverage)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)

## Why ArgusTransfer?

ArgusTransfer lets a background process -- typically a Windows service or a hosted worker -- expose a REST-like API to other processes on the same machine, without running an HTTP server. You map verbs and routes to handlers, like a minimal web API, and clients call them over a named pipe using a small text protocol (ARGUS/1.0) modeled on HTTP/1.1.

Use it when an HTTP server is too heavy, not allowed, or would open a network port you don't want. It is not meant for communication across machines or with browsers -- use HTTP for that.

## Features

- **Routing and modules** -- `MapGet`/`MapPost`/... with route parameters and `Guid` / `ShortGuid` constraints, organized in modules discovered automatically. [Routing and Modules](https://github.com/samgerene/ArgusTransfer/wiki/Routing-And-Modules)
- **Middleware** -- global and per-endpoint, with built-in logging and exception handling. [Middleware](https://github.com/samgerene/ArgusTransfer/wiki/Middleware)
- **Client** -- typed verb methods, query parameters, timeouts, streaming bodies and automatic retries for transient pipe failures. [Client Usage](https://github.com/samgerene/ArgusTransfer/wiki/Client-Usage)
- **Structured errors** -- RFC 7807 problem details for error responses. [Error Handling](https://github.com/samgerene/ArgusTransfer/wiki/Error-Handling)
- **Authentication** -- `Authorization` header convention and pluggable authentication middleware. [Authentication](https://github.com/samgerene/ArgusTransfer/wiki/Authentication)
- **Compression** -- gzip `Content-Encoding` for request and response bodies. [Compression](https://github.com/samgerene/ArgusTransfer/wiki/Compression)
- **Content negotiation** -- pluggable body serializers selected by `Content-Type` and `Accept`. [Serialization](https://github.com/samgerene/ArgusTransfer/wiki/Serialization)
- **Hosting** -- integrates with `Microsoft.Extensions.Hosting` and dependency injection, with concurrency limits, request timeouts, graceful shutdown and Windows pipe security for services. [Server Configuration](https://github.com/samgerene/ArgusTransfer/wiki/Server-Configuration)

Requires .NET 10. Developed on Windows and tested on Linux in CI; on Linux and macOS, .NET named pipes are Unix domain sockets, and pipe ACLs (`PipeSecurity`) apply to Windows only.

## Install

```bash
dotnet add package ArgusTransfer
dotnet add package Microsoft.Extensions.Hosting
```

## Quick start

A console app that hosts one endpoint and calls it -- in a real system the host and the client are separate processes:

```csharp
using ArgusTransfer.Client;
using ArgusTransfer.Extensions;
using ArgusTransfer.Protocol;
using ArgusTransfer.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddArgusModules();                                  // discovers HelloModule
builder.Services.AddArgusPipeHost(options => options.PipeName = "hello");
builder.Services.AddArgusClient("hello");

using var host = builder.Build();
await host.StartAsync();

var client = host.Services.GetRequiredService<IArgusClient>();
var response = await client.GetAsync("/hello/world");

Console.WriteLine($"{(int)response.StatusCode} {response.Body}");   // 200 Hello, world!

await host.StopAsync();

public class HelloModule : IArgusModule
{
    public void AddRoutes(IArgusRouteBuilder app)
    {
        app.MapGet("/hello/{name}", context =>
        {
            context.Response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                Body = $"Hello, {context.RouteValues["name"]}!"
            };

            return Task.CompletedTask;
        });
    }
}
```

The [Quick Start guide](https://github.com/samgerene/ArgusTransfer/wiki/Quick-Start) walks through a separate server and client, and the [sample project](https://github.com/samgerene/ArgusTransfer/tree/development/ArgusTransfer.Sample) shows a complete CRUD module.

## Documentation

The [wiki](https://github.com/samgerene/ArgusTransfer/wiki) is the reference documentation:

- [Architecture and Protocol](https://github.com/samgerene/ArgusTransfer/wiki/Architecture-And-Protocol) and the [Wire Format Reference](https://github.com/samgerene/ArgusTransfer/wiki/Wire-Format-Reference)
- [Dependency Injection](https://github.com/samgerene/ArgusTransfer/wiki/Dependency-Injection) -- all registration methods
- [Troubleshooting](https://github.com/samgerene/ArgusTransfer/wiki/Troubleshooting)

## Contributing

Bug reports and feature requests are welcome as [GitHub issues](https://github.com/samgerene/ArgusTransfer/issues). See [CONTRIBUTING](https://github.com/samgerene/ArgusTransfer/blob/development/.github/CONTRIBUTING.md) for how to make changes and submit pull requests, and the [Development Environment](https://github.com/samgerene/ArgusTransfer/wiki/Development-Environment) page for building and testing. Builds and tests run on GitHub Actions, and code quality is tracked on [SonarCloud](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer).

## Software Bill of Materials

Every NuGet package ships with a Software Bill of Materials (SBOM) generated during the build. It lists the third-party components, versions and licenses the package depends on, so you can track vulnerabilities and audit licensing.

## License

ArgusTransfer is licensed under the [Apache License 2.0](https://github.com/samgerene/ArgusTransfer/blob/development/LICENSE).
