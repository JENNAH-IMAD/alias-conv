import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';

const source = await readFile(new URL('../src/lib/save-export.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 },
}).outputText;
const { saveReadyExportBlob } = await import(
  `data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`
);
let pickerCalls = 0,
  writes = 0,
  clicked = 0,
  written;
globalThis.window = {
  isSecureContext: true,
  setTimeout: () => {},
  showSaveFilePicker: async () => {
    pickerCalls++;
    return {
      name: 'ecritures.txt',
      createWritable: async () => ({
        write: async (blob) => {
          writes++;
          written = await blob.text();
        },
        close: async () => {},
        abort: async () => {},
      }),
    };
  },
};
globalThis.document = {
  createElement: () => ({
    click: () => {
      clicked++;
    },
    remove: () => {},
  }),
  body: { appendChild: () => {} },
};
await assert.rejects(() => saveReadyExportBlob(new Blob([]), 'empty.txt', 'txt'), /vide/);
assert.equal(pickerCalls, 0);
assert.equal(writes, 0);
assert.equal(clicked, 0);
console.log('PASS ReadyExport_RejectEmptyBeforeCreatingFile');
await saveReadyExportBlob(
  new Blob(['512000;CDM;CHEQUE;Banque;100,00\r\n']),
  'ecritures.txt',
  'txt',
);
assert.equal(pickerCalls, 1);
assert.equal(writes, 1);
assert.equal(written, '512000;CDM;CHEQUE;Banque;100,00\r\n');
console.log('PASS ReadyExport_WritesGeneratedNonemptyContent');
window.showSaveFilePicker = async () => {
  throw new DOMException('User activation expired', 'SecurityError');
};
await saveReadyExportBlob(new Blob(['complete']), 'ecritures.txt', 'txt');
assert.equal(clicked, 1);
assert.equal(writes, 1);
console.log('PASS ReadyExport_ExpiredActivationFallsBackToDownload');
window.showSaveFilePicker = async () => {
  throw new DOMException('Cancelled', 'AbortError');
};
await assert.rejects(() => saveReadyExportBlob(new Blob(['complete']), 'ecritures.txt', 'txt'), {
  name: 'AbortError',
});
assert.equal(clicked, 1);
assert.equal(writes, 1);
console.log('PASS ReadyExport_CancelDoesNotWriteFile');
