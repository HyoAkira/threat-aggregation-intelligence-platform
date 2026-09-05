## Context

See proposal.md - Why. Controllers carry per-action try/catch with ad-hoc status mapping. The Application layer handlers are clean pass-throughs after Change 1. This change introduces a single exception-handling path: a MediatR pipeline behavior in the Application layer (cross-cutting, runs around every handler) and an `IExceptionHandler` in the API layer that converts exceptions to RFC-7807 `ProblemDetails`.

## Goals / Non-Goals

**Goals:**
- Uniform RFC-7807 `ProblemDetails` responses for every unhandled exception
- A stable exception → status mapping (401 / 502 / 422 / 500)
- Controllers free of try/catch
- Logging at the command/query boundary via the pipeline behavior
- Documented error responses in OpenAPI

**Non-Goals:**
- No validation pipeline behavior — deferred until commands have inputs (validators)
- No custom application exception taxonomy / domain error types — mapping keyed on existing exception types
- No `Result<T>` / discriminated-union error model — that would be a breaking CQRS contract change
- Error details in responses stay minimal; logging is the detailed record

## Decisions

### Decision 1: Use ASP.NET Core's `IExceptionHandler` (modern API) for ProblemDetails

`Program.cs` registers `AddProblemDetails()` and `AddExceptionHandler<ProblemDetailsExceptionHandler>()`, and the pipeline uses `UseExceptionHandler()`. The handler maps exceptions per the spec:
- `InvalidOperationException` with `not configured` → 401
- `HttpRequestException` → 502
- `InvalidDataException` → 422
- other → 500 (generic message; no internal detail leak)

Rationale: `IExceptionHandler` is the built-in, supported mechanism (CHANGE of philosophy from the earlier "custom middleware" wording, but the same observable contract). It pairs with `AddProblemDetails()` which also populates validation/other framework error responses. Alternative considered: hand-written `IMiddleware` — rejected because it re-implements what `UseExceptionHandler` + `IExceptionHandler` already provide and would diverge from framework conventions.

A custom `ProblemDetailsExceptionHandler` lives at `ThreatIntelligence.API/Middleware/ProblemDetailsExceptionHandler.cs` and implements `IExceptionHandler.TryHandleAsync`, writing an `ProblemDetails` (with `status`, `title`, and a sanitized `detail`) to `HttpContext.Response`.

### Decision 2: MediatR exception pipeline behavior in the Application layer

`Application/Behaviors/ExceptionHandlingBehavior<TRequest, TResponse>` implements `IPipelineBehavior`. It wraps the next delegate, logs the exception at the command/query boundary (with the request type), and **rethrows the original exception** — it never swallows or wraps. The middleware remains the single mapper.

Rationale: gives the Application layer a single cross-cutting guard for every handler (observable via logs, honoring the "exceptions propagate" requirement), without shifting the mapping responsibility out of the API layer. Registering it in `AddApplication` via `services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>))` keeps controllers and handlers unchanged.

Alternative considered: middleware-only (`IExceptionHandler`) with no pipeline behavior — rejected because the user explicitly asked for the pipeline behavior, and because logging at the handler boundary is genuinely useful for diagnosing which command failed.

### Decision 3: Controllers return typed DTOs with no try/catch

All three controller actions drop their try/catch and return `Ok(response)`. Exceptions bubble through the pipeline behavior → middleware → `ProblemDetails`. This is a pure removal; the DTO return types from Change 1 are unchanged.

### Decision 4: Atomicity on 401 detection

The 401 mapping matches `InvalidOperationException.Message.Contains("not configured")`, preserving today's controller behavior exactly (it matched `.Contains("not configured")` too). Documented in the handler with a comment explaining the convention; a future change can formalize this into an exception type.

## Risks / Trade-offs

- [`IExceptionHandler` API differs from "middleware" phrasing] → Observable contract is identical (ProblemDetails + mapping); implementation uses the framework's supported path.
- [Message-based 401 detection is fragile] → Matches existing behavior; callers always throw with that exact text. Formalizing into an exception type is a follow-up.
- [500 hides real error from clients] → Intended; full details go to logs via the pipeline behavior.
- [Unhandled errors in `IExceptionHandler` itself] → `TryHandleAsync` returns `false` on unknown exception types so the framework falls back to defaults; handler code is trivial and unlikely to throw.

## Migration Plan

Code change only. Deploy: add `AddProblemDetails` + `AddExceptionHandler`, `UseExceptionHandler`, register behavior in `AddApplication`, simplify controllers, rebuild, run tests.

## Open Questions

None.