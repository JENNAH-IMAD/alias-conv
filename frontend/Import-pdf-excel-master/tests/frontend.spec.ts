import { test, expect, type Page } from '@playwright/test';

const client = { id: 'client-demo', name: 'Atlas Conseil', legalName: 'Atlas Conseil SARL', ice: '123456789012345', if: '123456', rc: '1000', email: 'contact@example.test', phone: '', country: 'Maroc', banks: ['Banque de démonstration'], photoVersion: null, status: 'Actif' };
const account = { id: 'account-demo', clientId: client.id, bankId: 'bank-demo', bank: 'Banque de démonstration', accountNumber: '123456789', accountName: 'Compte courant', currency: 'MAD', journal: 'BQ', accountCode: '512000' };
const history = { id: 'document-demo', clientId: client.id, bankAccountId: account.id, client: client.name, bank: account.bank, inputFileName: 'releve-septembre.pdf', outputFileName: 'export.csv', status: 'Completed', errorMessage: null, count: 42, createdAt: '2026-09-28T09:00:00Z', completedAt: '2026-09-28T09:01:00Z' };

async function setup(page: Page) {
  await page.addInitScript(() => {
    sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'frontend-test-only', expiresAt: '2099-01-01T00:00:00Z', user: { id: 'test', name: 'Amine Test', email: 'test@example.test', role: 'Admin' } }));
    localStorage.setItem('bank-converter-theme', 'light');
  });
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    const endpoint = url.pathname.replace('/api', '');
    let body: unknown;
    if (endpoint === '/dashboard') body = { clients: 12, banks: 5, completed: 38, failed: 2, conversions: 40, recent: [history, { ...history, id: 'failed', status: 'Failed', count: 0 }] };
    else if (endpoint === '/documents' && route.request().method() === 'POST') body = { ...history, status: 'Pending' };
    else if (endpoint.includes('/archives')) body = [];
    else { const items = endpoint === '/clients' ? [client] : endpoint === '/bank-accounts' ? [account] : endpoint === '/history' ? [history] : []; body = { items, total: items.length, page: 1, pageSize: 20 }; }
    await route.fulfill({ json: body });
  });
}

test('pages principales : bureau, mobile et thèmes', async ({ page }) => {
  await setup(page);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: 'Bonjour, Amine.' })).toBeVisible();
  await expect(page.getByText('38', { exact: true })).toBeVisible();
  await page.screenshot({ path: '../../docs/frontend-dashboard-light.png', fullPage: true });
  await page.getByRole('button', { name: 'Activer le thème sombre' }).click();
  await expect(page.locator('.theme-root')).toHaveAttribute('data-theme', 'dark');
  await page.screenshot({ path: '../../docs/frontend-dashboard-dark.png', fullPage: true });
  await page.getByRole('button', { name: 'Activer le thème clair' }).click();
  for (const route of ['/conversions', '/conversions/history', '/clients', '/banks', '/bank-accounts', '/templates', '/extraction-profiles', '/client-workspace', '/settings']) {
    await page.goto(route);
    await expect(page.locator('main h1')).toBeVisible();
    await expect(page.locator('main [role="alert"]')).toHaveCount(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
  }
  await page.setViewportSize({ width: 390, height: 844 });
  for (const route of ['/dashboard', '/conversions', '/conversions/history', '/clients', '/banks', '/bank-accounts', '/templates', '/extraction-profiles', '/client-workspace', '/settings']) {
    await page.goto(route);
    await expect(page.locator('main h1')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), route).toBeTruthy();
  }
  await page.goto('/dashboard');
  await page.getByRole('button', { name: 'Ouvrir le menu' }).click();
  await expect(page.getByRole('dialog', { name: 'Navigation principale' })).toBeVisible();
  await page.getByRole('dialog').getByRole('link', { name: 'Conversions', exact: true }).click();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.getByRole('heading', { name: 'Un relevé. Trois étapes.' })).toBeVisible();
  await page.screenshot({ path: '../../docs/frontend-import-mobile.png', fullPage: true });
  expect(errors).toEqual([]);
});

test('import : validation, sélection du compte et envoi unique', async ({ page }) => {
  await setup(page);
  let uploads = 0;
  await page.route('**/api/documents', async route => {
    uploads++;
    const body = route.request().postDataBuffer()?.toString() || '';
    expect(body).toContain('client-demo'); expect(body).toContain('account-demo');
    await route.fulfill({ json: { ...history, status: 'Pending' } });
  });
  await page.goto('/conversions?clientId=client-demo');
  await expect(page.getByRole('combobox', { name: 'Client', exact: true })).toHaveValue(client.id);
  await page.getByRole('combobox', { name: 'Compte bancaire', exact: true }).selectOption(account.id);
  await page.getByLabel('Relevé PDF', { exact: true }).setInputFiles({ name: 'incorrect.txt', mimeType: 'text/plain', buffer: Buffer.from('test') });
  await expect(page.getByRole('main').getByRole('alert')).toContainText('format PDF');
  await expect(page.getByRole('button', { name: 'Importer le relevé', exact: true })).toBeDisabled();
  await page.getByLabel('Relevé PDF', { exact: true }).setInputFiles({ name: 'vide.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(0) });
  await expect(page.getByRole('main').getByRole('alert')).toContainText('vide');
  await page.getByLabel('Relevé PDF', { exact: true }).setInputFiles({ name: 'grand.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(20 * 1024 * 1024 + 1) });
  await expect(page.getByRole('main').getByRole('alert')).toContainText('20 Mo');
  await page.getByLabel('Relevé PDF', { exact: true }).setInputFiles({ name: 'releve.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\nfrontend test') });
  await expect(page.getByRole('main').getByRole('alert')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Importer le relevé', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Retirer le fichier' }).click();
  await expect(page.getByRole('button', { name: 'Importer le relevé', exact: true })).toBeDisabled();
  const transfer = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.items.add(new File(['%PDF-1.4\nfrontend test'], 'depose.pdf', { type: 'application/pdf' }));
    return data;
  });
  await page.getByRole('heading', { name: 'Glissez votre relevé ici' }).locator('..').dispatchEvent('drop', { dataTransfer: transfer });
  await expect(page.getByText('depose.pdf')).toBeVisible();
  await transfer.dispose();
  await page.getByRole('button', { name: 'Importer le relevé', exact: true }).click();
  await expect(page.getByText('Votre relevé a bien été importé')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Importer le relevé', exact: true })).toBeDisabled();
  await expect(page.getByRole('link', { name: 'Ouvrir l’espace client' })).toHaveAttribute('href', '/client-workspace?clientId=client-demo');
  expect(uploads).toBe(1);
});

test('tableau de bord : erreur récupérable et état vide', async ({ page }) => {
  await setup(page);
  let failing = true;
  await page.route('**/api/dashboard', route => failing ? route.fulfill({ status: 503, json: { title: 'Service temporairement indisponible' } }) : route.fulfill({ json: { clients: 0, banks: 0, completed: 0, failed: 0, conversions: 0, recent: [] } }));
  await page.goto('/dashboard');
  await expect(page.getByRole('main').getByRole('alert')).toContainText('Service temporairement indisponible');
  failing = false;
  await page.getByRole('button', { name: 'Réessayer' }).click();
  await expect(page.getByRole('heading', { name: 'Votre premier relevé vous attend' })).toBeVisible();
});

test('historique : filtre depuis le tableau de bord et recherche', async ({ page }) => {
  await setup(page);
  await page.goto('/dashboard');
  await page.getByRole('link', { name: /Conversions échouées/ }).click();
  await expect(page.getByLabel('Statut', { exact: true })).toHaveValue('Failed');
  const searchRequest = page.waitForRequest(request => request.url().includes('/api/history?') && new URL(request.url()).searchParams.get('search') === 'Atlas');
  await page.getByRole('searchbox', { name: 'Recherche' }).fill('Atlas');
  await searchRequest;
});

test('connexion : champs accessibles et affichage du mot de passe', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Adresse e-mail').fill('test@example.test');
  await page.getByLabel('Mot de passe', { exact: true }).fill('exemple');
  await page.getByRole('button', { name: 'Afficher le mot de passe' }).click();
  await expect(page.getByLabel('Mot de passe', { exact: true })).toHaveAttribute('type', 'text');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
});



test('comptes et défilement indépendant du menu et de la page', async ({ page }) => {
  await setup(page);
  await page.route('**/api/bank-accounts?**', route => route.fulfill({ json: { items: Array.from({ length: 12 }, (_, index) => ({ ...account, id: `account-${index}`, accountName: `Compte bancaire ${index + 1}`, accountNumber: '01178000012345678901234' })), total: 12, page: 1, pageSize: 20 } }));
  await page.setViewportSize({ width: 1440, height: 700 });
  await page.goto('/bank-accounts');
  await expect(page.getByRole('heading', { name: 'Compte bancaire 1', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Activer le thème sombre' }).click();
  await page.screenshot({ path: '../../docs/frontend-accounts-modern.png' });
  const nav = page.locator('.desktop-sidebar .sidebar-nav');
  const scroll = page.locator('.app-scroll');
  await nav.evaluate(element => { element.scrollTop = element.scrollHeight; });
  expect(await nav.evaluate(element => element.scrollTop)).toBeGreaterThan(0);
  expect(await scroll.evaluate(element => element.scrollTop)).toBe(0);
  await expect(page.locator('.desktop-sidebar .logout-button')).toBeInViewport();
  await scroll.evaluate(element => { element.scrollTop = element.scrollHeight; });
  await expect(page.getByRole('heading', { name: 'Compte bancaire 12', exact: true })).toBeInViewport();
  await expect(page.locator('.topbar')).toBeInViewport();
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  await page.getByRole('link', { name: 'Vue d’ensemble', exact: true }).click();
  await expect(page.locator('.app-scroll')).toHaveJSProperty('scrollTop', 0);
  await page.setViewportSize({ width: 390, height: 600 });
  await page.goto('/bank-accounts');
  await expect(page.getByRole('heading', { name: 'Compte bancaire 1', exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await page.getByRole('button', { name: 'Ouvrir le menu' }).click();
  await expect(page.getByRole('dialog').getByRole('button', { name: 'Se déconnecter' })).toBeInViewport();
  await page.getByRole('dialog').getByRole('link', { name: 'Paramètres', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Profil et préférences' })).toBeVisible();
});

test('utilisateurs : gestion admin et accès réservé', async ({ page }) => {
  await setup(page);
  const managed = { id: 'user-2', name: 'Utilisateur Démo', email: 'demo@example.test', role: 'User', createdAt: '2026-09-28T09:00:00Z' };
  let rows = [managed];
  await page.route('**/api/users**', async route => {
    const method = route.request().method();
    if (method === 'POST') { const input = route.request().postDataJSON(); rows = [...rows, { ...input, id: 'new-user', createdAt: managed.createdAt }]; await route.fulfill({ status: 201, json: rows[1] }); }
    else if (method === 'PUT') { const input = route.request().postDataJSON(); rows = rows.map(item => item.id === managed.id ? { ...item, ...input } : item); await route.fulfill({ json: rows[0] }); }
    else if (method === 'DELETE') { rows = rows.filter(item => item.id !== managed.id); await route.fulfill({ status: 204 }); }
    else await route.fulfill({ json: { items: rows, total: rows.length, page: 1, pageSize: 20 } });
  });
  await page.goto('/users');
  await expect(page.getByRole('heading', { name: 'Utilisateurs', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Nouvel utilisateur' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Nom complet').fill('Nouveau Test');
  await dialog.getByLabel('Adresse e-mail').fill('new@example.test');
  await dialog.getByLabel('Mot de passe', { exact: true }).fill('TestPassword123!');
  await dialog.getByRole('button', { name: 'Enregistrer' }).click();
  await expect(page.getByText('Nouveau Test', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Modifier Utilisateur Démo' }).click();
  await dialog.getByRole('combobox', { name: 'Rôle', exact: true }).selectOption('Admin');
  await dialog.getByRole('button', { name: 'Enregistrer' }).click();
  await expect(page.getByRole('row').filter({ hasText: 'Utilisateur Démo' })).toContainText('Administrateur');
  await page.getByRole('button', { name: 'Supprimer Utilisateur Démo' }).click();
  await dialog.getByRole('button', { name: 'Confirmer la suppression' }).click();
  await expect(page.getByText('Utilisateur Démo', { exact: true })).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 700 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await page.screenshot({ path: '../../docs/frontend-users-mobile.png' });
  await page.unrouteAll({ behavior: 'wait' });
  await page.addInitScript(() => { sessionStorage.setItem('bank-converter-session', JSON.stringify({ token: 'test', expiresAt: '2099-01-01', user: { id: 'regular', name: 'Regular', role: 'User' } })); });
  let requests = 0;
  await page.route('**/api/users**', async route => { requests++; await route.fulfill({ status: 403 }); });
  await page.goto('/users');
  await expect(page.getByRole('heading', { name: 'Accès réservé' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Utilisateurs', exact: true })).toHaveCount(0);
  expect(requests).toBe(0);
});


test('traitement intégré : étapes consultables et corrections conservées', async ({ page }) => {
  await setup(page);
  const doc = { ...history, bankId: 'bank-demo', account: account.accountNumber, transactionCount: 1, hasOriginal: false, hasStandard: true, hasExport: true, bankStatementTemplateId: 'profile', exportTemplateId: 'export' };
  const profile = { id: 'profile', name: 'Lecture automatique', bankId: null, parserKey: 'multibank-auto', isActive: true, dateFormat: 'dd/MM/yyyy', numberCulture: 'fr-FR' };
  const template = { id: 'export', name: 'Export test', type: 'CUSTOMCSV', fileExtension: 'csv', encoding: 'utf-8', delimiter: ';', isActive: true, fields: [{ fieldName: 'Libellé', sourceField: 'Description', position: 0 }] };
  await page.route('**/api/bank-statement-templates?**', route => route.fulfill({ json: { items: [profile], total: 1 } }));
  await page.route('**/api/export-templates?**', route => route.fulfill({ json: { items: [template], total: 1 } }));
  await page.route('**/api/client-workspace/**', route => route.fulfill({ json: route.request().url().includes('/preview/') ? { rows: [['Date', 'Description', 'Debit', 'Credit', 'Amount', 'Balance'], ['2026-09-01', 'Opération test', '10', '0', '-10', '100']], truncated: false } : [doc] }));
  await page.goto('/client-workspace');
  await page.getByRole('button', { name: 'Consulter les étapes du traitement' }).click();
  const panel = page.getByRole('region', { name: /Étapes du traitement releve/ });
  await expect(panel.getByRole('heading', { name: 'Tableau de bord du traitement' })).toBeVisible();
  await expect(page.locator('dialog[open]')).toHaveCount(0);
  await panel.getByRole('button', { name: 'Modifier', exact: true }).click();
  await panel.getByRole('textbox', { name: 'Libelle', exact: true }).fill('Correction conservée');
  await panel.getByRole('button', { name: '2 Lecture et configuration' }).click();
  await expect(panel.getByRole('combobox', { name: 'Profil de lecture PDF' })).toHaveValue('profile');
  await expect(panel.getByRole('heading', { name: 'Correspondance des champs' })).toBeVisible();
  await panel.getByRole('button', { name: '1 Document importé' }).click();
  await expect(panel.getByText('releve-septembre.pdf', { exact: true })).toBeVisible();
  await panel.getByRole('button', { name: '3 Contrôle et export' }).click();
  await expect(panel.getByRole('textbox', { name: 'Libelle', exact: true })).toHaveValue('Correction conservée');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await panel.getByRole('button', { name: '2 Lecture et configuration' }).click();
  await panel.scrollIntoViewIfNeeded();
  await page.screenshot({ path: '../../docs/traitement-integre-mobile.png' });
});

test('traitement intégré : configuration et résultat sans dialogue', async ({ page }) => {
  await setup(page);
  const doc = { ...history, status: 'Pending', bankId: 'bank-demo', account: account.accountNumber, transactionCount: 0, hasOriginal: true, hasStandard: false, hasExport: false };
  await page.route('**/api/client-workspace/**', route => route.request().url().includes('/files/original') ? route.fulfill({ contentType: 'application/pdf', body: '%PDF-1.4' }) : route.fulfill({ json: [doc] }));
  await page.route('**/api/bank-statement-templates?**', route => route.fulfill({ json: { items: [{ id: 'p', name: 'Automatique', bankId: null, parserKey: 'multibank-auto', isActive: true }], total: 1 } }));
  await page.route('**/api/export-templates?**', route => route.fulfill({ json: { items: [{ id: 'e', name: 'CSV', type: 'CUSTOMCSV', fileExtension: 'csv', encoding: 'utf-8', delimiter: ';', fields: [], isActive: true }], total: 1 } }));
  let calls = 0;
  await page.route('**/api/documents/*/process', route => { calls++; return route.fulfill({ json: { history, transactions: [{ id: 't', date: '2026-09-01', description: 'Résultat test', reference: '', debit: 10, credit: 0, amount: -10, balance: 100 }] } }); });
  await page.goto('/client-workspace');
  await page.getByRole('button', { name: 'Configurer et traiter' }).click();
  const panel = page.getByRole('region', { name: /Étapes du traitement releve/ });
  await panel.getByRole('combobox', { name: 'Profil de lecture PDF' }).selectOption('p');
  await panel.getByRole('combobox', { name: 'Format d’export' }).selectOption('e');
  await panel.getByRole('button', { name: 'Lancer le traitement' }).click();
  await expect(panel.getByRole('heading', { name: 'Tableau de bord du traitement' })).toBeVisible();
  await panel.getByRole('button', { name: '2 Lecture et configuration' }).click();
  await expect(panel.getByRole('combobox', { name: 'Profil de lecture PDF' })).toBeDisabled();
  await expect(page.locator('dialog[open]')).toHaveCount(0);
  expect(calls).toBe(1);
});
