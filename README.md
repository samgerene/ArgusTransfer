![ArgusTransfer](argus-transfer.png)

## Introduction

ArgusTransfer is framework that provides routing over named pipes inspired by the HTTP 1.1 request/response protocol. It is typically used to host services running in the background for which you would like to expose a REST like API but running a fully fledged HTTP server is too heavy or not allowed.

## Error Responses (Problem Details)

ArgusTransfer provides `ArgusProblemDetails`, a structured error body modeled on [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807), so that clients can parse errors consistently across services. Problem details are serialized as JSON with the content type `application/problem+json`.

In a handler, use `ArgusContext.Problem`, which sets the response and uses the request correlation token as the `instance`:

```csharp
app.MapGet("/items/{id:Guid}", context =>
{
    context.Problem(ArgusStatusCode.NotFound, "Item not found", new Dictionary<string, object> { ["id"] = context.RouteValues["id"] });
    return Task.CompletedTask;
});
```

Or use one of the static factory methods (`BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `UnprocessableEntity`, `InternalServerError`, `ServiceUnavailable`):

```csharp
context.Response = ArgusProblemDetails.BadRequest("Validation failed", new Dictionary<string, object> { ["field"] = "name" });
```

This produces a body such as:

```json
{"type":"about:blank","title":"Bad Request","status":400,"detail":"Validation failed","field":"name"}
```

Entries in `Extensions` are written as top-level members, as specified by RFC 7807. On the client, read the problem details from a response:

```csharp
var response = await client.GetAsync("/items/42");

if (ArgusProblemDetails.TryRead(response, out var problem))
{
    Console.WriteLine($"{problem.Title}: {problem.Detail}");
}
```

## Code Quality

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=coverage)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=samgerene_ArgusTransfer&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=samgerene_ArgusTransfer)

## Build Status

GitHub actions are used to build and test the Argus Health application

Branch | Build Status
------- | :------------
Main | ![Build Status](https://github.com/samgerene/ArgusTransfer/actions/workflows/CodeQuality.yml/badge.svg?branch=main)
Development | ![Build Status](https://github.com/samgerene/ArgusTransfer/actions/workflows/CodeQuality.yml/badge.svg?branch=development)

# Software Bill of Materials (SBOM) and Provenance

As part of our commitment to security, transparency, and traceability the docker images and nuget packaves Software Bill of Materials (SBOM), the docker containers also contain Provenance information. These are automatically generated during the build process, providing detailed insights into the components, their licenses, versions, and the integrity of the nugets and docker images. What is Included:

## SBOM (Software Bill of Materials):

- A comprehensive list of all open-source and third-party components included in the Docker images and nugets.
- Tracks software dependencies, licenses, and versions.
- Helps with vulnerability management by allowing users to quickly identify potential risks tied to specific components.

## Provenance:

- A record of the image's origin and build process, providing traceability and assurance regarding the integrity of the image.
- This ensures that the image was built using the declared sources and under the specified conditions, helping verify its authenticity and consistency.

## Why SBOM and Provenance?

- Improved Transparency: Provides full visibility into the open-source and third-party components included in the image.
- Security Assurance: Enables easier tracking of vulnerabilities associated with specific components, promoting proactive security measures.
- Compliance: Ensures adherence to licensing requirements and simplifies audits of dependencies and build processes.
- Image Integrity: Provenance guarantees that the image is built as expected, without unauthorized modifications.

# License

The ArgusTransfer software are provided to the community under the Apache License 2.0.