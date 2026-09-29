import { cpSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const standalone = path.join(root, '.next', 'standalone');
const server = path.join(standalone, 'server.js');
if (!existsSync(server)) {
  console.error('Lancez npm run build avant npm start.');
  process.exit(1);
}

// The standalone build omits these assets; copy them for local serving.
cpSync(path.join(root, 'public'), path.join(standalone, 'public'), { recursive: true });
cpSync(path.join(root, '.next', 'static'), path.join(standalone, '.next', 'static'), { recursive: true });
const child = spawn(process.execPath, [server], {
  cwd: root,
  stdio: 'inherit',
  env: { ...process.env, NODE_ENV: 'production', PORT: process.env.PORT || '3002', HOSTNAME: 'localhost' },
});
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => child.kill(signal));
}
child.on('error', (error) => { console.error(error.message); process.exitCode = 1; });
child.on('exit', (code) => { process.exitCode = code ?? 1; });
