import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { createServer } from 'node:net';
import { resolve, join } from 'node:path';
const root=resolve(import.meta.dirname,'..');
test('port validation preserves settings and rejects collisions and database ports', async()=>{
 const directory=await mkdtemp(join(root,'output/manager-test-'));
 const data=join(directory,'data'); await mkdir(data);await mkdir(join(directory,'services'));
 const reserve=createServer();await new Promise(done=>reserve.listen(0,'127.0.0.1',done));const occupied=reserve.address().port;
 const free=createServer();await new Promise(done=>free.listen(0,'127.0.0.1',done));const front=free.address().port;await new Promise(done=>free.close(done));
 const second=createServer();await new Promise(done=>second.listen(0,'127.0.0.1',done));const back=second.address().port;await new Promise(done=>second.close(done));
 const path=join(data,'Server.json');
 const original=JSON.stringify({FrontendPort:3006,BackendPort:5086,PublicUrl:'http://server:3006',DatabaseMode:'0'});
 await writeFile(path,original);await writeFile(join(data,'Postgres.Admin.json'),JSON.stringify({Port:55432}));
 await writeFile(join(directory,'services/AliasBackend.xml'),`<service><env name="ALIAS_CONFIG_FILE" value="${join(data,'Alias.Runtime.json')}" /></service>`);
 function check(first,last){return spawnSync('powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',join(root,'installer/Server-Control.ps1'),'-Action','ValidatePorts','-AppRoot',directory,'-FrontendPort',String(first),'-BackendPort',String(last),'-PublicUrl',`http://server:${first}`],{encoding:'utf8'});}
 try {
  const good=check(front,back);assert.equal(good.status,0,good.stderr);
  assert.notEqual(check(front,front).status,0);
  const collision=check(occupied,back);assert.notEqual(collision.status,0);assert.match(collision.stderr,/occupe/);
  await writeFile(path,JSON.stringify({FrontendPort:occupied,BackendPort:5086,PublicUrl:`http://server:${occupied}`,DatabaseMode:'0'}));
  assert.notEqual(check(occupied,back).status,0,'an unrelated process on the old port must still be rejected');
  await writeFile(path,original);
  await writeFile(join(data,'Postgres.Admin.json'),JSON.stringify({Port:front}));
  const database=check(front,back);assert.notEqual(database.status,0);assert.match(database.stderr,/PostgreSQL/);
  assert.equal(await readFile(path,'utf8'),original);
 } finally {await new Promise(done=>reserve.close(done));}
});
