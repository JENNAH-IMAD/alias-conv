import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve, join } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const require = createRequire(join(root, 'frontend/package.json'));

test('UUIDs work in a real HTTP browser without crypto.randomUUID', async () => {
  const ts = require('typescript');
  const { chromium } = require('@playwright/test');
  const source = await readFile(join(root, 'frontend/src/lib/create-uuid.ts'), 'utf8');
  const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
  const server = createServer((request, response) => {
    response.setHeader('Content-Type', 'text/html');
    response.end(`<script>var exports = {}; ${code}</script>`);
  });
  await new Promise((done) => server.listen(0, '127.0.0.1', done));
  let browser;
  try {
    browser = await chromium.launch({ channel: 'msedge', args: ['--host-resolver-rules=MAP alias-http.test 127.0.0.1', '--no-proxy-server'] });
    const page = await browser.newPage();
    await page.goto(`http://alias-http.test:${server.address().port}`);
    const result = await page.evaluate(() => ({ secure: isSecureContext, native: typeof crypto.randomUUID, ids: Array.from({ length: 100 }, () => exports.createUuid()) }));
    assert.equal(result.secure, false);
    assert.equal(result.native, 'undefined');
    assert.equal(new Set(result.ids).size, 100);
    for (const id of result.ids) assert.match(id, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  } finally {
    if (browser) await browser.close();
    await new Promise((done) => server.close(done));
  }
});
