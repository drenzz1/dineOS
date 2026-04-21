import { test, expect } from '@playwright/test';

test('Cashier can create a pickup order and see it on the live board', async ({ page }) => {
  // Step 1 — navigate to login
  await page.goto('/login');

  // Step 2 — select the Cashier role.
  // Each tenant-role button inside login-role-select calls handleDevLogin immediately on click
  // (no separate submit step). login-submit is the SuperAdmin-only button.
  await page.getByTestId('login-role-select').getByRole('button', { name: /cashier/i }).click();

  // Step 3 — wait for the automatic redirect that handleDevLogin triggers
  await page.waitForURL(/\/orders(\/|$)/);

  // Step 4 — confirm URL is on the orders board
  await expect(page).toHaveURL(/\/orders(\/|$)/);

  // Step 5 — open the New Order wizard
  await page.getByTestId('new-order-button').click();
  await page.waitForURL(/\/orders\/new/);
  await expect(page.getByTestId('order-wizard')).toBeVisible();

  // Step 6 — Wizard Step 1: choose pickup, advance
  await page.getByTestId('order-type-pickup').click();
  await page.getByTestId('wizard-next').click();

  // Step 7 — Wizard Step 2: wait for menu items to load, select first item, advance
  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();
  await page.getByTestId('menu-item-card').first().getByRole('checkbox').click();
  await expect(page.getByTestId('menu-item-qty-increase').first()).toBeVisible();
  await page.getByTestId('wizard-next').click();

  // Step 8 — Wizard Step 3: fill note and submit.
  // Playwright cannot trigger React 19's synthetic onChange on an uncontrolled textarea directly,
  // so we dispatch a custom DOM event that the wizard listens to and writes via ref.current.value.
  await expect(page.getByTestId('order-note-input')).toBeVisible();
  await page.evaluate(() =>
    document.dispatchEvent(
      new CustomEvent('__e2e:set-order-note', { detail: 'Extra napkins' }),
    ),
  );
  await expect(page.getByTestId('order-note-input')).toHaveValue('Extra napkins');
  await page.getByTestId('wizard-submit').click();

  // Step 9 — success toast must appear
  await expect(page.getByTestId('toast-success')).toBeVisible();

  // Step 10 — redirect back to board; find a New + Pick-up card (uniquely our order:
  // seed ord-001 is New but dine-in; no seed New pickup exists)
  await page.waitForURL(/\/orders(\/|$)/);
  await expect(page.getByTestId('orders-list')).toBeVisible();

  const newPickupCard = page
    .getByTestId('order-card')
    .filter({ has: page.getByTestId('order-status-badge').filter({ hasText: /^New$/i }) })
    .filter({ hasText: /pick-up/i })
    .first();

  await expect(newPickupCard).toBeVisible();
  await expect(newPickupCard.getByTestId('order-status-badge')).toHaveText(/^New$/i);

  // Step 11 — click that card; detail panel must show the note "Extra napkins"
  await newPickupCard.click();

  const detailPanel = page.getByRole('dialog', { name: /order details/i });
  await expect(detailPanel).toBeVisible();
  await expect(detailPanel).toContainText('Extra napkins');
});
