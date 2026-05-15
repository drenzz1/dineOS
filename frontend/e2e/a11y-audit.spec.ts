import { test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import fs from 'fs';
import path from 'path';

// ─── helpers ────────────────────────────────────────────────────────────────

function saveResult(slug: string, label: string, violations: import('axe-core').Result[]) {
  const dir = path.join(process.cwd(), 'docs', 'a11y-tmp');
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(
    path.join(dir, `${slug}.json`),
    JSON.stringify({ label, violations }, null, 2),
  );
}

async function audit(
  page: import('@playwright/test').Page,
  slug: string,
  label: string,
) {
  const results = await new AxeBuilder({ page }).analyze();
  saveResult(slug, label, results.violations);
}

// ─── tests ──────────────────────────────────────────────────────────────────
// The authenticated-route audits (dashboard, orders, kitchen, menu, staff,
// shifts) were removed when /login switched to real backend auth — the old
// dev-login role buttons they relied on no longer exist, and the e2e CI job
// has no backend to authenticate against. They will be rebuilt on top of a
// seeded auth fixture + Playwright route mocks in a follow-up.

test('/login — no auth', async ({ page }) => {
  await page.goto('/login');
  await page.waitForLoadState('networkidle');
  await audit(page, '01-login', '/login');
});
