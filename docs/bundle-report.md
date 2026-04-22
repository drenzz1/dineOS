# Bundle Analysis Report

**Generated:** 2026-04-22
**Branch:** `43-task-endriti-bundle-analysis-with-nextbundle-analyzer-m28`
**Tool:** `@next/bundle-analyzer` v16.2.4 (webpack mode)
**Command:** `npm run analyze` → `cross-env ANALYZE=true next build --webpack`

> Screenshots of both treemaps are saved in `docs/bundle-screenshots/`.

---

## 1. Top 5 Largest Modules (Client Bundle)

These are the five heaviest webpack chunks shipped to the browser, ranked by gzip size.

| Rank | Chunk | Primary Contents | Parsed Size | Gzip Size |
|------|-------|-----------------|-------------|-----------|
| 1 | `3653.js` | `recharts/es6`, `victory-vendor` (d3-scale + 80 modules), `@reduxjs/toolkit` | 378 kB | **112 kB** |
| 2 | `4bd1.js` | `react-dom` (production build) | 195 kB | **61 kB** |
| 3 | `3794.js` | `next/dist` client runtime, `react-server-dom-webpack` | 217 kB | **59 kB** |
| 4 | `framework.js` | React + Next.js framework runtime | 185 kB | **58 kB** |
| 5 | `3823.js` | `zod/v4` (29 kB), `react-hook-form` (10 kB) | 140 kB | **40 kB** |

**Notes:**
- Chunks 3, 4 (`next/dist`, framework) are unavoidable Next.js internals — they cannot be reduced.
- `@reduxjs/toolkit` (8 kB gzip) inside chunk 1 is a peer dependency pulled in by `victory-vendor` (recharts internals) — the project does not use Redux directly.
- The total first-load client payload across all shared chunks is approximately **387 kB gzip**.

---

## 2. Routes Exceeding 200 kB Gzip

**None.** No route in this build exceeds the 200 kB gzip threshold.

The largest route-specific chunk is `/orders/new` at **6 kB gzip** (the OrderWizard page module). All heavy dependencies (`recharts`, `zod`, `react-hook-form`) land in shared chunks that are downloaded once and cached across navigations.

This is a positive result — per-route splitting is working correctly. The concern is not individual routes but the **shared chunk total** (~387 kB gzip) which every user downloads on first visit.

---

## 3. Lazy-Loading Recommendations

### 3.1 `recharts` — partially done, verify boundary

**Chunk:** `3653.js` (112 kB gzip)
**Current state:** `RestaurantGrowthChartInner.tsx` is already wrapped in `next/dynamic` with `ssr: false` inside `RestaurantGrowthChart.tsx`. This is correct.
**Issue:** recharts still appears in a shared chunk (`3653.js`) rather than a route-specific chunk, meaning it is downloaded by users who never visit `/admin/dashboard`.
**Action:** Confirm the dynamic import boundary is not being undermined by a static import elsewhere. If recharts leaks into shared chunks despite the dynamic wrapper, move the `next/dynamic` call up to the page level (`admin/dashboard/page.tsx`) rather than the component level.

```tsx
// admin/dashboard/page.tsx
const RestaurantGrowthChart = dynamic(
  () => import("@/components/admin/RestaurantGrowthChart"),
  { ssr: false, loading: () => <ChartSkeleton /> }
);
```

---

### 3.2 `OrderWizard` — candidate for lazy-loading

**Chunk:** `3823.js` (40 kB gzip — zod + react-hook-form)
**Current state:** `OrderWizard` is a 523-line multi-step form component imported statically into `/orders/new/page.tsx`. It pulls `zod` and `react-hook-form` into the shared chunk that loads across all protected routes.
**Action:** Wrap `OrderWizard` in `next/dynamic` on the orders new page. Users visiting `/orders`, `/kitchen`, `/dashboard` will no longer pay for the form library chunk until they actually navigate to the order creation flow.

```tsx
// app/(protected)/orders/new/page.tsx
const OrderWizard = dynamic(
  () => import("@/components/orders/OrderWizard"),
  { loading: () => <WizardSkeleton /> }
);
```

**Estimated saving:** Defers 40 kB gzip from the initial shared load for non-order-creation routes.

---

### 3.3 `OrderDetailPanel` — candidate for lazy-loading

**Current state:** `OrderDetailPanel` is statically imported in `OrderBoard.tsx`. It is a slide-in panel that only renders when a user clicks an order card — it is never visible on initial page load.
**Action:** Lazy-load it with `next/dynamic`. The panel only needs to be fetched on first open, not on page mount.

```tsx
// components/orders/OrderBoard.tsx
const OrderDetailPanel = dynamic(
  () => import("@/components/orders/OrderDetailPanel")
);
```

**Justification:** Conditionally rendered UI that is invisible on load is the ideal target for `next/dynamic`. Zero UX impact — the panel downloads in the background while the user reads the order board.

---

### 3.4 `recharts` replacement (longer term)

**Current state:** `recharts` (61 kB gzip) + its `victory-vendor`/`d3-scale` dependencies (16 kB gzip) + `@reduxjs/toolkit` peer (8 kB gzip) = **~85 kB gzip** for a single bar chart used in one admin route.
**Action:** Evaluate replacing recharts with a lighter alternative:

| Library | Gzip size | Tradeoff |
|---------|-----------|----------|
| `recharts` (current) | ~85 kB | Full-featured, React-native |
| `chart.js` + `react-chartjs-2` | ~40 kB | Smaller, canvas-based |
| `uplot` | ~15 kB | Minimal, requires more setup |

This is a medium-effort refactor but would be the single largest bundle reduction available in this codebase.

---

## Summary

| Priority | Action | Estimated Saving |
|----------|--------|-----------------|
| High | Verify recharts dynamic boundary lands in route chunk, not shared | Up to 112 kB gzip off initial load |
| Medium | Lazy-load `OrderWizard` on `/orders/new` | ~40 kB gzip deferred |
| Medium | Lazy-load `OrderDetailPanel` in `OrderBoard` | Small, ~5 kB deferred |
| Low | Replace `recharts` with lighter chart library | ~45 kB gzip permanent reduction |

---

## 4. Results After Optimization

All three components from Section 3 were wrapped with `next/dynamic`. Results confirmed via a second `npm run analyze` run after applying changes.

### What changed in code

| File | Change |
|------|--------|
| `src/app/(admin)/admin/dashboard/page.tsx` | Static import of `RestaurantGrowthChart` → `dynamic()` with `ssr: false` |
| `src/app/(protected)/orders/new/page.tsx` | Static import of `OrderWizard` → `dynamic()` (no `ssr: false` — Server Component page) |
| `src/components/orders/OrderBoard.tsx` | Static import of `OrderDetailPanel` → `dynamic()` with `ssr: false`, `loading: () => null` |

### Bundle size changes

#### Client chunk comparison

| Chunk | Before | After | Change |
|-------|--------|-------|--------|
| `3653.js` (recharts ecosystem) | 112 kB gzip — **in shared bundle** | 112 kB gzip — **on-demand only** | Content unchanged, loading deferred ✓ |
| `orders/new` page chunk | 6 kB gzip | 5 kB gzip | −1 kB |
| `admin/dashboard` page chunk | ~5 kB gzip | 3 kB gzip | −2 kB |
| `orders` page chunk | 4 kB gzip | 4 kB gzip | No change |
| `3823.js` (zod + react-hook-form) | 40 kB gzip | 40 kB gzip | No change — used by 4 other forms |

#### New on-demand chunks created

| Chunk | Contents | Gzip Size | Loaded when |
|-------|----------|-----------|-------------|
| `3110.js` | `RestaurantGrowthChart` wrapper | ~0 kB | `/admin/dashboard` mounts |
| `3653.js` | recharts + victory-vendor + d3-scale | 112 kB | `/admin/dashboard` chart renders |
| `2659.js` | `OrderDetailPanel` | 2 kB | User clicks an order card |

### Key finding: recharts is now truly on-demand

Before this change, the `react-loadable-manifest.json` did not isolate recharts to the admin route — it was leaking into the shared bundle. After adding the page-level `dynamic()` wrapper, the manifest confirms:

```
app\(admin)\admin\dashboard\page.tsx → @/components/admin/RestaurantGrowthChart → chunk 3110
components\admin\RestaurantGrowthChart.tsx → ./RestaurantGrowthChartInner → chunk 3653 (recharts)
components\orders\OrderBoard.tsx → ./OrderDetailPanel → chunk 2659
```

Recharts (112 kB gzip) is now only downloaded by users who visit `/admin/dashboard`. Users on `/orders`, `/kitchen`, `/menu`, `/staff`, and `/shifts` no longer pay for it.

### Why `zod` + `react-hook-form` (chunk 3823) did not change

`OrderWizard` uses `zod` and `react-hook-form`, but so do `StaffMemberForm`, `ShiftNoteForm`, `MenuItemForm`, and `RestaurantOnboardForm`. Webpack correctly keeps these in a shared chunk since multiple routes need them. The 40 kB chunk is justified — it is not a candidate for further splitting.

### Remaining opportunity

Replacing `recharts` with a lighter chart library (Section 3.4) remains the only structural reduction available — it would cut ~72 kB gzip permanently from the admin dashboard load rather than just deferring it.
