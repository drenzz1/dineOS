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

  // Step 5 — open the New Order quick-create screen
  await page.getByTestId('new-order-button').click();
  await page.waitForURL(/\/orders\/new/);
  await expect(page.getByTestId('quick-order-form')).toBeVisible();

  // Step 6 — choose pickup and add an item directly from the menu grid
  await page.getByTestId('order-type-pickup').click();
  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();
  await page.getByTestId('menu-item-card').first().click();
  await expect(page.getByText(/in cart: 1/i)).toBeVisible();

  // Step 7 — fill note and submit
  await expect(page.getByTestId('order-note-input')).toBeVisible();
  await page.getByTestId('order-note-input').fill('Extra napkins');
  await expect(page.getByTestId('order-note-input')).toHaveValue('Extra napkins');
  await page.getByRole('button', { name: /send order/i }).click();

  // Step 8 — redirect back to board; find a New + Pick-up card (uniquely our order:
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

  // Step 9 — click that card; detail panel must show the note "Extra napkins"
  await newPickupCard.click();

  const detailPanel = page.getByRole('dialog', { name: /order details/i });
  await expect(detailPanel).toBeVisible();
  await expect(detailPanel).toContainText('Extra napkins');
});
