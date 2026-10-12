import { cp, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
const require = createRequire(import.meta.url);
const root = dirname(require.resolve('pdfjs-dist/package.json'));
const destination = new URL('../public/pdfjs/', import.meta.url);
await mkdir(destination, { recursive: true });
await cp(join(root, 'build/pdf.worker.min.mjs'), new URL('pdf.worker.min.mjs', destination));
for (const folder of ['cmaps', 'standard_fonts', 'wasm']) {
  await cp(join(root, folder), new URL(`${folder}/`, destination), { recursive: true });
}
