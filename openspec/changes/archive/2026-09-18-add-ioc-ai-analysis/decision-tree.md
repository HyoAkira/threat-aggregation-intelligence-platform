# IOC AI analysis decision tree

This is the interview frontier for the change. A recommendation in an ADR is a proposal, not an agreed decision. Update this tree and the relevant ADR after each answer before asking the next round.

## Settled decisions

- **Ephemeral report:** [ADR 0001](adrs/0001-report-role.md). Anyone generates a report, downloads it if useful, and the server keeps no report or chat history.
- **Historical report identity:** [ADR 0003](adrs/0003-report-identity.md) is superseded because reports are not persistent.
- **Evidence scope:** [ADR 0004](adrs/0004-report-evidence-scope.md). Use stored provider-attributed IOC evidence; do not refresh ThreatFox or retrieve IOC URLs during generation.
- **Model endpoints:** [ADR 0002](adrs/0002-model-destination.md). Admins may configure any absolute HTTP or HTTPS OpenAI-compatible endpoint, including local and private-network servers.

## Round 2: independent decisions

1. **Evidence completeness:** [ADR 0005](adrs/0005-evidence-truncation.md). When the bounded projection omits sources, should generation proceed with a prominent limitation, require a minimum coverage rule, or fail?
2. **Validation failure:** [ADR 0006](adrs/0006-output-validation.md). If the model omits a section or cites a nonexistent source label, should the run fail or show a visible warning?
3. **Failure and operational limits:** [ADR 0007](adrs/0007-generation-limits.md). Should the proposed initial limits be adopted, or should they be different?

## Later frontiers

- Report-download format and filename conventions can be decided once the frontend is designed.

## Established plan choices

The current design already chooses explicit authenticated generation, Admin-edited versioned instructions, a bounded single-IOC projection, no model tools, and no persistent report history. These are treated as existing plan choices unless the interview surfaces a reason to revisit them.
