# ADR 0005: Behavior when report evidence is truncated

**Status:** Pending user decision (grilling round 3)

## Context

To bound latency and model cost, the report request limits sources and bytes before sending the request-scoped IOC evidence projection to the model. A large IOC group can therefore have omitted sources.

## Decision to make

Choose one:

1. **Generate with disclosure (recommended):** Generate the report and make the omitted-source count prominent in the evidence and limitations sections.
2. **Minimum-coverage gate:** Generate only if the selected projection meets a defined coverage threshold.
3. **Fail:** Reject the request whenever any source would be omitted.

## Dependent decisions

The numeric source and byte budgets, disclosure wording, and error response follow from this choice.
