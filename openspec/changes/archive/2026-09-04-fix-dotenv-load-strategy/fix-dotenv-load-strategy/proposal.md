## Why

The application currently relies on .NET's built-in configuration for loading environment variables from .env files, which does not reliably load .env keys. This causes API keys (ThreatFox, PhishTank) and database credentials to fail to load, breaking application startup and functionality.

## What Changes

- Replace the current .env loading mechanism with the `DotNetEnv` package
- Ensure all .env keys are properly loaded at application startup
- Add verification tests to confirm .env keys are correctly loaded

## Capabilities

### New Capabilities

- `dotenv-integration`: Integrate DotNetEnv package to load .env files reliably, replacing the current broken .NET configuration approach. This capability creates `specs/dotnet-env-integration/spec.md`.

### Modified Capabilities

- *(none - the current .env loading has no existing spec to modify)*

## Impact

- **Dependencies**: Add `DotNetEnv` NuGet package to the project
- **Configuration**: Modify program.cs or startup to initialize DotNetEnv before building the WebApplication factory
- **API Keys**: ThreatFox `AuthKey__AbuseCh` and PhishTank `PhishTank__ApiKey` will load correctly from .env
- **Database**: SA_PASSWORD and other MSSQL credentials will load reliably