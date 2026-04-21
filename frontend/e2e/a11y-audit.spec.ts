import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import fs from 'fs';
import path from 'path';

// ─── helpers ────────────────────────────────────────────────────────────────

type Role = 'Manager' | 'Cashier' | 'KitchenStaff' | 'SuperAdmin';

async function loginAs(page: import('@playwright/test').Page, role: Role) {
  await page.goto('/login');
  await page.waitForLoadState('networkidle');

  if (role === 'SuperAdmin') {
    await page.getByTestId('login-submit').click();
  } else {
    await page
      .getByTestId('login-role-select')
      .getByRole('button', { name: role })
      .click();
  }
  await page.waitForLoadState('networkidle');
}

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

test('/login — no auth', async ({ page }) => {
  await page.goto('/login');
  await page.waitForLoadState('networkidle');
  await audit(page, '01-login', '/login');
});

test('/dashboard — Manager', async ({ page }) => {
  await loginAs(page, 'Manager');
  await page.goto('/dashboard');
  await page.waitForLoadState('networkidle');
  await audit(page, '02-dashboard', '/dashboard');
});

test('/orders — Cashier', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.waitForLoadState('networkidle');
  // wait for board h1 to confirm client components have rendered
  await page.waitForSelector('h1', { state: 'visible' });
  await audit(page, '03-orders', '/orders');
});

test('/orders/new Step 1 — Cashier', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('order-wizard')).toBeVisible();
  await audit(page, '04-orders-new-step1', '/orders/new — Step 1');
});

test('/orders/new Step 2 — Cashier', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('order-wizard')).toBeVisible();

  await page.getByTestId('order-type-pickup').click();
  await page.getByTestId('wizard-next').click();
  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();
  await page.waitForLoadState('networkidle');
  await audit(page, '05-orders-new-step2', '/orders/new — Step 2');
});

test('/orders/new Step 3 — Cashier', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('order-wizard')).toBeVisible();

  await page.getByTestId('order-type-pickup').click();
  await page.getByTestId('wizard-next').click();
  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();
  await page.getByTestId('menu-item-card').first().getByRole('checkbox').click();
  await expect(page.getByTestId('menu-item-qty-increase').first()).toBeVisible();
  await page.getByTestId('wizard-next').click();
  await expect(page.getByTestId('order-note-input')).toBeVisible();
  await page.waitForLoadState('networkidle');
  await audit(page, '06-orders-new-step3', '/orders/new — Step 3');
});

test('/kitchen — KitchenStaff', async ({ page }) => {
  await loginAs(page, 'KitchenStaff');
  await page.waitForURL(/\/kitchen(\/|$)/);
  await page.waitForLoadState('networkidle');
  await page.waitForSelector('h1', { state: 'visible' });
  await audit(page, '07-kitchen', '/kitchen');
});

test('/menu — Manager', async ({ page }) => {
  await loginAs(page, 'Manager');
  await page.goto('/menu');
  await page.waitForLoadState('networkidle');
  await audit(page, '08-menu', '/menu');
});

test('/staff — Manager', async ({ page }) => {
  await loginAs(page, 'Manager');
  await page.goto('/staff');
  await page.waitForLoadState('networkidle');
  await audit(page, '09-staff', '/staff');
});

test('/shifts — Manager', async ({ page }) => {
  await loginAs(page, 'Manager');
  await page.goto('/shifts');
  await page.waitForLoadState('networkidle');
  await audit(page, '10-shifts', '/shifts');
});
