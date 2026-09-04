## Context

The application currently uses .NET's built-in configuration to load environment variables from .env files, which does not reliably load .env keys. The .env file contains critical API keys (ThreatFox AuthKey__AbuseCh, PhishTank PhishTank__ApiKey) and database credentials (SA_PASSWORD). Without proper .env loading, the application fails to start or function correctly. Application will fail to start if .env file is missing or keys are invalid.

## Goals / Non-Goals

**Goals:**
- Integrate DotNetEnv package to reliably load .env files at application startup
- Ensure all environment variables from .env are accessible via `Environment.GetEnvironmentVariable`
- Ensure application fails to start if .env file is missing or keys are invalid
- Maintain backward compatibility with system environment variables for other configurations

**Non-Goals:**
- Modify existing database connection configuration
- Change API key retrieval logic beyond .env loading
- Restructure the entire configuration pipeline

## Decisions

- **Add DotNetEnv NuGet package**: Install `DotNetEnv` package (v3.0.0 or later) which is the .NET implementation of the dotenv format
- **Initialize DotNetEnv in Program.cs**: Call `DotEnv.Load()` early in the `CreateBuilder` pipeline, before `BuildWebApplication`, to ensure all .env variables are available
- **Mandatory .env configuration**: Application will fail to start if .env file is missing or `AuthKey__AbuseCh`/`PhishTank__ApiKey`/`SA_PASSWORD` keys are not properly configured
- **No changes to existing config structure**: The existing `IConfiguration` setup remains intact; DotNetEnv simply populates the environment variables that .NET configuration already knows how to read

## Risks / Trade-offs

- [Risk: DotNetEnv version compatibility with .NET 10.0] → Mitigation: Use latest stable DotNetEnv version (v3.x) which targets .NET 8/9/10
- [Risk: .env file committed accidentally]: → Mitigation: Ensure .env is listed in .gitignore (already present)
- [Trade-off: Additional runtime dependency]: → Mitigation: DotNetEnv is lightweight and widely used in .NET ecosystem

## Migration Plan

1. Verify `DotNetEnv` package is installed in `ThreatIntelligence.API.csproj`
2. Add `DotEnv.Load()` call in `Program.cs` before `BuildWebApplication`
3. Verify .env file exists at project root and contains required keys: `AuthKey__AbuseCh`, `PhishTank__ApiKey`, `SA_PASSWORD`
4. If .env file is missing or keys are invalid, application will fail to start during `BuildWebApplication`
5. Add database schema migration if needed: Ensure `providers.ThreatFox.Reference` column supports 500 characters minimum
6. Run application to verify .env keys load correctly
7. Commit changes with .env in .gitignore