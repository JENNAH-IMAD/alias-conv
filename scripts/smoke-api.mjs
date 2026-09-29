// Test the actual frontend HTTP client against a running local API, without a browser.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { request, all } from '../frontend/Import-pdf-excel-master/src/services/api.ts';

if (!process.env.SMOKE_EMAIL || !process.env.SMOKE_PASSWORD) throw new Error('Set SMOKE_EMAIL and SMOKE_PASSWORD for a seeded demo account.');
const values=new Map();
globalThis.window=new EventTarget();
globalThis.sessionStorage={getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,value)};
const session=await request('/auth/login',{method:'POST',body:JSON.stringify({email:process.env.SMOKE_EMAIL,password:process.env.SMOKE_PASSWORD})});
sessionStorage.setItem('bank-converter-session',JSON.stringify(session));
const [clients,accounts,profiles,exports]=await Promise.all([all('/clients'),all('/bank-accounts'),all('/bank-statement-templates'),all('/export-templates')]);
const client=clients.find(c=>c.name==='Client fictif'); assert.ok(client,'Enable Seed__DemoData');
const account=accounts.find(a=>a.clientId===client.id);assert.ok(account);
const profile=profiles.find(p=>p.bankId===account.bankId);assert.ok(profile);
const template=exports.find(e=>e.type==='CUSTOMCSV');assert.ok(template);
const file=await fs.readFile(new URL('../backend/BankStatementConverter.Tests/Fixtures/fictional-statement.pdf',import.meta.url));
const form=new FormData();form.append('file',new Blob([file],{type:'application/pdf'}),'fictional-statement.pdf');
for(const [key,value] of Object.entries({clientId:client.id,bankAccountId:account.id,bankStatementTemplateId:profile.id,exportTemplateId:template.id}))form.append(key,value);
const result=await request('/conversions',{method:'POST',body:form});assert.equal(result.history.status,'Completed');assert.equal(result.transactions.length,2);
const history=await request('/history/'+result.history.id);assert.equal(history.count,2);
console.log('Client API TypeScript : login, référentiels, upload PDF, conversion et historique OK.');
