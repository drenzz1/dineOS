# AI-Assisted Swagger Enrichment — Proof (M3.14)

This page documents the AI-assisted Swagger enrichment exercise for
`PaymentsController`. It pairs with `backend/.cursorrules`, which is the
backend rules file an AI assistant is expected to read **before** writing
backend code in this repo.

## Why Payments

`PaymentsController` was the right target because it has the widest set of
failure modes in the codebase:

- `400` from FluentValidation
- `401` / `403` from auth and the tenant policy
- `404` when the order isn't in the caller's tenant
- `422` from two distinct business rules (order already settled, amount mismatch)
- `429` from the `authenticated` rate-limit policy

That spread made the controller a good demonstration of what good Swagger looks
like when every response code is documented and every rule is named.

## Process

### 1. Author the rules

A new `backend/.cursorrules` was written first. The Swagger section commits to:

- XML doc generation on for both `DineOS.Api` and **`DineOS.Application`** (so
  DTO + request properties feed Swagger schema descriptions, not just route
  parameters).
- Every action carries `<summary>`, `<remarks>`, `<param>`, and `<response>`
  XML tags **and** matching `[ProducesResponseType]` attributes — the pair is
  what produces useful Swagger UI, neither alone is enough.
- 422 must be documented for any endpoint whose service can return
  `ServiceResult<T>.UnprocessableEntity(...)`.

### 2. Generated draft

Claude Code (Opus 4.7) was asked to rewrite `PaymentsController.cs`,
`ProcessPaymentRequest.cs`, and `PaymentDto.cs` against the new rules.

The model produced:

- A class-level `<remarks>` block explaining that all endpoints in the
  controller are tenant-scoped and read the tenant from the JWT, not the body.
- Per-action `<remarks>` that enumerate the business rules enforced by the
  service (order existence, status, amount equality, allowed methods).
- An explicit non-idempotency note on `POST /api/v1/payments` — replaying a
  successful 201 returns 422 because the order is no longer payable.
- `<response>` tags and matching `[ProducesResponseType(... 422 ...)]` on
  `ProcessPayment`, which the original controller was missing.
- `<summary>` + `<example>` on every property of `ProcessPaymentRequest` and
  `PaymentDto`, including the matching `tenant_id` claim semantics.

### 3. Human review

| AI suggestion | Human verdict | Change |
|---|---|---|
| Class-level `<remarks>` calling out JWT-as-authoritative-tenant | Kept verbatim | None |
| `<remarks>` enumerating 4 business rules on `ProcessPayment` | Kept; matches `PaymentService.ProcessPaymentAsync` exactly | None |
| Adding `[ProducesResponseType(... 422 ...)]` to `ProcessPayment` | Kept — was missing; service returns 422 for two distinct rules | Added attribute + `<response code="422">` |
| Marking `POST /payments` as **not idempotent** | Kept — replay returns 422, not 201, and that's worth telling consumers | Added explicit "not idempotent" line in `<remarks>` |
| `<example>` values on every request/DTO property | Kept; Swagger UI renders them on the schema panel | Property-level `<example>` blocks |
| Initial draft suggested adding `[Tags("Payments")]` for grouping | Rejected — Swashbuckle already groups by controller name; redundant | Removed |
| Initial draft suggested an `OperationId` per action | Rejected — Asp.Versioning's default operation IDs are already stable; explicit ones drift | Removed |
| Initial draft mentioned partial payments as a possible future extension | Rejected — speculative, not in the current contract; would mis-document the API | Removed |
| Application-project XML doc generation | Kept — without it the DTO/request `<summary>` tags would be compiled away and Swagger UI would still show empty schema descriptions | Enabled `<GenerateDocumentationFile>` in `DineOS.Application.csproj` and registered the second XML path in `Program.cs` |

### 4. Result

Concrete code/doc improvements that landed:

1. `backend/.cursorrules` — new backend rules file, linked from
   `backend/README.md`.
2. `DineOS.Application.csproj` — XML doc generation enabled with `NoWarn 1591`.
3. `Program.cs` — Swagger now picks up `DineOS.Application.xml` alongside
   `DineOS.Api.xml`.
4. `PaymentsController.cs` — class + action `<remarks>`, `<param>`,
   `<response>` tags, and the missing 422 declaration on `ProcessPayment`.
5. `ProcessPaymentRequest.cs` — per-property `<summary>` + `<example>` and a
   request-level example body.
6. `PaymentDto.cs` — per-property `<summary>` + `<example>`.

## Productivity note

Drafting the enriched XML for the controller and the request/DTO from scratch
would have taken roughly 30–40 minutes of careful typing per controller. The
AI draft + human review path took about **10 minutes**, with the bulk of the
human time spent (a) rejecting speculative additions (partial payments,
operation IDs, redundant tags) and (b) noticing the missing
`[ProducesResponseType(422)]` — which is exactly the kind of cross-file
consistency the AI is good at and humans tend to skip on routine edits.

The rules file is the durable artifact: subsequent controllers can be enriched
in the same shape with a one-line prompt ("apply backend/.cursorrules Swagger
section to `<Controller>`") and a short review pass.
