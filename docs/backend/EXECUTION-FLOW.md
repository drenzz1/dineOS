# Execution flow — Milestone 4 backlog (#156 – #177)

How to ship the 21 issues created on 2026-05-13. Designed so 3 people (Dreni / Endriti / Hera) can work in parallel without blocking each other, with explicit gates between phases. Phase order matters: each subsequent phase assumes the previous one's "Gate" check passes.

---

## Owners

| Tag       | GitHub    | Focus                              |
| --------- | --------- | ---------------------------------- |
| Dreni     | @drenzz1  | Backend lead + Orders/Payments FE  |
| Endriti   | @endrit77 | Auth + Admin FE                    |
| Hera      | @herakurti | Operator pages + DX                |

---

## Phase 0 — Backend safety net (do first, in parallel)

Unblocks everything downstream. Each issue is small and independent — work in parallel.

| Issue | Title                                                                 | Owner   | Why first |
| ----- | --------------------------------------------------------------------- | ------- | --------- |
| #172  | `AnthropicOptions.Model` default invalid id                           | Dreni   | One-line fix; AI feature broken out of the box |
| #173  | `EmailVerificationController.Confirm` auth gate review                | Dreni   | Blocks the FE-403 verification UI design       |
| #174  | RabbitMQ publish failure has no SignalR fallback                      | Dreni   | Defines the contract that FE-501 SignalR consumes |
| #175  | Centralize role + policy names as constants                           | Endriti | Touches every controller; do before parallel auth work |
| #176  | Move `db.Database.Migrate()` into `IDatabaseMigrator`                 | Dreni   | Cleanup; can run in background                  |

**Gate to Phase 1:** #173 has a decision recorded; #174 has a decision recorded. The two decisions shape the FE Phase 2 contract.

---

## Phase 1 — Auth foundation (blocks every other frontend issue)

Sequential within this phase. Until #156 is merged, no frontend wiring is meaningful — every other call sits behind a token the backend doesn't currently issue.

```
#156 (Login → POST /auth/login)
   └── #157 (Refresh-on-401 interceptor)
         └── #158 (Logout → POST /auth/logout)
               └── #159 (useMe hook)
```

| Issue | Owner   | Depends on | Notes |
| ----- | ------- | ---------- | ----- |
| #156  | Endriti | Phase 0   | Switch login from direct Keycloak to backend `/auth/login` |
| #157  | Endriti | #156       | Coalesce concurrent 401s; one in-flight refresh |
| #158  | Endriti | #156       | Calls backend `/auth/logout` before clearing cookies |
| #159  | Endriti | #156       | Replaces ad-hoc JWT-decoder usage in `useTenant`     |

**Gate to Phase 2:** Logging in via the dineOS UI hits `POST /api/v1/auth/login`, expired tokens silently refresh, Logout blacklists the token (verify on `POST /auth/refresh` → 401).

---

## Phase 2 — Operator pages (parallel — main feature work)

Once Phase 1 is in, every page wiring is independent. Three workstreams in parallel; nothing here blocks anything else here.

```
┌── Menu (#160 → #161)
│       Hera owns Menu CRUD then layers the AI ✨ button on top
│
├── Orders & Payments (#162 → #163, plus #164)
│       Dreni owns Orders+Detail page; Hera owns Kitchen board
│       #164 (Kitchen) and #163 (Payments) can run in parallel with #162 (Orders)
│
├── Shifts (#165)
│       Hera; standalone
│
└── Reports (#166)
        Hera; standalone
```

| Issue | Title                                       | Owner   |
| ----- | ------------------------------------------- | ------- |
| #160  | Wire /menu to real backend                  | Hera    |
| #161  | ✨ AI Describe button on menu cards          | Hera    |
| #162  | Wire /orders + /orders/new + /orders/[id]   | Dreni   |
| #163  | Wire /payments                              | Dreni   |
| #164  | Wire /kitchen board                         | Hera    |
| #165  | Wire /shifts + shift notes                  | Hera    |
| #166  | Wire /reports tabs                          | Hera    |

**Suggested order for Hera:** #160 → #164 → #165 → #166 → #161. (The AI button (#161) goes last because it only needs the existing menu mutations from #160.)

**Suggested order for Dreni:** #162 → #163. (Payments depends on at least one open order existing, which #162 produces.)

**Gate to Phase 3:** All 7 issues merged; smoke test: a Cashier can complete an order end-to-end (`/orders/new` → Kitchen → `/payments`).

---

## Phase 3 — Settings + SuperAdmin (parallel)

New pages and the remaining admin holes. Fully independent from each other.

| Issue | Title                                                       | Owner   |
| ----- | ----------------------------------------------------------- | ------- |
| #167  | Build `/settings/profile` + `/settings/tables` pages        | Dreni   |
| #168  | Wire `/admin/users` + delete-restaurant modal               | Endriti |
| #169  | Restaurant email-verification UI (resend + 6-digit confirm) | Endriti |

**Gate to Phase 4:** Manager can edit restaurant profile + tables; SuperAdmin can list users and confirm a restaurant's email.

---

## Phase 4 — Realtime + DX polish

Last pieces. #170 turns the boards live; #171 standardizes errors across everything already wired.

| Issue | Title                                                           | Owner   | Depends on |
| ----- | --------------------------------------------------------------- | ------- | ---------- |
| #170  | SignalR client for `/hubs/orders`                               | Dreni   | #162, #164 (the boards that consume the events); also #174 from Phase 0 (contract) |
| #171  | ApiResponse envelope helper + standard 401/403/422/429 toasts   | Hera    | All Phase 2/3 wiring (refactor pass) |

**Gate to ship:** Creating an order on one screen appears on another's board within ~1s with no refresh; every error path in the app shows a consistent toast with the right copy and (in dev) the backend `traceId`.

---

## Per-issue Definition-of-Done checklist

For every frontend wiring issue, the following has to hold before moving the project-board card to **Done**:

1. The mock module has been deleted, not just commented out.
2. Types in `src/types/<feature>.ts` match the C# DTO 1:1 (no string-vs-number id drift).
3. Query keys live in `queryKeys.ts`; mutations invalidate the right keys.
4. RHF + Zod schemas live in `src/lib/validations/<feature>.ts`.
5. `loading.tsx` and `error.tsx` exist alongside any new `page.tsx`.
6. The matching Postman folder in `docs/backend/postman/dineOS.postman_collection.json` was used to verify the contract.
7. The AI-Development-Log was appended with one row per merged task.

---

## Suggested cadence

| Week | Focus                                                                 |
| ---- | --------------------------------------------------------------------- |
| W1   | Phase 0 wraps in days 1–2; Phase 1 (#156–#159) by end of week         |
| W2   | Phase 2 operator pages (#160, #162, #163, #164 in parallel)           |
| W3   | Finish Phase 2 (#165, #166, #161); Phase 3 (#167, #168, #169)         |
| W4   | Phase 4 realtime + DX polish (#170, #171); regression / e2e pass      |

Track active work via the **dineOS** project board (`In progress` column). Move cards to **In review** when the PR opens, to **Done** when merged.

---

## Cross-cutting reminders

- Read `docs/backend/SERVICE-TEST-BLUEPRINT.md` before opening a PR — Section 4 (RBAC matrix) and Section 5 (tenant isolation) catch the most common regressions.
- Run the Postman folder for the area you're touching before requesting review.
- Frontend changes that depend on a Phase 0 backend decision should not start until the Phase 0 issue is closed.
- Every PR description must link the issue number (e.g. `Closes #160`).
