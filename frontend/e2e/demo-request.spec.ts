import { test, expect } from '@playwright/test';

// Happy-path e2e for the public /demo request flow (#216). The backend is
// mocked at the network boundary so the test does not depend on Keycloak,
// SMTP, or the Postgres demo seeder being reachable from CI.

test.describe('/demo', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/demo/request', async (route) => {
      await route.fulfill({
        status: 202,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {},
        }),
      });
    });
  });

  test('submits email and swaps to the inbox panel', async ({ page }) => {
    await page.goto('/demo');

    await expect(
      page.getByRole('heading', { name: /try dineos/i }),
    ).toBeVisible();

    await page.getByLabel(/work email/i).fill('visitor@example.com');
    await page
      .getByLabel(/i understand demo accounts share a single restaurant/i)
      .check();

    await page.getByRole('button', { name: /email me a demo/i }).click();

    await expect(
      page.getByRole('heading', { name: /check your inbox/i }),
    ).toBeVisible();
    await expect(page.getByText('visitor@example.com')).toBeVisible();
  });

  test('blocks submission when the email is invalid', async ({ page }) => {
    await page.goto('/demo');

    await page.getByLabel(/work email/i).fill('not-an-email');
    await page
      .getByLabel(/i understand demo accounts share a single restaurant/i)
      .check();
    await page.getByRole('button', { name: /email me a demo/i }).click();

    // Form stays on the same view — the inbox heading must NOT appear.
    await expect(
      page.getByRole('heading', { name: /check your inbox/i }),
    ).toHaveCount(0);
  });

  test('honeypot field is hidden from the accessibility tree', async ({ page }) => {
    await page.goto('/demo');

    const honeypot = page.locator('#company_name');
    await expect(honeypot).toBeHidden();
  });
});
