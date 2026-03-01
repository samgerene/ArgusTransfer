# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ArgusTransfer is framework that provides routing for named pipes inspired by the HTTP 1.1 request/response protocol that runs over named pipes layer. It provides a 

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


## Architecture

### IPC Protocol


## Git Conventions

- **No co-author trailer**: Do not add a `Co-Authored-By` line for Claude in commit messages.

## Code Conventions

- **language**: C#, no using python
- **Explicit usings**: `ImplicitUsings` is disabled — all `using` directives must be written explicitly.
- **Nullable**: disabled in `ArgusTransfer` and `ArgusTransfer.Tests`
- **XML doc comments**: required on all public types and members.
- **License header**: every `.cs` file starts with the Apache-2.0 copyright block.

## Testing Conventions

- Test class naming: `{ClassName}TestFixture`
- Test method naming: `Verify_that_{scenario_in_snake_case}`
- Test convention: use Nunit with `Assert.That` syntax
