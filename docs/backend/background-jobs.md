# Background Jobs — Hangfire + Email (M3.7)

DineOS uses [Hangfire](https://www.hangfire.io/) for background work. All
storage lives in the existing Postgres database (`hangfire.*` schema, created
on first startup), so there is no extra infra to operate.

## What runs in the background

| Job | Trigger | Recurring? | DLQ on failure |
|---|---|---|---|
| `AccountVerificationEmailJob.SendAsync(tenantId)` | Enqueued from `AdminRestaurantService.CreateAsync` after a SuperAdmin creates a restaurant. Also exposed via `POST /api/v1/admin/restaurants/{tenantId}/email-verification/resend`. | No (fire-and-forget) | Yes — row in `DeadLetterEmails` |
| `DailyPaymentSummaryJob.RunAsync` | Hangfire recurring job — default cron `55 23 * * *` (23:55 UTC daily). Sends each active, verified-owner tenant an HTML summary of the day's completed payments. | Yes | Yes |
| `OverduePaymentNotificationJob.RunAsync` | Hangfire recurring job — default cron `*/5 * * * *` (every 5 min). Finds `PaymentStatus.Pending` payments older than `PaymentNotifications:OverdueThresholdMinutes` (default 30 min) that have not yet been notified, emails the owner, and stamps `Payments.OverdueNotifiedAt`. | Yes | Yes |

Retries: every email job carries `[AutomaticRetry(Attempts = 3,
DelaysInSeconds = …, OnAttemptsExceeded = Fail)]`. Permanent failures
transition to `FailedState`, at which point `DeadLetterEmailFilter` writes a
row to `DeadLetterEmails` with the job id, subject, attempt count, exception
details, and tenant id.

## Email sending

`IEmailSender` is implemented by `MailKitEmailSender`. In Docker, it talks to
the bundled **MailHog** container (`smtp://mailhog:1025`) and you can read
captured mail at `http://localhost:8025`. Outside Docker, point
`Smtp:Host` / `Smtp:Port` at any SMTP relay (or set `Email:Enabled=false`
to short-circuit to a log line).

Templates are Razor (`.cshtml`) files under
`backend/src/DineOS.Infrastructure/EmailTemplates/`, rendered with
`RazorLight`:

- `_Layout.cshtml` — shared HTML wrapper
- `AccountVerification.cshtml`
- `DailyPaymentSummary.cshtml`
- `OverduePayment.cshtml`

## Hangfire dashboard

Mapped at **`/hangfire`** (e.g. `http://localhost:5000/hangfire`).

- In `Development`, anonymous access is allowed (toggle with
  `Hangfire:Dashboard:AllowAnonymous`).
- In other environments, the user must be authenticated and in the
  `SuperAdmin` role. See `SuperAdminDashboardAuthorizationFilter`.

## How to verify the flow

```bash
# 1. Bring everything up
cd backend && docker compose up -d

# 2. Create a restaurant as SuperAdmin (Swagger or curl). Watch the API logs
#    for "Account verification email enqueued: RestaurantId=… JobId=…".

# 3. Open MailHog at http://localhost:8025 — the verification email arrives
#    in a few seconds. Copy the 6-digit code.

# 4. Confirm the code:
curl -X POST http://localhost:5000/api/v1/admin/restaurants/{tenantId}/email-verification/confirm \
     -H "Authorization: Bearer <super-admin-jwt>" \
     -H "Content-Type: application/json" \
     -d '{"code":"123456"}'
```

## How to verify retry + dead-letter

Set `Email:SimulateFailure=true` (env var: `Email__SimulateFailure=true`).
The MailKit sender will throw on every call, so Hangfire retries 3 times,
then transitions to `FailedState` and `DeadLetterEmailFilter` writes a row.

```bash
docker compose stop api
ASPNETCORE_Email__SimulateFailure=true docker compose up -d api

# Create a restaurant, then check the table:
psql -h localhost -U dineos -d dineos -c \
    "SELECT \"Id\", \"JobType\", \"ToAddress\", \"FailureReason\", \"AttemptCount\" FROM \"DeadLetterEmails\" ORDER BY \"Id\" DESC LIMIT 5;"
```

The dashboard (`/hangfire/jobs/failed`) will also list the failed job.

## Configuration reference

| Key | Default | Purpose |
|---|---|---|
| `Email:Enabled` | `true` | When `false`, the sender logs and returns without dispatching. |
| `Email:FromAddress` / `Email:FromName` | `no-reply@dineos.local` / `DineOS` | From header. |
| `Email:SimulateFailure` | `false` | Dev-only switch that forces the sender to throw — used to exercise retries + DLQ. |
| `Smtp:Host` / `Smtp:Port` | `localhost` / `1025` | SMTP target. MailHog defaults match. |
| `Smtp:UseStartTls` | `false` | StartTLS toggle for real relays. |
| `EmailVerification:CodeTtlMinutes` | `15` | Lifetime of an account verification code. |
| `EmailVerification:MaxAttemptsPerCode` | `5` | After this many wrong submissions the code is locked. |
| `PaymentNotifications:OverdueThresholdMinutes` | `30` | Age at which a `Pending` payment becomes "overdue". |
| `PaymentNotifications:DailySummaryCron` | `55 23 * * *` | Daily summary schedule. |
| `PaymentNotifications:OverdueScanCron` | `*/5 * * * *` | Overdue scan schedule. |
| `Hangfire:Dashboard:AllowAnonymous` | `null` (dev-only) | Force anonymous on or off; otherwise auto from `Environment`. |
