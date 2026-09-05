# architecture/mediatr-pattern Specification

## Purpose
Defines the MediatR CQRS architecture for the Application layer: service
interfaces in `Application.Abstractions/`, command/handler pairs in
`Application.Features/`, and the contract that controllers depend on `IMediator`
only rather than injecting Infrastructure services directly.

## Requirements

### Requirement: Application layer exposes service interfaces

The system SHALL define `IThreatFoxApiService` and `IPhishTankApiService`
interfaces in `ThreatIntelligence.Application.Abstractions/`, exposing
`DownloadDatasetAsync` and `RecentIoCsAsync` methods that return the typed
response DTOs defined by the `application/dto-responses` capability.

#### Scenario: Service interface is implemented by infrastructure
- **WHEN** `AddInfrastructure` registers services
- **THEN** `ThreatFoxApiService` implements `IThreatFoxApiService`
- **AND** `PhishTankApiService` implements `IPhishTankApiService`
- **AND** dependency injection resolves to the concrete implementation

#### Scenario: Service methods return typed DTOs
- **WHEN** `DownloadDatasetAsync` or `RecentIoCsAsync` completes successfully
- **THEN** the return value is a typed response DTO
- **AND** it contains the count of processed records

### Requirement: MediatR commands exist for ThreatFox operations

The system SHALL define `DownloadThreatFoxDatasetCommand` and
`GetRecentThreatFoxIoCsCommand` in
`ThreatIntelligence.Application.Features.ThreatFox/` with corresponding handlers
that return the typed response DTOs defined by the `application/dto-responses`
capability.

#### Scenario: DownloadThreatFoxDatasetCommand handler fetches and stores IOCs
- **WHEN** a `DownloadThreatFoxDatasetCommand` is dispatched via MediatR
- **THEN** `IThreatFoxApiService.DownloadDatasetAsync` is called
- **AND** the count of processed IOCs is returned in a typed response DTO

#### Scenario: GetRecentThreatFoxIoCsCommand handler fetches recent IOCs
- **WHEN** a `GetRecentThreatFoxIoCsCommand` is dispatched via MediatR
- **THEN** `IThreatFoxApiService.RecentIoCsAsync` is called
- **AND** the count of processed IOCs is returned in a typed response DTO

### Requirement: MediatR command exists for PhishTank download

The system SHALL define `DownloadPhishTankDatasetCommand` in
`ThreatIntelligence.Application.Features.PhishTank/` with a corresponding handler
that returns the typed response DTO defined by the `application/dto-responses`
capability.

#### Scenario: DownloadPhishTankDatasetCommand handler fetches and stores phish entries
- **WHEN** a `DownloadPhishTankDatasetCommand` is dispatched via MediatR
- **THEN** `IPhishTankApiService.DownloadDatasetAsync` is called
- **AND** the count of processed entries is returned in a typed response DTO

### Requirement: Controllers use MediatR instead of direct service injection

The system SHALL have controllers depend on `IMediator` only, dispatching
MediatR commands for all operations. Controllers SHALL NOT inject
`ThreatFoxApiService` or `PhishTankApiService` directly. Controller responses
SHALL be built from the typed response DTOs returned by handlers, without
re-wrapping values in anonymous objects.

#### Scenario: ThreatFoxController dispatches via MediatR
- **WHEN** `GET /api/threatfox/download` is requested
- **THEN** `ThreatFoxController` dispatches `DownloadThreatFoxDatasetCommand` via `IMediator`
- **AND** the response returns the typed DTO with the processed IOCs count

#### Scenario: ThreatFoxController recent IOCs dispatches via MediatR
- **WHEN** `GET /api/threatfox/recent` is requested
- **AND** `ThreatFoxController` is authorized (valid API key configured)
- **THEN** `ThreatFoxController` dispatches `GetRecentThreatFoxIoCsCommand` via `IMediator`
- **AND** the response returns the typed DTO with the processed IOCs count

#### Scenario: PhishTankController dispatches via MediatR
- **WHEN** `GET /api/phishtank/download` is requested
- **THEN** `PhishTankController` dispatches `DownloadPhishTankDatasetCommand` via `IMediator`
- **AND** the response returns the typed DTO with the processed entries count

### Requirement: MediatR and FluentValidation are registered in DI

The system SHALL register MediatR and FluentValidation in `AddApplication`.

#### Scenario: Application services are registered at startup
- **WHEN** `Program.cs` calls `AddApplication()`
- **THEN** `IMediator` is registered in `IServiceCollection`
- **AND** MediatR scans the Application assembly for handlers
- **AND** FluentValidation scans for validators
