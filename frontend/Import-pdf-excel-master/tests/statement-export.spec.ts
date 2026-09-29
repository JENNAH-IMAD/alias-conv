import { test, expect, type Page } from '@playwright/test';

const item = { id: '12345678-abcd', clientId: 'client', bankAccountId: 'account', client: 'Client test', bank: 'Banque test', accountName: 'Compte principal', accountNumber: '123456', inputFileName: 'releve.pdf', outputFileName: 'export.txt', status: 'Completed', count: 2, createdAt: '2026-09-29T09:00:00Z', completedAt: '2026-09-29T10:00:00Z', processedByName: 'Sarah Traitement', processedAt: '2026-09-29T10:00:00Z', modifiedByName: 'Amine Modification', modifiedAt: '2026-09-29T09:30:00Z' };
const payload = '01/09/2026;01/09/2026;Opération;0,00;125,50';
async function setup(page: Page) {
  await page.addInitScript(() => sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'test', expiresAt: '2099-01-01', user: { id: 'admin', name: 'Admin', role: 'Admin' } })));
  await page.route('**/api/**', route => new URL(route.request().url()).pathname.endsWith('/download') ? route.fulfill({ body: payload, contentType: 'text/plain' }) : route.fulfill({ json: { items: [item], total: 1 } }));
  await page.goto('/conversions/history');
}

test('historique : auteurs, banque, compte et téléchargement renommé', async ({ page }) => {
  await setup(page);
  await expect(page.getByText('Traité par : Sarah Traitement')).toBeVisible();
  await expect(page.getByText('Modifié par : Amine Modification')).toBeVisible();
  await expect(page.getByText('Banque test · Compte principal')).toBeVisible();
  await page.getByRole('button', { name: 'Télécharger releve.pdf' }).click();
  await page.getByLabel('Nom du fichier').fill('../interdit.txt');
  await page.getByRole('dialog').getByRole('button', { name: 'Télécharger', exact: true }).click();
  await expect(page.getByRole('dialog').getByRole('alert')).toBeVisible();
  await page.getByLabel('Nom du fichier').fill('Mon_releve.txt');
  const pending = page.waitForEvent('download');
  await page.getByRole('dialog').getByRole('button', { name: 'Télécharger', exact: true }).click();
  const download = await pending;
  expect(download.suggestedFilename()).toBe('Mon_releve.txt');
  const stream = await download.createReadStream();
  const chunks = []; for await (const chunk of stream!) chunks.push(chunk);
  expect(Buffer.concat(chunks).toString()).toBe(payload);
  await expect(page.getByRole('status')).toContainText('Téléchargement de');
});

test('choix du dossier : annulation sans téléchargement puis enregistrement', async ({ page }) => {
  await page.addInitScript(() => {
    let calls = 0;
    Object.defineProperty(window, 'showSaveFilePicker', { configurable: true, value: async () => {
      if (++calls === 1) throw new DOMException('Annulé', 'AbortError');
      return { name: 'Choisi.txt', createWritable: async () => ({ write: async (blob: Blob) => { document.documentElement.dataset.savedContent = await blob.text(); }, close: async () => {}, abort: async () => {} }) };
    } });
  });
  await setup(page);
  await page.getByRole('button', { name: 'Télécharger releve.pdf' }).click();
  await page.getByRole('button', { name: 'Choisir le dossier et enregistrer' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('alert')).toHaveCount(0);
  await page.getByRole('button', { name: 'Choisir le dossier et enregistrer' }).click();
  await expect(page.getByRole('status')).toContainText('Choisi.txt');
  expect(await page.locator('html').getAttribute('data-saved-content')).toBe(payload);
});
