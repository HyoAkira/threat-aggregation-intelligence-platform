# ADR 0006: Behavior when report output is structurally incomplete

**Status:** Pending user decision (grilling round 3)

## Context

The report contract requires an executive summary, technical analysis, context where supported, recommendations, limitations, disclaimer, and valid evidence-source labels. A model can omit a section or cite a nonexistent label even after the response streams successfully.

## Decision to make

Choose one:

1. **Fail the run (recommended):** Do not display or offer download for an output that fails structural or source-label validation; let the user retry.
2. **Display with warning:** Show and allow download of the output with clear missing-section or invalid-citation warnings.

## Dependent decisions

The validator's strictness and client retry behavior follow from this choice.
