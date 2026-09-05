## 1. MediatR Pipeline Behavior

- [x] 1.1 Create `Application/Behaviors/ExceptionHandlingBehavior.cs` implementing `IPipelineBehavior<TRequest, TResponse>` that logs and rethrows exceptions — verify: solution compiles
- [x] 1.2 Register the behavior in `AddApplication` via `services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>))` — verify: solution compiles

## 2. ProblemDetails Middleware

- [x] 2.1 Create `API/Middleware/ProblemDetailsExceptionHandler.cs` implementing `IExceptionHandler` with mapping: not-configured→401, HttpRequestException→502, InvalidDataException→422, other→500 (generic message) — verify: solution compiles
- [x] 2.2 Wire `AddProblemDetails()`, `AddExceptionHandler<ProblemDetailsExceptionHandler>()`, and `UseExceptionHandler()` in `Program.cs` in the correct order — verify: solution compiles

## 3. Controllers

- [x] 3.1 Remove try/catch from `ThreatFoxController.DownloadDataset` and `.GetRecentIoCs`; return DTO directly — verify: solution compiles and actions have no try/catch
- [x] 3.2 Remove try/catch from `PhishTankController.DownloadDataset`; return DTO directly — verify: solution compiles and action has no try/catch

## 4. Tests

- [x] 4.1 Add unit tests for `ProblemDetailsExceptionHandler` covering all four mappings (401/502/422/500) and response content type — verify: `dotnet test` passes
- [x] 4.2 Add unit tests for `ExceptionHandlingBehavior` asserting the original exception is rethrown and the next delegate is invoked — verify: `dotnet test` passes

## 5. Integration Verification

- [x] 5.1 Verify `dotnet build ThreatIntelligence.slnx` and `dotnet test` both green — verify: exit code 0
- [x] 5.2 Run `openspec validate error-handling-middleware` — verify: change is valid