import { test, expect, Page } from '@playwright/test';
import { mkdir, readFile } from 'node:fs/promises';
import statementExample from '../../data/examples/normalized-statement.json';
const dashboardExample = {
  clients: 12,
  banks: 5,
  accounts: 18,
  statements: 42,
  exports: 64,
  pendingReview: 8,
  unassigned: 3,
  statuses: [
    { status: 'EXPORTED', count: 24 },
    { status: 'VALIDATED', count: 9 },
    { status: 'NEEDS_REVIEW', count: 8 },
    { status: 'FAILED', count: 1 },
  ],
  activity: Array.from({ length: 90 }, (_, index) => ({
    date: new Date(Date.UTC(2026, 6, 15 + index)).toISOString().slice(0, 10),
    imports: (index * 7) % 9,
    exports: (index * 5) % 7,
  })),
  recent: [
    {
      id: 'statement-1',
      sourceFileName: 'releve_septembre.pdf',
      bankName: 'Crédit Agricole',
      statementNumber: 'EXEMPLE-202609',
      clientId: 'client-1',
      clientName: 'Atlas Conseil',
      accountName: 'Compte principal',
      status: 'EXPORTED',
      transactionCount: 22,
      createdAt: '2026-10-12T09:00:00Z',
    },
    {
      id: 'statement-2',
      sourceFileName: 'releve_a_verifier.pdf',
      bankName: 'Crédit Du Maroc',
      statementNumber: null,
      clientId: null,
      clientName: null,
      accountName: null,
      status: 'NEEDS_REVIEW',
      transactionCount: 44,
      createdAt: '2026-10-11T11:00:00Z',
    },
  ],
  updatedAt: '2026-10-12T10:00:00Z',
};
const admin = {
  id: 'admin-1',
  name: 'Amine Admin',
  email: 'amine@example.test',
  role: 'Admin',
  isActive: true,
  permissions: [] as string[],
  version: 1,
  lastLoginAt: null,
  createdAt: '2026-10-08T09:00:00Z',
};
const readPermissions = [
  'dashboard.read',
  'clients.read',
  'banks.read',
  'accounts.read',
  'templates.read',
  'documents.read',
];
const catalog = [
  { key: 'clients.read', group: 'Clients', label: 'Consulter', requires: [] },
  { key: 'accounts.read', group: 'Comptes', label: 'Consulter', requires: [] },
  { key: 'banks.read', group: 'Banques', label: 'Consulter', requires: [] },
  { key: 'templates.read', group: 'Modèles', label: 'Consulter', requires: [] },
  {
    key: 'documents.read',
    group: 'Relevés et archives',
    label: 'Consulter les PDF, mouvements et historiques',
    requires: ['clients.read', 'accounts.read', 'banks.read', 'templates.read'],
  },
  {
    key: 'documents.write',
    group: 'Relevés et archives',
    label: 'Importer, adapter, corriger et valider',
    requires: ['documents.read'],
  },
  {
    key: 'documents.export',
    group: 'Relevés et archives',
    label: 'Télécharger les fichiers et exports',
    requires: ['documents.read'],
  },
];
const pagination = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 20 });
const client = {
  id: 'client-1',
  name: 'Atlas Conseil',
  legalName: 'Atlas Conseil SARL',
  email: 'atlas@example.test',
  ice: '000000000000001',
  if: '1',
  rc: '1',
  phone: '',
  country: 'Maroc',
  banks: ['Crédit Agricole'],
  status: 'Actif',
  photoVersion: null,
};
async function routes(page: Page, options: { setup?: boolean; reader?: boolean } = {}) {
  let user: typeof admin | null = options.setup
    ? null
    : options.reader
      ? {
          ...admin,
          id: 'reader-1',
          name: 'Collaborateur lecture',
          role: 'User',
          permissions: readPermissions,
        }
      : admin;
  const users = [admin];
  await page.route('**/api/**', async (route) => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    const method = route.request().method();
    const respond = (body: unknown) => route.fulfill({ json: body });
    if (path === '/auth/session') return respond({ setupRequired: !!options.setup && !user, user });
    if (path === '/auth/setup') {
      expect(route.request().headers()['x-alias-request']).toBe('1');
      expect(route.request().postDataJSON()).toMatchObject({
        name: 'Amine Admin',
        email: 'amine@example.test',
        password: 'Fictional-password-2026',
      });
      user = admin;
      return respond(admin);
    }
    if (path === '/auth/logout') {
      user = null;
      return route.fulfill({ status: 204 });
    }
    if (path === '/users/permissions')
      return respond({
        permissions: catalog,
        presets: {
          reader: catalog.filter((p) => p.key.endsWith('.read')).map((p) => p.key),
          accountant: catalog.map((p) => p.key),
        },
      });
    if (path === '/users' && method === 'POST') {
      const input = route.request().postDataJSON();
      expect(input.permissions).toContain('documents.export');
      expect(input.permissions).toContain('documents.read');
      users.push({ ...admin, ...input, id: 'new-user', version: 1 });
      return respond(users.at(-1));
    }
    if (path === '/users') return respond(pagination(users));
    if (path === '/clients') return respond(pagination([client]));
    if (path === '/banks')
      return respond(pagination([{ id: 'bank-1', name: 'Crédit Agricole', code: 'CA' }]));
    if (path === '/bank-accounts')
      return respond(
        pagination([
          {
            id: 'account-1',
            clientId: client.id,
            bankId: 'bank-1',
            bank: 'Crédit Agricole',
            accountName: 'Compte principal',
            accountNumber: 'DEMO1234',
            currency: 'MAD',
            journal: 'BQ',
            accountCode: '512000',
          },
        ]),
      );
    if (path === '/documents') return respond(pagination([]));
    if (path === '/statement-imports') return respond(pagination([]));
    if (path === '/export-templates')
      return respond(
        pagination([
          {
            id: 'x3',
            name: 'Sage X3 ASCII',
            type: 'SAGEX3',
            description: 'Soldes et mouvements bancaires',
            fileExtension: 'txt',
            delimiter: ';',
            encoding: 'ascii',
            numberCulture: 'en-US',
            includeHeader: false,
            isActive: true,
            fields: [
              {
                fieldName: 'Date opération',
                sourceField: 'Date',
                position: 0,
                format: 'yyyyMMdd',
                required: true,
              },
            ],
          },
        ]),
      );
    if (path === '/dashboard') return respond(dashboardExample);
    if (path === '/extraction-profiles/history' || path === '/client-workspace/history')
      return respond(pagination([]));
    if (
      path === '/client-workspace/client-1/statements' ||
      path === '/client-workspace/statements'
    ) {
      const params = new URL(route.request().url()).searchParams;
      const assigned = {
        id: 'statement-1',
        clientId: 'client-1',
        clientName: client.name,
        bankAccountId: 'account-1',
        bankName: 'Crédit Agricole',
        sourceFileName: 'releve_mars.pdf',
        periodStart: '2026-03-01',
        periodEnd: '2026-03-31',
        accountName: 'Compte principal',
        accountNumber: 'DEMO1234',
        status: 'VALIDATED',
        version: 2,
        transactionCount: 22,
        createdAt: '2026-10-08T09:00:00Z',
        importedByName: admin.name,
        modifiedByName: null,
        exportCount: 1,
        lastExportAt: '2026-10-08T09:15:00Z',
      };
      let items = path.includes('client-1/')
        ? [assigned]
        : [
            assigned,
            ...Array.from({ length: 21 }, (_, i) => ({
              ...assigned,
              id: `unassigned-${i + 1}`,
              clientId: null,
              clientName: null,
              bankAccountId: null,
              sourceFileName: `releve_a_classer_${i + 1}.pdf`,
              accountName: null,
              accountNumber: null,
              status: 'NEEDS_REVIEW',
              exportCount: 0,
            })),
          ];
      if (params.get('unassigned') === 'true') items = items.filter((item) => !item.clientId);
      if (params.get('status'))
        items = items.filter((item) => item.status === params.get('status'));
      if (params.get('search'))
        items = items.filter((item) =>
          `${item.sourceFileName} ${item.clientName || ''} ${item.bankName}`
            .toLowerCase()
            .includes(params.get('search')!.toLowerCase()),
        );
      const pageNumber = Number(params.get('page') || 1);
      return respond({
        items: items.slice((pageNumber - 1) * 20, pageNumber * 20),
        total: items.length,
        page: pageNumber,
        pageSize: 20,
      });
    }
    if (/^\/statement-imports\/(statement-1|unassigned-\d+)$/.test(path)) {
      const id = path.split('/').at(-1)!;
      return respond({
        id,
        status: id === 'statement-1' ? 'VALIDATED' : 'NEEDS_REVIEW',
        version: 2,
        errors: [],
        warnings: [],
        clientId: id === 'statement-1' ? client.id : null,
        bankAccountId: id === 'statement-1' ? 'account-1' : null,
        sourceFileName: 'releve.pdf',
        statement: { ...statementExample, id, bankName: 'Crédit Agricole', bankCode: 'CA' },
      });
    }
    if (path === '/statement-imports/statement-1/exports')
      return respond(
        pagination([
          {
            id: 'job-1',
            templateId: 'x3',
            templateName: 'Sage X3 ASCII',
            statementVersion: 2,
            fileName: 'releve_sagex3.txt',
            rowCount: 24,
            extension: 'txt',
            encoding: 'ascii',
            createdAt: '2026-10-08T09:15:00Z',
            status: 'SUCCEEDED',
            hasFile: true,
            generatedByName: admin.name,
            contentSha256: 'ABC',
          },
        ]),
      );
    if (path.endsWith('/archives/job-1/preview'))
      return respond({
        fileName: 'releve_sagex3.txt',
        statementVersion: 2,
        rowCount: 24,
        encoding: 'ascii',
        delimiter: ';',
        generatedByName: admin.name,
        templateNameSnapshot: 'Sage X3 ASCII',
        text: '20260301;20260301;;15245.75;-1;CA;MAD;RBCA032026;1;1\r\n',
        truncated: false,
      });
    if (path.endsWith('/archives/job-1/file'))
      return route.fulfill({
        body: '20260301;20260301;;15245.75;-1;CA;MAD;RBCA032026;1;1\r\n',
        contentType: 'text/plain',
        headers: { 'Content-Disposition': 'attachment; filename="releve_sagex3.txt"' },
      });
    return route.fulfill({ status: 404, json: { title: 'Introuvable' } });
  });
}
test('Tous les relevés, même sans client, restent consultables dans l’espace clients', async ({
  page,
}) => {
  await routes(page);
  await page.goto('/client-workspace');
  await expect(page.getByText('22 éléments · Page 1 sur 2', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'releve_mars.pdf', exact: true })).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'releve_a_classer_1.pdf', exact: true }),
  ).toBeVisible();
  await page.getByLabel('Classement', { exact: true }).selectOption('unassigned');
  await expect(page.getByText('21 éléments · Page 1 sur 2', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'releve_mars.pdf', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Suivant', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'releve_a_classer_21.pdf', exact: true }),
  ).toBeVisible();
  const card = page
    .locator('article')
    .filter({ has: page.getByRole('heading', { name: 'releve_a_classer_21.pdf', exact: true }) });
  await card.getByRole('button', { name: 'Ouvrir le relevé', exact: true }).click();
  const viewer = page.locator('main');
  await expect(page).toHaveURL(/conversions\?statementId=unassigned-21/);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(
    viewer.getByLabel('Tableau des mouvements').getByText('VIREMENT CLIENT', { exact: true }),
  ).toBeVisible();
  await expect(viewer.getByRole('link', { name: 'Voir le PDF original' })).toHaveAttribute(
    'href',
    '/api/statement-imports/unassigned-21/original',
  );
  await viewer.getByRole('button', { name: 'Classer', exact: true }).click();
  const classification = page.locator('main section.ui-card').filter({
    has: page.getByRole('heading', {
      name: 'Classer le relevé dans un dossier client',
      exact: true,
    }),
  });
  await expect(classification).toBeVisible();
  await classification.getByLabel('Client', { exact: true }).selectOption('client-1');
  await expect(
    classification
      .getByLabel('Compte bancaire', { exact: true })
      .getByRole('option', { name: /Compte principal/ }),
  ).toHaveCount(1);
  await classification.getByRole('button', { name: 'Annuler', exact: true }).click();
  await expect(classification).not.toBeVisible();
  await expect(viewer).toBeVisible();
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.locator('.app-scroll').evaluate((el) => {
    el.scrollTop = 0;
  });
  expect(await viewer.evaluate((el) => el.scrollWidth <= el.clientWidth + 1)).toBe(true);
  expect(
    await viewer.locator('.conversion-export-panel').evaluate((panel) => {
      const bounds = panel.getBoundingClientRect();
      return Array.from(panel.querySelectorAll('button')).every(
        (button) => button.getBoundingClientRect().right <= bounds.right + 1,
      );
    }),
  ).toBe(true);
  await viewer.screenshot({
    path: '../output/screenshots/redesign/client-statement-desktop.png',
    animations: 'disabled',
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('.app-scroll').evaluate((el) => {
    el.scrollTop = 0;
  });
  expect(await viewer.evaluate((el) => el.scrollWidth <= el.clientWidth + 1)).toBe(true);
  await viewer.screenshot({
    path: '../output/screenshots/redesign/client-statement-mobile.png',
    animations: 'disabled',
  });
  await page.goto('/client-workspace');
  await page.getByLabel('Classement', { exact: true }).selectOption('all');
  await page.getByLabel('Statut du relevé', { exact: true }).selectOption('VALIDATED');
  await expect(page.getByText('1 élément · Page 1 sur 1', { exact: true })).toBeVisible();
  await page.getByLabel('Statut du relevé', { exact: true }).selectOption('');
  await page.getByLabel('Rechercher un relevé', { exact: true }).fill('releve_a_classer_21.pdf');
  await page.getByRole('button', { name: 'Rechercher les relevés', exact: true }).click();
  await expect(page.getByText('1 élément · Page 1 sur 1', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'releve_a_classer_21.pdf', exact: true }),
  ).toBeVisible();
  await page.getByLabel('Client', { exact: true }).selectOption('client-1');
  await expect(page.getByRole('heading', { name: 'releve_mars.pdf', exact: true })).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'releve_a_classer_21.pdf', exact: true }),
  ).toHaveCount(0);
});

async function openSessionMenu(page: Page) {
  const menu = page.getByRole('menu', { name: 'Apparence et session', exact: true });
  if (!(await menu.isVisible()))
    await page.getByRole('button', { name: 'Apparence et session', exact: true }).click();
  return menu;
}

for (const location of ['desktop', 'compact', 'mobile']) {
  test(`Logout depuis ${location} termine la session et affiche la connexion`, async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('bank-converter-theme', 'light'));
    await routes(page);
    await page.goto('/dashboard');
    const toolbar = await openSessionMenu(page);
    await expect(toolbar.getByRole('menuitemradio')).toHaveCount(2);
    if (location === 'desktop') {
      await toolbar.getByRole('menuitemradio', { name: 'Sombre', exact: true }).click();
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
      expect(await page.evaluate(() => localStorage.getItem('bank-converter-theme'))).toBe('dark');
      await page.screenshot({
        path: '../output/screenshots/compact-toolbar-dark.png',
        animations: 'disabled',
      });
      await toolbar.getByRole('menuitemradio', { name: 'Clair', exact: true }).click();
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
      await page.screenshot({
        path: '../output/screenshots/compact-toolbar-light.png',
        animations: 'disabled',
      });
    }

    await expect(page.locator('.sidebar-workspace')).toHaveCount(0);
    await expect(
      page.locator('.desktop-sidebar').getByRole('button', { name: 'Se déconnecter' }),
    ).toHaveCount(0);
    await page.keyboard.press('Escape');
    if (location === 'compact') await page.getByRole('button', { name: 'Réduire le menu' }).click();
    if (location === 'mobile') await page.setViewportSize({ width: 390, height: 844 });
    const finished = page.waitForResponse((r) => r.url().endsWith('/auth/logout'));
    await (await openSessionMenu(page)).getByRole('menuitem', { name: 'Se déconnecter' }).click();
    const response = await finished;
    expect(response.request().method()).toBe('POST');
    expect(response.request().headers()['x-alias-request']).toBe('1');
    expect(response.status()).toBe(204);
    await expect(page.getByRole('heading', { name: 'Connectez-vous' })).toBeVisible();
    await expect(page.locator('.app-shell')).toHaveCount(0);
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Connectez-vous' })).toBeVisible();
  });
}

test('Logout affiche une erreur récupérable et empêche les requêtes en double', async ({
  page,
}) => {
  await routes(page);
  let release!: () => void;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  let calls = 0;
  await page.route('**/api/auth/logout', async (route) => {
    calls++;
    if (calls === 1) {
      await waiting;
      return route.fulfill({ status: 503, json: { title: 'Serveur indisponible. Réessayez.' } });
    }
    return route.fallback();
  });
  await page.goto('/dashboard');
  const sidebar = await openSessionMenu(page);
  await sidebar.getByRole('menuitem', { name: 'Se déconnecter' }).click();
  await expect(sidebar.getByRole('menuitem', { name: 'Déconnexion en cours' })).toBeDisabled();
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  expect(calls).toBe(1);
  release();
  await expect(sidebar.getByRole('alert')).toContainText('Serveur indisponible');
  await expect(page.locator('.app-shell')).toBeVisible();
  await sidebar.getByRole('menuitem', { name: 'Se déconnecter' }).click();
  await expect(page.getByRole('heading', { name: 'Connectez-vous' })).toBeVisible();
  expect(calls).toBe(2);
});

test('Logout accepte une session déjà expirée', async ({ page }) => {
  await routes(page);
  await page.route('**/api/auth/logout', (route) =>
    route.fulfill({ status: 401, json: { title: 'Session expirée' } }),
  );
  await page.goto('/dashboard');
  await (await openSessionMenu(page)).getByRole('menuitem', { name: 'Se déconnecter' }).click();
  await expect(page.getByRole('heading', { name: 'Connectez-vous' })).toBeVisible();
});

test('Logout protège les corrections non enregistrées', async ({ page }) => {
  await routes(page);
  let logoutCalls = 0;
  page.on('request', (request) => {
    if (request.url().endsWith('/auth/logout')) logoutCalls++;
  });
  await page.goto('/conversions?statementId=statement-1');
  await page.getByRole('button', { name: 'Corriger les informations', exact: true }).click();
  await page.getByLabel('Numéro du relevé', { exact: true }).fill('CORRECTION NON ENREGISTRÉE');
  await expect(page.getByText('Des corrections sont en attente')).toBeVisible();
  page.once('dialog', (dialog) => dialog.dismiss());
  const logout = (await openSessionMenu(page)).getByRole('menuitem', { name: 'Se déconnecter' });
  await logout.click();
  await expect(page.getByLabel('Numéro du relevé', { exact: true })).toHaveValue(
    'CORRECTION NON ENREGISTRÉE',
  );
  expect(logoutCalls).toBe(0);
  page.once('dialog', (dialog) => dialog.accept());
  await logout.click();
  await expect(page.getByRole('heading', { name: 'Connectez-vous' })).toBeVisible();
});

test('Premier administrateur puis création d’un collaborateur avec ses permissions', async ({
  page,
}) => {
  await routes(page, { setup: true });
  await page.goto('/users');
  await expect(page.getByRole('heading', { name: 'Configurez votre espace' })).toBeVisible();
  await page.getByLabel('Votre nom').fill('Amine Admin');
  await page.getByLabel('Adresse e-mail').fill('amine@example.test');
  await page.getByLabel('Mot de passe', { exact: true }).fill('Fictional-password-2026');
  await page.getByRole('button', { name: 'Créer mon espace' }).click();
  await expect(page.getByRole('heading', { name: 'Votre équipe, les bons accès.' })).toBeVisible();
  await page.getByRole('button', { name: 'Ajouter un utilisateur' }).click();
  const dialog = page.getByRole('dialog', { name: 'Ajouter un collaborateur' });
  await dialog.getByLabel('Nom', { exact: true }).fill('Sara Comptable');
  await dialog.getByLabel('E-mail', { exact: true }).fill('sara@example.test');
  await dialog.getByLabel('Mot de passe', { exact: true }).fill('Fictional-password-2026');
  await dialog.getByRole('button', { name: 'Traitement comptable' }).click();
  await expect(dialog.getByLabel('Télécharger les fichiers et exports')).toBeChecked();
  await dialog.getByRole('button', { name: 'Créer l’utilisateur' }).click();
  await expect(page.getByText('Sara Comptable', { exact: true })).toBeVisible();
  await mkdir('../output/screenshots', { recursive: true });
  await page.screenshot({
    path: '../output/screenshots/users-and-permissions.png',
    fullPage: true,
  });
});
test('Espace client : aperçu de l’archive et téléchargement du TXT exact', async ({ page }) => {
  await page.addInitScript(() =>
    Object.defineProperty(window, 'showSaveFilePicker', { value: undefined, configurable: true }),
  );
  await routes(page);
  await page.goto('/client-workspace?clientId=client-1');
  await expect(page.getByRole('heading', { name: 'Atlas Conseil', exact: true })).toBeVisible();
  await expect(page.getByText('22 mouvements', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Importer et convertir' }).click();
  const importer = page.locator('.conversion-import-page');
  await expect(importer.getByLabel('Client du relevé')).toHaveValue('client-1');
  await importer.getByLabel('Compte bancaire du relevé').selectOption('account-1');
  await expect(importer.getByLabel('Banque du relevé', { exact: true })).toHaveValue('bank-1');
  await expect(importer.getByLabel('Banque du relevé', { exact: true })).toBeDisabled();
  await expect(importer.getByLabel('Devise du relevé')).toHaveValue('MAD');
  await importer.getByRole('button', { name: 'Annuler', exact: true }).click();
  await page.goto('/client-workspace?clientId=client-1');
  await page.getByRole('button', { name: 'Exports & versions' }).click();
  const archive = page.getByRole('dialog', { name: 'Exports · releve_mars.pdf' });
  await archive.getByRole('button', { name: 'Voir le fichier' }).click();
  const preview = page.getByRole('dialog', { name: 'releve_sagex3.txt', exact: true });
  await expect(preview.locator('pre')).toContainText('RBCA032026;1;1');
  await preview.getByRole('button', { name: 'Fermer', exact: true }).click();
  page.once('dialog', (dialog) => dialog.accept('releve_sagex3.txt'));
  const downloading = page.waitForEvent('download');
  await archive.getByRole('button', { name: 'Télécharger .txt' }).click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe('releve_sagex3.txt');
  expect(await readFile((await download.path())!, 'utf8')).toContain(
    '20260301;20260301;;15245.75;-1;CA;MAD;RBCA032026;1;1',
  );
  await archive.getByRole('button', { name: 'Fermer', exact: true }).click();
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 0));
  await page.screenshot({ path: '../output/screenshots/client-exports.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.getByRole('button', { name: 'Exports & versions' }).click();
  await expect(page.getByRole('button', { name: 'Télécharger .txt' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: '../output/screenshots/client-exports-mobile.png',
    fullPage: true,
    animations: 'disabled',
  });
});
test('Consultation seule : actions de traitement et téléchargement masquées', async ({ page }) => {
  await routes(page, { reader: true });
  await page.goto('/client-workspace?clientId=client-1');
  await expect(page.getByText('22 mouvements', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Importer et convertir' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Original', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Exports & versions' }).click();
  await expect(page.getByRole('button', { name: 'Voir le fichier' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Télécharger .txt' })).toHaveCount(0);
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Ouvrir le relevé', exact: true }).click();
  const viewer = page.locator('main');
  await expect(page).toHaveURL(/conversions\?statementId=statement-1/);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(
    viewer.getByLabel('Tableau des mouvements').getByText('VIREMENT CLIENT', { exact: true }),
  ).toBeVisible();
  await expect(viewer.getByRole('button', { name: 'Ajouter une ligne', exact: true })).toHaveCount(
    0,
  );
  await expect(
    viewer.getByRole('button', { name: 'Corriger la transaction 1', exact: true }),
  ).toBeDisabled();
  await expect(viewer.getByRole('button', { name: 'Télécharger', exact: true })).toHaveCount(0);
  await page.goto('/users');
  await expect(page.getByRole('heading', { name: 'Accès limité' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Utilisateurs & permissions' })).toHaveCount(0);
});
test('Écrans de connexion et espace client utilisables sur mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await routes(page, { setup: true });
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Configurez votre espace' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: '../output/screenshots/first-admin-mobile.png', fullPage: true });
});

test('Toutes les pages et le sidebar restent lisibles en clair, sombre et sur mobile', async ({
  page,
}) => {
  await routes(page);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await mkdir('../output/screenshots/redesign', { recursive: true });
  const paths = [
    '/dashboard',
    '/clients',
    '/banks',
    '/bank-accounts',
    '/templates',
    '/client-workspace?clientId=client-1',
    '/conversions',
    '/users',
  ];
  for (const mode of ['light', 'dark']) {
    await page.addInitScript((theme) => localStorage.setItem('bank-converter-theme', theme), mode);
    await page.setViewportSize({ width: 1440, height: 1000 });
    for (const path of paths) {
      await page.goto(path);
      await expect(page.locator('html')).toHaveAttribute('data-theme', mode);
      await expect(page.locator('main h1')).toBeVisible();
      await expect(page.getByText('Chargement…', { exact: true })).toHaveCount(0);
      await expect(page.locator('main').getByRole('alert')).toHaveCount(0);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      const slug = path.split('?')[0].slice(1);
      await page.screenshot({
        path: `../output/screenshots/redesign/${slug}-${mode}.png`,
        animations: 'disabled',
      });
      await page.setViewportSize({ width: 390, height: 844 });
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      await page.screenshot({
        path: `../output/screenshots/redesign/${slug}-${mode}-mobile.png`,
        animations: 'disabled',
      });
      await page.setViewportSize({ width: 1440, height: 1000 });
    }
  }
  await page.goto('/dashboard');
  await page.getByRole('button', { name: 'Réduire le menu' }).click();
  await expect(page.locator('.app-shell')).toHaveClass(/sidebar-compact/);
  await expect(
    (await openSessionMenu(page)).getByRole('menuitem', { name: 'Se déconnecter' }),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Paramètres', exact: true })).toHaveCount(0);
  const removedPage = await page.goto('/settings');
  expect(removedPage?.status()).toBe(404);
  await page.goto('/dashboard');
  await page.locator('.desktop-sidebar').getByRole('link', { name: 'Conversion & export' }).click();
  await expect(page).toHaveURL(/\/conversions$/);
  await expect(page.locator('main h1')).toHaveText('Convertissez vos relevés en toute simplicité.');
  await page.screenshot({
    path: '../output/screenshots/redesign/sidebar-compact.png',
    animations: 'disabled',
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('button', { name: 'Ouvrir le menu' }).click();
  const menu = page.getByRole('dialog', { name: 'Navigation principale' });
  await expect(menu).toBeVisible();
  await page.screenshot({
    path: '../output/screenshots/redesign/sidebar-mobile.png',
    animations: 'disabled',
  });
  await menu.getByRole('link', { name: 'Utilisateurs & permissions' }).click();
  await expect(menu).not.toBeVisible();
  await expect(page.locator('main h1')).toHaveText('Votre équipe, les bons accès.');
  expect(errors).toEqual([]);
});

test('Les écrans étroits et tablettes gardent les actions visibles, y compris sans animations', async ({
  page,
}) => {
  await routes(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  for (const width of [320, 768, 1024]) {
    await page.setViewportSize({ width, height: 900 });
    for (const path of [
      '/dashboard',
      '/clients',
      '/banks',
      '/bank-accounts',
      '/templates',
      '/client-workspace?clientId=client-1',
      '/conversions',
      '/users',
    ]) {
      await page.goto(path);
      await expect(page.locator('main h1')).toBeVisible();
      await expect(page.getByText('Chargement…', { exact: true })).toHaveCount(0);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      const duration = await page
        .locator('main')
        .evaluate((el) => parseFloat(getComputedStyle(el).animationDuration));
      expect(duration).toBeLessThan(0.001);
    }
    await page.goto('/users');
    const manage = page.getByRole('button', { name: 'Gérer Amine Admin', exact: true });
    await manage.scrollIntoViewIfNeeded();
    const rect = await manage.boundingBox();
    expect(rect!.x).toBeGreaterThanOrEqual(0);
    expect(rect!.x + rect!.width).toBeLessThanOrEqual(width);
    await manage.click();
    const dialog = page.getByRole('dialog', { name: 'Gérer Amine Admin' });
    await expect(dialog).toBeVisible();
    const save = dialog.getByRole('button', { name: 'Enregistrer les accès', exact: true });
    await expect(save).toBeVisible();
    const footer = await save.boundingBox();
    expect(footer!.y + footer!.height).toBeLessThanOrEqual(900);
    expect(await dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1)).toBe(true);
    await expect(dialog.getByRole('button', { name: 'Fermer', exact: true })).toBeInViewport();
    await dialog.getByRole('button', { name: 'Annuler', exact: true }).click();
    await expect(dialog).not.toBeVisible();
  }
});

test('Historique : dates, auteurs et suppressions restent lisibles sur mobile', async ({
  page,
}) => {
  await routes(page);
  await page.route('**/api/client-workspace/history?**', (route) =>
    route.fulfill({
      json: pagination([
        {
          id: 'event-2',
          statementId: 'deleted-1',
          clientId: 'client-1',
          bankAccountId: 'account-1',
          clientName: 'Atlas Conseil',
          accountName: 'Compte principal',
          bankName: 'Crédit Agricole',
          fileName: 'ancien_releve.pdf',
          action: 'DELETED',
          actorName: 'Sara Comptable',
          version: 3,
          createdAt: '2026-10-10T12:00:00Z',
        },
        {
          id: 'event-1',
          statementId: 'deleted-1',
          clientId: 'client-1',
          bankAccountId: 'account-1',
          clientName: 'Atlas Conseil',
          accountName: 'Compte principal',
          bankName: 'Crédit Agricole',
          fileName: 'ancien_releve.pdf',
          action: 'IMPORTED',
          actorName: 'Amine Admin',
          version: 1,
          createdAt: '2026-09-01T09:00:00Z',
        },
      ]),
    }),
  );
  await page.goto('/history?statementId=deleted-1');
  await expect(page.getByText('Relevé supprimé définitivement', { exact: false })).toBeVisible();
  await expect(page.locator('main').getByText('Sara Comptable', { exact: true })).toBeVisible();
  await expect(page.locator('main time')).toHaveCount(2);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.screenshot({ path: '../output/screenshots/history-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: '../output/screenshots/history-mobile.png', fullPage: true });
  await page.getByRole('link', { name: 'Tout l’historique', exact: true }).click();
  await expect(page).toHaveURL(/\/history$/);
});

test('Import en page complète : classement visible sans fenêtres sur mobile', async ({ page }) => {
  await routes(page);
  for (const size of [
    { width: 1920, height: 1080 },
    { width: 1366, height: 768 },
    { width: 1280, height: 720 },
  ]) {
    await page.setViewportSize(size);
    await page.goto('/conversions?import=1&clientId=client-1');
    await page.getByLabel('Compte bancaire du relevé').selectOption('account-1');
    await expect(page.getByLabel('Client du relevé')).toBeInViewport({ ratio: 1 });
    await expect(page.getByLabel('Compte bancaire du relevé')).toBeInViewport({ ratio: 1 });
    await expect(
      page.getByRole('button', { name: 'Importer et continuer', exact: true }),
    ).toBeInViewport({ ratio: 1 });
    expect(
      await page.locator('.app-scroll').evaluate((el) => el.scrollHeight <= el.clientHeight + 1),
    ).toBe(true);
    await page.screenshot({
      path: `../output/screenshots/compact-import-${size.width}.png`,
      animations: 'disabled',
    });
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/conversions?import=1&clientId=client-1');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByLabel('Client du relevé')).toHaveValue('client-1');
  await page.getByLabel('Compte bancaire du relevé').selectOption('account-1');
  await expect(page.getByLabel('Banque du relevé', { exact: true })).toHaveValue('bank-1');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: '../output/screenshots/import-page-mobile.png', fullPage: true });
});

test('Historique vidé : la page et les accès restent disponibles', async ({ page }) => {
  await routes(page);
  await page.goto('/history');
  await expect(page.getByRole('heading', { name: 'Historique', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Aucune activité enregistrée' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Historique', exact: true }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Actualiser', exact: true }).click();
  await expect(page).toHaveURL(/\/history$/);
  await expect(page.getByRole('heading', { name: 'Aucune activité enregistrée' })).toBeVisible();
  await page.screenshot({
    path: '../output/screenshots/history-empty-preserved.png',
    animations: 'disabled',
  });
});

test('Configuration PaddleOCR : saisie masquée, conservation du jeton et accès administrateur', async ({
  page,
}) => {
  await routes(page);
  let settings = {
    provider: 'LOCAL',
    cloudModel: 'PP-OCRv6',
    baseUrl: 'http://127.0.0.1:8091',
    hasToken: false,
    apiUrl: 'https://paddleocr.aistudio-app.com/api/v2/ocr/jobs',
  };
  let saves = 0;
  await page.route('**/api/settings/ocr', (route) => {
    if (route.request().method() === 'PUT') {
      const input = route.request().postDataJSON();
      expect(input.cloudToken).toBe(saves === 0 ? 'fictional-browser-token' : null);
      settings = {
        ...settings,
        provider: input.provider,
        cloudModel: input.cloudModel,
        hasToken: true,
      };
      saves++;
    }
    return route.fulfill({ json: settings });
  });
  await page.goto('/settings');
  await expect(
    page.getByRole('heading', { name: 'Configuration PaddleOCR', exact: true }),
  ).toBeVisible();
  await page.getByLabel('Moteur de lecture', { exact: true }).selectOption('AI_STUDIO');
  const token = page.getByLabel('Jeton API PaddleOCR', { exact: true });
  await expect(token).toHaveAttribute('type', 'password');
  await token.fill('fictional-browser-token');
  await page.getByRole('button', { name: 'Enregistrer la configuration', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('sans redémarrage');
  await expect(token).toHaveValue('');
  await expect(
    page.getByRole('button', { name: 'Enregistrer la configuration', exact: true }),
  ).toBeDisabled();
  await page.getByLabel('Modèle PaddleOCR', { exact: true }).selectOption('PaddleOCR-VL-1.6');
  await page.getByRole('button', { name: 'Enregistrer la configuration', exact: true }).click();
  await expect.poll(() => saves).toBe(2);
  await expect(
    page.getByRole('button', { name: 'Enregistrer la configuration', exact: true }),
  ).toBeDisabled();
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(
    true,
  );
  await page.screenshot({
    path: '../output/screenshots/ocr-settings-mobile.png',
    animations: 'disabled',
  });
  await page.route('**/api/auth/session', (route) =>
    route.fulfill({
      json: {
        setupRequired: false,
        user: { ...admin, role: 'User', permissions: readPermissions },
      },
    }),
  );
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'Configuration PaddleOCR', exact: true }),
  ).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Configuration OCR', exact: true })).toHaveCount(0);
});

test('Dashboard : graphiques, périodes et liens utilisent les données reçues', async ({ page }) => {
  await routes(page);
  await page.setViewportSize({ width: 1600, height: 1100 });
  await page.addInitScript(() => localStorage.setItem('bank-converter-theme', 'light'));
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: 'Vue d’ensemble.' })).toBeVisible();
  const metrics = page.getByRole('region', { name: 'Indicateurs de votre activité' });
  await expect(metrics.getByRole('link', { name: /Relevés disponibles/ })).toContainText('42');
  const chart = page.getByRole('region', { name: 'Activité des conversions' });
  await expect(chart.getByRole('button', { name: '30 jours', exact: true })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await expect(chart.locator('.overview-chart-totals')).toContainText(
    String(dashboardExample.activity.slice(-30).reduce((sum, day) => sum + day.imports, 0)),
  );
  await chart.getByRole('button', { name: '7 jours', exact: true }).click();
  await expect(chart.locator('.overview-chart-hit')).toHaveCount(7);
  await expect(chart.locator('.overview-chart-totals')).toContainText(
    String(dashboardExample.activity.slice(-7).reduce((sum, day) => sum + day.exports, 0)),
  );
  await chart.locator('.overview-chart-hit').last().focus();
  await expect(chart.locator('.overview-chart-readout')).toContainText('12 oct.');
  await chart.getByRole('button', { name: '90 jours', exact: true }).click();
  await expect(chart.locator('.overview-chart-hit')).toHaveCount(90);
  await chart.getByRole('button', { name: '30 jours', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Statuts des relevés' })).toContainText('24');
  await expect(page.getByRole('region', { name: 'À suivre' })).toContainText(
    '8 relevé(s) à vérifier',
  );
  await expect(
    page
      .getByRole('region', { name: 'Relevés récents' })
      .getByRole('link', { name: /releve_septembre.pdf/ }),
  ).toHaveAttribute('href', '/conversions?statementId=statement-1');
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 0));
  await page.screenshot({
    path: '../output/screenshots/dashboard-redesign-light.png',
    fullPage: true,
    animations: 'disabled',
  });
  const requests = page.waitForResponse((response) => response.url().endsWith('/api/dashboard'));
  await page.getByRole('button', { name: 'Actualiser le dashboard' }).click();
  await requests;
  await expect(chart.locator('.overview-chart-hit')).toHaveCount(30);
});

test('Dashboard : rendu sombre et mobile sans débordement', async ({ page }) => {
  await routes(page);
  await page.addInitScript(() => localStorage.setItem('bank-converter-theme', 'dark'));
  await page.setViewportSize({ width: 1600, height: 1100 });
  await page.goto('/dashboard');
  await expect(page.getByRole('region', { name: 'Statuts des relevés' })).toContainText('24');
  await page.screenshot({
    path: '../output/screenshots/dashboard-redesign-dark.png',
    fullPage: true,
    animations: 'disabled',
  });
  for (const width of [768, 390]) {
    await page.setViewportSize({ width, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(
      true,
    );
    await expect(page.getByRole('button', { name: '90 jours', exact: true })).toBeVisible();
  }
  await page.screenshot({
    path: '../output/screenshots/dashboard-redesign-mobile.png',
    fullPage: true,
    animations: 'disabled',
  });
});

test('Dashboard : absence de données et erreur récupérable sans chiffres inventés', async ({
  page,
}) => {
  await routes(page);
  let failed = true;
  await page.route('**/api/dashboard', (route) =>
    failed
      ? route.fulfill({ status: 503, json: { title: 'Statistiques indisponibles' } })
      : route.fulfill({
          json: {
            ...dashboardExample,
            clients: 0,
            banks: 0,
            accounts: 0,
            statements: 0,
            exports: 0,
            pendingReview: 0,
            unassigned: 0,
            recent: [],
            statuses: [],
            activity: dashboardExample.activity.map((day) => ({ ...day, imports: 0, exports: 0 })),
          },
        }),
  );
  await page.goto('/dashboard');
  await expect(page.locator('.overview-page').getByRole('alert')).toContainText(
    'Statistiques indisponibles',
  );
  await expect(page.locator('.overview-metric-value').first()).toHaveText('—');
  failed = false;
  await page.getByRole('button', { name: 'Réessayer', exact: true }).click();
  await expect(page.getByText('Aucune activité sur cette période', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'Votre premier relevé vous attend.' }),
  ).toBeVisible();
  await expect(page.locator('.overview-metric-value').first()).toHaveText('0');
});

test('Dashboard : les liens et actions respectent les droits de consultation', async ({ page }) => {
  await routes(page, { reader: true });
  await page.goto('/dashboard');
  await expect(page.getByRole('link', { name: 'Importer et convertir', exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByRole('region', { name: 'Activité des conversions' })).toBeVisible();
  await page.route('**/api/auth/session', (route) =>
    route.fulfill({
      json: {
        setupRequired: false,
        user: { ...admin, role: 'User', permissions: ['dashboard.read'] },
      },
    }),
  );
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Vue d’ensemble.' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Activité des conversions' })).toHaveCount(0);
  await expect(
    page.getByRole('region', { name: 'Indicateurs de votre activité' }).getByRole('link'),
  ).toHaveCount(0);
});

test('Espace clients : seuls les relevés et exports sont affichés, sans dépôt PDF séparé', async ({
  page,
}) => {
  await routes(page);
  const documentRequests: string[] = [];
  page.on('request', (request) => {
    if (/\/api\/documents(?:\?|$)/.test(request.url())) documentRequests.push(request.url());
  });
  await page.goto('/client-workspace');
  await expect(page.getByText('PDF déposés', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('tablist', { name: 'Archives du client' })).toHaveCount(0);
  await expect(page.getByText('releve_mars.pdf', { exact: true })).toBeVisible();
  await page.getByLabel('Client', { exact: true }).selectOption('client-1');
  await expect(page).toHaveURL(/clientId=client-1/);
  await expect(
    page.getByRole('heading', { name: 'Atlas Conseil', exact: true }).first(),
  ).toBeVisible();
  expect(documentRequests).toHaveLength(0);
});

test('client workspace deletes only selected statements after confirmation', async ({ page }) => {
  await routes(page);
  const deleted: unknown[] = [];
  await page.route('**/api/statement-imports/delete-selection', async (route) => {
    deleted.push(route.request().postDataJSON());
    await route.fulfill({ status: 204 });
  });
  await page.goto('/client-workspace');
  await page.getByRole('checkbox', { name: 'Sélectionner releve_mars.pdf', exact: true }).check();
  await page
    .getByRole('checkbox', { name: 'Sélectionner releve_a_classer_1.pdf', exact: true })
    .check();
  await page.getByRole('button', { name: 'Supprimer la sélection', exact: true }).click();
  await page.getByRole('button', { name: 'Annuler', exact: true }).click();
  expect(deleted).toHaveLength(0);
  await page.getByRole('button', { name: 'Supprimer la sélection', exact: true }).click();
  await page.getByRole('button', { name: 'Supprimer définitivement', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Supprimer la sélection', exact: true }),
  ).toBeDisabled();
  expect(deleted).toEqual([
    [
      { id: 'statement-1', version: 2 },
      { id: 'unassigned-1', version: 2 },
    ],
  ]);
});

test('client workspace selects and clears every displayed statement', async ({ page }) => {
  await routes(page);
  await page.goto('/client-workspace');
  const all = page.getByRole('checkbox', { name: 'Tout sélectionner sur cette page' });
  await all.check();
  await expect(page.getByRole('checkbox', { checked: true })).toHaveCount(21);
  await all.uncheck();
  await expect(page.getByRole('checkbox', { checked: true })).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: 'Supprimer la sélection', exact: true }),
  ).toBeDisabled();
});

test('Exported statements become validated only after explicit confirmation', async ({ page }) => {
  await routes(page);
  let verified = false;
  await page.route('**/api/client-workspace/client-1/statements?**', (route) =>
    route.fulfill({
      json: pagination([
        {
          id: 'statement-1',
          clientId: 'client-1',
          clientName: client.name,
          bankAccountId: 'account-1',
          bankName: 'BMCE',
          sourceFileName: 'exporte.pdf',
          accountName: 'Compte principal',
          accountNumber: 'DEMO1234',
          status: verified ? 'VALIDATED' : 'EXPORTED',
          version: verified ? 3 : 2,
          transactionCount: 1,
          exportCount: 1,
          createdAt: '2026-10-11T12:00:00Z',
          updatedAt: '2026-10-11T12:00:00Z',
          modifiedByName: 'Admin',
          importedByName: 'Admin',
        },
      ]),
    }),
  );
  await page.route('**/api/statement-imports/statement-1/validate', (route) => {
    expect(route.request().postDataJSON()).toEqual({ version: 2 });
    verified = true;
    return route.fulfill({ json: { status: 'VALIDATED', version: 3 } });
  });
  await page.goto('/client-workspace?clientId=client-1');
  await expect(page.getByText('Exporté', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Confirmer la vérification', exact: true }).click();
  await page.getByRole('button', { name: 'Annuler', exact: true }).click();
  expect(verified).toBe(false);
  await page.getByRole('button', { name: 'Confirmer la vérification', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmer et valider', exact: true }).click();
  await expect(page.getByText('Validé', { exact: true })).toBeVisible();
});
