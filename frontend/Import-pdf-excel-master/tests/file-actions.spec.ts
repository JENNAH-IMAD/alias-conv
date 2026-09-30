import { test, expect } from '@playwright/test';

test('cartes fichiers : aperçus visibles, téléchargement et Excel dans les deux thèmes', async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'test', expiresAt: '2099-01-01', user: { id: 'test', name: 'Admin', role: 'Admin' } })));
  const doc = { id: 'document', clientId: 'client', inputFileName: 'releve.pdf', outputFileName: 'export.txt', bank: 'Banque Atlas', accountName: 'Compte principal', account: '123456789', status: 'Completed', hasOriginal: true, hasStandard: true, hasExport: true, transactionCount: 2, createdAt: '2026-09-29', completedAt: '2026-09-29', processedByName: 'Sarah', modifiedByName: 'Amine' };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path.includes('/preview/')) return route.fulfill({ json: { rows: [['Date', 'ValueDate', 'Reference', 'Description', 'Debit', 'Credit', 'Balance', 'Amount', 'Direction'], ['2026-09-29', '2026-09-28', '', 'Opération test', '0.00', '3207.62', '18453.37', '3207.62', 'C']], truncated: false } });
    if (path.includes('/files/')) return route.fulfill({ body: path.endsWith('/original') ? '%PDF-1.4' : '01/09/2026;Opération;123,45', contentType: path.endsWith('/original') ? 'application/pdf' : 'application/octet-stream' });
    if (path.includes('/archives')) return route.fulfill({ json: [doc] });
    return route.fulfill({ json: { items: [{ id: 'client', name: 'Atlas Conseil', legalName: 'Atlas Conseil' }], total: 1 } });
  });
  await page.goto('/client-workspace');
  for (const theme of ['clair', 'sombre']) {
    await page.getByRole('button', { name: `Activer le thème ${theme}` }).click();
    for (const label of ['PDF original', 'CSV standard', 'Export comptable']) {
      const card = page.getByRole('region', { name: `${label} — releve.pdf`, exact: true });
      await card.getByRole('button', { name: /Consulter|Aperçu ouvert/ }).click();
      const preview = page.getByRole('region', { name: 'Aperçu du fichier sélectionné' });
      await expect(preview).toBeInViewport();
      await expect(preview).toBeFocused();
      if (label === 'PDF original') await expect(preview.locator('iframe')).toBeVisible();
      else await expect(preview.getByText('Opération test')).toBeVisible();
      if (label === 'CSV standard') {
        for (const width of [390, 768, 1440]) {
          await page.setViewportSize({ width, height: 900 });
          const tableRegion = preview.getByRole('region', { name: 'Tableau du fichier — défilement horizontal' });
          await expect(tableRegion.getByRole('columnheader', { name: 'Description', exact: true })).toBeVisible();
          const bounds = await preview.boundingBox();
          expect(bounds!.x).toBeGreaterThanOrEqual(0);
          expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
          expect(await page.locator('.app-scroll').evaluate(node => node.scrollWidth <= node.clientWidth + 1)).toBeTruthy();
          await tableRegion.scrollIntoViewIfNeeded();
          await tableRegion.evaluate(node => { node.scrollLeft = node.scrollWidth; });
          await expect(tableRegion.getByRole('cell', { name: 'C', exact: true })).toBeInViewport();
        }
        await page.setViewportSize({ width: 1280, height: 720 });
      }
      await card.getByRole('button', { name: 'Télécharger', exact: true }).click();
      await expect(page.getByRole('dialog', { name: 'Enregistrer le fichier' })).toBeVisible();
      const pending = page.waitForEvent('download');
      await page.getByRole('dialog').getByRole('button', { name: 'Télécharger', exact: true }).click();
      expect((await pending).suggestedFilename()).toContain('Banque_Atlas_Compte_principal');
      if (label !== 'PDF original') {
        await card.getByRole('button', { name: 'Excel (.xlsx)', exact: true }).click();
        await expect(page.getByLabel('Nom du fichier')).toHaveValue(/\.xlsx$/);
        await page.getByRole('button', { name: 'Annuler', exact: true }).click();
      }
    }
    await page.getByRole('region', { name: 'CSV standard — releve.pdf', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: `../../docs/saas-files-${theme}.png` });
  }
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
});
