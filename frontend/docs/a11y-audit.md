# Accessibility Audit — dineOS M2.7

**Tool:** axe-core 4.11.3 via @axe-core/playwright  
**Browser:** Chromium (headless)  
**Audit date:** 2026-04-21  
**Branch:** `42-task-endriti-axe-devtools-accessibility-audit-remediation-m27`

---

## Summary

| Impact | Before fixes | After fixes |
|--------|-------------|-------------|
| Critical | **1** | **0** |
| Serious | **9** | **0** |
| Moderate | **3** | **0** |
| Minor | **0** | **0** |
| **Total** | **13** | **0** |

Routes audited: `/login`, `/dashboard`, `/orders`, `/orders/new` (steps 1–3), `/kitchen`, `/menu`, `/staff`, `/shifts`

---

## Per-Route Findings

### /login

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | `span` "or" divider — `#9f9fa9` on white (2.62:1 < 4.5:1) | 1 | 0 |
| `landmark-one-main` | moderate | Document had no `<main>` landmark | 1 | 0 |
| `region` | moderate | Content blocks not enclosed in any landmark region | 3 | 0 |

**Fix:** Replaced outer `<div>` with `<main id="main-content">`; changed divider text to `text-zinc-600`.

**Totals — Before: critical 0, serious 1, moderate 2 → After: 0, 0, 0**

---

### /dashboard

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `label` | critical | Date `<input>` had no associated accessible label | 1 | 0 |
| `color-contrast` | serious | Stat-card label/subtext — `#9f9fa9` on white (2.62:1 < 4.5:1) | 13 | 0 |

**Fix:** Added `<label htmlFor="dashboard-date" className="sr-only">` for date input; changed all `text-zinc-400` labels/subtexts to `text-zinc-600`.

**Totals — Before: critical 1, serious 1, moderate 0 → After: 0, 0, 0**

---

### /orders

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `document-title` | serious | Document had no non-empty `<title>` | 1 | 0 |
| `page-has-heading-one` | moderate | Page had no `<h1>` | 1 | 0 |

**Fix:** Added metadata title template in layout (`%s | dineOS`); board page already has `<h1>Order Board>` — timing fix via `waitForSelector('h1')` in the audit spec resolved false positive.

**Totals — Before: critical 0, serious 1, moderate 1 → After: 0, 0, 0**

---

### /orders/new — Step 1

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | Step-indicator inactive labels + table-number hint — `#9f9fa9` on white (2.62:1) | 3 | 0 |

**Fix:** Changed `text-zinc-400` to `text-zinc-600` for inactive step circles/labels and table number hint text.

**Totals — Before: critical 0, serious 1, moderate 0 → After: 0, 0, 0**

---

### /orders/new — Step 2

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | Step-indicator labels + menu category headings — `#9f9fa9` on white (2.62:1) | 6 | 0 |

**Fix:** Changed `text-zinc-400` to `text-zinc-600` for category headings and step labels.

**Totals — Before: critical 0, serious 1, moderate 0 → After: 0, 0, 0**

---

### /orders/new — Step 3

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | Step-indicator labels + notes hint text — `#9f9fa9` on white (2.62:1) | 4 | 0 |

**Fix:** Changed `text-zinc-400` to `text-zinc-600` for notes "(optional)" hint and "Max 300 characters" label.

**Totals — Before: critical 0, serious 1, moderate 0 → After: 0, 0, 0**

---

### /kitchen

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | Order-ID text `text-zinc-500` on `bg-zinc-800` (3.08:1 < 4.5:1); "Mark Ready" button `bg-green-600 text-white` (3.21:1); loading span `text-zinc-500` on `bg-zinc-900` (3.26:1) | 3 | 0 |

**Fix:** Order-ID changed to `text-zinc-400` (lighter on dark bg = more contrast, ~6:1); button changed to `bg-green-700` (~4.7:1); loading span changed to `text-zinc-300` (~9.5:1).

**Totals — Before: critical 0, serious 1, moderate 0 → After: 0, 0, 0**

---

### /menu

No violations detected before or after fixes.

**Totals — Before: 0, 0, 0 → After: 0, 0, 0**

---

### /staff

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | Inactive staff rows had `opacity-50` causing all cell text to composite below 4.5:1; inactive status badge `text-zinc-500` on `bg-zinc-100` (4.32:1) | 6 | 0 |

**Fix:** Replaced `opacity-50` on inactive rows with `bg-zinc-50` for a distinct-but-legible appearance; changed badge to `text-zinc-700` (~8.4:1 on zinc-100).

**Totals — Before: critical 0, serious 1, moderate 0 → After: 0, 0, 0**

---

### /shifts

| Rule ID | Impact | Description | Before | After |
|---------|--------|-------------|--------|-------|
| `color-contrast` | serious | Empty-state paragraph `text-zinc-400` on white (2.62:1) | 1 | 0 |

**Fix:** Changed to `text-zinc-600`.

**Totals — Before: critical 0, serious 1, moderate 0 → After: 0, 0, 0**

---

## Keyboard Navigation Results

Tested with Playwright keyboard actions (no mouse after login). All tests in `e2e/keyboard-nav.spec.ts` — **4 passed**.

| # | Test | Result |
|---|------|--------|
| A1 | Tab through `/orders/new` wizard all 3 steps — every input, checkbox, qty button, and nav button reachable and operable via Tab / Shift+Tab / Enter / Space | **PASS** |
| A2 | Open staff "Add Staff Member" modal → Tab 15× never escapes the dialog; Escape closes the modal | **PASS** |
| A3 | Navigate wizard to Step 3 → focus submit button → press Enter → form submits and success toast appears | **PASS** |
| A4 | Focus dine-in radio in Step 1 → ArrowRight selects pickup (focus + checked moves); ArrowLeft returns to dine-in | **PASS** |

---

## Files Changed

| File | Change |
|------|--------|
| `src/app/layout.tsx` | Added skip-to-content link; updated `<title>` template |
| `src/app/(protected)/layout.tsx` | Added `id="main-content"` to `<main>`; added metadata title |
| `src/app/(admin)/layout.tsx` | Added `id="main-content"` to `<main>` |
| `src/app/login/page.tsx` | Replaced outer `<div>` with `<main id="main-content">`; `text-zinc-400` → `text-zinc-600` on "or" divider |
| `src/app/(protected)/dashboard/page.tsx` | Added sr-only label for date input |
| `src/app/(protected)/menu/page.tsx` | Added `aria-label` to category name input |
| `src/components/dashboard/SummaryCards.tsx` | `text-zinc-400` → `text-zinc-600` for label and subtext |
| `src/components/dashboard/OrdersTable.tsx` | `text-zinc-400` → `text-zinc-600` for all `<th>` and empty state |
| `src/components/orders/OrderCard.tsx` | `text-zinc-400` → `text-zinc-600` for ID, elapsed, notes preview |
| `src/components/orders/OrderBoard.tsx` | `text-red-600` → `text-red-700` for Cancelled header; `text-zinc-400` → `text-zinc-600` for empty state |
| `src/components/orders/OrderWizard.tsx` | `text-zinc-400` → `text-zinc-600` for step labels, hints, category headings; added `aria-label` on qty buttons |
| `src/components/orders/OrderDetailPanel.tsx` | Added `useFocusTrap`; added `role="dialog" aria-modal aria-label` |
| `src/components/kitchen/KitchenTicket.tsx` | `text-zinc-500` → `text-zinc-400` for order ID on dark bg; `bg-green-600` → `bg-green-700` for Mark Ready |
| `src/components/kitchen/KitchenBoard.tsx` | `text-zinc-500` → `text-zinc-300` for loading text on dark bg |
| `src/components/staff/StaffTable.tsx` | Removed `opacity-50`; added `bg-zinc-50` for inactive rows; `text-zinc-500` → `text-zinc-700` for inactive badge |
| `src/components/staff/StaffMemberForm.tsx` | `text-zinc-400` → `text-zinc-600` for PIN hint (modal not scanned by axe, fixed proactively) |
| `src/components/shifts/ShiftNoteList.tsx` | `text-zinc-400` → `text-zinc-600` for empty state |
| `src/components/ui/Modal.tsx` | Added `useFocusTrap`; confirmed `role="dialog" aria-modal aria-labelledby` |
| `src/hooks/useFocusTrap.ts` | New hook — traps Tab/Shift+Tab within a container; moves initial focus on activation |
| `e2e/a11y-audit.spec.ts` | New spec — axe-core scan across 10 routes, per-test JSON output |
| `e2e/keyboard-nav.spec.ts` | New spec — 4 keyboard navigation tests (A1–A4) |
