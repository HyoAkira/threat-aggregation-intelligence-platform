## 1. Package Setup

- [ ] 1.1 Add `DotNetEnv` package to `ThreatIntelligence.API.csproj` `<ItemGroup><PackageReference Include="DotNetEnv" Version="3.0.0" /></ItemGroup>`
- [ ] 1.2 Restore packages with `dotnet restore`

##  **Verification**: `dotnet restore` completes successfully and `DotNetEnv` appears in the dependency graph

## 2. Program.cs Integration

- [ ] 2.1 Add `using DotNetEnv;` at the top of `Program.cs`
- [ ] 2.2 Add `DotEnv.Load()` call before `BuildWebApplication` in the `CreateBuilder` method

  **Verification**: Application compiles and loads .env keys from project root `.env` file

## 3. .env File Validation

- [ ] 3.1 Verify `.env` file exists at project root directory
- [ ] 3.2 Verify `.env` contains required keys: `AuthKey__AbuseCh`, `PhishTank__ApiKey`, `SA_PASSWORD`
- [ ] 3.3 Application will fail to start if .env file is missing or keys are invalid

  **Verification**: Application startup fails when `.env` file is missing or required keys are absent

## 4. .env Key Verification

- [ ] 4.1 Verify `AuthKey__AbuseCh` loads from `.env` correctly
- [ ] 4.2 Verify `PhishTank__ApiKey` loads from `.env` correctly
- [ ] 4.3 Verify `SA_PASSWORD` loads from `.env` correctly

  **Verification**: All three environment variables resolve to the values defined in `.env`

## 5. Database Schema Verification

- [ ] 5.1 Verify `providers.ThreatFox.Reference` column size suffices for API response data (minimum 500 characters)
- [ ] 5.2 If column size is insufficient, apply migration: `ALTER TABLE providers.ThreatFox ALTER COLUMN Reference varchar(500)`

  **Verification**: Database schema validated and migration applied if needed; ThreatFox API data insertion succeeds without truncation errors