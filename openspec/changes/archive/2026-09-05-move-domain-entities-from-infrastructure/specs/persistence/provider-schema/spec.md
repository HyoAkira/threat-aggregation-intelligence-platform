## ADDED Requirements

### Requirement: Entity types in migration metadata reference Domain layer

The system SHALL reference `PhishTank`, `ThreatFox`, and `Provider` entities by
their fully-qualified type names in `ThreatIntelligence.Domain.Entities.*`
(not `ThreatIntelligence.Infrastructure.*`).

#### Scenario: ModelSnapshot resolves entity types from Domain.Entities
- **WHEN** the application starts and `ThreatIntelDbContext` is initialized
- **THEN** the `ModelSnapshot` references `"ThreatIntelligence.Domain.Entities.PhishTank"`
- **AND** `"ThreatIntelligence.Domain.Entities.ThreatFox"`
- **AND** `"ThreatIntelligence.Domain.Entities.Provider"`
- **AND** EF Core's model validation passes without type-resolution errors

#### Scenario: Migration designer files use correct type names
- **WHEN** a developer inspects the `Initial.Designer.cs` migration
- **THEN** `modelBuilder.Entity("ThreatIntelligence.Domain.Entities.PhishTank", ...)`
  matches the actual entity type location
- **AND** `modelBuilder.Entity("ThreatIntelligence.Domain.Entities.ThreatFox", ...)`
  matches the actual entity type location
- **AND** `modelBuilder.Entity("ThreatIntelligence.Domain.Entities.Provider", ...)`
  matches the actual entity type location