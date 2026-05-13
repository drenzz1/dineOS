import { test, expect } from '@playwright/test';

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

// ─── Part A: Keyboard navigation tests ───────────────────────────────────────

// A1 — Tab through /orders/new quick-order flow
//      Verifies every interactive control is keyboard-reachable and operable.
test('Keyboard A1: /orders/new quick order — core controls accessible by keyboard', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('quick-order-form')).toBeVisible();

  // Both radio inputs must accept keyboard focus
  const dineinRadio = page.getByTestId('order-type-dinein');
  const pickupRadio = page.getByTestId('order-type-pickup');

  await dineinRadio.focus();
  await expect(dineinRadio).toBeFocused();

  await page.keyboard.press('ArrowRight');
  await expect(pickupRadio).toBeChecked();

  await page.waitForSelector('[data-testid="menu-item-card"]', { state: 'visible', timeout: 10000 });

  const firstItem = page.getByTestId('menu-item-card').first();
  await firstItem.focus();
  await expect(firstItem).toBeFocused();
  await page.keyboard.press('Space');
  await expect(page.getByText(/in cart: 1/i)).toBeVisible();

  // Quantity buttons are keyboard-reachable
  const qtyDec = page.getByRole('button', { name: /decrease quantity/i }).first();
  const qtyInc = page.getByRole('button', { name: /increase quantity/i }).first();
  await qtyDec.focus(); await expect(qtyDec).toBeFocused();
  await qtyInc.focus(); await expect(qtyInc).toBeFocused();

  // Notes textarea and submit button reachable
  const notesInput = page.getByTestId('order-note-input');
  const submitBtn = page.getByRole('button', { name: /send order/i });

  await notesInput.focus();
  await expect(notesInput).toBeFocused();
  await submitBtn.focus();
  await expect(submitBtn).toBeFocused();
});

// A2 — Modal: Tab must not leave the dialog; Escape must close it.
test('Keyboard A2: Modal — Tab stays inside dialog, Escape closes it', async ({ page }) => {
  await loginAs(page, 'Manager');
  await page.waitForURL(/\/dashboard/);
  await page.goto('/staff');
  await page.waitForLoadState('networkidle');

  // Open modal via the "Add Staff Member" button
  await page.getByRole('button', { name: 'Add Staff Member' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // useFocusTrap moves focus to the first element inside the dialog on open
  await page.waitForFunction(
    () => !!(document.activeElement?.closest('[role="dialog"]')),
  );

  // Tab 15 times — focus must never escape the dialog
  for (let i = 0; i < 15; i++) {
    await page.keyboard.press('Tab');
    const insideDialog = await page.evaluate(
      () => !!(document.activeElement?.closest('[role="dialog"]')),
    );
    expect(insideDialog, `Tab ${i + 1}: focus escaped the dialog`).toBe(true);
  }

  // Escape must close the modal
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
});

// A3 — Enter on a focused submit button submits the form.
test('Keyboard A3: Enter on focused submit button submits the quick-order form', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('quick-order-form')).toBeVisible();

  await page.getByTestId('order-type-pickup-option').click();
  await page.waitForSelector('[data-testid="menu-item-card"]', { state: 'visible', timeout: 10000 });
  await page.getByTestId('menu-item-card').first().click();
  await expect(page.getByTestId('order-note-input')).toBeVisible();

  // Focus the submit button and press Enter — must trigger form submission
  const submitBtn = page.getByRole('button', { name: /send order/i });
  await submitBtn.focus();
  await expect(submitBtn).toBeFocused();
  await page.keyboard.press('Enter');

  await page.waitForURL(/\/orders(\/|$)/);
  await expect(page.getByTestId('orders-list')).toBeVisible({ timeout: 5000 });
});

// A4 — Arrow keys navigate radio groups.
test('Keyboard A4: Arrow keys navigate order-type radio group in /orders/new', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('quick-order-form')).toBeVisible();

  const dineinRadio = page.getByTestId('order-type-dinein');
  const pickupRadio = page.getByTestId('order-type-pickup');

  // Default: dine-in is checked
  await dineinRadio.focus();
  await expect(dineinRadio).toBeFocused();
  await expect(dineinRadio).toBeChecked();

  // ArrowRight moves focus and selection to pickup
  await page.keyboard.press('ArrowRight');
  await expect(pickupRadio).toBeFocused();
  await expect(pickupRadio).toBeChecked();
  await expect(dineinRadio).not.toBeChecked();

  // ArrowLeft moves back to dine-in
  await page.keyboard.press('ArrowLeft');
  await expect(dineinRadio).toBeFocused();
  await expect(dineinRadio).toBeChecked();
  await expect(pickupRadio).not.toBeChecked();
});
