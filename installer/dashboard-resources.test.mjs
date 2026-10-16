import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { createServer } from 'node:net';
import { spawn } from 'node:child_process';
import { readFile, mkdir } from 'node:fs/promises';
import { resolve, join } from 'node:path';
const root = resolve(import.meta.dirname, '..');
const require = createRequire(join(root, 'frontend/package.json'));

test('dashboard prompt and real downloads work over HTTP, including mobile', { timeout: 90000 }, async () => {
  const { chromium } = require('@playwright/test');
  const reserve = createServer();
  await new Promise((done) => reserve.listen(0, '127.0.0.1', done));
  const port = reserve.address().port;
  await new Promise((done) => reserve.close(done));
  const directory = join(root, 'output/runtime/frontend');
  const child = spawn(process.execPath, [join(directory, 'server.js')], { cwd: directory, env: { ...process.env, PORT: String(port), HOSTNAME: '127.0.0.1', NODE_ENV: 'production' }, windowsHide: true, stdio: 'ignore' });
  let browser;
  try {
    for (let attempt = 0; attempt < 60; attempt++) {
      try { if ((await fetch(`http://127.0.0.1:${port}/dashboard`)).ok) break; } catch {}
      await new Promise((done) => setTimeout(done, 500));
    }
    browser = await chromium.launch({ channel: 'msedge', args: ['--host-resolver-rules=MAP alias-dashboard.test 127.0.0.1', '--no-proxy-server'] });
    const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
    page.on('pageerror', (error) => process.stderr.write(`Browser: ${error.message}\n`));
    await page.route('**/api/auth/session', (route) => route.fulfill({ json: { setupRequired: false, user: { id: 'test', name: 'Test', role: 'Admin', permissions: [], isActive: true, email: 'test@example.test', createdAt: '2026-01-01', lastLoginAt: null, version: 1 } } }));
    await page.route('**/api/dashboard', (route) => route.fulfill({ json: { clients: 0, banks: 0, accounts: 0, statements: 0, exports: 0, unassigned: 0, pendingReview: 0, statuses: [], activity: [], recent: [], updatedAt: '2026-01-01' } }));
    await page.goto(`http://alias-dashboard.test:${port}/dashboard`);
    const card = page.getByRole('region', { name: 'Prompt relevé', exact: true });
    try { await card.waitFor({ timeout: 10000 }); } catch (error) { process.stderr.write((await page.locator('body').innerText()).slice(0, 1400)); throw error; }
    assert.equal(await page.evaluate(() => isSecureContext), false);
    await card.getByRole('button', { name: 'Copier le prompt' }).click();
    await card.getByRole('textbox', { name: 'Prompt de conversion pour ChatGPT' }).waitFor();
    const prompt = await card.getByRole('textbox', { name: 'Prompt de conversion pour ChatGPT' }).inputValue();
    assert.ok(prompt.includes('Date opération | Date valeur | Libellé | Débit | Crédit'));
    assert.ok(prompt.includes('Ne présente jamais une donnée incertaine comme entièrement validée.'));
    const chatGptLink = card.getByRole('link', { name: 'Ouvrir ChatGPT avec le prompt' });
    const chatGptUrl = new URL(await chatGptLink.getAttribute('href'));
    assert.equal(chatGptUrl.origin, 'https://chatgpt.com');
    assert.equal(chatGptUrl.searchParams.get('q'), prompt);
    assert.equal(await chatGptLink.getAttribute('target'), '_blank');
    for (const [label, filename] of [['PDF · 5 colonnes', 'modele-releve-alias.pdf'], ['Modèle EMA', 'Relever.ema']]) {
      const pending = page.waitForEvent('download');
      await page.locator('.overview-header').getByRole('link', { name: label }).click();
      const download = await pending;
      assert.equal(download.suggestedFilename(), filename);
      const received = await readFile(await download.path());
      assert.deepEqual(received, await readFile(join(root, 'frontend/public/resources', filename)));
    }
    await mkdir(join(root, 'output/dashboard-resources-preview'), { recursive: true });
    await card.screenshot({ path: join(root, 'output/dashboard-resources-preview/desktop.png') });
    await page.setViewportSize({ width: 390, height: 844 });
    await card.scrollIntoViewIfNeeded();
    const box = await card.boundingBox();
    assert.ok(box.width <= 390, 'resource card fits a mobile viewport');
    await card.screenshot({ path: join(root, 'output/dashboard-resources-preview/mobile.png') });
  } finally {
    if (browser) await browser.close();
    child.kill();
    if (child.exitCode === null) await new Promise((done) => child.once('exit', done));
  }
});
