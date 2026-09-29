import { test, expect } from '@playwright/test';

test('administrateur : modifier et enregistrer les permissions', async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'test', expiresAt: '2099-01-01', user: { id: 'admin', name: 'Admin', role: 'Admin' } })));
  let saved: string[] = [];
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/users/permissions')) return route.fulfill({ json: { items: [{ key: 'banks.read', group: 'Banques', label: 'Consulter', requires: [] }, { key: 'banks.write', group: 'Banques', label: 'Modifier', requires: ['banks.read'] }], defaults: ['banks.read'] } });
    if (route.request().method() === 'PUT') { saved = route.request().postDataJSON().permissions; return route.fulfill({ json: {} }); }
    return route.fulfill({ json: { items: [{ id: 'regular', name: 'Utilisateur test', email: 'test@example.test', role: 'User', permissions: [], createdAt: '2026-01-01' }], total: 1 } });
  });
  await page.goto('/users');
  await page.getByRole('button', { name: 'Permissions de Utilisateur test' }).click();
  await page.getByRole('checkbox', { name: 'Banques : Modifier' }).check();
  await expect(page.getByRole('checkbox', { name: 'Banques : Consulter' })).toBeChecked();
  await page.getByRole('button', { name: 'Enregistrer les permissions' }).click();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  expect(saved.sort()).toEqual(['banks.read', 'banks.write']);
});

test('utilisateur sans droits : liens masqués et accès direct refusé', async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'test', expiresAt: '2099-01-01', user: { id: 'regular', name: 'Test', role: 'User', permissions: [] } })));
  await page.goto('/users');
  await expect(page.getByRole('heading', { name: 'Accès réservé' })).toBeVisible();
  await expect(page.locator('.desktop-sidebar').getByRole('link', { name: 'Utilisateurs', exact: true })).toHaveCount(0);
  await page.goto('/banks');
  await expect(page.getByRole('heading', { name: 'Accès réservé' })).toBeVisible();
});
