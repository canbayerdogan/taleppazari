# API conventions

How every endpoint of the Api host behaves. Decisions come from [ADR-006](../adr/ADR-006-api-conventions.md); this page describes the implementation (T-008a). The `Example` module is the reference. Observability (correlation ID, logging setup, telemetry, health) is T-008b.

## Routes and versioning
- The Api host owns the version prefix: `Program.cs` creates one route group, `app.MapGroup("/api/v1")`, and passes it to every `Map<Name>Endpoints`. The literal `/api/v1` appears only there.
- A module adds only its own prefix, the lower-case module name: `endpoints.MapGroup("/<module>")`, for example `/api/v1/example/items`.
- Give every endpoint an operation name (`.WithName("<Verb><Entity>")`) and the module tag (`.WithTags("<Module>")`); both appear in the OpenAPI document.

## Errors: RFC 9457 problem details
Every non-success response has the content type `application/problem+json`, whatever the request's `Accept` header says (also `text/html` or `text/plain`), and the same shape:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1", "title": "Bad Request", "status": 400, "traceId": "00-…" }
```

Validation failures add `errors`, a map from the command or query property name to messages:

```json
{ "type": "…", "title": "One or more validation errors occurred.", "status": 400, "errors": { "Name": ["'Name' must not be empty."] }, "traceId": "00-…" }
```

| Case | Status | Produced by |
|---|---|---|
| Validation failure (FluentValidation in Application) | 400 with `errors` | endpoint: `TypedResults.ValidationProblem(result.ValidationErrors)` |
| Malformed or missing JSON body, unparsable route/query value | 400 | framework, turned into problem details by `UseStatusCodePages` |
| Unknown route / method not allowed / wrong content type | 404 / 405 / 415 | framework, same |
| Unhandled exception | 500 | `ApiExceptionHandler` (host) |
| Exception after the response started | none: the request is aborted | `ResponseStartedExceptionMiddleware` (host) |

- All problem responses are written by one host writer, `ProblemJsonWriter`, registered ahead of the framework's default writer (which declines when `Accept` does not allow JSON). It fills `type` and `title` from the framework's RFC 9110 defaults and adds `traceId`.
- `traceId` is present on every path (`Activity.Current.Id`, or the request's trace identifier). Whether a correlation member is added is decided in T-008b.
- An exception after the response has started cannot become problem details (the status line is already sent). The request is aborted, so the client sees a broken response rather than a truncated one that looks complete; logging follows the rule below.
- Problem details never contain stack traces, exception messages or internal type names; `detail` and `instance` are not set.
- `RouteHandlerOptions.ThrowOnBadRequest` is `false` in every environment, so bad requests are 400/415, never 500.
- Logging of unhandled exceptions (before or after the response started): one Error entry with the exception type and the route template only (never the exception message, the query string or the body); the full exception at Debug only (same rule as T-007b). The framework's exception middleware and the server do not log the exception themselves, because the host always handles it; `UnhandledExceptionTests` checks every log category, also with `Accept: text/html`. Client aborts are not handled here (T-008b).
- Problem-details and exception code lives in the Api host only (`src/Hosts/TalepPazari.Api/ErrorHandling/`). Modules return results; they never build problem details by hand except `TypedResults.ValidationProblem`.

## Validation messages
Messages are FluentValidation's built-in English texts, whatever the server culture: the Api host sets `ValidatorOptions.Global.LanguageManager.Enabled = false` at startup. Custom messages are written in English. Localization is a later product decision. Error keys are the command or query property names (`Name`, `Limit`, `Cursor`).

## Cursor pagination
Every list endpoint uses the same query parameters and response shape:

```
GET /api/v1/example/items?limit=20&cursor=<nextCursor>
200 { "items": [ … ], "nextCursor": "AQGhm3y…" }   // nextCursor is null on the last page
```

- `limit`: optional, default **20**, maximum **100**. Out of range (below 1 or above 100) returns a 400 problem with `errors.Limit`; it is never clamped silently.
- `cursor`: optional, opaque. Clients pass back `nextCursor` unchanged. Any other value returns a 400 problem with `errors.Cursor`: wrong length, characters outside the base64url alphabet, padding or whitespace, non-zero unused bits, an unknown format version or an empty id. The decoder never throws and accepts only the exact (canonical) string the server produced.
- Order: ascending `Id` (UUID v7, ADR-004), keyset query `WHERE "Id" > @last ORDER BY "Id" LIMIT limit + 1`; the extra row decides whether `nextCursor` is set. No count query, no offset.
- Stable under inserts: an item created after a page was served has a larger Id, so it appears on a later page; nothing is returned twice or skipped.
- Cursor format (internal, may change): base64url without padding (23 characters) of a format-version byte plus the 16-byte Id in big-endian order.
- **Known limit:** UUID v7 orders by millisecond; Ids created within the same millisecond are ordered randomly. An item created in the same millisecond as the last item of a served page can sort before it and then not appear on later pages. Accepted for now.
- Ordering happens in SQL. PostgreSQL compares `uuid` byte by byte (time order for v7); .NET `Guid.CompareTo` does not, so do not sort Ids in memory.
- Layers: query record, validator, handler and cursor codec in Application (`ListExampleItems/`); the query (`I<Entity>Queries` implementation) in Infrastructure; the endpoint maps query parameters to the query and the page to a response record. The cursor codec is a per-module copy until a second module needs it (then it moves to a shared place by decision).

## OpenAPI
- One document (`v1`) for the Api host, generated by the built-in `Microsoft.AspNetCore.OpenApi`. A document transformer removes server URLs, so the document holds no machine data.
- Served at `/openapi/v1.json` in Development only, until the deployment target is chosen.
- `tests/TalepPazari.ApiTests/Snapshots/openapi-v1.json` is the committed snapshot. `OpenApiSnapshotTests` generates the document in memory (it does not call the endpoint) and fails on any difference, writing `openapi-v1.received.json` to the test output directory.
- **Deliberate update** (local only, never in CI): set `TALEPPAZARI_UPDATE_SNAPSHOTS=true`, run `dotnet test tests/TalepPazari.ApiTests --filter "FullyQualifiedName~OpenApiSnapshotTests"` (the test rewrites the snapshot and reports itself as skipped), unset the variable, review the diff and call out the API change in the PR. With `CI` set to `true` or `1` (any case) the update is refused.

## Authorization
Not part of this convention yet. Endpoints are anonymous until Identity exists (Phase 2); real endpoints authorize per resource in Application (ADR-006).
