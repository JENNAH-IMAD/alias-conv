import { test, expect } from '@playwright/test';

test('Alias : logos, thèmes, menu compact et navigation responsive', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'ui-test-only', expiresAt: '2099-01-01T00:00:00Z', user: { id: 'test', name: 'Amine Test', role: 'Admin' } }));
  });
  await page.route('**/api/**', route => route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 20 } }));
  await page.setViewportSize({ width: 1440, height: 700 });
  await page.goto('/bank-accounts');
  const sidebar = page.locator('.desktop-sidebar');
  await expect(sidebar).toBeVisible();
  for (const theme of ['clair', 'sombre']) {
    await page.getByRole('button', { name: `Activer le thème ${theme}` }).click();
    const dark = theme === 'sombre';
    await expect(sidebar.locator(dark ? '.alias-logo-dark' : '.alias-logo-light')).toBeVisible();
    await expect(sidebar.locator(dark ? '.alias-logo-light' : '.alias-logo-dark')).not.toBeVisible();
    await expect(page.locator('.theme-root')).toHaveAttribute('data-theme', dark ? 'dark' : 'light');
    await sidebar.getByRole('button', { name: 'Réduire le menu' }).click();
    await expect(sidebar).toHaveCSS('width', '88px');
    await expect(sidebar.getByRole('link', { name: 'Comptes bancaires', exact: true })).toBeVisible();
    await page.screenshot({ path: `../../docs/alias-${dark ? 'dark' : 'light'}-compact.png` });
    await page.reload();
    await expect(sidebar).toHaveCSS('width', '88px');
    await sidebar.getByRole('button', { name: 'Déplier le menu' }).click();
    await expect(sidebar).toHaveCSS('width', '264px');
    await page.screenshot({ path: `../../docs/alias-${dark ? 'dark' : 'light'}-standard.png` });
  }
  await page.getByRole('button', { name: 'Activer le thème système' }).click();
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('.theme-root')).toHaveAttribute('data-theme', 'dark');
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(page.locator('.theme-root')).toHaveAttribute('data-theme', 'light');
  for (const width of [768, 390, 320]) {
    await page.setViewportSize({ width, height: 740 });
    await page.getByRole('button', { name: 'Ouvrir le menu' }).click();
    const drawer = page.getByRole('dialog', { name: 'Navigation principale' });
    await drawer.getByRole('button', { name: 'Réduire le menu' }).click();
    await expect(drawer).toHaveCSS('width', '88px');
    await drawer.getByRole('button', { name: 'Déplier le menu' }).click();
    await expect(drawer).toHaveCSS('width', `${Math.min(290, width * .9)}px`);
    await page.keyboard.press('Escape');
    await expect(drawer).not.toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  }
  await page.getByRole('button', { name: 'Ouvrir le menu' }).click();
  await page.getByRole('dialog').getByRole('link', { name: 'Paramètres', exact: true }).click();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page).toHaveURL(/settings/);
  expect(errors).toEqual([]);
});
