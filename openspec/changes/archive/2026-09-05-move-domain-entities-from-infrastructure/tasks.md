## 1. Fix migration type names

- [x] 1.1 Update `ThreatIntelDbContextModelSnapshot.cs`: replace `"ThreatIntelligence.Infrastructure.PhishTank"` → `"ThreatIntelligence.Domain.Entities.PhishTank"`
- [x] 1.2 Update `ThreatIntelDbContextModelSnapshot.cs`: replace `"ThreatIntelligence.Infrastructure.ThreatFox"` → `"ThreatIntelligence.Domain.Entities.ThreatFox"`
- [x] 1.3 Update `ThreatIntelDbContextModelSnapshot.cs`: replace `"ThreatIntelligence.Domain.Provider"` → `"ThreatIntelligence.Domain.Entities.Provider"`
- [x] 1.4 Update `20260904102535_Initial.Designer.cs`: same three replacements
- [x] 1.5 Update `20260904181846_IncreaseReferenceColumnLength.Designer.cs`: same three replacements
- [x] 1.6 Verify `dotnet build` compiles (no errors)

## 2. Create service abstractions in Application layer

- [x] 2.1 Create `ThreatIntelligence.Application.Abstractions/IThreatFoxApiService.cs`
- [x] 2.2 Create `ThreatIntelligence.Application.Abstractions/IPhishTankApiService.cs`
- [x] 2.3 Add MediatR + FluentValidation NuGet references to Application csproj
- [x] 2.4 Update Infrastructure csproj to reference the new interfaces (already references Application)

## 3. Implement MediatR commands and handlers

- [x] 3.1 Create `ThreatIntelligence.Application.Features.ThreatFox/DownloadDataset/` — `DownloadThreatFoxDatasetCommand` + handler
- [x] 3.2 Create `ThreatIntelligence.Application.Features.ThreatFox/GetRecentIoCs/` — `GetRecentThreatFoxIoCsCommand` + handler
- [x] 3.3 Create `ThreatIntelligence.Application.Features.PhishTank/DownloadDataset/` — `DownloadPhishTankDatasetCommand` + handler

## 4. Refactor controllers to use MediatR

- [x] 4.1 Refactor `ThreatFoxController.cs`: inject `IMediator`, dispatch commands, remove `IConfiguration`
- [x] 4.2 Refactor `PhishTankController.cs`: inject `IMediator`, dispatch commands, remove `IConfiguration`

## 5. Register MediatR and update DI

- [x] 5.1 Update `Application.DependencyInjection`: add `AddMediatR` + FluentValidation
- [x] 5.2 Update `Infrastructure.DependencyInjection`: interface-to-implementation registrations
- [x] 5.3 Update `Program.cs` if needed (using statement updated for new namespace)

## 6. Verification

- [x] 6.1 `dotnet build` — all 4 projects compile, no errors (warnings only)
- [x] 6.2 `openspec validate` — change is complete and valid