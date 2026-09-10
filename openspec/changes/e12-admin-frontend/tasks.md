## 1. Admin Frontend Foundation

- [ ] 1.1 Add Zod schemas for provider state, trigger guards, job status, history, summary, logs, and settings responses, and verify valid fixtures parse while malformed contracts fail safely.
- [ ] 1.2 Add the typed Admin API client, scoped TanStack Query keys, and mutation invalidation helpers, and verify request paths, parameters, bodies, and affected cache keys with unit tests.
- [ ] 1.3 Add the shared Admin route layout and navigation for providers, summary, logs, and settings under the E11 Admin guard, and verify a User session neither requests nor renders Admin data.

## 2. Provider Status and Availability

- [ ] 2.1 Build the provider status presentation for enabled state, interval, latest status, run times, default/supported modes, and never-run/null states, and verify it against representative response fixtures.
- [ ] 2.2 Add manual refresh, visible-page polling no faster than 15 seconds, last-success timestamps, and stale-data handling, and verify polling pauses while hidden and failures preserve prior data.
- [ ] 2.3 Render API-authoritative trigger availability with disabled reasons, remaining time, and absolute UTC availability, and verify countdown expiry refetches state without locally overriding `canTrigger`.

## 3. Manual Fetch Operations

- [ ] 3.1 Implement default and Full endpoint selection from API-reported modes, and verify unsupported modes are absent and each supported action targets the documented endpoint.
- [ ] 3.2 Add accessible Full-fetch confirmation and a per-provider single-flight mutation lock, and verify cancellation restores focus and rapid duplicate activation sends one request.
- [ ] 3.3 Handle accepted jobs by showing `jobId`, polling no faster than every 2 seconds, stopping on terminal state, and invalidating provider, history, and summary once; verify the complete transition with fake time.
- [ ] 3.4 Handle 409 and other ProblemDetails failures without optimistic queued state, and verify safe guard text, correlation-ID behavior, and provider refresh.

## 4. Provider History

- [ ] 4.1 Add validated URL-backed provider selection and history paging with page 1, size 20 defaults and size 100 maximum, and verify reload plus Back/Forward restores the selected state.
- [ ] 4.2 Render trigger, mode, status, timestamps, duration, counters, loading, empty, stale, and error states, and verify page controls cannot move beyond response bounds.
- [ ] 4.3 Add accessible long-error inspection with wrapping and whitespace preservation, and verify hostile markup is inert text and cannot overflow the viewport.

## 5. Summary and Logs

- [ ] 5.1 Build the summary view from the exact API counts with refresh and last-success metadata, and verify changed responses update all values while refresh failure leaves clearly stale prior values.
- [ ] 5.2 Add supported-level and count controls for logs with default 100 and bounds 1 through 500, and verify invalid input issues no request and no file-path input or request is possible.
- [ ] 5.3 Render newest-first structured logs with refresh, loading, empty, stale, and failure states, and verify messages and exceptions containing markup remain inert plain text.

## 6. Runtime Settings

- [ ] 6.1 Build the non-secret settings form for log level and provider enabled/interval values, and verify no environment, credential, connection, JWT, password, key, or path field is requested or displayed.
- [ ] 6.2 Validate the complete draft against supported log levels and API-supplied minimum intervals, and verify accessible field errors preserve the draft and prevent PUT submission.
- [ ] 6.3 Add baseline-based dirty tracking, Reset, and discard warnings for navigation, reload, close, and logout, and verify choosing Stay preserves every draft value.
- [ ] 6.4 Submit one atomic full-snapshot PUT with duplicate-save prevention, and verify failure preserves the draft while success adopts the returned baseline, clears dirty state, refreshes caches, and displays the 30-second application message.

## 7. Cross-Cutting Completion

- [ ] 7.1 Make every Admin workflow usable by keyboard and at narrow and wide layouts, and verify focus restoration, accessible names/status announcements, and no horizontal overflow in browser-capable tests.
- [ ] 7.2 Run frontend formatting, linting, focused component/flow tests, route generation checks, and the production build, and verify every command succeeds from a clean checkout.
