import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:http';
import { mkdtemp, mkdir, copyFile, readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { createRequire } from 'node:module';

const root = resolve(import.meta.dirname, '..');
const require = createRequire(join(root, 'frontend/package.json'));

test('Linux compose isolates PostgreSQL and routes the selected backend port', async () => {
  const compose = require('js-yaml').load(await readFile(join(root, 'installer/compose.linux.yml'), 'utf8'));
  assert.equal(compose.services.db.ports, undefined);
  assert.equal(compose.services.backend.ports[0], '127.0.0.1:${BACKEND_PORT:-5081}:${BACKEND_PORT:-5081}');
  assert.equal(compose.services.frontend.build.args.API_PROXY_URL, 'http://backend:${BACKEND_PORT:-5081}');
  for (const service of Object.values(compose.services)) assert.equal(service.restart, 'unless-stopped');
  assert.equal(compose.services.backend.environment.Ocr__Provider, 'AI_STUDIO');
});

test('installation failures retain a diagnostic without credentials', async () => {
  const work = await mkdtemp(join(root, 'output/installation-log-test-'));
  const directory = join(work, 'installer');
  await mkdir(directory);
  await copyFile(join(root, 'installer/Run-Installation.ps1'), join(directory, 'Run-Installation.ps1'));
  await writeFile(join(directory, 'Configure-Server.ps1'), "param($AppRoot,$SettingsFile)\nWrite-Output 'Host=localhost;Password=synthetic-private-value;Database=alias'\nthrow \"PASSWORD 'synthetic-sql-private-value'\"\n");
  const result = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', join(directory, 'Run-Installation.ps1'), '-AppRoot', work, '-SettingsFile', join(work, 'unused.json')]);
  assert.equal(result.status, 1);
  const log = await readFile(join(directory, 'installation.log'), 'utf8');
  assert.ok(log.includes('Password=***'));
  assert.ok(log.includes("PASSWORD '***'"));
  assert.ok(!log.includes('synthetic-private-value'));
  assert.ok(!log.includes('synthetic-sql-private-value'));
});

test('Linux installation creates private configuration and reuses it without resetting the database', async () => {
  const work = await mkdtemp(join(root, 'output/linux-test-'));
  const bin = join(work, 'bin');
  await mkdir(bin);
  await copyFile(join(root, 'installer/install-linux.sh'), join(work, 'install-linux.sh'));
  await writeFile(join(bin, 'docker'), '#!/usr/bin/env bash\nprintf "%s\\n" "$*" >> "$MOCK_LOG"\n');
  await writeFile(join(bin, 'curl'), '#!/usr/bin/env bash\nprintf \'{"setupRequired":true}\\n\'\n');
  await writeFile(join(bin, 'ss'), '#!/usr/bin/env bash\nexit 0\n');
  const bash = 'C:/Program Files/Git/bin/bash.exe';
  const windowsToBash = (path) => path.replaceAll('\\', '/').replace(/^([A-Za-z]):/, (_, drive) => `/${drive.toLowerCase()}`);
  const log = join(work, 'commands.log');
  const execute = (input) => spawnSync(bash, ['-c', 'chmod +x "$MOCK_BIN"/*; export PATH="$MOCK_BIN:$PATH"; bash install-linux.sh'], {
    cwd: work, input, encoding: 'utf8', env: { ...process.env, MOCK_BIN: windowsToBash(bin), MOCK_LOG: windowsToBash(log) },
  });
  const invalid = execute('3102\n3102\nserver\n');
  assert.notEqual(invalid.status, 0, 'reject duplicate ports before creating configuration');
  const first = execute('3102\n5102\nserver\n');
  assert.equal(first.status, 0, first.stderr);
  const environment = await readFile(join(work, '.env'), 'utf8');
  assert.match(environment, /DB_PASSWORD=[a-f0-9]{64}/);
  assert.match(environment, /FRONTEND_PORT=3102/);
  const second = execute('');
  assert.equal(second.status, 0, second.stderr);
  assert.equal(await readFile(join(work, '.env'), 'utf8'), environment);
  assert.match(await readFile(join(work, 'Acces-Alias.url'), 'utf8'), /http:\/\/server:3102/);
  const commands = await readFile(log, 'utf8');
  assert.ok(commands.includes('up -d --build'));
  assert.ok(!commands.includes('down -v'));
});

test('Windows configuration rejects invalid ports and supports a published frontend on custom ports', { timeout: 90000 }, async () => {
  const work = await mkdtemp(join(root, 'output/server-test-'));
  const app = join(work, 'app');
  const data = join(work, 'data');
  await mkdir(app, { recursive: true });
  const copy = spawnSync('robocopy.exe', [join(root, 'output/runtime/frontend'), join(app, 'frontend'), '/E', '/XD', 'cache', 'route-cache', '/NFL', '/NDL', '/NJH', '/NJS', '/NP']);
  assert.ok(copy.status < 8, 'copy standalone frontend');
  await copyFile(join(root, 'scripts/Alias.Runtime.json.example'), join(app, 'Alias.Runtime.json.example'));
  const backend = createServer(async (request, response) => {
    let size = 0;
    for await (const chunk of request) size += chunk.length;
    response.setHeader('Content-Type', 'application/json');
    response.setHeader('Set-Cookie', 'Alias.Test=ok; HttpOnly; SameSite=Strict');
    response.end(JSON.stringify({ setupRequired: true, path: request.url, cookie: request.headers.cookie, size }));
  });
  await new Promise((done) => backend.listen(0, '127.0.0.1', done));
  const back = backend.address().port;
  const reserve = createServer();
  await new Promise((done) => reserve.listen(0, '127.0.0.1', done));
  const front = reserve.address().port;
  await new Promise((done) => reserve.close(done));
  const settings = join(work, 'input.json');
  const config = { FrontendPort: front, BackendPort: back, PublicUrl: `http://localhost:${front}`, DataDirectory: data, ConnectionString: 'Host=invalid.test;Password=synthetic-test-secret' };
  const configure = () => spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', join(root, 'installer/Configure-Server.ps1'), '-AppRoot', app, '-SettingsFile', settings, '-ConfigureOnly'], { encoding: 'utf8' });
  let child;
  try {
    await writeFile(settings, JSON.stringify({ ...config, BackendPort: front }));
    assert.notEqual(configure().status, 0, 'reject duplicate ports');
    await writeFile(settings, JSON.stringify(config));
    const configured = configure();
    assert.equal(configured.status, 0, configured.stderr);
    const publicSettings = await readFile(join(data, 'Server.json'), 'utf8');
    assert.ok(!publicSettings.includes('synthetic-test-secret'));
    const xml = await readFile(join(app, 'services/AliasBackend.xml'), 'utf8');
    assert.ok(xml.includes(`http://127.0.0.1:${back}`));
    assert.ok(xml.includes('<domain>NT AUTHORITY</domain><user>NetworkService</user>'));
    assert.ok(!xml.includes('<username>'));
    assert.ok(!xml.includes('synthetic-test-secret'));
    const link = await readFile(join(app, 'Acces-Alias.url'), 'utf8');
    assert.ok(link.includes(`URL=http://localhost:${front}`));
    child = spawn(process.execPath, [join(app, 'frontend/server.js')], {
      cwd: join(app, 'frontend'), env: { ...process.env, HOSTNAME: '127.0.0.1', PORT: String(front), NODE_ENV: 'production' }, stdio: 'ignore', windowsHide: true,
    });
    let response;
    for (let attempt = 0; attempt < 60; attempt++) {
      try { response = await fetch(`http://127.0.0.1:${front}/api/auth/session`, { signal: AbortSignal.timeout(2000) }); if (response.ok) break; } catch {}
      await new Promise((done) => setTimeout(done, 500));
    }
    assert.ok(response?.ok, 'published frontend available');
    assert.equal((await response.json()).setupRequired, true);
    const upload = await fetch(`http://127.0.0.1:${front}/api/test-upload`, {
      method: 'POST', headers: { Cookie: 'Alias.Session=synthetic-session' }, body: Buffer.alloc(2 * 1024 * 1024, 42), signal: AbortSignal.timeout(20000),
    });
    assert.equal(upload.status, 200);
    assert.match(upload.headers.get('set-cookie'), /Alias.Test=ok/);
    const result = await upload.json();
    assert.equal(result.path, '/api/test-upload');
    assert.equal(result.cookie, 'Alias.Session=synthetic-session');
    assert.equal(result.size, 2 * 1024 * 1024);
  } finally {
    if (child) { child.kill(); await new Promise((done) => child.once('exit', done)); }
    await new Promise((done) => backend.close(done));
  }
});
