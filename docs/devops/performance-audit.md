# DineOS — Performance Audit (M5.4)

Full-stack performance audit: Lighthouse, caching headers, CDN, Redis impact, and
image optimization. Owned by DevOps. Supersedes the M2.8 pre-integration summary
(`frontend/docs/perf-summary.md`).

- **Date:** 2026-06-10
- **Build audited:** local **production** build (`next build` + `next start`), Next.js 16, with the M5.4 image-format fix applied.
- **Tool:** Lighthouse 12 (headless Chrome), desktop + mobile presets.
- **Backend perf:** measured via the `MenuCacheBenchmark` harness (see [Redis impact](#4-redis-impact)).

---

## 1. Lighthouse

### Method
Production build served by `next start`; Lighthouse run headless against it.
Desktop and mobile presets captured for the public landing page (`/`), which is
the most external-facing, content-heavy route and needs no auth.

### Results — landing page `/` (prod build)

| Profile | Performance | Accessibility | Best Practices | SEO |
|---------|:-----------:|:-------------:|:--------------:|:---:|
| **Desktop** | **100** | 93 | 100 | 91 |
| **Mobile**  | **88**  | 93 | 100 | 91 |

**Core metrics**

| Metric | Desktop | Mobile |
|--------|:-------:|:------:|
| First Contentful Paint | 0.3 s | 1.0 s |
| Largest Contentful Paint | 0.7 s | 3.2 s |
| Total Blocking Time | 10 ms | 270 ms |
| Cumulative Layout Shift | 0 | 0 |
| Speed Index | 0.3 s | — |

Desktop is essentially perfect; CLS is 0 on both (no layout jank). The mobile gap
(LCP 3.2 s, TBT 270 ms) is JavaScript-execution bound under mobile CPU throttling,
not layout or image weight — see opportunities below.

### Top opportunities (both profiles)

| Opportunity | Est. saving | Note |
|---|---|---|
| Reduce unused JavaScript | ~88 KiB | Largest lever; candidates for further `next/dynamic` code-splitting |
| Avoid legacy JavaScript to modern browsers | ~13 KiB | Tighten the browserslist / `next` transpile target |
| Initial server response | 20 ms | Already excellent |

### Authenticated pages (baseline)
The M2.8 audit scored `/dashboard` **97** and `/orders` **79** on *mock* data.
Those pages now run against the integrated backend (TanStack Query + SignalR).
Re-auditing them requires a logged-in session against a running backend; the
method is documented in [Reproduce](#reproduce) — run Lighthouse with a real
`access_token`+`role` cookie via `--extra-headers`. The `/orders` board is the
known hotspot (heavy realtime grid) and the priority target for the next pass.

---

## 2. Caching headers

Caching is layered; each layer owns a class of asset. (`nginx` here is a pure
reverse proxy — it does not serve static files itself, so asset caching is set by
the Next.js server and the API.)

| Asset class | Set by | Header | Rationale |
|---|---|---|---|
| Hashed JS/CSS chunks (`/_next/static/*`) | Next.js server | `Cache-Control: public, max-age=31536000, immutable` | Content-hashed filenames → cache forever, bust on deploy |
| Optimized images (`/_next/image`) | Next.js optimizer | `minimumCacheTTL` = 31 days (set in M5.4) | Avoid re-encoding on repeat views |
| Uploaded images (`/uploads/*`) | API (`Program.cs`) | `Cache-Control: public, max-age=3600` | User content; 1 h is a safe revalidation window |
| HTML / RSC / API JSON | Next.js / API | `no-cache` / dynamic | Always fresh; correctness over caching |
| Text/JSON over the wire | nginx | `gzip` (level 5, ≥1 KiB) | ~70 % transfer reduction on text assets |

**Verdict:** correct and complete for the workload. Immutable long-cache on
fingerprinted assets is the single most important caching win and it is already
in place. **No additional headers needed** — this section documents an existing
(previously undocumented) strategy.

---

## 3. CDN

**Current edge:** `ingress-nginx` + cert-manager / Let's Encrypt TLS on
`app.project-06.gjirafa.dev`. There is **no CDN** in front of the app today.

**Analysis.** Because Next already marks `/_next/static/*` as
`immutable, max-age=1y`, those assets are ideal for edge caching — a CDN would
serve repeat visitors entirely from the edge, cutting origin load and improving
global latency. A CDN would also add TLS termination, HTTP/3, and DDoS absorption.

**Recommendation (prioritized, not yet implemented):**
- **Recommended:** put **Cloudflare** (free tier) in front of
  `app.project-06.gjirafa.dev` — DNS proxy on, cache everything under
  `/_next/static/*` and `/uploads/*`, respect origin `Cache-Control` elsewhere.
  Near-zero config given the existing immutable headers.
- **Deferred for the class demo:** a single-region school cluster with low traffic
  does not *need* a CDN to meet its SLOs, and adding one touches DNS owned outside
  the team. Documented here as the clear next step for a production/public launch.

---

## 4. Redis impact

The API uses a **cache-aside** pattern for the hot read path
(`MenuService.GetMenuItemsAsync`, 5-min TTL, tenant-scoped key), backed by Redis.
Full design + methodology: [`docs/backend/redis-caching.md`](../backend/redis-caching.md).

**Measured cold-vs-warm** (1,000 menu items, `MenuCacheBenchmark`, steady state):

| Path | Latency | |
|---|---|---|
| Cold (Redis miss → Postgres → populate) | ~36 ms | |
| Warm (Redis hit → deserialize) | **2–3 ms** | dominated by Redis RTT + JSON |
| **Speed-up** | **~11.5×** (up to 13.5× across runs) | |

**Impact:** the hot menu read is an order of magnitude faster from cache, and
since each entry serves many requests before its 5-min TTL expires, steady-state
production traffic sees the warm 2–3 ms path almost always. Redis also backs the
SignalR backplane (multi-replica realtime fan-out) and the refresh-token
blacklist — both latency-sensitive and well-suited to Redis.

---

## 5. Image optimization

- **`next/image`** is used for content images (e.g. menu-item images); the only
  raw `<img>` in the tree is a test mock. Images are lazy-loaded and correctly
  sized, which is why **CLS is 0**.
- **`next/font`** self-hosts fonts (no layout shift, no third-party font fetch).
- **M5.4 fix:** added an `images` block to `next.config.ts`:
  `formats: ["image/avif", "image/webp"]` (the optimizer now serves AVIF/WebP
  with original fallback) and `minimumCacheTTL` ≈ 31 days. AVIF/WebP typically cut
  image bytes 30–50 % vs JPEG/PNG, applied app-wide to every optimized image.
- **Bundle:** see [`docs/bundle-report.md`](../bundle-report.md) +
  `docs/bundle-screenshots/` (treemaps). The Lighthouse "unused JS (~88 KiB)"
  finding is the actionable follow-up — extend `next/dynamic` splitting on heavy
  authenticated routes.

---

## Findings & recommendations (prioritized)

| # | Finding | Severity | Action | Status |
|---|---|---|---|---|
| 1 | No `images` format config (no AVIF/WebP) | Med | Add `formats` + `minimumCacheTTL` | ✅ **applied (M5.4)** |
| 2 | Performance strategy undocumented | Med | This report | ✅ done |
| 3 | ~88 KiB unused JS (mobile TBT 270 ms) | Med | More `next/dynamic` on heavy routes | ⏳ recommended |
| 4 | ~13 KiB legacy JS to modern browsers | Low | Tighten browserslist/transpile target | ⏳ recommended |
| 5 | No CDN at the edge | Low (demo) | Cloudflare in front (config above) | ⏳ recommended |
| 6 | `/orders` authenticated perf (79 @ M2.8) | Med | Re-audit on integrated data; optimize the realtime grid | ⏳ next pass |
| 7 | Caching headers (immutable + Redis) | — | Already optimal | ✅ verified |

---

## Changes applied in M5.4
- `frontend/next.config.ts` — `images.formats = [avif, webp]` + `minimumCacheTTL`.
- This report (`docs/devops/performance-audit.md`) + raw Lighthouse JSON evidence
  (`docs/devops/lighthouse-landing-desktop.json`, `…-mobile.json`).

## Reproduce

```bash
# 1. Build + serve the production frontend
cd frontend && npx next build && npx next start -p 3200

# 2. Lighthouse the public landing page (desktop + mobile)
npx lighthouse@12 http://localhost:3200/ \
  --only-categories=performance,accessibility,best-practices,seo \
  --preset=desktop --output=html --output-path=./lh-desktop.html

npx lighthouse@12 http://localhost:3200/ \
  --only-categories=performance,accessibility,best-practices,seo \
  --output=html --output-path=./lh-mobile.html   # mobile is the default preset

# 3. Authenticated page (needs the backend up + a real session cookie):
#    log in, then pass the cookie so middleware lets Lighthouse through.
npx lighthouse@12 http://localhost:3200/dashboard \
  --extra-headers='{"Cookie":"access_token=<JWT>; role=Manager"}' \
  --preset=desktop --output=html --output-path=./lh-dashboard.html

# Backend cache benchmark (Redis cold-vs-warm):
#   see docs/backend/redis-caching.md → "Reproducing locally"
```
