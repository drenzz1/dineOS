# DineOS — Security Audit (M5.5)

Application security audit owned by DevOps. Covers the four required checks:
**git-secrets scan**, **OWASP ZAP results**, **secrets exposure check**, and
**Lighthouse CI**. Complements (does not replace) the container-security doc
([`security.md`](security.md)), which covers Trivy image/dependency scanning.

- **Date:** 2026-06-10
- **Auditor:** DevOps (Endriti)
- **App audited:** local **production** build (`next build` + `next start`), Next.js 16.
- **Scope:** the frontend **public surface** (`/`, `/login`, `/signup`) — the
  external, unauthenticated attacker view. Authenticated/API DAST is documented
  as a follow-up (see [ZAP § coverage](#coverage-and-caveats)).
- **Tools:** gitleaks 8 (Docker), OWASP ZAP baseline (Docker `zaproxy/zaproxy:stable`),
  Lighthouse CI (`@lhci/cli`, headless Chrome).

---

## Executive summary

| Area | Tool | Result | Status |
|---|---|---|---|
| Secrets exposure (history) | gitleaks (full history, 344 commits) | 1 hit → **confirmed false positive**; no real/live secrets | ✅ clean |
| Secret-scan gate (CI) | gitleaks-action | Added; runs on every push/PR; gate green | ✅ added |
| DAST | OWASP ZAP baseline | 0 high/critical; missing-header findings **fixed** (WARN 10→6) | ✅ hardened |
| Web quality gate (CI) | Lighthouse CI | Added; a11y ≥ 0.90 enforced; passes | ✅ added |

**Bottom line:** no high- or critical-severity issues. The audit's one material
code change was adding the **HTTP security-header set** the app was missing
(found by ZAP); that fix is applied and verified. Two new CI gates (secret scan,
Lighthouse) now prevent regressions.

---

## 1. Secret scanning — git-secrets scan + secrets exposure check

> The deliverable lists "git-secrets scan" and "secrets exposure check" separately.
> Both are about committed credentials, so both are satisfied by **gitleaks**: a
> full-history scan (the exposure check) plus a permanent CI gate (the scan).
> `gitleaks` is used in place of AWS `git-secrets` — it is cross-platform
> (Windows-friendly), actively maintained, scans full history, and is the de-facto
> standard. `git-secrets` is effectively unmaintained and awkward on Windows.

### Full-history exposure check

```
gitleaks detect --source . --redact         # via ghcr.io/gitleaks/gitleaks
344 commits scanned · 8.89 MB · 1 finding
```

**The single finding — classified as a false positive:**

| Field | Value |
|---|---|
| Rule | `generic-api-key` |
| File | `deploy/helm/dineos/values.project-06.yaml` (line 306, commit `e69cf71`) |
| Match | a `sk_test_…` Stripe key inside a `helm upgrade --set …` **usage comment** |
| Why it's a false positive | **Test-mode** key (`sk_test_`, not `sk_live_`), **truncated** — body is 17 chars (a real Stripe key body is ~99) and ends in a literal `...` ellipsis. Not a working credential. |
| Residual risk | Minor: the fragment echoes the Stripe **test** account-id. No live key, no complete key. |

This is suppressed by fingerprint in **`.gitleaksignore`** (the standard gitleaks
mechanism for confirmed false positives), so the CI gate is green. Re-scan after
adding the ignore: **`no leaks found` (exit 0).**

**Optional hygiene (not blocking):** next time `values.project-06.yaml` is edited,
replace the example with an obviously-fake placeholder (e.g.
`sk_test_REPLACE_ME`) so the comment carries no real account fragment. Left to the
deploy-file owner to avoid a merge conflict with in-flight deploy work.

**Verdict:** the team's "never commit real keys — inject via GitHub secrets +
`helm --set`" practice holds up across the entire history. No secret was ever
exposed.

### CI gate

`.github/workflows/secret-scan.yml` runs `gitleaks-action` on every push and PR
(`fetch-depth: 0` for full history). A real committed secret fails the check.

---

## 2. OWASP ZAP — DAST baseline

### Method

OWASP ZAP **baseline** scan (passive spider + passive rules — no attack payloads,
non-destructive) via the official Docker image, against the local production
build. Baseline is the CI-appropriate, reproducible DAST mode.

### Result: before → after the header fix

| | Fail | Warn | Pass |
|---|:---:|:---:|:---:|
| **Before** (no security headers) | 0 | 10 | 57 |
| **After** (headers applied) | 0 | **6** | **61** |

Evidence: [`zap-baseline-before.json`](zap-baseline-before.json),
[`zap-baseline-after.json`](zap-baseline-after.json) (trimmed alert summaries).

**Cleared by the fix** (every "header not set" finding):

| Finding | Risk | Fixed by |
|---|---|---|
| CSP header not set | Medium | `Content-Security-Policy` added |
| Missing anti-clickjacking header | Medium | `X-Frame-Options: DENY` + CSP `frame-ancestors 'none'` |
| X-Content-Type-Options missing | Low | `X-Content-Type-Options: nosniff` |
| Permissions-Policy not set | Low | `Permissions-Policy` added |
| X-Powered-By information leak | Low | `poweredByHeader: false` |
| COOP / CORP missing | Low | `Cross-Origin-Opener-Policy` + `-Resource-Policy: same-origin` |

**Remaining after the fix (all understood / accepted):**

| Finding | Risk | Disposition |
|---|---|---|
| CSP: `unsafe-inline` / `unsafe-eval` / wildcard `https:` (x4) | Medium | **Known trade-off of a non-nonce baseline CSP.** Next.js injects inline hydration scripts; a strict CSP needs per-request **nonces** via middleware. Tracked as the prioritized follow-up below. |
| Cross-Origin-Embedder-Policy missing | Low | **Intentionally omitted.** `COEP: require-corp` breaks cross-origin resources (images, the Stripe Checkout redirect). Low value for this app. |
| Application Error Disclosure (x2) | Low | Local-setup artifact: `/api/v1` and `/hubs/orders` returned 500 because the proxied backend isn't wired to this standalone frontend build. Not a frontend issue; backend error handling is covered by `ExceptionMiddleware`. |
| Sensitive info in URL (`?from=`), cache hints, content-type | Info | Informational. `?from=` is the post-login redirect target (a path, not a secret). |

### Coverage and caveats

- Scans the **unauthenticated public surface** — the legitimate external view.
  Protected routes (`/dashboard`, `/orders`, …) redirect to `/login` and were not
  spidered.
- **Follow-up (deeper pass):** authenticated DAST by seeding ZAP with an
  `access_token`+`role` cookie (same technique as the M5.2 verification and the
  M5.4 authenticated-Lighthouse method), and an API scan against the running
  backend (needs the full Postgres + Redis + Keycloak stack).

---

## 3. Lighthouse CI

Automates what M5.4 ran manually — Lighthouse now runs in CI on every frontend PR,
with assertion budgets, so quality (perf / a11y / best-practices / SEO) is gated
and regressions are caught.

- **Config:** [`frontend/lighthouserc.json`](../../frontend/lighthouserc.json) —
  3 runs (median) over `/`, `/login`, `/signup`; reports saved to the filesystem
  (no upload to public storage).
- **Workflow:** [`.github/workflows/lighthouse.yml`](../../.github/workflows/lighthouse.yml)
  — build → `lhci autorun` → upload reports artifact.
- **Assertions:** `accessibility ≥ 0.90` is an **error** (blocks); `performance`,
  `best-practices`, `seo` are **warns** (informational).

**Local run (mobile preset, median of 3):**

| Route | Performance | Accessibility | Best Practices | SEO |
|---|:---:|:---:|:---:|:---:|
| `/` | 79 | 93 | 100 | 91 |
| `/login` | 78 | 95 | 100 | 91 |
| `/signup` | 84 | 96 | 100 | 91 |

`lhci autorun` exits **0** — the accessibility error-gate passes on every route.
Mobile **performance** (78–84) trips the informational `< 0.80` warn on `/` and
`/login`; this is the same JS-execution-bound mobile gap M5.4 identified (~88 KiB
unused JS) and is tracked there, not a regression. Desktop is far higher
(M5.4 measured desktop 100). Mobile is used as the CI default deliberately — the
frontend is mobile-first.

---

## 4. Existing controls (for completeness)

This audit is application-layer. The platform layer is already covered and is
referenced here so the security posture is complete:

- **Trivy** dependency + image scanning on every PR and image build — see
  [`security.md`](security.md). Blocks merge/deploy on CRITICAL/HIGH.
- **Container hardening** — non-root (UID 1001), Alpine runtime, `--chown` copies,
  `.dockerignore` excludes `.env*`. See [`security.md`](security.md).
- **Endpoint protection / RBAC audit** —
  [`../backend/endpoint-protection-audit-m312.md`](../backend/endpoint-protection-audit-m312.md).
- **Secret management** — secrets injected at deploy via GitHub Actions secrets +
  `helm --set`, never committed (confirmed by §1).

---

## Findings & recommendations (prioritized)

| # | Finding | Severity | Action | Status |
|---|---|---|---|---|
| 1 | No HTTP security headers (CSP, anti-clickjacking, nosniff, Permissions-Policy) | Med | Add header set in `next.config.ts` | ✅ **applied (M5.5)** |
| 2 | `X-Powered-By` framework leak | Low | `poweredByHeader: false` | ✅ applied (M5.5) |
| 3 | No secret-scan CI gate | Med | gitleaks-action + `.gitleaksignore` | ✅ applied (M5.5) |
| 4 | No Lighthouse CI gate | Low | `lighthouserc.json` + workflow | ✅ applied (M5.5) |
| 5 | CSP allows `unsafe-inline`/`unsafe-eval` (baseline) | Med | Move to **nonce-based strict CSP** via middleware | ⏳ recommended |
| 6 | Truncated test-key fragment in a deploy comment | Low | Replace with fake placeholder when next editing the file | ⏳ optional |
| 7 | DAST covers public surface only | Low | Authenticated + API ZAP pass | ⏳ next pass |
| 8 | Mobile performance 78–84 on `/`, `/login` | Low | Code-split unused JS (per M5.4) | ⏳ tracked (M5.4) |

---

## Changes applied in M5.5

- `frontend/next.config.ts` — `poweredByHeader: false` + `headers()` security set
  (CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy,
  Permissions-Policy, HSTS, COOP, CORP).
- `.github/workflows/secret-scan.yml` + `.gitleaksignore` — gitleaks CI gate.
- `frontend/lighthouserc.json` + `.github/workflows/lighthouse.yml` +
  `@lhci/cli` devDependency — Lighthouse CI gate.
- This report + trimmed ZAP evidence (`zap-baseline-before.json`,
  `zap-baseline-after.json`).

**Verification performed:** headers confirmed present via `curl`; CSP confirmed
non-breaking via a headless-Chrome pass over the three public routes
(**0 CSP violations, 0 console errors**); gitleaks re-scan clean (exit 0);
`lhci autorun` green (exit 0); ZAP re-scan confirmed the header findings cleared.

---

## Reproduce

```bash
# 1. Secret scan (full history) — needs Docker
docker run --rm -v "$PWD:/repo:ro" ghcr.io/gitleaks/gitleaks:latest \
  detect --source=/repo --redact            # reads .gitleaksignore automatically

# 2. Build + serve the production frontend
cd frontend && npx next build && npx next start -p 3200

# 3. OWASP ZAP baseline (public surface) — needs Docker
docker run --rm -v "$PWD/zap:/zap/wrk:rw" ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py -t http://host.docker.internal:3200 -r zap-report.html -m 3 -I

# 4. Lighthouse CI (build, audit, assert)
cd frontend && npx lhci autorun

# Verify the security headers are present
curl -sD - -o /dev/null http://localhost:3200/ | grep -iE \
  'content-security-policy|x-frame-options|x-content-type|permissions-policy'
```

## See also

- **[Container security](security.md)** — Trivy scanning, image hardening, `.trivyignore`.
- **[Performance audit](performance-audit.md)** — Lighthouse (manual M5.4), caching, CDN, Redis.
- **[CI/CD pipeline](cicd.md)** — workflows, secrets, deploy.
