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

// A1 — Tab through /orders/new across all 3 steps
//      Verifies every interactive control is keyboard-reachable and operable.
test('Keyboard A1: /orders/new wizard — all controls accessible via keyboard across all 3 steps', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('order-wizard')).toBeVisible();

  // ── Step 1 ──
  // Both radio inputs must accept keyboard focus
  const dineinRadio = page.getByTestId('order-type-dinein');
  const pickupRadio = page.getByTestId('order-type-pickup');

  await dineinRadio.focus();
  await expect(dineinRadio).toBeFocused();

  // Arrow-right switches to pickup (also tested in A4; confirmed here for tab-out)
  await page.keyboard.press('ArrowRight');
  await expect(pickupRadio).toBeChecked();
  // Pickup selected → table-number field is gone → Tab exits group to Cancel then Next
  await page.keyboard.press('Tab'); // → Cancel button
  await page.keyboard.press('Tab'); // → Next button
  await expect(page.getByTestId('wizard-next')).toBeFocused();
  await page.keyboard.press('Enter'); // advance to Step 2

  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();

  // ── Step 2 ──
  const firstCheckbox = page.getByTestId('menu-item-card').first().getByRole('checkbox');
  await firstCheckbox.focus();
  await expect(firstCheckbox).toBeFocused();
  // Space selects the item (qty controls appear)
  await page.keyboard.press('Space');
  await expect(page.getByTestId('menu-item-qty-decrease').first()).toBeVisible();

  // Quantity buttons are keyboard-reachable
  const qtyDec = page.getByTestId('menu-item-qty-decrease').first();
  const qtyInc = page.getByTestId('menu-item-qty-increase').first();
  await qtyDec.focus(); await expect(qtyDec).toBeFocused();
  await qtyInc.focus(); await expect(qtyInc).toBeFocused();

  // Back and Next buttons reachable
  await page.getByRole('button', { name: 'Back' }).focus();
  await expect(page.getByRole('button', { name: 'Back' })).toBeFocused();
  await page.getByTestId('wizard-next').focus();
  await expect(page.getByTestId('wizard-next')).toBeFocused();
  await page.keyboard.press('Enter'); // advance to Step 3

  await expect(page.getByTestId('order-note-input')).toBeVisible();

  // ── Step 3 ──
  // Notes textarea and submit button reachable
  const notesInput = page.getByTestId('order-note-input');
  const submitBtn = page.getByTestId('wizard-submit');

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
test('Keyboard A3: Enter on focused submit button submits the wizard form', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('order-wizard')).toBeVisible();

  // Reach Step 3 quickly using mouse (keyboard submit is what we're testing, not full nav)
  await page.getByTestId('order-type-pickup').click();
  await page.getByTestId('wizard-next').click();
  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();
  await page.getByTestId('menu-item-card').first().getByRole('checkbox').click();
  await page.getByTestId('wizard-next').click();
  await expect(page.getByTestId('order-note-input')).toBeVisible();

  // Focus the submit button and press Enter — must trigger form submission
  const submitBtn = page.getByTestId('wizard-submit');
  await submitBtn.focus();
  await expect(submitBtn).toBeFocused();
  await page.keyboard.press('Enter');

  await expect(page.getByTestId('toast-success')).toBeVisible({ timeout: 5000 });
});

// A4 — Arrow keys navigate radio groups.
test('Keyboard A4: Arrow keys navigate radio group in /orders/new Step 1', async ({ page }) => {
  await loginAs(page, 'Cashier');
  await page.waitForURL(/\/orders(\/|$)/);
  await page.goto('/orders/new');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId('order-wizard')).toBeVisible();

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
