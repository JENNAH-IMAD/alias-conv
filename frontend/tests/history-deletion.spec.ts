import { test, expect, Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

async function mockHistory(page: Page, admin = true) {
  let events = Array.from({ length: 25 }, (_, i) => ({
    id: `event-${i}`,
    statementId: 'statement-1',
    clientId: 'client-1',
    clientName: 'Client test',
    accountName: 'Compte test',
    bankName: 'Banque test',
    fileName: `releve-${i}.pdf`,
    action: 'IMPORTED',
    actorName: 'Admin test',
    version: 1,
    createdAt: '2026-10-15T09:00:00Z',
  }));
  let profiles = Array.from({ length: 2 }, (_, i) => ({
    id: `profile-event-${i}`,
    profileName: `Modèle ${i}`,
    action: 'CREATED',
    actorName: 'Admin test',
    version: 1,
    createdAt: '2026-10-15T09:00:00Z',
    snapshotJson: '{}',
  }));
  const deletions: {
    path: string;
    body: { all: boolean; ids: string[]; excludeIds: string[]; statementId?: string };
  }[] = [];
  const state = { fail: false };
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace('/api', '');
    if (path === '/auth/session')
      return route.fulfill({
        json: {
          setupRequired: false,
          user: {
            id: 'user-1',
            name: 'Test',
            email: 'test@example.test',
            role: admin ? 'Admin' : 'User',
            permissions: ['documents.read'],
            isActive: true,
          },
        },
      });
    if (path.startsWith('/history/')) {
      const body = route.request().postDataJSON();
      deletions.push({ path, body });
      if (state.fail)
        return route.fulfill({ status: 500, json: { title: 'Suppression indisponible' } });
      const remove = (id: string) =>
        body.all ? !body.excludeIds.includes(id) : body.ids.includes(id);
      let deleted: number;
      if (path.includes('/statements/')) {
        const old = events.length;
        events = events.filter((e) => !remove(e.id));
        deleted = old - events.length;
      } else {
        const old = profiles.length;
        profiles = profiles.filter((e) => !remove(e.id));
        deleted = old - profiles.length;
      }
      return route.fulfill({ json: { deleted } });
    }
    if (path === '/client-workspace/history' || path === '/extraction-profiles/history') {
      const items = path.includes('client-workspace') ? events : profiles;
      const current = Number(url.searchParams.get('page') || 1);
      return route.fulfill({
        json: {
          items: items.slice((current - 1) * 20, current * 20),
          total: items.length,
          page: current,
          pageSize: 20,
        },
      });
    }
    return route.fulfill({ json: {} });
  });
  return { deletions, state };
}

test('Admin deletes one action, a selection, and all pages with exclusions and active filters', async ({
  page,
}) => {
  const { deletions } = await mockHistory(page);
  await page.goto('/history?statementId=statement-1');
  const history = page.getByRole('region', { name: 'Historique des relevés' });
  await expect(history.locator('article')).toHaveCount(20);
  await history
    .locator('article')
    .first()
    .getByRole('button', { name: 'Supprimer cette action' })
    .click();
  await page.getByRole('button', { name: 'Annuler', exact: true }).click();
  expect(deletions).toHaveLength(0);
  await history
    .locator('article')
    .first()
    .getByRole('button', { name: 'Supprimer cette action' })
    .click();
  await page.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(history.getByText('releve-0.pdf', { exact: true })).toHaveCount(0);
  expect(deletions[0].body.ids).toEqual(['event-0']);
  await history.locator('article').nth(0).getByRole('checkbox').check();
  await history.locator('article').nth(1).getByRole('checkbox').check();
  await history.getByRole('button', { name: 'Supprimer la sélection' }).click();
  await page.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(
    history.getByRole('button', { name: 'Tout sélectionner (22)', exact: true }),
  ).toBeVisible();
  expect(deletions[1].body.ids).toEqual(['event-1', 'event-2']);
  await history.getByRole('button', { name: 'Tout sélectionner (22)', exact: true }).click();
  await history.locator('article').first().getByRole('checkbox').uncheck();
  await history.getByRole('button', { name: 'Suivant', exact: true }).click();
  await expect(history.locator('article')).toHaveCount(2);
  await expect(history.locator('article').first().getByRole('checkbox')).toBeChecked();
  await history.getByRole('button', { name: 'Supprimer la sélection' }).click();
  await expect(page.getByRole('dialog')).toContainText('21 action(s)');
  await page.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(history.locator('article')).toHaveCount(1);
  await expect(history).toContainText('releve-3.pdf');
  await expect(history).toContainText('Page 1 sur 1');
  expect(deletions[2].body).toEqual({
    all: true,
    ids: [],
    excludeIds: ['event-3'],
    statementId: 'statement-1',
  });
});

test('Selection survives an error; profile history has independent deletion controls; mobile fits', async ({
  page,
}) => {
  const { state, deletions } = await mockHistory(page);
  await page.goto('/history');
  const history = page.getByRole('region', { name: 'Historique des relevés' });
  await history.getByRole('checkbox', { name: 'Sélectionner cette page' }).check();
  await mkdir('../output/screenshots', { recursive: true });
  await page.screenshot({ path: '../output/screenshots/history-admin-desktop.png' });
  await expect(history.getByRole('status').filter({ hasText: 'sélectionnée' })).toContainText(
    '20 action(s)',
  );
  state.fail = true;
  await history.getByRole('button', { name: 'Supprimer la sélection' }).click();
  await page.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(page.getByRole('dialog').getByRole('alert')).toContainText(
    'Suppression indisponible',
  );
  state.fail = false;
  await page.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(history.locator('article')).toHaveCount(5);
  const profiles = page.locator('details.history-profiles');
  await profiles.locator(':scope > summary').click();
  await profiles.getByRole('button', { name: 'Tout sélectionner (2)', exact: true }).click();
  await profiles.getByRole('button', { name: 'Supprimer la sélection' }).click();
  await page.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(profiles.locator('article')).toHaveCount(0);
  expect(deletions.at(-1)?.path).toBe('/history/profiles/delete-selection');
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(
    history.getByRole('button', { name: 'Tout sélectionner (5)', exact: true }),
  ).toBeVisible();
  await history.getByRole('button', { name: 'Tout sélectionner (5)', exact: true }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: '../output/screenshots/history-admin-mobile.png' });
});

test('Collaborators cannot see history selection or deletion buttons', async ({ page }) => {
  await mockHistory(page, false);
  await page.goto('/history');
  await expect(page.locator('.history-event')).toHaveCount(20);
  await page.locator('details.history-profiles > summary').click();
  await expect(page.getByRole('checkbox')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Supprimer|Tout sélectionner/ })).toHaveCount(0);
});
