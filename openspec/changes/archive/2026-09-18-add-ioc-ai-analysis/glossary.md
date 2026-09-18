# IOC AI analysis glossary

This glossary distinguishes the current domain model from concepts proposed by this change. It records terms for the design review; pending decisions are tracked in `adrs/`.

| Term | Meaning |
| --- | --- |
| IOC row | One stored provider observation (`IoC`) with its own integer `Id`, provider identity, normalized IOC value/type, port, timestamps, and provider fields. A provider upsert can change its normalized identity. |
| IOC group | All current IOC rows with the same normalized `(IOC type, IOC value)`, regardless of provider or port. The detail query resolves a row ID and then loads this group. |
| Representative row ID | The smallest current row ID in a group, exposed as `IocGroupResult.Id`. It is a navigation handle, not a durable group identifier. |
| Source | One provider row projected into `IocSourceResult`. Source labels such as `S1` are proposed report-local labels, not persistent source IDs. |
| False-positive state | A computed source flag based on the row's `UpdatedAt` and the current time. It can change without a row update. |
| Request-scoped evidence projection | A canonical, bounded projection of one IOC group's source data used only while generating the response. It is discarded after the request completes. |
| Report policy version | Proposed immutable published administrator instruction text pinned when generation starts. It controls report style and analysis guidance, not data access or tools. |
| AI report | An ephemeral, validated model response generated at a user's explicit request, with or without authentication. Streamed text is provisional until validation succeeds; the browser can download the validated result, and the server retains no report history. |

Current model references: `src/ThreatIntelligence.Infrastructure/Persistence/Repositories/IocQueryReader.cs`, `src/ThreatIntelligence.Application/Features/Iocs/Queries/IocGroupResult.cs`, `src/ThreatIntelligence.Application/Features/Iocs/Queries/IocSourceResult.cs`, `src/ThreatIntelligence.Domain/Entities/IoC.cs`, and `src/ThreatIntelligence.Infrastructure/Authentication/JwtTokenIssuer.cs`.
