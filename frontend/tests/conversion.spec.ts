import { test, expect, Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import statementExample from '../../data/examples/normalized-statement.json';
import type { BankStatement, StatementTransaction } from '../src/services/statement-exports';

const csv = {
  id: 'csv-1',
  name: 'CSV personnalisé',
  type: 'CUSTOMCSV',
  description: '',
  fileExtension: 'csv',
  delimiter: ';',
  encoding: 'utf-8',
  numberCulture: 'fr-FR',
  includeHeader: true,
  isActive: true,
  fields: [
    { fieldName: 'Date', sourceField: 'Date', position: 0, format: 'dd/MM/yyyy', required: true },
    { fieldName: 'Libellé', sourceField: 'Description', position: 4, format: null, required: true },
    { fieldName: 'Montant', sourceField: 'Amount', position: 8, format: '0.00', required: true },
  ],
};
const ascii = {
  ...csv,
  id: 'ascii-1',
  name: 'Sage X3 ASCII',
  type: 'SAGEX3',
  fileExtension: 'txt',
  encoding: 'ascii',
  numberCulture: 'en-US',
  includeHeader: false,
  fields: [
    {
      fieldName: 'Date opération',
      sourceField: 'Date',
      position: 0,
      format: 'yyyyMMdd',
      required: true,
    },
    {
      fieldName: 'Date valeur',
      sourceField: 'ValueDate',
      position: 1,
      format: 'yyyyMMdd',
      required: true,
    },
    {
      fieldName: 'Libellé',
      sourceField: 'Description',
      position: 2,
      format: null,
      required: false,
    },
    {
      fieldName: 'Montant en devise',
      sourceField: 'Amount',
      position: 3,
      format: '0.00',
      required: true,
    },
    { fieldName: 'Sens', sourceField: 'Direction', position: 4, format: null, required: true },
    { fieldName: 'Code', sourceField: 'BankCode', position: 5, format: null, required: true },
    { fieldName: 'Devise', sourceField: 'Currency', position: 6, format: null, required: true },
    {
      fieldName: 'Numéro relevé',
      sourceField: 'StatementNumber',
      position: 7,
      format: null,
      required: true,
    },
    { fieldName: 'Numéro', sourceField: 'RecordType', position: 8, format: null, required: true },
    {
      fieldName: 'Numéro interne',
      sourceField: 'Sequence',
      position: 9,
      format: null,
      required: true,
    },
  ],
};
const x3 = { ...ascii, id: 'x3-1', name: 'Sage X3 UTF-8', encoding: 'utf-8' };
const x3Rows = [
  ['20260901', '20260901', '1000.00', '-1', 'DEMO', 'MAD', 'RBDEMO092026', '1', '1'],
  [
    '20260901',
    '20260901',
    'VIREMENT CLIENT',
    '12500.00',
    '1',
    'DEMO',
    'MAD',
    'RBDEMO092026',
    '2',
    '2',
  ],
  [
    '20260902',
    '20260902',
    'FRAIS BANCAIRES',
    '125.50',
    '-1',
    'DEMO',
    'MAD',
    'RBDEMO092026',
    '2',
    '3',
  ],
  ['20260930', '20260930', '13374.50', '-1', 'DEMO', 'MAD', 'RBDEMO092026', '3', '4'],
];
const x3Content = x3Rows.map((row) => row.join(';')).join('\r\n') + '\r\n';
const content =
  'Date;Libellé;Montant\r\n01/09/2026;VIREMENT CLIENT;12500,00\r\n02/09/2026;FRAIS BANCAIRES;125,50\r\n';
async function mockApi(page: Page) {
  let imported = false,
    version = 1,
    status = 'NEEDS_REVIEW',
    exports = 0;
  const models = [csv, ascii, x3];
  let lastExportStatus = 'SUCCEEDED';
  let statement = {
    ...statementExample,
    id: 'statement-1',
    transactions: statementExample.transactions.map((t, i) => ({
      ...t,
      id: `transaction-${i + 1}`,
      currency: null,
      reference: null,
    })),
  };
  await page.route('**/api/**', async (route) => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    const method = route.request().method();
    const detail = () => ({
      id: statement.id,
      status,
      version,
      validatedAt: status === 'VALIDATED' ? '2026-10-07T10:00:00Z' : null,
      statement,
      warnings: [],
      errors: [],
      sourceFileName: 'releve.pdf',
    });
    const respond = (body: unknown) => route.fulfill({ json: body });
    if (path === '/auth/session')
      return respond({
        setupRequired: false,
        user: {
          id: 'admin-test',
          name: 'Administrateur test',
          email: 'admin@example.test',
          role: 'Admin',
          isActive: true,
          permissions: [],
          version: 1,
        },
      });
    if (path === '/clients' || path === '/bank-accounts')
      return respond({ items: [], total: 0, page: 1, pageSize: 100 });
    const pagination = (items: unknown[]) => ({
      items,
      total: items.length,
      page: 1,
      pageSize: 20,
    });
    if (path === '/export-templates' && method === 'POST') {
      const created = { ...route.request().postDataJSON(), id: 'custom-new' };
      models.push(created);
      return respond(created);
    }
    if (path.startsWith('/export-templates/') && method === 'PUT') {
      const id = path.split('/').at(-1)!;
      const edited = { ...route.request().postDataJSON(), id };
      const index = models.findIndex((m) => m.id === id);
      models[index] = edited;
      return respond(edited);
    }
    if (path === '/export-templates') return respond(pagination(models));
    if (path === '/banks')
      return respond(pagination([{ id: 'bank-1', name: 'BANQUE DE DÉMONSTRATION', code: 'DEMO' }]));
    if (path === '/statement-imports/pdf' && method === 'POST') {
      expect(route.request().headers()['content-type']).toContain('multipart/form-data');
      expect(route.request().postData()).toContain('releve.pdf');
      imported = true;
      return respond(detail());
    }
    if (path === '/statement-imports')
      return respond(
        pagination(
          imported
            ? [
                {
                  id: statement.id,
                  bankName: statement.bankName,
                  statementNumber: statement.statementNumber,
                  currency: 'MAD',
                  periodStart: statement.periodStart,
                  periodEnd: statement.periodEnd,
                  status,
                  version,
                  transactionCount: 2,
                  createdAt: '2026-10-07T10:00:00Z',
                },
              ]
            : [],
        ),
      );
    if (path === '/statement-imports/statement-1' && method === 'PUT') {
      statement = route.request().postDataJSON();
      version++;
      status = 'NEEDS_REVIEW';
      return respond(detail());
    }
    if (path === '/statement-imports/statement-1') return respond(detail());
    if (path.endsWith('/validate')) {
      expect(route.request().postDataJSON().version).toBe(version);
      status = 'VALIDATED';
      version++;
      return respond(detail());
    }
    if (path.endsWith('/preview') || path.endsWith('/prepare')) {
      expect(route.request().postDataJSON().statementVersion).toBe(version);
      const prepare = path.endsWith('/prepare');
      const modelId = prepare ? route.request().postDataJSON().templateId : path.split('/').at(-2);
      const model = models.find((item) => item.id === modelId) ?? csv;
      const errors =
        model === ascii
          ? [
              {
                code: 'ENCODING_ERROR',
                message: 'Caractère U+00C8 incompatible avec ascii.',
                transactionSequence: 1,
                fieldName: 'Libellé',
                sourceField: 'Description',
              },
            ]
          : [];
      const preview = {
        template: { id: model.id, name: model.name },
        text: model.type === 'SAGEX3' ? x3Content : content,
        textTruncated: false,
        columns: model.fields.map((f) => f.fieldName),
        rows:
          model.type === 'SAGEX3'
            ? x3Rows
            : [
                ['01/09/2026', 'VIREMENT CLIENT', '12500,00'],
                ['02/09/2026', 'FRAIS BANCAIRES', '125,50'],
              ],
        rowColumnIndices:
          model.type === 'SAGEX3'
            ? [
                [0, 1, 3, 4, 5, 6, 7, 8, 9],
                [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
                [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
                [0, 1, 3, 4, 5, 6, 7, 8, 9],
              ]
            : undefined,
        rowSequences: model.type === 'SAGEX3' ? [null, 1, 2, null] : [1, 2],
        rowKinds:
          model.type === 'SAGEX3'
            ? ['OPENING_BALANCE', 'TRANSACTION', 'TRANSACTION', 'CLOSING_BALANCE']
            : ['TRANSACTION', 'TRANSACTION'],
        totalRows: model.type === 'SAGEX3' ? 4 : 2,
        isValidated: status === 'VALIDATED',
        openingBalance: statement.openingBalance,
        closingBalance: statement.closingBalance,
        errors,
        warnings:
          status === 'VALIDATED'
            ? []
            : [
                {
                  code: 'STATEMENT_NOT_VALIDATED',
                  message: 'Validez le relevé avant de télécharger.',
                  transactionSequence: null,
                  fieldName: null,
                  sourceField: null,
                },
              ],
        statementVersion: version,
        templateFingerprint: 'snapshot-1',
      };
      return respond(prepare ? { detail: detail(), preview } : preview);
    }
    if (path === '/statement-imports/statement-1/exports')
      return respond(
        pagination(
          exports
            ? [
                {
                  id: 'job-1',
                  templateId: csv.id,
                  templateName: csv.name,
                  statementVersion: version,
                  fileName: 'DEMO_1234_2026-09_CUSTOMCSV.csv',
                  rowCount: 2,
                  extension: 'csv',
                  encoding: 'utf-8',
                  createdAt: '2026-10-07T10:00:00Z',
                  status: lastExportStatus,
                  contentSha256: 'ABC',
                },
              ]
            : [],
        ),
      );
    if (path === '/statement-imports/statement-1/exports/csv-1') {
      expect(status).toBe('VALIDATED');
      expect(route.request().postDataJSON()).toEqual({
        statementVersion: version,
        templateFingerprint: 'snapshot-1',
      });
      exports++;
      return route.fulfill({
        body: content,
        contentType: 'text/csv; charset=utf-8',
        headers: {
          'Content-Disposition': 'attachment; filename="DEMO_1234_2026-09_CUSTOMCSV.csv"',
        },
      });
    }
    if (
      path === '/statement-imports/statement-1/exports/x3-1' ||
      path === '/statement-imports/statement-1/exports/ascii-1'
    ) {
      const input = route.request().postDataJSON();
      if (!input.allowErrors) expect(status).toBe('VALIDATED');
      if (path.includes('ascii-1')) expect(input.allowErrors).toBe(true);
      expect(input.templateFingerprint).toBe('snapshot-1');
      exports++;
      lastExportStatus = input.allowErrors ? 'FORCED' : input.editedText ? 'EDITED' : 'SUCCEEDED';
      const fileName = `DEMO_1234_2026-09_SAGEX3${input.allowErrors ? '_a_verifier' : input.editedText ? '_modifie' : ''}.txt`;
      return route.fulfill({
        body: input.editedText ?? x3Content,
        contentType: 'text/plain; charset=utf-8',
        headers: { 'Content-Disposition': `attachment; filename="${fileName}"` },
      });
    }
    return route.fulfill({ status: 404, json: { title: 'Introuvable' } });
  });
}
async function importToModel(page: Page) {
  await page.goto('/conversions');
  await page.getByRole('button', { name: 'Importer un relevé', exact: true }).click();
  const dialog = page.locator('.conversion-import-page');
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'releve.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.7 synthetic mock'),
  });
  await dialog.getByLabel('Banque du relevé', { exact: true }).selectOption('bank-1');
  await dialog.getByRole('button', { name: 'Importer et continuer' }).click();
  await expect(page).toHaveURL(/step=model/);
}
async function importExample(page: Page) {
  await importToModel(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByRole('button', { name: 'Afficher le résultat', exact: true }).click();
  await expect(page.getByRole('tab', { name: 'Aperçu du fichier' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await page.goto('/conversions?statementId=statement-1');
  await expect(page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Voir le PDF original' })).toHaveAttribute(
    'href',
    '/api/statement-imports/statement-1/original',
  );
}

async function editCell(page: Page, label: string, value: string) {
  await page.getByRole('button', { name: `Modifier ${label}`, exact: true }).click();
  const input = page.getByLabel(label, { exact: true });
  await input.fill(value);
  await input.press('Enter');
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'showSaveFilePicker', { value: undefined, configurable: true });
  });
  await mockApi(page);
});

test('Le classement facultatif reste accessible au clavier et conserve le compte choisi', async ({
  page,
}) => {
  await page.route('**/api/clients?**', (route) =>
    route.fulfill({
      json: {
        items: [{ id: 'client-1', name: 'Client exemple' }],
        total: 1,
        page: 1,
        pageSize: 100,
      },
    }),
  );
  await page.route('**/api/bank-accounts?**', (route) =>
    route.fulfill({
      json: {
        items: [
          {
            id: 'account-1',
            clientId: 'client-1',
            bankId: 'bank-1',
            bank: 'BANQUE DE DÉMONSTRATION',
            accountName: 'Compte principal',
            accountNumber: '123456',
            currency: 'MAD',
          },
        ],
        total: 1,
        page: 1,
        pageSize: 100,
      },
    }),
  );
  await page.goto('/conversions?import=1');
  const dialog = page.locator('.conversion-import-page');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(dialog.getByLabel('Client du relevé')).toBeVisible();
  await expect(page.getByText('Mes relevés', { exact: true })).toHaveCount(0);
  await dialog.getByLabel('Client du relevé').selectOption('client-1');
  await expect(dialog.getByRole('button', { name: 'Importer et continuer' })).toBeDisabled();
  await dialog.getByLabel('Compte bancaire du relevé').selectOption('account-1');
  await expect(dialog.getByLabel('Banque du relevé', { exact: true })).toHaveValue('bank-1');
  await expect(dialog.getByLabel('Banque du relevé', { exact: true })).toBeDisabled();
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'releve.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.7 test'),
  });
  await expect(dialog.getByRole('button', { name: 'Importer et continuer' })).toBeEnabled();
  await page.screenshot({ path: '../output/conversion-import-simple.png', animations: 'disabled' });
  const upload = page.waitForRequest(
    (r) => r.url().endsWith('/statement-imports/pdf') && r.method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Importer et continuer' }).click();
  const body = (await upload).postData();
  expect(body).toContain('client-1');
  expect(body).toContain('account-1');
  await expect(dialog).not.toBeVisible();
});

function selectablePdf() {
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 7 0 R >> >> /Contents 4 0 R >>',
    '',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R >>',
    '',
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ];
  const streams = [
    'BT /F1 14 Tf 20 350 Td (35 023,45) Tj 0 -30 Td (31/03/2025) Tj 0 -30 Td (VIREMENT CLIENT) Tj ET',
    'BT /F1 14 Tf 20 350 Td (9 876,50) Tj ET',
  ];
  objects[3] = `<< /Length ${streams[0].length} >>\nstream\n${streams[0]}\nendstream`;
  objects[5] = `<< /Length ${streams[1].length} >>\nstream\n${streams[1]}\nendstream`;
  let pdf = '%PDF-1.4\n';
  const offsets = [0];
  objects.forEach((object, index) => {
    offsets.push(Buffer.byteLength(pdf));
    pdf += `${index + 1} 0 obj\n${object}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf);
  pdf += `xref\n0 ${offsets.length}\n0000000000 65535 f \n${offsets
    .slice(1)
    .map((offset) => `${String(offset).padStart(10, '0')} 00000 n \n`)
    .join('')}trailer\n<< /Size ${offsets.length} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`;
  return Buffer.from(pdf);
}

test('Le PDF à droite permet de sélectionner, zoomer et corriger les montants et les dates', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1800, height: 1100 });
  await page.route('**/api/statement-imports/statement-1/original', (route) =>
    route.fulfill({ body: selectablePdf(), contentType: 'application/pdf' }),
  );
  await page.goto('/conversions?statementId=statement-1');
  const panel = page.getByRole('complementary', { name: 'Consultation et sélection du PDF' });
  const amount = panel.getByRole('button', { name: 'Sélectionner 35 023,45', exact: true });
  await expect(amount).toBeVisible();
  const bounds = await panel.boundingBox();
  const dataBounds = await page.locator('.conversion-main').boundingBox();
  expect(bounds!.x).toBeGreaterThan(dataBounds!.x);
  await amount.click();
  await expect(panel.getByLabel('Valeur sélectionnée dans le PDF')).toHaveValue('35 023,45');
  await panel.getByRole('button', { name: 'Appliquer la valeur au champ' }).click();
  await expect(page.getByText('Des corrections sont en attente')).toBeVisible();
  await expect(page.locator('.conversion-balances')).toContainText('35\u202f023,45');
  await panel.getByLabel('Champ à corriger depuis le PDF').selectOption('transaction.credit');
  await panel.getByLabel('Mouvement à corriger depuis le PDF').selectOption('1');
  await panel.getByRole('button', { name: 'Appliquer la valeur au champ' }).click();
  const row = page
    .getByRole('row')
    .filter({ has: page.getByRole('button', { name: 'Modifier Libellé · ligne 1', exact: true }) });
  await expect(row).toContainText('35\u202f023,45');
  await panel.getByRole('button', { name: 'Effacer la sélection' }).click();
  await panel.getByRole('button', { name: 'Sélectionner 31/03/2025', exact: true }).click();
  await panel.getByLabel('Champ à corriger depuis le PDF').selectOption('transaction.date');
  await panel.getByRole('button', { name: 'Appliquer la valeur au champ' }).click();
  await expect(row).toContainText('31/03/2025');
  const saved = page.waitForRequest(
    (request) =>
      request.url().includes('/statement-imports/statement-1?version=') &&
      request.method() === 'PUT',
  );
  await page.getByRole('button', { name: 'Enregistrer les corrections', exact: true }).click();
  const stored = (await saved).postDataJSON();
  expect(stored.openingBalance).toBe('35023.45');
  expect(stored.transactions[0]).toMatchObject({
    credit: '35023.45',
    amount: '35023.45',
    direction: 'CREDIT',
    debit: null,
    date: '2025-03-31',
  });
  await expect(page.getByText('Des corrections sont en attente')).toHaveCount(0);
  await expect(
    panel.getByRole('button', { name: 'Sélectionner 35 023,45', exact: true }),
  ).toBeVisible();
  await panel.getByRole('button', { name: 'Agrandir le PDF', exact: true }).click();
  await expect(panel.getByText('125 %', { exact: true })).toBeVisible();
  await expect(amount).toBeVisible();
  const pdfToggle = panel.getByRole('button', { name: 'Affichage du PDF original', exact: true });
  await pdfToggle.click();
  await expect(amount).toBeHidden();
  await expect(pdfToggle).toHaveAttribute('aria-expanded', 'false');
  await pdfToggle.click();
  await expect(amount).toBeVisible();
  await expect(panel.getByText('125 %', { exact: true })).toBeVisible();
  await page.screenshot({ path: '../output/conversion-pdf-selection.png', animations: 'disabled' });
  await panel.getByRole('button', { name: 'Page suivante du PDF' }).click();
  await expect(
    panel.getByRole('button', { name: 'Sélectionner 9 876,50', exact: true }),
  ).toBeVisible();
  await expect(panel.getByLabel('Valeur sélectionnée dans le PDF')).toHaveValue('');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('Les zones OCR du PDF peuvent servir de repère de colonne sans modifier le relevé', async ({
  page,
}) => {
  await page.route('**/api/statement-imports/statement-1/original', (route) =>
    route.fulfill({ body: selectablePdf(), contentType: 'application/pdf' }),
  );
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version: 1,
        statement: { ...statementExample, id: 'statement-1' },
        warnings: [],
        errors: [],
        sourceFileName: 'scan.pdf',
        extraction: {
          method: 'PADDLE_OCR_V6',
          pageCount: 2,
          lowConfidenceCount: 0,
          needsMapping: false,
        },
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
    route.fulfill({
      json: {
        method: 'PADDLE_OCR_V6',
        pages: [
          {
            number: 1,
            width: 300,
            height: 400,
            boxes: [
              {
                text: 'Libellé OCR',
                left: 90,
                right: 180,
                top: 120,
                bottom: 134,
                confidence: 0.99,
              },
            ],
          },
        ],
      },
    }),
  );
  await page.goto('/conversions?statementId=statement-1');
  await page.getByRole('button', { name: 'Sélectionner Libellé OCR', exact: true }).click();
  await page.getByRole('tab', { name: 'Texte lu & adaptation' }).click();
  await page.getByRole('button', { name: 'Régler les colonnes', exact: true }).click();
  const field = page.getByLabel('Repère pour Libellé', { exact: true }).locator('..');
  await field.getByRole('button', { name: 'Utiliser la sélection du PDF' }).click();
  await expect(field).toContainText('Position retenue : 30 %');
  await expect(page.getByText('Des corrections sont en attente')).toHaveCount(0);
});

test('Le téléchargement conserve le nom choisi et ajoute uniquement son extension', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page
    .getByText('J’ai vérifié les données du relevé et les avertissements.', { exact: true })
    .click();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  await page.getByLabel('Nom du fichier', { exact: true }).fill('Relevé septembre.csv');
  const waiting = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Télécharger', exact: true }).click();
  const file = await waiting;
  expect(file.suggestedFilename()).toBe('Relevé septembre.csv');
  expect(await readFile((await file.path())!, 'utf8')).toBe(content);
});

test('Enregistrer sous est ouvert avant la requête et conserve les octets du fichier', async ({
  page,
}) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'showSaveFilePicker', {
      configurable: true,
      value: async (options: { suggestedName: string }) => {
        (window as unknown as Record<string, unknown>).exportSave = {
          suggestedName: options.suggestedName,
        };
        return {
          name: 'Relevé choisi.csv',
          createWritable: async () => ({
            write: async (blob: Blob) => {
              (window as unknown as Record<string, unknown>).exportSavedBytes = Array.from(
                new Uint8Array(await blob.arrayBuffer()),
              );
            },
            close: async () => {
              (window as unknown as Record<string, unknown>).exportSaveClosed = true;
            },
            abort: async () => {},
          }),
        };
      },
    });
  });
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page
    .getByText('J’ai vérifié les données du relevé et les avertissements.', { exact: true })
    .click();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  await page.getByLabel('Nom du fichier', { exact: true }).fill('Mon relevé');
  await expect(page.getByLabel('Choisir l’emplacement avec « Enregistrer sous »')).toBeChecked();
  await page.route('**/exports/csv-1/preview', async (route) => {
    expect(
      await page.evaluate(() => (window as unknown as Record<string, unknown>).exportSave),
    ).toEqual({ suggestedName: 'Mon relevé.csv' });
    await route.fallback();
  });
  await page.getByRole('button', { name: 'Télécharger', exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as Record<string, unknown>).exportSaveClosed),
    )
    .toBe(true);
  expect(
    await page.evaluate(() => (window as unknown as Record<string, unknown>).exportSavedBytes),
  ).toEqual(Array.from(Buffer.from(content)));
});

test('Annuler Enregistrer sous ne crée aucun export', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'showSaveFilePicker', {
      configurable: true,
      value: async () => {
        throw new DOMException('Annulé', 'AbortError');
      },
    });
  });
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page
    .getByText('J’ai vérifié les données du relevé et les avertissements.', { exact: true })
    .click();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  const exports: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && /\/exports\//.test(request.url()))
      exports.push(request.url());
  });
  await page.getByRole('button', { name: 'Télécharger', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeEnabled();
  await expect(page.locator('.conversion-alert.error')).toHaveCount(0);
  expect(exports).toEqual([]);
});

test('Le texte du fichier Sage peut être modifié et le téléchargement reprend les changements exacts', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(x3.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await page.getByRole('button', { name: 'Texte du fichier', exact: true }).click();
  const text = page.getByLabel('Contenu du fichier à télécharger', { exact: true });
  await expect(text).toHaveValue(x3Content.replace(/\r\n/g, '\n'));
  const edited = x3Content
    .replace('VIREMENT CLIENT', 'LIBELLE CORRIGE')
    .replace('12500.00', '12345678901234567890.12345678');
  await text.fill(edited);
  await page.locator('.export-text-editor').screenshot({
    path: '../output/screenshots/redesign/export-text-desktop.png',
    animations: 'disabled',
  });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.locator('.export-text-editor').screenshot({
    path: '../output/screenshots/redesign/export-text-mobile.png',
    animations: 'disabled',
  });
  const request = page.waitForRequest(
    (req) => req.url().endsWith('/exports/x3-1') && req.method() === 'POST',
  );
  const downloading = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Télécharger', exact: true }).click();
  expect((await request).postDataJSON()).toMatchObject({
    statementVersion: 1,
    templateFingerprint: 'snapshot-1',
    editedText: edited,
  });
  const file = await downloading;
  expect(file.suggestedFilename()).toContain('_a_verifier.txt');
  expect(await readFile((await file.path())!, 'utf8')).toBe(edited);
  await expect(text).toHaveValue(edited.replace(/\r\n/g, '\n'));
  await page.getByRole('tab', { name: /Historique/ }).click();
  await expect(
    page.getByText('Téléchargé avec erreurs · à vérifier', { exact: true }),
  ).toBeVisible();
});

test('Le téléchargement avec erreurs reste explicite et fonctionne avant validation', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(ascii.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Texte du fichier', exact: true }).click();
  const edited = x3Content.replace('VIREMENT CLIENT', 'عملية تجريبية');
  await page.getByLabel('Contenu du fichier à télécharger', { exact: true }).fill(edited);
  await page.getByLabel('Autoriser le téléchargement malgré les erreurs', { exact: true }).check();
  await expect(page.getByText('Export à vérifier', { exact: true })).toBeVisible();
  const request = page.waitForRequest(
    (req) => req.url().endsWith('/exports/ascii-1') && req.method() === 'POST',
  );
  const downloading = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Télécharger avec erreurs', exact: true }).click();
  expect((await request).postDataJSON()).toMatchObject({
    statementVersion: 1,
    allowErrors: true,
    editedText: edited,
  });
  const file = await downloading;
  expect(file.suggestedFilename()).toContain('_a_verifier.txt');
  expect(await readFile((await file.path())!, 'utf8')).toBe(edited);
  await expect(page.getByRole('button', { name: 'Valider le relevé', exact: true })).toBeVisible();
  await page.getByRole('tab', { name: /Historique/ }).click();
  await expect(
    page.getByText('Téléchargé avec erreurs · à vérifier', { exact: true }),
  ).toBeVisible();
});

for (const view of ['Mouvements', 'Aperçu du fichier'] as const) {
  test(`Insertion et glisser-déposer avec renumérotation dans ${view}`, async ({ page }) => {
    await importExample(page);
    let rows: StatementTransaction[] = Array.from({ length: 60 }, (_, index) => ({
      ...statementExample.transactions[0],
      id: `ordered-${index + 1}`,
      sequence: index + 1,
      description: `ORIGINAL ${index + 1}`,
      reference: null,
      currency: null,
    }));
    let version = 1;
    const result = () => ({
      id: 'statement-1',
      status: 'NEEDS_REVIEW',
      version,
      sourceFileName: 'releve.pdf',
      warnings: [],
      errors: [],
      statement: { ...statementExample, id: 'statement-1', transactions: rows },
    });
    await page.route('**/api/statement-imports/statement-1', (route) =>
      route.fulfill({ json: result() }),
    );
    await page.route('**/api/statement-imports/statement-1?*', (route) => {
      rows = route.request().postDataJSON().transactions;
      version++;
      return route.fulfill({ json: result() });
    });
    await page.route('**/api/statement-imports/statement-1/exports/csv-1/preview', (route) =>
      route.fulfill({
        json: {
          template: csv,
          columns: ['Date', 'Libellé', 'Montant'],
          rows: rows.slice(0, 20).map((row) => ['01/09/2026', row.description, String(row.amount)]),
          rowSequences: rows.slice(0, 20).map((row) => row.sequence),
          rowKinds: rows.slice(0, 20).map(() => 'TRANSACTION'),
          totalRows: rows.length,
          statementVersion: version,
          templateFingerprint: 'ordered',
          errors: [],
          warnings: [],
        },
      }),
    );
    await page.reload();
    await expect(
      page.getByRole('button', { name: 'Ajouter une ligne', exact: true }),
    ).toBeVisible();
    await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
    await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
    if (view === 'Mouvements') await page.getByRole('tab', { name: /^Mouvements/ }).click();
    const panel = page.getByRole('tabpanel');
    await panel.getByLabel('Position d’insertion', { exact: true }).fill('20');
    await panel.getByRole('button', { name: 'Insérer à cette position', exact: true }).click();
    const dateLabel = view === 'Mouvements' ? 'Date opération · ligne 20' : 'Date · ligne 20';
    await page.getByLabel(dateLabel, { exact: true }).fill('2026-09-20');
    await page.getByLabel(dateLabel, { exact: true }).press('Enter');
    await editCell(page, 'Libellé · ligne 20', 'INSÉRÉ AU MILIEU');
    await expect(
      panel.getByRole('button', { name: 'Modifier Libellé · ligne 21', exact: true }),
    ).toContainText('ORIGINAL 20');
    await panel
      .getByRole('button', { name: 'Déplacer la ligne 21 par glisser-déposer', exact: true })
      .dragTo(
        panel.getByRole('button', {
          name: 'Déplacer la ligne 19 par glisser-déposer',
          exact: true,
        }),
      );
    await expect(
      panel.getByRole('button', { name: 'Modifier Libellé · ligne 19', exact: true }),
    ).toContainText('ORIGINAL 20');
    await expect(
      panel.getByRole('button', { name: 'Modifier Libellé · ligne 21', exact: true }),
    ).toContainText('INSÉRÉ AU MILIEU');
    await panel
      .getByRole('button', { name: 'Insérer une ligne avant la ligne 21', exact: true })
      .click();
    await page.getByLabel(dateLabel.replace('20', '21'), { exact: true }).press('Escape');
    await expect(
      panel.getByRole('button', { name: 'Modifier Libellé · ligne 22', exact: true }),
    ).toContainText('INSÉRÉ AU MILIEU');
    await panel.getByRole('button', { name: 'Annuler la dernière action', exact: true }).click();
    await expect(
      panel.getByRole('button', { name: 'Modifier Libellé · ligne 21', exact: true }),
    ).toContainText('INSÉRÉ AU MILIEU');
    // Both tables reflect the same order, including rows outside the server's first 20 preview rows.
    await page
      .getByRole('tab', { name: view === 'Mouvements' ? 'Aperçu du fichier' : /^Mouvements/ })
      .click();
    await expect(
      page.getByRole('button', { name: 'Modifier Libellé · ligne 19', exact: true }),
    ).toContainText('ORIGINAL 20');
    await expect(
      page.getByRole('button', { name: 'Modifier Libellé · ligne 21', exact: true }),
    ).toContainText('INSÉRÉ AU MILIEU');
    await page.getByRole('tab', { name: 'Aperçu du fichier' }).click();
    await page
      .getByRole('navigation', { name: 'Pages de l’aperçu' })
      .getByRole('button', { name: 'Suivant', exact: true })
      .click();
    await expect(
      page.getByRole('button', { name: 'Modifier Libellé · ligne 61', exact: true }),
    ).toContainText('ORIGINAL 60');
    await page
      .getByRole('navigation', { name: 'Pages de l’aperçu' })
      .getByRole('button', { name: 'Précédent', exact: true })
      .click();
    await page.getByRole('button', { name: 'Affichage du PDF original', exact: true }).click();
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.screenshot({
      path: `../output/screenshots/row-order-${view === 'Mouvements' ? 'movements' : 'preview'}.png`,
      animations: 'disabled',
    });
    await page.setViewportSize({ width: 390, height: 844 });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1),
    ).toBe(true);
    await page.screenshot({
      path: `../output/screenshots/row-order-${view === 'Mouvements' ? 'movements' : 'preview'}-mobile.png`,
      animations: 'disabled',
    });
    await page
      .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
      .first()
      .click();
    await expect.poll(() => version).toBe(2);
    expect(rows).toHaveLength(61);
    expect(rows.map((row) => row.sequence)).toEqual(
      Array.from({ length: 61 }, (_, index) => index + 1),
    );
    expect(rows[18].id).toBe('ordered-20');
    expect(rows[20]).toMatchObject({ description: 'INSÉRÉ AU MILIEU', date: '2026-09-20' });
    expect(rows[60].id).toBe('ordered-60');
  });
}

test('Le PDF se replie et les étapes précédentes restent accessibles sans perdre le relevé', async ({
  page,
}) => {
  await importToModel(page);
  await page.getByRole('button', { name: 'Importer le relevé', exact: true }).click();
  await expect(page.locator('.conversion-import-page')).toBeVisible();
  await page.getByRole('button', { name: 'Annuler', exact: true }).click();
  await expect(page).toHaveURL(/statementId=statement-1.*step=model/);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(x3.id);
  await page.getByRole('button', { name: 'Afficher le résultat', exact: true }).click();
  const pdfToggle = page.getByRole('button', { name: 'Affichage du PDF original', exact: true });
  await expect(pdfToggle).toHaveAttribute('aria-expanded', 'true');
  await pdfToggle.click();
  await expect(pdfToggle).toHaveAttribute('aria-expanded', 'false');
  await expect(
    page.getByRole('heading', { name: 'Votre relevé original', exact: true }),
  ).toBeHidden();
  await pdfToggle.click();
  await expect(
    page.getByRole('heading', { name: 'Votre relevé original', exact: true }),
  ).toBeVisible();
  await page
    .getByRole('button', { name: 'Insérer une ligne avant la ligne 2', exact: true })
    .click();
  await page.getByLabel('Date opération · ligne 2', { exact: true }).press('Escape');
  const table = page.locator('table.preview');
  await expect(table.locator('tbody tr')).toHaveCount(5);
  await expect(table.locator('tbody tr').first()).toContainText('Solde initial');
  await expect(table.locator('tbody tr').last()).toContainText('Solde final');
  await expect(table.getByText('Non exporté', { exact: true })).toHaveCount(2);
  await expect(table.locator('tbody tr').last().locator('td').last()).toHaveText('5');
  page.once('dialog', (dialog) => dialog.dismiss());
  await page.getByRole('button', { name: 'Choisir le modèle', exact: true }).click();
  await expect(page).toHaveURL(/step=result/);
  await expect(table.locator('tbody tr')).toHaveCount(5);
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Choisir le modèle', exact: true }).click();
  await expect(page).toHaveURL(/step=model/);
  await expect(page.getByLabel('Modèle d’exportation', { exact: true })).toHaveValue(x3.id);
});

test('Les cellules des mouvements se corrigent sans dialogue et préservent les décimales', async ({
  page,
}) => {
  await importExample(page);
  await editCell(page, 'Date opération · ligne 1', '2026-09-04');
  await editCell(page, 'Date valeur · ligne 1', '2026-09-05');
  await editCell(page, 'Libellé · ligne 1', 'LIBELLÉ CORRIGÉ DIRECTEMENT');
  await page.getByRole('button', { name: 'Champs complémentaires', exact: true }).click();
  await editCell(page, 'Devise · ligne 1', 'eur');
  await editCell(page, 'Compte comptable · ligne 1', '512001');
  await page.getByRole('button', { name: 'Modifier Débit · ligne 1', exact: true }).click();
  const amount = page.getByLabel('Débit · ligne 1', { exact: true });
  await amount.fill('montant incorrect');
  await amount.press('Enter');
  await expect(page.locator('.statement-cell-error')).toContainText('Saisissez un montant positif');
  await expect(
    page.getByRole('button', { name: 'Enregistrer les corrections', exact: true }).first(),
  ).toBeDisabled();
  await amount.fill('12 345,12345678');
  await amount.press('Enter');
  await page.getByRole('button', { name: 'Modifier Libellé · ligne 1', exact: true }).click();
  await page.getByLabel('Libellé · ligne 1', { exact: true }).fill('À ANNULER');
  await page.getByLabel('Libellé · ligne 1', { exact: true }).press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  const save = page.waitForRequest(
    (req) => req.method() === 'PUT' && req.url().includes('/statement-imports/statement-1?'),
  );
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  expect((await save).postDataJSON().transactions[0]).toMatchObject({
    date: '2026-09-04',
    valueDate: '2026-09-05',
    description: 'LIBELLÉ CORRIGÉ DIRECTEMENT',
    currency: 'EUR',
    account: '512001',
    debit: '12345.12345678',
    credit: null,
    amount: '12345.12345678',
    direction: 'DEBIT',
  });
});

test('Les cellules de l’aperçu corrigent les sources et régénèrent le fichier enregistré', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await editCell(page, 'Libellé · ligne 1', 'MODIFIÉ DANS APERÇU');
  await expect(page.getByLabel('Aperçu du fichier exporté')).toBeVisible();
  await editCell(page, 'Montant · ligne 1', '12345678901234567890,12345678');
  await expect(page.getByRole('button', { name: 'Texte du fichier', exact: true })).toBeDisabled();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  let saved: BankStatement;
  page.on('request', (req) => {
    if (req.method() === 'PUT' && req.url().includes('/statement-imports/statement-1?'))
      saved = req.postDataJSON();
  });
  await page.route('**/api/statement-imports/statement-1/exports/csv-1/preview', async (route) => {
    if (!saved) return route.fallback();
    expect(route.request().postDataJSON().statementVersion).toBe(2);
    const text = `Date;Libellé;Montant\r\n01/09/2026;${saved.transactions[0].description};${saved.transactions[0].amount}\r\n`;
    return route.fulfill({
      json: {
        template: csv,
        columns: ['Date', 'Libellé', 'Montant'],
        rows: [['01/09/2026', saved.transactions[0].description, saved.transactions[0].amount]],
        rowSequences: [1],
        rowKinds: ['TRANSACTION'],
        totalRows: 1,
        text,
        errors: [],
        warnings: [],
        statementVersion: 2,
        templateFingerprint: 'edited',
        isValidated: false,
      },
    });
  });
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  await expect(page.getByRole('tab', { name: 'Aperçu du fichier' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.getByLabel('Aperçu du fichier exporté')).toContainText('MODIFIÉ DANS APERÇU');
  expect(saved!.transactions[0]).toMatchObject({
    amount: '12345678901234567890.12345678',
    credit: '12345678901234567890.12345678',
  });
  await page.getByRole('button', { name: 'Texte du fichier', exact: true }).click();
  await expect(page.getByLabel('Contenu du fichier à télécharger')).toHaveValue(
    /MODIFIÉ DANS APERÇU/,
  );
  await page.route('**/api/statement-imports/statement-1/exports/csv-1', async (route) => {
    expect(route.request().postDataJSON()).toMatchObject({
      statementVersion: 2,
      templateFingerprint: 'edited',
    });
    expect(route.request().postDataJSON().editedText).toBeUndefined();
    await route.fulfill({
      body: `${saved.transactions[0].description};${saved.transactions[0].amount}\r\n`,
      contentType: 'text/csv; charset=utf-8',
      headers: { 'Content-Disposition': 'attachment; filename="correction.csv"' },
    });
  });
  const downloaded = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Télécharger', exact: true }).first().click();
  expect(await readFile((await (await downloaded).path())!, 'utf8')).toBe(
    'MODIFIÉ DANS APERÇU;12345678901234567890.12345678\r\n',
  );
});

test('Les soldes Sage X3 sont corrigés dans l’aperçu sans décaler les colonnes omises', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(x3.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  const table = page.getByLabel('Aperçu du fichier exporté');
  await expect(table.getByText('Non exporté', { exact: true })).toHaveCount(2);
  await editCell(page, 'Montant en devise · solde initial (montant signé)', '-1000,55');
  await editCell(page, 'Date opération · solde initial', '2026-08-31');
  await editCell(page, 'Montant en devise · solde final (montant signé)', '13000,75');
  await page.getByRole('button', { name: 'Modifier Sens · ligne 1', exact: true }).click();
  await page.getByLabel('Sens · ligne 1', { exact: true }).selectOption('DEBIT');
  await page.getByLabel('Sens · ligne 1', { exact: true }).press('Enter');
  await expect(table.getByRole('button', { name: /Modifier Numéro interne/ })).toHaveCount(0);
  await expect(table.getByRole('button', { name: /Modifier Sens · solde/ })).toHaveCount(0);
  const save = page.waitForRequest(
    (req) => req.method() === 'PUT' && req.url().includes('/statement-imports/statement-1?'),
  );
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  expect((await save).postDataJSON()).toMatchObject({
    openingBalance: '-1000.55',
    openingBalanceDate: '2026-08-31',
    closingBalance: '13000.75',
  });
});

test('Un conflit conserve les cellules corrigées et bloque le téléchargement non enregistré', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await editCell(page, 'Libellé · ligne 1', 'CORRECTION À CONSERVER');
  await page.route('**/api/statement-imports/statement-1?version=*', (route) =>
    route.fulfill({
      status: 409,
      json: { title: 'Le relevé a été modifié par un autre utilisateur.' },
    }),
  );
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  await expect(page.getByLabel('Aperçu du fichier exporté')).toContainText(
    'CORRECTION À CONSERVER',
  );
  await expect(
    page.getByText('Le relevé a été modifié par un autre utilisateur.', { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Télécharger', exact: true }).first(),
  ).toBeDisabled();
});

test('La saisie dans les tableaux reste lisible sur ordinateur et mobile', async ({ page }) => {
  await page.setViewportSize({ width: 1500, height: 950 });
  await importExample(page);
  await page.getByRole('button', { name: 'Modifier Libellé · ligne 1', exact: true }).click();
  await page
    .getByLabel('Libellé · ligne 1', { exact: true })
    .fill('LIBELLÉ CORRIGÉ DANS LE TABLEAU');
  await page
    .getByLabel('Tableau des mouvements')
    .screenshot({ path: '../output/screenshots/inline-movements.png' });
  await page.getByLabel('Libellé · ligne 1', { exact: true }).press('Escape');
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(x3.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await page
    .getByRole('button', {
      name: 'Modifier Montant en devise · solde initial (montant signé)',
      exact: true,
    })
    .click();
  await page
    .getByLabel('Aperçu du fichier exporté')
    .screenshot({ path: '../output/screenshots/inline-preview.png' });
  await page
    .getByLabel('Montant en devise · solde initial (montant signé)', { exact: true })
    .press('Escape');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('button', { name: 'Modifier Libellé · ligne 1', exact: true }).click();
  await expect(page.getByLabel('Libellé · ligne 1', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page
    .getByLabel('Aperçu du fichier exporté')
    .screenshot({ path: '../output/screenshots/inline-preview-mobile.png' });
});

test('Les lignes peuvent être ajoutées, supprimées et restaurées avant enregistrement', async ({
  page,
}) => {
  await importExample(page);
  await page.getByRole('button', { name: 'Ajouter une ligne', exact: true }).click();
  await page.getByLabel('Date opération · ligne 3', { exact: true }).fill('2026-09-03');
  await page.getByLabel('Date opération · ligne 3', { exact: true }).press('Enter');
  await editCell(page, 'Date valeur · ligne 3', '2026-09-03');
  await editCell(page, 'Libellé · ligne 3', 'LIGNE AJOUTÉE');
  const exactAmount = '12345678901234567890.12345678';
  await editCell(page, 'Crédit · ligne 3', exactAmount);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(
    page.getByLabel('Tableau des mouvements').getByText('LIGNE AJOUTÉE', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Supprimer la ligne 2', exact: true }).click();
  const removal = page.getByRole('dialog', { name: 'Supprimer des lignes' });
  await removal.getByRole('button', { name: 'Annuler', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Corriger la transaction 2' })).toBeVisible();
  await page.getByRole('button', { name: 'Supprimer la ligne 2', exact: true }).click();
  await removal.getByRole('button', { name: 'Supprimer les lignes', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Corriger la transaction 2' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Annuler la dernière action', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Corriger la transaction 2' })).toBeVisible();
  await page.getByLabel('Sélectionner la ligne 2', { exact: true }).check();
  await page.getByRole('button', { name: 'Supprimer la sélection', exact: true }).click();
  await removal.getByRole('button', { name: 'Supprimer les lignes', exact: true }).click();
  const save = page.waitForRequest(
    (req) => req.url().includes('/statement-imports/statement-1?') && req.method() === 'PUT',
  );
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  const input = (await save).postDataJSON();
  expect(input.transactions).toHaveLength(2);
  expect(input.transactions[0].id).toBe('transaction-1');
  expect(input.transactions[1]).toMatchObject({
    sequence: 3,
    description: 'LIGNE AJOUTÉE',
    amount: exactAmount,
    credit: exactAmount,
    debit: null,
    direction: 'CREDIT',
  });
  expect(input.transactions[1].id).toMatch(/^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i);
  await expect(page.getByLabel('Tableau des mouvements')).toContainText('LIGNE AJOUTÉE');
  await expect(page.locator('.conversion-unsaved')).toHaveCount(0);
});

test('Une ligne peut être insérée au milieu puis déplacée sans perdre les mouvements', async ({
  page,
}) => {
  await importExample(page);
  await page.getByRole('button', { name: 'Ajouter une ligne', exact: true }).click();
  await page.getByLabel('Date opération · ligne 3', { exact: true }).press('Escape');
  await editCell(page, 'Libellé · ligne 3', 'MOUVEMENT INTERCALÉ');
  await editCell(page, 'Crédit · ligne 3', '123.45');
  await page.getByLabel('Position de la ligne 3', { exact: true }).fill('2');
  await page.getByLabel('Position de la ligne 3', { exact: true }).press('Enter');
  const rows = page.getByLabel('Tableau des mouvements').locator('tbody tr');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(1)).toContainText('MOUVEMENT INTERCALÉ');
  await page.getByLabel('Position de la ligne 3', { exact: true }).fill('1');
  await page.getByLabel('Position de la ligne 3', { exact: true }).press('Enter');
  await expect(rows.nth(0)).toContainText(statementExample.transactions[1].description!);
  await expect(rows.nth(2)).toContainText('MOUVEMENT INTERCALÉ');
  await page.getByRole('button', { name: 'Annuler la dernière action', exact: true }).click();
  await expect(rows.nth(1)).toContainText('MOUVEMENT INTERCALÉ');
  await expect(rows.nth(2)).toContainText(statementExample.transactions[1].description!);
  const saved = page.waitForRequest(
    (req) => req.url().includes('/statement-imports/statement-1?') && req.method() === 'PUT',
  );
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  const transactions = (await saved).postDataJSON().transactions;
  expect(transactions.map((row: StatementTransaction) => row.sequence)).toEqual([1, 2, 3]);
  expect(transactions[0].id).toBe('transaction-1');
  expect(transactions[1]).toMatchObject({
    description: 'MOUVEMENT INTERCALÉ',
    credit: '123.45',
    amount: '123.45',
  });
  expect(transactions[2].id).toBe('transaction-2');
  expect(transactions[2].debit).toBe(String(statementExample.transactions[1].debit));
});

test('La modification d’une colonne concerne toutes les pages ou uniquement la sélection', async ({
  page,
}) => {
  await importExample(page);
  let rows: StatementTransaction[] = Array.from({ length: 60 }, (_, i) => ({
    ...statementExample.transactions[0],
    id: `row-${i + 1}`,
    sequence: i + 1,
    description: `MOUVEMENT ${i + 1}`,
    reference: null,
    currency: null,
  }));
  let version = 1;
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version,
        warnings: [],
        errors: [],
        sourceFileName: 'releve.pdf',
        statement: { ...statementExample, id: 'statement-1', transactions: rows },
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1?*', (route) => {
    rows = route.request().postDataJSON().transactions;
    version++;
    return route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version,
        warnings: [],
        errors: [],
        statement: { ...statementExample, id: 'statement-1', transactions: rows },
      },
    });
  });
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Modifier une colonne', exact: true }).click();
  let dialog = page.getByRole('dialog', { name: 'Modifier une colonne' });
  await expect(dialog.getByLabel('Lignes concernées')).toHaveValue('all');
  await dialog.getByLabel('Colonne à modifier').selectOption('currency');
  await dialog.getByLabel('Nouvelle valeur').fill('eur');
  await expect(dialog.getByLabel('Nouvelle valeur')).toHaveValue('EUR');
  await dialog.screenshot({
    path: '../output/screenshots/redesign/bulk-editor.png',
    animations: 'disabled',
  });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1)).toBe(true);
  await dialog.screenshot({
    path: '../output/screenshots/redesign/bulk-editor-mobile.png',
    animations: 'disabled',
  });
  await dialog.getByRole('button', { name: 'Appliquer aux 60 lignes', exact: true }).click();
  await page.getByLabel('Sélectionner toutes les lignes', { exact: true }).check();
  await expect(
    page.getByText('60 ligne(s) sélectionnée(s), toutes les pages', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Désélectionner', exact: true }).click();
  await page.getByLabel('Sélectionner la ligne 1', { exact: true }).check();
  await page.getByRole('button', { name: 'Modifier une colonne', exact: true }).click();
  dialog = page.getByRole('dialog', { name: 'Modifier une colonne' });
  await expect(dialog.getByLabel('Lignes concernées')).toHaveValue('selected');
  await dialog.getByLabel('Colonne à modifier').selectOption('debit');
  await dialog.getByLabel('Nouvelle valeur').fill('42.12345678');
  await dialog.getByRole('button', { name: 'Appliquer aux 1 lignes', exact: true }).click();
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  await expect.poll(() => rows[0].debit).toBe('42.12345678');
  expect(rows).toHaveLength(60);
  expect(rows.every((row) => row.currency === 'EUR')).toBe(true);
  expect(rows[0]).toMatchObject({ amount: '42.12345678', direction: 'DEBIT', credit: null });
  expect(rows[59]).toMatchObject({
    id: 'row-60',
    description: 'MOUVEMENT 60',
    credit: statementExample.transactions[0].credit,
  });
  // Explicitly disabling synchronization keeps all other financial columns intact.
  await page.getByLabel('Sélectionner la ligne 1', { exact: true }).check();
  await page.getByRole('button', { name: 'Modifier une colonne', exact: true }).click();
  dialog = page.getByRole('dialog', { name: 'Modifier une colonne' });
  await dialog.getByLabel('Colonne à modifier').selectOption('amount');
  await dialog.getByLabel('Nouvelle valeur').fill('11.99');
  await dialog.getByRole('checkbox').uncheck();
  await dialog.getByRole('button', { name: 'Appliquer aux 1 lignes', exact: true }).click();
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  await expect.poll(() => rows[0].amount).toBe('11.99');
  expect(rows[0].debit).toBe('42.12345678');
  await page.getByLabel('Sélectionner la ligne 1', { exact: true }).check();
  await page.getByRole('button', { name: 'Modifier une colonne', exact: true }).click();
  dialog = page.getByRole('dialog', { name: 'Modifier une colonne' });
  await dialog.getByLabel('Colonne à modifier').selectOption('currency');
  await dialog.getByLabel('Action sur la colonne').selectOption('clear');
  await expect(dialog.getByLabel('Nouvelle valeur')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Appliquer aux 1 lignes', exact: true }).click();
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  await expect.poll(() => rows[0].currency).toBeNull();
  expect(rows[59].currency).toBe('EUR');
});

test('Un accès en consultation ne permet pas de modifier les lignes extraites', async ({
  page,
}) => {
  await importExample(page);
  await page.route('**/api/auth/session', (route) =>
    route.fulfill({
      json: {
        setupRequired: false,
        user: {
          id: 'reader-test',
          name: 'Lecteur',
          email: 'reader@example.test',
          role: 'User',
          isActive: true,
          version: 1,
          permissions: [
            'documents.read',
            'clients.read',
            'banks.read',
            'accounts.read',
            'templates.read',
          ],
        },
      },
    }),
  );
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByLabel('Tableau des mouvements').getByText('VIREMENT CLIENT', { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ajouter une ligne', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Modifier une colonne', exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByRole('button', { name: /Supprimer la ligne/ })).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: 'Corriger la transaction 1', exact: true }),
  ).toBeDisabled();
  await expect(page.getByLabel('Sélectionner toutes les lignes', { exact: true })).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: 'Modifier Libellé · ligne 1', exact: true }),
  ).toBeDisabled();
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Modifier Libellé · ligne 1', exact: true }),
  ).toBeDisabled();
});

test('Les champs vides sont complétés depuis le relevé et les corrections restent manuelles', async ({
  page,
}) => {
  await importExample(page);
  let completed = false;
  let version = 1;
  const detail = () => ({
    id: 'statement-1',
    status: 'NEEDS_REVIEW',
    version,
    validatedAt: null,
    errors: [],
    warnings: [],
    sourceFileName: 'releve.pdf',
    extraction: { method: 'PADDLE_OCR', pageCount: 1, lowConfidenceCount: 0, needsMapping: false },
    statement: {
      ...statementExample,
      id: 'statement-1',
      totalDebit: completed ? '125.50' : null,
      totalCredit: completed ? '12500.00' : null,
      balanceConvention: completed ? 'CREDIT_INCREASES' : null,
    },
  });
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({ json: detail() }),
  );
  await page.route('**/api/statement-imports/statement-1/complete-metadata', (route) => {
    expect(route.request().method()).toBe('POST');
    expect(route.request().postDataJSON()).toEqual({ version: 1 });
    completed = true;
    version++;
    return route.fulfill({ json: detail() });
  });
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Corriger les informations', exact: true }).click();
  await expect(page.getByLabel('Total débit', { exact: true })).toHaveValue('');
  await page.getByRole('button', { name: 'Compléter les champs vides', exact: true }).click();
  await expect.poll(() => completed).toBe(true);
  await page.getByRole('button', { name: 'Corriger les informations', exact: true }).click();
  await expect(page.getByLabel('Total débit', { exact: true })).toHaveValue('125.50');
  await expect(page.getByLabel('Total crédit', { exact: true })).toHaveValue('12500.00');
  await expect(page.getByLabel('Convention des soldes', { exact: true })).toHaveValue(
    'CREDIT_INCREASES',
  );
  await page.getByLabel('Total débit', { exact: true }).fill('124.00');
  await expect(
    page.getByRole('button', { name: 'Compléter les champs vides', exact: true }),
  ).toBeDisabled();
  await expect(page.getByLabel('Total débit', { exact: true })).toHaveValue('124.00');
});

test('Depuis la conversion, un modèle peut être configuré ou dupliqué sans perdre ses champs', async ({
  page,
}) => {
  await importExample(page);
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(x3.id);
  await page.getByRole('button', { name: 'Configurer', exact: true }).click();
  let dialog = page.getByRole('dialog', { name: 'Modifier le modèle' });
  await expect(dialog.getByLabel('Libellé sur les lignes de solde', { exact: true })).toHaveValue(
    'Description',
  );
  await dialog
    .getByLabel('Libellé sur les lignes de solde', { exact: true })
    .selectOption('MovementDescription');
  await dialog.getByLabel('Séparateur', { exact: true }).selectOption('|');
  const save = page.waitForRequest(
    (req) => req.url().endsWith('/export-templates/x3-1') && req.method() === 'PUT',
  );
  await dialog.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  const input = (await save).postDataJSON();
  expect(input.fields).toHaveLength(10);
  expect(input.fields[2].sourceField).toBe('MovementDescription');
  expect(input.delimiter).toBe('|');
  await expect(dialog).not.toBeVisible();
  await page.getByRole('button', { name: 'Dupliquer', exact: true }).click();
  dialog = page.getByRole('dialog', { name: 'Dupliquer le modèle' });
  await expect(dialog.getByLabel('Nom du modèle', { exact: true })).toHaveValue(
    `${x3.name} (copie)`,
  );
  await expect(dialog.getByLabel('Libellé sur les lignes de solde', { exact: true })).toHaveValue(
    'MovementDescription',
  );
  await dialog.getByLabel('Nom du modèle', { exact: true }).fill('Mon format X3');
  await dialog.getByRole('button', { name: 'Retirer la colonne 10', exact: true }).click();
  const create = page.waitForRequest(
    (req) => req.url().endsWith('/export-templates') && req.method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  expect((await create).postDataJSON().fields).toHaveLength(9);
  await expect(page.getByLabel('Modèle d’exportation', { exact: true })).toHaveValue('custom-new');
  await page.getByRole('button', { name: 'Configurer', exact: true }).click();
  dialog = page.getByRole('dialog', { name: 'Modifier le modèle' });
  await expect(dialog.getByText('Colonnes (9/30)', { exact: true })).toBeVisible();
  await dialog.screenshot({
    path: '../output/conversion-template-editor.png',
    animations: 'disabled',
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect.poll(() => dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1)).toBe(true);
  await dialog.screenshot({
    path: '../output/conversion-template-editor-mobile.png',
    animations: 'disabled',
  });
});

test('Un nouveau modèle Sage X3 propose les dix champs et reste personnalisable', async ({
  page,
}) => {
  await page.goto('/templates');
  await page.getByRole('button', { name: 'Ajouter un modèle', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Ajouter un modèle' });
  await dialog.getByLabel('Type', { exact: true }).selectOption('SAGEX3');
  await expect(dialog.getByText('Colonnes (10/30)', { exact: true })).toBeVisible();
  await expect(dialog.getByLabel('Source 3', { exact: true })).toHaveValue('Description');
  await expect(dialog.getByLabel('Source 10', { exact: true })).toHaveValue('Sequence');
  await dialog.getByLabel('Nom du modèle', { exact: true }).fill('Sage X3 personnalisé');
  await dialog.getByRole('button', { name: 'Descendre la colonne 1', exact: true }).click();
  await expect(dialog.getByLabel('Source 1', { exact: true })).toHaveValue('ValueDate');
  await dialog
    .getByLabel('Libellé sur les lignes de solde', { exact: true })
    .selectOption('DescriptionWithBalances');
  const request = page.waitForRequest(
    (req) => req.url().endsWith('/export-templates') && req.method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  const saved = (await request).postDataJSON();
  expect(saved.encoding).toBe('ascii');
  expect(saved.numberCulture).toBe('en-US');
  expect(saved.includeHeader).toBe(false);
  expect(saved.fields[2].sourceField).toBe('DescriptionWithBalances');
  expect(saved.fields[0].sourceField).toBe('ValueDate');
  await expect(dialog).not.toBeVisible();
});

test('Le traitement affiche les étapes du serveur, les pages et le temps réel', async ({
  page,
}) => {
  let release!: () => void;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  let stage = 'queued';
  await page.route('**/api/statement-imports/processing/*', (route) =>
    route.fulfill({
      json: {
        stage,
        percent: stage === 'queued' ? 20 : 47,
        extractedPages: stage === 'queued' ? 0 : 1,
        totalPages: 2,
      },
    }),
  );
  await page.route('**/api/statement-imports/pdf', async (route) => {
    expect(route.request().postData()).toContain('processingId');
    await waiting;
    await route.fallback();
  });
  await page.goto('/conversions?import=1');
  const dialog = page.locator('.conversion-import-page');
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'releve.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.7 test'),
  });
  await dialog.getByLabel('Banque du relevé', { exact: true }).selectOption('bank-1');
  await dialog.getByRole('button', { name: 'Importer et continuer' }).click();
  const progress = dialog.getByRole('progressbar', { name: 'Étapes du traitement terminées' });
  await expect(dialog.getByRole('progressbar')).toHaveCount(6);
  await expect(progress).toHaveAttribute('aria-valuenow', '20');
  await expect(dialog.getByRole('status')).toHaveText('En attente du moteur OCR');
  stage = 'reading';
  await expect(progress).toHaveAttribute('aria-valuenow', '47');
  await expect(dialog.getByText('1 / 2 pages lues')).toBeVisible();
  await expect(
    dialog.getByRole('progressbar', { name: 'Lecture des pages', exact: true }),
  ).toHaveAttribute('aria-valuenow', '50');
  await expect(
    dialog.getByRole('progressbar', { name: 'Contrôle du PDF', exact: true }),
  ).toHaveAttribute('aria-valuenow', '100');
  await expect(
    dialog.getByRole('progressbar', { name: 'Organisation des mouvements', exact: true }),
  ).toHaveAttribute('aria-valuenow', '0');
  await expect(dialog.getByText(/0 min 0[1-9] s écoulées/)).toBeVisible();
  await page.screenshot({ path: '../output/processing-progress.png', animations: 'disabled' });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({
    path: '../output/processing-progress-mobile.png',
    animations: 'disabled',
  });
  release();
  await expect(dialog).not.toBeVisible();
  await expect(page).toHaveURL(/step=model/);
  await expect(page.getByRole('region', { name: 'Choix du modèle d’exportation' })).toBeVisible();
});

test('Un refus OCR arrête les barres et permet de relancer le même fichier', async ({ page }) => {
  let release!: () => void;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/statement-imports/processing/*', (route) =>
    route.fulfill({
      json: { stage: 'queued', percent: 20, extractedPages: 0, totalPages: 2 },
    }),
  );
  await page.route('**/api/statement-imports/pdf', async (route) => {
    await waiting;
    await route.fulfill({
      status: 503,
      json: {
        title: 'La file d’attente PaddleOCR est pleine. Réessayez dans quelques instants.',
      },
    });
  });
  await page.goto('/conversions?import=1');
  const dialog = page.locator('.conversion-import-page');
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'releve.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.7 test'),
  });
  await dialog.getByLabel('Banque du relevé', { exact: true }).selectOption('bank-1');
  await dialog.getByRole('button', { name: 'Importer et continuer' }).click();
  await expect(dialog.getByRole('status')).toHaveText('En attente du moteur OCR');
  release();
  await expect(dialog.getByRole('alert')).toContainText('file d’attente');
  await expect(dialog.getByRole('status')).toHaveText('Traitement interrompu');
  await expect(dialog.locator('.processing-track.indeterminate')).toHaveCount(0);
  await expect(
    dialog.getByRole('progressbar', { name: 'Envoi du PDF', exact: true }),
  ).toHaveAttribute('aria-valuenow', '100');
  await expect(dialog.getByRole('button', { name: 'Importer et continuer' })).toBeEnabled();
  await expect(dialog.getByLabel('Fichier PDF du relevé')).toHaveValue(/releve.pdf/);
});

test('Une erreur immédiate retrouve la dernière étape du serveur', async ({ page }) => {
  let failed = false;
  await page.route('**/api/statement-imports/processing/*', (route) =>
    failed
      ? route.fulfill({
          json: {
            stage: 'failed',
            activeStage: 'queued',
            percent: 20,
            extractedPages: 0,
            totalPages: 2,
          },
        })
      : route.fulfill({ status: 404, json: { title: 'Suivi indisponible' } }),
  );
  await page.route('**/api/statement-imports/pdf', (route) => {
    failed = true;
    return route.fulfill({
      status: 503,
      json: { title: 'La connexion HTTPS PaddleOCR est bloquée.' },
    });
  });
  await page.goto('/conversions?import=1');
  const dialog = page.locator('.conversion-import-page');
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'releve.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.7 test'),
  });
  await dialog.getByLabel('Banque du relevé', { exact: true }).selectOption('bank-1');
  await dialog.getByRole('button', { name: 'Importer et continuer' }).click();
  await expect(dialog.getByRole('status')).toHaveText('Traitement interrompu');
  await expect(
    dialog.getByRole('progressbar', { name: 'Contrôle du PDF', exact: true }),
  ).toHaveAttribute('aria-valuenow', '100');
  await expect(
    dialog.getByRole('progressbar', { name: 'Lecture des pages', exact: true }),
  ).toHaveAttribute('aria-valuetext', 'Interrompu, 0 %');
  await expect(dialog.locator('.processing-track.indeterminate')).toHaveCount(0);
});

test('La commande du bandeau ouvre réellement un nouvel import PDF', async ({ page }) => {
  await page.goto('/conversions');
  await page.getByRole('link', { name: 'Importer un PDF', exact: true }).click();
  const dialog = page.locator('.conversion-import-page');
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Annuler', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await page.getByRole('link', { name: 'Importer un PDF', exact: true }).click();
  await expect(dialog).toBeVisible();
});

test('PDF selection rejects JSON and presents OCR availability errors without creating a statement', async ({
  page,
}) => {
  await page.goto('/conversions');
  await page.getByRole('button', { name: 'Importer un relevé', exact: true }).click();
  const dialog = page.locator('.conversion-import-page');
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'releve.json',
    mimeType: 'application/json',
    buffer: Buffer.from('{}'),
  });
  await expect(dialog.getByRole('alert')).toHaveText('Choisissez un fichier au format PDF.');
  await expect(dialog.getByRole('button', { name: 'Importer et continuer' })).toBeDisabled();
  await dialog.getByLabel('Fichier PDF du relevé').setInputFiles({
    name: 'scan.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-scan'),
  });
  await dialog.getByLabel('Banque du relevé', { exact: true }).selectOption('bank-1');
  await page.route('**/api/statement-imports/pdf', (route) =>
    route.fulfill({ status: 503, json: { title: 'Le moteur OCR local est indisponible.' } }),
  );
  await dialog.getByRole('button', { name: 'Importer et continuer' }).click();
  await expect(dialog.getByRole('alert')).toHaveText('Le moteur OCR local est indisponible.');
  await expect(dialog.getByRole('button', { name: 'Importer et continuer' })).toBeEnabled();
  await dialog.screenshot({ path: '../output/pdf-import-dialog.png' });
});
async function mockIncompleteStatementPreview(page: Page, isAdapted = () => false) {
  const detail = () => ({
    id: 'statement-1',
    status: 'NEEDS_REVIEW',
    version: isAdapted() ? 2 : 1,
    validatedAt: null,
    statement: {
      ...statementExample,
      id: 'statement-1',
      transactions: statementExample.transactions.map((transaction, index) => ({
        ...transaction,
        id: `transaction-${index + 1}`,
        date: isAdapted() ? transaction.date : null,
      })),
    },
    extraction: {
      method: 'PADDLE_OCR',
      pageCount: 1,
      lowConfidenceCount: 0,
      needsMapping: false,
    },
    sourceFileName: 'scan.pdf',
    warnings: [],
    errors: isAdapted()
      ? []
      : [
          {
            code: 'INVALID_DATE',
            message: 'Date opération manquante.',
            transactionSequence: 1,
            sourceField: 'Date',
          },
        ],
  });
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({ json: detail() }),
  );
  await page.route('**/api/statement-imports/statement-1/exports/csv-1/preview', (route) => {
    expect(route.request().postDataJSON()).toEqual({ statementVersion: detail().version });
    const rows = detail().statement.transactions.map((transaction) => [
      transaction.date ?? '',
      transaction.description,
      transaction.amount,
    ]);
    return route.fulfill({
      json: {
        template: { id: csv.id, name: csv.name },
        columns: csv.fields.map((field) => field.fieldName),
        rows,
        text: rows.map((row) => row.join(';')).join('\n'),
        textTruncated: false,
        rowSequences: [1, 2],
        rowKinds: ['TRANSACTION', 'TRANSACTION'],
        totalRows: 2,
        isValidated: false,
        errors: detail().errors,
        warnings: [],
        statementVersion: detail().version,
        templateFingerprint: 'incomplete-preview',
      },
    });
  });
  await page.goto('/conversions?statementId=statement-1');
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
}

test('L’adaptation depuis l’aperçu protège le texte non enregistré et recharge les mouvements sans les valider', async ({
  page,
}) => {
  let adapted = false;
  let validationCalls = 0;
  let finishAdaptation!: () => void;
  const adaptationPending = new Promise<void>((resolve) => {
    finishAdaptation = resolve;
  });
  await page.route('**/api/statement-imports/statement-1/validate', (route) => {
    validationCalls++;
    return route.fulfill({ status: 500, json: { title: 'Validation inattendue' } });
  });
  await page.route('**/api/statement-imports/statement-1/adapt', async (route) => {
    expect(route.request().method()).toBe('POST');
    expect(route.request().postDataJSON()).toEqual({ statementVersion: 1 });
    await adaptationPending;
    adapted = true;
    await route.fulfill({ json: {} });
  });
  await mockIncompleteStatementPreview(page, () => adapted);
  const repair = page.getByRole('region', { name: 'Adaptation automatique du relevé' });
  await expect(repair).toContainText('2 ligne(s) avec des champs incomplets');
  await expect(repair).toContainText('corrections manuelles des lignes remplacées');
  const restart = repair.getByRole('button', { name: 'Relancer l’adaptation automatique' });
  await page.getByRole('button', { name: 'Texte du fichier', exact: true }).click();
  const text = page.getByLabel('Contenu du fichier à télécharger', { exact: true });
  const generated = await text.inputValue();
  await text.fill(generated + '\nCORRECTION MANUELLE');
  await expect(restart).toBeDisabled();
  await text.fill(generated);
  await expect(restart).toBeEnabled();
  await restart.click();
  await expect(repair.getByRole('button', { name: 'Adaptation en cours…' })).toBeDisabled();
  finishAdaptation();
  await expect(page.getByRole('tab', { name: 'Mouvements' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  const rows = page.getByLabel('Tableau des mouvements');
  await expect(rows.getByRole('row')).toHaveCount(3);
  await expect(rows).toContainText('01/09/2026');
  await expect(rows).toContainText('02/09/2026');
  await expect(
    page.getByLabel('J’ai vérifié les données du relevé et les avertissements.'),
  ).not.toBeChecked();
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeDisabled();
  expect(validationCalls).toBe(0);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await expect(repair).toHaveCount(0);
});

test('Un échec d’adaptation depuis l’aperçu conserve les mouvements existants et permet de réessayer', async ({
  page,
}) => {
  let attempts = 0;
  await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
    attempts++;
    expect(route.request().postDataJSON()).toEqual({ statementVersion: 1 });
    return route.fulfill({
      status: 422,
      json: { title: 'Les dates restent illisibles dans le PDF.' },
    });
  });
  await mockIncompleteStatementPreview(page);
  const preview = page.getByLabel('Aperçu du fichier exporté');
  const originalPreview = await preview.innerText();
  const repair = page.getByRole('region', { name: 'Adaptation automatique du relevé' });
  await repair.getByRole('button', { name: 'Relancer l’adaptation automatique' }).click();
  await expect(
    page.getByRole('alert').filter({ hasText: 'Les dates restent illisibles dans le PDF.' }),
  ).toBeVisible();
  await expect(preview).toHaveText(originalPreview, { useInnerText: true });
  await expect(
    repair.getByRole('button', { name: 'Relancer l’adaptation automatique' }),
  ).toBeEnabled();
  await page.getByRole('tab', { name: 'Mouvements' }).click();
  const rows = page.getByLabel('Tableau des mouvements');
  await expect(rows.getByRole('row')).toHaveCount(3);
  await expect(rows).toContainText('VIREMENT CLIENT');
  await expect(rows).toContainText('FRAIS BANCAIRES');
  await page.getByRole('button', { name: 'Corriger la transaction 1', exact: true }).click();
  await expect(page.getByLabel('Date opération · ligne 1', { exact: true })).toHaveValue('');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(attempts).toBe(1);
});

test('Le correctif du sens Sage 100 change seulement la correspondance Sens et conserve les autres colonnes', async ({
  page,
}) => {
  const legacy = {
    ...csv,
    id: 'sage-100-legacy',
    type: 'SAGE100',
    name: 'Sage 100 Sens',
    fileExtension: 'txt',
    includeHeader: false,
    fields: [
      ...csv.fields,
      { fieldName: 'Sens', sourceField: 'Amount', position: 12, format: '0.00', required: false },
    ],
  };
  let savedFields: unknown[] | undefined;
  let currentModel = legacy;
  await page.route('**/api/export-templates?*', (route) =>
    route.fulfill({
      json: {
        items: [currentModel],
        total: 1,
        page: 1,
        pageSize: 20,
      },
    }),
  );
  await page.route('**/api/export-templates/sage-100-legacy', (route) => {
    expect(route.request().method()).toBe('PUT');
    const saved = route.request().postDataJSON();
    savedFields = saved.fields;
    currentModel = { ...saved, id: legacy.id };
    return route.fulfill({ json: { ...saved, id: legacy.id } });
  });
  await page.goto('/conversions?statementId=statement-1');
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(legacy.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  const guidance = page.getByRole('region', { name: 'Correspondance du sens' });
  await guidance.getByRole('button', { name: 'Configurer le sens D/C' }).click();
  const editor = page.getByRole('dialog', { name: 'Modifier le modèle' });
  await expect(editor.getByLabel('Source 4', { exact: true })).toHaveValue('Direction');
  await expect(editor.getByLabel('Format 4', { exact: true })).toHaveValue('');
  await expect(editor.getByLabel('Colonne 4 obligatoire', { exact: true })).toBeChecked();
  await editor.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  await expect(editor).not.toBeVisible();
  expect(savedFields).toEqual([
    ...legacy.fields.slice(0, 3),
    { ...legacy.fields[3], sourceField: 'Direction', format: null, required: true },
  ]);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await expect(guidance).toHaveCount(0);
});

test('Une erreur de reconnaissance guide vers les repères et bloque les colonnes incohérentes', async ({
  page,
}) => {
  let adapted = false;
  let attempts = 0;
  const boxes = [
    ['Date', 8],
    ['Valeur', 20],
    ['Libellé', 32],
    ['Débit', 75],
    ['Crédit', 88],
  ].map(([text, left]) => ({
    text,
    left,
    right: Number(left) + 8,
    top: 30,
    bottom: 32,
    confidence: 0.99,
  }));
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version: adapted ? 2 : 1,
        validatedAt: null,
        statement: { ...statementExample, id: 'statement-1' },
        warnings: [],
        errors: [],
        sourceFileName: 'scan.pdf',
        extraction: {
          method: 'PADDLE_OCR_V6',
          pageCount: 1,
          lowConfidenceCount: 0,
          needsMapping: false,
        },
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
    route.fulfill({
      json: {
        method: 'PADDLE_OCR_V6',
        pages: [{ number: 1, width: 100, height: 100, boxes }],
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
    attempts++;
    const input = route.request().postDataJSON();
    if (!input.mapping)
      return route.fulfill({
        status: 422,
        json: {
          title:
            'Les colonnes du relevé ne sont pas reconnues. Consultez le texte extrait et réglez l’adaptation du tableau.',
        },
      });
    expect(input.mapping).toEqual({
      date: 8,
      valueDate: null,
      description: 32,
      debit: 75,
      credit: 88,
      headerTop: 32,
    });
    expect(input.statementVersion).toBe(1);
    adapted = true;
    return route.fulfill({ json: {} });
  });
  await page.goto('/conversions?statementId=statement-1');
  await page.getByRole('tab', { name: 'Texte lu & adaptation' }).click();
  await page.getByRole('button', { name: 'Relancer l’adaptation automatique' }).click();
  await expect(page.getByRole('tabpanel').getByRole('alert')).toContainText(
    'les mouvements existants sont conservés',
  );
  await page.getByRole('button', { name: 'Corriger le réglage des colonnes' }).click();
  await page.getByLabel('Repère pour Date opération', { exact: true }).selectOption('0');
  await page
    .getByLabel('Repère pour Date valeur (facultative)', { exact: true })
    .selectOption('absent');
  await page.getByLabel('Repère pour Libellé', { exact: true }).selectOption('2');
  await page.getByLabel('Repère pour Débit', { exact: true }).selectOption('2');
  await page.getByLabel('Repère pour Crédit', { exact: true }).selectOption('4');
  await page.getByLabel('Repère pour Début du tableau', { exact: true }).selectOption('0');
  await page.getByRole('button', { name: 'Appliquer aux mouvements' }).click();
  await expect(page.getByRole('tabpanel').getByRole('alert')).toContainText(
    'Choisissez un repère différent',
  );
  expect(attempts).toBe(1);
  await page.getByLabel('Repère pour Débit', { exact: true }).selectOption('3');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Appliquer aux mouvements' }).click();
  await expect.poll(() => adapted).toBe(true);
  await expect(page.getByRole('tab', { name: 'Mouvements' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
});

test('La validation bloquée explique la cause et ouvre la correction du mouvement', async ({
  page,
}) => {
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version: 1,
        validatedAt: null,
        statement: { ...statementExample, id: 'statement-1' },
        warnings: [],
        errors: [
          {
            code: 'MISSING_TRANSACTION_AMOUNT',
            message: 'Renseignez le montant vérifié de cette transaction.',
            transactionSequence: 1,
            sourceField: 'Amount',
          },
        ],
      },
    }),
  );
  await page.goto('/conversions?statementId=statement-1');
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByLabel('J’ai vérifié les données du relevé et les avertissements.').check();
  await expect(page.getByRole('button', { name: 'Valider le relevé', exact: true })).toBeDisabled();
  await expect(page.getByText('1 erreur(s) bloquent la validation')).toBeVisible();
  await page.setViewportSize({ width: 1500, height: 1000 });
  await page
    .getByRole('complementary', { name: 'Paramètres de l’export' })
    .screenshot({ path: '../output/conversion-export-guided.png' });
  await page.getByRole('button', { name: 'Voir les points à corriger' }).click();
  await expect(page.locator('.conversion-diagnostic-anchor')).toBeFocused();
  await page.getByRole('button', { name: 'Corriger', exact: true }).click();
  await expect(page.getByLabel('Date opération · ligne 1', { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('Raw OCR data stays available before adaptation and applying columns returns to review', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1600, height: 1080 });
  await importExample(page);
  let applied = false;
  await page.route('**/api/statement-imports/statement-1', async (route) => {
    await route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version: applied ? 2 : 1,
        validatedAt: null,
        warnings: [],
        errors: [],
        sourceFileName: 'scan.pdf',
        extraction: {
          method: 'PADDLE_OCR',
          pageCount: 1,
          lowConfidenceCount: 1,
          needsMapping: !applied,
        },
        statement: {
          ...statementExample,
          id: 'statement-1',
          transactions: applied ? statementExample.transactions : [],
        },
      },
    });
  });
  await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
    route.fulfill({
      json: {
        method: 'PADDLE_OCR',
        pages: [
          {
            number: 1,
            width: 595,
            height: 842,
            boxes: [
              {
                text: 'VIREMENT CLIENT',
                left: 100,
                right: 220,
                top: 280,
                bottom: 290,
                confidence: 0.85,
              },
              { text: '12500,00', left: 510, right: 555, top: 280, bottom: 290, confidence: 0.99 },
            ],
          },
        ],
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/adapt', async (route) => {
    expect(route.request().postDataJSON().mapping).toEqual({
      date: 8,
      valueDate: 58,
      description: 15.5,
      debit: 72.5,
      credit: 85.9,
      headerTop: 30,
    });
    expect(route.request().postDataJSON().statementVersion).toBe(1);
    applied = true;
    await route.fulfill({ json: {} });
  });
  // Trigger a fresh detail request with extraction metadata.
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Texte lu & adaptation' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Texte lu & adaptation' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.getByLabel('Texte extrait du PDF')).toContainText('VIREMENT CLIENT');
  await expect(page.getByLabel('Texte extrait du PDF')).toContainText('85 %');
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 500));
  await page.screenshot({ path: '../output/conversion-ocr-adaptation.png' });
  await page.getByText('Ajuster les positions manuellement', { exact: true }).click();
  await page.getByLabel('Colonne date valeur (%)', { exact: true }).fill('58');
  await page.getByLabel('Colonne libellé (%)', { exact: true }).fill('15.5');
  await page.getByLabel('Colonne débit (%)', { exact: true }).fill('72.5');
  await page.getByLabel('Colonne crédit (%)', { exact: true }).fill('85.9');
  await page.getByRole('button', { name: 'Appliquer aux mouvements' }).click();
  await expect.poll(() => applied).toBe(true);
});

test('VL tables, raw JSON and Markdown remain readable and downloadable before adapting cells', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1600, height: 1080 });
  await importExample(page);
  const providerPage = {
    prunedResult: {
      width: 1191,
      height: 1684,
      parsing_res_list: [
        { block_label: 'table', block_content: '<table><tr><td>TEST</td></tr></table>' },
      ],
    },
    markdown: { text: '# Relevé BMCE\nDate;Libellé;Débit;Crédit', images: {} },
  };
  const raw = {
    method: 'PADDLE_OCR_VL_1_6',
    pages: [
      {
        number: 1,
        width: 595,
        height: 842,
        boxes: [],
        markdown: providerPage.markdown.text,
        rawResult: providerPage,
        tables: [
          {
            rows: [
              ['Date', 'Date valeur', 'Opération', 'Débit', 'Crédit'],
              ['01/03/2026', '28/02/2026', 'VIR TEST\nOrigine : -999,99 MAD', '', '1 129,00'],
              ['', '', '<script>window.ocrInjected=true</script>', '', ''],
            ],
          },
        ],
      },
    ],
  };
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({
      json: {
        id: 'statement-1',
        version: 1,
        status: 'NEEDS_REVIEW',
        validatedAt: null,
        sourceFileName: 'bmce.pdf',
        warnings: [],
        errors: [],
        statement: { ...statementExample, id: 'statement-1' },
        extraction: {
          method: raw.method,
          pageCount: 1,
          lowConfidenceCount: 0,
          needsMapping: false,
        },
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
    route.fulfill({ json: raw }),
  );
  let applied = false;
  await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
    expect(route.request().postDataJSON().mapping.tableColumns).toEqual({
      date: 0,
      valueDate: 1,
      description: 2,
      debit: 3,
      credit: 4,
      startRow: 1,
    });
    applied = true;
    return route.fulfill({ json: {} });
  });
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION', exact: true }),
  ).toBeVisible();
  await page.getByRole('tab', { name: 'Texte lu & adaptation' }).click();
  await expect(page.getByLabel('Tableaux extraits par PaddleOCR')).toContainText(
    'Origine : -999,99 MAD',
  );
  expect(await page.evaluate(() => 'ocrInjected' in window)).toBe(false);
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 500));
  await page.screenshot({ path: '../output/conversion-paddle-vl.png' });
  await page.getByRole('button', { name: 'JSON', exact: true }).click();
  await expect(page.getByLabel('JSON PaddleOCR brut')).toContainText('parsing_res_list');
  const jsonDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Extraire le JSON' }).click();
  const file = await jsonDownload;
  expect(JSON.parse(await readFile((await file.path())!, 'utf8'))).toEqual({
    layoutParsingResults: [providerPage],
  });
  await page.getByRole('button', { name: 'Markdown', exact: true }).click();
  await expect(page.getByLabel('Markdown extrait')).toContainText('# Relevé BMCE');
  await page.getByText('Adapter les colonnes du tableau', { exact: true }).click();
  await page.getByRole('button', { name: 'Appliquer aux mouvements' }).click();
  await expect.poll(() => applied).toBe(true);
});

test('An empty OCR import can be adapted, reviewed and downloaded as a Sage X3 txt', async ({
  page,
}) => {
  await importExample(page);
  let recovered = false;
  await page.route('**/api/statement-imports/statement-1', (route) =>
    recovered
      ? route.fallback()
      : route.fulfill({
          json: {
            id: 'statement-1',
            version: 1,
            status: 'NEEDS_REVIEW',
            validatedAt: null,
            sourceFileName: 'scan.pdf',
            statement: {
              ...statementExample,
              id: 'statement-1',
              transactions: [],
              openingBalance: null,
              closingBalance: null,
            },
            errors: [
              {
                code: 'INVALID_TRANSACTIONS',
                message: 'Aucun mouvement adapté.',
                transactionSequence: null,
                fieldName: null,
                sourceField: null,
              },
            ],
            warnings: [],
            extraction: {
              method: 'PADDLE_OCR_VL_1_6',
              pageCount: 1,
              lowConfidenceCount: 0,
              needsMapping: true,
            },
          },
        }),
  );
  await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
    route.fulfill({
      json: {
        method: 'PADDLE_OCR_VL_1_6',
        pages: [
          {
            number: 1,
            width: 595,
            height: 842,
            boxes: [],
            tables: [],
            markdown: 'Relevé à adapter',
            rawResult: {},
          },
        ],
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
    expect(route.request().postDataJSON()).toEqual({ statementVersion: 1 });
    recovered = true;
    return route.fulfill({ json: {} });
  });
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'BANQUE DE DÉMONSTRATION', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Relancer l’adaptation automatique' }).click();
  await expect.poll(() => recovered).toBe(true);
  await expect(page.getByRole('tab', { name: 'Mouvements 2' })).toBeVisible();
  await page
    .getByRole('combobox', { name: 'Modèle d’exportation', exact: true })
    .selectOption(x3.id);
  await page
    .getByRole('checkbox', { name: 'J’ai vérifié les données du relevé et les avertissements.' })
    .check();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  const waiting = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Télécharger', exact: true }).click();
  const file = await waiting;
  expect(file.suggestedFilename()).toBe('DEMO_1234_2026-09_SAGEX3.txt');
  expect(await readFile((await file.path())!, 'utf8')).toBe(x3Content);
});

test('Import, review, preview, validate and download the exact file with audit history', async ({
  page,
}) => {
  await importExample(page);
  const download = page.getByRole('button', { name: 'Télécharger', exact: true });
  await expect(download).toBeDisabled();
  await page
    .getByRole('combobox', { name: 'Modèle d’exportation', exact: true })
    .selectOption(csv.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await expect(page.getByLabel('Aperçu du fichier exporté')).toContainText('12500,00');
  await expect(download).toBeDisabled();
  await page
    .getByRole('checkbox', { name: 'J’ai vérifié les données du relevé et les avertissements.' })
    .check();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  await expect(page.getByText('Relevé validé', { exact: true })).toBeVisible();
  await expect(download).toBeEnabled();
  const promise = page.waitForEvent('download');
  await download.click();
  const file = await promise;
  expect(file.suggestedFilename()).toBe('DEMO_1234_2026-09_CUSTOMCSV.csv');
  expect(await readFile((await file.path())!, 'utf8')).toBe(content);
  await page.getByRole('tab', { name: 'Historique' }).click();
  await expect(page.getByRole('tabpanel')).toContainText('DEMO_1234_2026-09_CUSTOMCSV.csv');
  await page.getByRole('tab', { name: 'Mouvements' }).click();
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 0));
  await page.screenshot({ path: '../output/conversion-desktop.png' });
});
test('ASCII diagnostics identify the transaction and block the download', async ({ page }) => {
  await importExample(page);
  await page
    .getByRole('combobox', { name: 'Modèle d’exportation', exact: true })
    .selectOption(ascii.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  await expect(page.getByText('ENCODING_ERROR', { exact: true })).toBeVisible();
  await expect(page.getByText('Transaction 1 · Libellé', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Corriger', exact: true }).click();
  await expect(page.getByLabel('Date opération · ligne 1', { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
});
test('Sage X3 preview identifies both balances and downloads nine balance fields and ten movement fields with continuous numbering', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1720, height: 1080 });
  await importExample(page);
  await page
    .getByRole('combobox', { name: 'Modèle d’exportation', exact: true })
    .selectOption(x3.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  const table = page.getByLabel('Aperçu du fichier exporté');
  await expect(table.getByRole('row')).toHaveCount(5);
  await expect(table.getByRole('cell', { name: 'Solde initial', exact: true })).toBeVisible();
  await expect(table.getByRole('cell', { name: 'Solde final', exact: true })).toBeVisible();
  await expect(
    table.getByRole('columnheader', { name: 'Numéro interne', exact: true }),
  ).toBeVisible();
  await expect(table).toContainText('RBDEMO092026');
  await expect(table.getByText('Non exporté', { exact: true })).toHaveCount(2);
  await expect(table.getByRole('row').nth(1).getByRole('cell').nth(4)).toHaveText('1000.00');
  await page
    .getByRole('checkbox', { name: 'J’ai vérifié les données du relevé et les avertissements.' })
    .check();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  await expect(page.getByText('Relevé validé', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  const promise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Télécharger', exact: true }).click();
  const file = await promise;
  expect(file.suggestedFilename()).toBe('DEMO_1234_2026-09_SAGEX3.txt');
  expect(await readFile((await file.path())!, 'utf8')).toBe(x3Content);
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 0));
  await page.screenshot({ path: '../output/conversion-sage-x3.png' });
});
test('Corrections preserve decimal text, invalidate the preview and return to review', async ({
  page,
}) => {
  await importExample(page);
  await page
    .getByRole('checkbox', { name: 'J’ai vérifié les données du relevé et les avertissements.' })
    .check();
  await page.getByRole('button', { name: 'Valider le relevé', exact: true }).click();
  await page.getByRole('button', { name: 'Corriger les informations' }).click();
  await page.getByLabel('Solde initial', { exact: true }).fill('12345678901234567890.12345678');
  await page.getByText('Dates et libellés des soldes · Sage X3', { exact: true }).click();
  await page.getByLabel('Date du solde initial', { exact: true }).fill('2026-09-01');
  await page.getByLabel('Date valeur du solde initial', { exact: true }).fill('2026-08-31');
  await page.getByLabel('Libellé du solde initial', { exact: true }).fill('SOLDE LU DANS LE PDF');
  const changed = page.waitForRequest(
    (r) => r.method() === 'PUT' && r.url().includes('/statement-imports/statement-1'),
  );
  await page
    .getByRole('button', { name: 'Enregistrer les corrections', exact: true })
    .first()
    .click();
  const input = (await changed).postDataJSON();
  expect(input.openingBalance).toBe('12345678901234567890.12345678');
  expect(input.openingBalanceDate).toBe('2026-09-01');
  expect(input.openingBalanceValueDate).toBe('2026-08-31');
  expect(input.openingBalanceDescription).toBe('SOLDE LU DANS LE PDF');
  await expect(page.getByRole('button', { name: 'Valider le relevé', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeDisabled();
});
test('Mobile layout has no page overflow and all export controls remain reachable', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await importExample(page);
  await page
    .getByRole('combobox', { name: 'Modèle d’exportation', exact: true })
    .selectOption(csv.id);
  await page.getByRole('button', { name: 'Aperçu', exact: true }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await expect(page.getByRole('button', { name: 'Télécharger', exact: true })).toBeVisible();
  await page.screenshot({ path: '../output/conversion-mobile-export.png' });
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 0));
  await page.screenshot({ path: '../output/conversion-mobile.png' });
});

test('Wide desktop and dark mode preserve a readable workspace and keyboard tabs', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1720, height: 1080 });
  await page.addInitScript(() => localStorage.setItem('bank-converter-theme', 'dark'));
  await importExample(page);
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  const movements = page.getByRole('tab', { name: 'Mouvements' });
  await movements.focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Aperçu du fichier' })).toBeFocused();
  await expect(page.getByRole('tab', { name: 'Aperçu du fichier' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await page.keyboard.press('End');
  await expect(page.getByRole('tab', { name: 'Historique' })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.keyboard.press('Home');
  await expect(movements).toBeFocused();
  expect(await page.locator('.app-scroll').evaluate((el) => el.scrollLeft)).toBe(0);
  await page.evaluate(() => document.querySelector('.app-scroll')?.scrollTo(0, 0));
  await page.screenshot({ path: '../output/conversion-dark.png', animations: 'disabled' });
});

test('Guided conversion downloads and completes the statement before opening client workspace', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1366, height: 768 });
  await importToModel(page);
  await page.route('**/api/statement-imports/statement-1/download-complete', (route) =>
    route.fulfill({ json: { status: 'VALIDATED', version: 2 } }),
  );
  const choose = page.getByRole('region', { name: 'Choix du modèle d’exportation' });
  await expect(choose.getByRole('button', { name: 'Afficher le résultat' })).toBeDisabled();
  await choose.getByLabel('Modèle d’exportation', { exact: true }).selectOption(x3.id);
  await expect(choose.getByLabel('Colonnes du modèle')).toContainText('Date valeur');
  await expect(choose.getByRole('button', { name: 'Afficher le résultat' })).toBeInViewport();
  await page.screenshot({ path: '../output/screenshots/model-step-desktop.png' });
  const preparation = page.waitForRequest((req) => req.url().endsWith('/prepare'));
  await choose.getByRole('button', { name: 'Afficher le résultat' }).click();
  expect((await preparation).postDataJSON()).toEqual({ statementVersion: 1, templateId: x3.id });
  await expect(page.getByRole('tab', { name: 'Aperçu du fichier' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.locator('table.preview')).toContainText('VIREMENT CLIENT');
  await page.screenshot({ path: '../output/screenshots/guided-result-desktop.png' });
  await expect(page.getByRole('button', { name: 'Valider le relevé', exact: true })).toHaveCount(0);
  await expect(
    page
      .locator('.export-preview-switch')
      .getByRole('button', { name: 'Télécharger', exact: true }),
  ).toBeEnabled();
  const waiting = page.waitForEvent('download');
  await page
    .locator('.export-preview-switch')
    .getByRole('button', { name: 'Télécharger', exact: true })
    .click();
  const downloaded = await waiting;
  expect(await readFile((await downloaded.path())!, 'utf8')).toBe(x3Content);
  await expect(page).toHaveURL(/client-workspace/);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    .toBe(true);
  await page.screenshot({ path: '../output/screenshots/guided-download-mobile.png' });
});

test('Guided conversion allows download despite missing model fields', async ({ page }) => {
  await importToModel(page);
  await page.route('**/api/statement-imports/statement-1/prepare', async (route) => {
    const prepared = {
      detail: {
        id: 'statement-1',
        version: 1,
        status: 'NEEDS_REVIEW',
        validatedAt: null,
        sourceFileName: 'releve.pdf',
        statement: { ...statementExample, id: 'statement-1' },
        warnings: [],
        errors: [],
      },
      preview: {
        template: { id: csv.id, name: csv.name },
        columns: ['Date opération', 'Libellé', 'Montant'],
        rows: [['01/09/2026', 'VIREMENT CLIENT', '12500,00']],
        rowSequences: [1],
        totalRows: 1,
        isValidated: false,
        statementVersion: 1,
        templateFingerprint: 'missing-required',
        warnings: [],
        errors: [] as unknown[],
      },
    };
    prepared.preview.errors = [
      {
        code: 'REQUIRED_VALUE_MISSING',
        message: 'Date opération absente dans le relevé.',
        transactionSequence: 1,
        fieldName: 'Date opération',
        sourceField: 'Date',
      },
    ];
    prepared.preview.rows[0][0] = '';
    await route.fulfill({ json: prepared });
  });
  await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
  await page.getByRole('button', { name: 'Afficher le résultat' }).click();
  await expect(
    page.getByText('Date opération absente dans le relevé.', { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Valider le relevé', exact: true })).toHaveCount(0);
  await expect(
    page
      .locator('.export-preview-switch')
      .getByRole('button', { name: 'Télécharger', exact: true }),
  ).toBeEnabled();
  await page.getByRole('button', { name: 'Changer de modèle', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Choix du modèle d’exportation' })).toBeVisible();
});

function reusableLayout(renamed = false) {
  return {
    method: 'PADDLE_OCR_VL_1_6',
    pages: [
      {
        number: 1,
        width: 600,
        height: 800,
        boxes: [],
        tables: [
          {
            rows: [
              [renamed ? 'Référence' : 'Date', 'Valeur', 'Libellé', 'Débit', 'Crédit'],
              ['01/09/2026', '01/09/2026', 'VIREMENT CLIENT', '', '12500,00'],
            ],
          },
        ],
      },
    ],
  };
}
const reusableMapping = {
  date: 8,
  valueDate: 20,
  description: 32,
  debit: 75,
  credit: 88,
  headerTop: 30,
  tableColumns: { date: 0, valueDate: 1, description: 2, debit: 3, credit: 4, startRow: 1 },
};

for (const same of [true, false]) {
  test(
    same
      ? 'Application reuses a validated layout without adding bank-specific server rules'
      : 'Application rejects a saved mapping for a different layout',
    async ({ page }) => {
      await importToModel(page);
      let adapted = false;
      const fresh = () => ({
        id: 'statement-1',
        version: adapted ? 2 : 1,
        status: 'NEEDS_REVIEW',
        validatedAt: null,
        sourceFileName: 'releve.pdf',
        extraction: {
          method: 'PADDLE_OCR_VL_1_6',
          pageCount: 1,
          lowConfidenceCount: 0,
          needsMapping: false,
        },
        errors: [],
        warnings: [],
        statement: {
          ...statementExample,
          id: 'statement-1',
          transactions: statementExample.transactions.map((row, index) => ({
            ...row,
            id: 'transaction-' + (index + 1),
            date: adapted ? row.date : null,
          })),
        },
      });
      await page.route('**/api/statement-imports/statement-1', (route) =>
        route.fulfill({ json: fresh() }),
      );
      await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
        route.fulfill({ json: reusableLayout(!same) }),
      );
      await page.route('**/api/statement-imports?**', (route) =>
        route.fulfill({
          json: {
            items: [
              {
                id: 'validated-layout',
                bankName: statementExample.bankName,
                currency: statementExample.currency,
                status: 'VALIDATED',
              },
            ],
            total: 1,
            page: 1,
            pageSize: 100,
          },
        }),
      );
      await page.route('**/api/statement-imports/validated-layout/extraction', (route) =>
        route.fulfill({ json: { ...reusableLayout(), lastMapping: reusableMapping } }),
      );
      await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
        expect(same).toBe(true);
        expect(route.request().postDataJSON()).toEqual({
          statementVersion: 1,
          mapping: reusableMapping,
        });
        adapted = true;
        return route.fulfill({ json: fresh() });
      });
      await page.route('**/api/statement-imports/statement-1/prepare', (route) => {
        expect(route.request().postDataJSON()).toEqual({
          statementVersion: same ? 2 : 1,
          templateId: csv.id,
        });
        return route.fulfill({
          json: {
            detail: fresh(),
            preview: {
              template: { id: csv.id, name: csv.name },
              columns: ['Date', 'Libellé', 'Montant'],
              rows: [[same ? '01/09/2026' : '', 'VIREMENT CLIENT', '12500,00']],
              rowSequences: [1],
              totalRows: 1,
              isValidated: false,
              statementVersion: same ? 2 : 1,
              templateFingerprint: 'reusable-profile',
              warnings: [],
              errors: same
                ? []
                : [
                    {
                      code: 'REQUIRED_VALUE_MISSING',
                      message: 'Date à compléter',
                      transactionSequence: 1,
                      fieldName: 'Date',
                      sourceField: 'Date',
                    },
                  ],
            },
          },
        });
      });
      await page.reload();
      await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
      await page.getByRole('button', { name: 'Afficher le résultat' }).click();
      await expect(page.getByRole('tab', { name: 'Aperçu du fichier' })).toHaveAttribute(
        'aria-selected',
        'true',
      );
      expect(adapted).toBe(same);
      if (same) await expect(page.getByText(/Le réglage d’un relevé validé/)).toBeVisible();
      else await expect(page.getByText('Date à compléter', { exact: true })).toBeVisible();
      await expect(
        page.getByRole('button', { name: 'Continuer vers le téléchargement' }),
      ).toBeDisabled();
    },
  );
}

test('Extraction models can be saved with custom synonyms and applied from the app', async ({
  page,
}) => {
  await importExample(page);
  let saved: Record<string, unknown> | null = null;
  let applied = false;
  await page.route('**/api/extraction-profiles', async (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      expect(body.aliases.Date).toEqual(['Jour bancaire']);
      expect(body.aliases.OpeningBalance).toEqual(['Disponible début']);
      saved = {
        id: 'profile-1',
        name: body.name,
        bankCode: 'DEMO',
        currency: 'MAD',
        mappingJson: JSON.stringify(body.mapping),
        aliasesJson: JSON.stringify(body.aliases),
        sampleJson: '{}',
        isActive: true,
        version: 1,
        createdByName: 'Admin',
        modifiedByName: 'Admin',
        updatedAt: '2026-10-10T12:00:00Z',
      };
      return route.fulfill({ json: saved });
    }
    return route.fulfill({ json: saved ? [saved] : [] });
  });
  await page.route('**/api/extraction-profiles/history*', (route) =>
    route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 100 } }),
  );
  await page.route('**/api/statement-imports/statement-1', (route) =>
    route.fulfill({
      json: {
        id: 'statement-1',
        status: 'NEEDS_REVIEW',
        version: applied ? 2 : 1,
        validatedAt: null,
        warnings: [],
        errors: [],
        sourceFileName: 'scan.pdf',
        statement: { ...statementExample, id: 'statement-1' },
        extraction: {
          method: 'PADDLE_OCR',
          pageCount: 1,
          lowConfidenceCount: 0,
          needsMapping: false,
        },
      },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
    route.fulfill({
      json: { method: 'PADDLE_OCR', pages: [{ number: 1, width: 600, height: 800, boxes: [] }] },
    }),
  );
  await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
    expect(route.request().postDataJSON()).toEqual({ statementVersion: 1, profileId: 'profile-1' });
    applied = true;
    return route.fulfill({ json: {} });
  });
  await page.reload();
  await page.getByRole('tab', { name: 'Texte lu & adaptation' }).click();
  await page.getByLabel('Nom du modèle', { exact: true }).fill('BMCE format compact');
  await page.getByText('Synonymes personnalisés', { exact: true }).click();
  await page.getByLabel('Synonymes Date d’opération', { exact: true }).fill('Jour bancaire');
  await page.getByLabel('Synonymes Solde initial', { exact: true }).fill('Disponible début');
  await page.getByRole('button', { name: 'Enregistrer le modèle', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Modèle enregistré');
  await page.getByRole('button', { name: 'Appliquer au relevé', exact: true }).click();
  await expect.poll(() => applied).toBe(true);
});

for (const compatible of [true, false]) {
  test(`Persistent extraction profile is ${compatible ? 'reused' : 'rejected for another layout'} automatically`, async ({
    page,
  }) => {
    await importToModel(page);
    let adapted = false;
    const fresh = () => ({
      id: 'statement-1',
      version: adapted ? 2 : 1,
      status: 'NEEDS_REVIEW',
      validatedAt: null,
      sourceFileName: 'releve.pdf',
      extraction: {
        method: 'PADDLE_OCR_VL_1_6',
        pageCount: 1,
        lowConfidenceCount: 0,
        needsMapping: false,
      },
      errors: [],
      warnings: [],
      statement: { ...statementExample, id: 'statement-1' },
    });
    await page.route('**/api/statement-imports/statement-1', (route) =>
      route.fulfill({ json: fresh() }),
    );
    await page.route('**/api/statement-imports/statement-1/extraction', (route) =>
      route.fulfill({ json: reusableLayout(!compatible) }),
    );
    await page.route('**/api/extraction-profiles', (route) =>
      route.fulfill({
        json: [
          {
            id: 'persistent-profile',
            bankCode: 'DEMO',
            currency: 'MAD',
            isActive: true,
            sampleJson: JSON.stringify({ ...reusableLayout(), lastMapping: reusableMapping }),
          },
        ],
      }),
    );
    await page.route('**/api/statement-imports/statement-1/adapt', (route) => {
      expect(compatible).toBe(true);
      expect(route.request().postDataJSON()).toEqual({
        statementVersion: 1,
        profileId: 'persistent-profile',
      });
      adapted = true;
      return route.fulfill({ json: fresh() });
    });
    await page.route('**/api/statement-imports/statement-1/prepare', (route) => {
      expect(route.request().postDataJSON().statementVersion).toBe(compatible ? 2 : 1);
      return route.fulfill({
        json: {
          detail: fresh(),
          preview: {
            template: { id: csv.id, name: csv.name },
            columns: ['Date', 'Libellé', 'Montant'],
            rows: [['01/09/2026', 'VIREMENT CLIENT', '12500,00']],
            rowSequences: [1],
            totalRows: 1,
            isValidated: false,
            statementVersion: compatible ? 2 : 1,
            templateFingerprint: 'persistent-profile',
            warnings: [],
            errors: [],
          },
        },
      });
    });
    await page.reload();
    await page.getByLabel('Modèle d’exportation', { exact: true }).selectOption(csv.id);
    await page.getByRole('button', { name: 'Afficher le résultat' }).click();
    await expect(page.locator('table.preview')).toContainText('VIREMENT CLIENT');
    expect(adapted).toBe(compatible);
  });
}
