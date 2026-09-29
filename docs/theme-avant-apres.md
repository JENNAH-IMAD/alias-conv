# Fichiers modifiés — avant → après

## src/app/banks/page.tsx

```diff
+                <p className={isLightMode ? 'text-lg font-semibold text-foreground' : 'text-lg font-semibold text-foreground'}>{bank.name}</p>
-                <p className={isLightMode ? 'text-lg font-semibold text-slate-900' : 'text-lg font-semibold text-white'}>{bank.name}</p>
```

## src/app/extraction-profiles/page.tsx

```diff
+              <div className={`flex items-start justify-between gap-4 border-b ${isLightMode ? 'border-border' : 'border-border'} pb-4`}>
-              <div className={`flex items-start justify-between gap-4 border-b ${isLightMode ? 'border-slate-200' : 'border-white/10'} pb-4`}>
+                    <h2 className={isLightMode ? 'text-lg font-semibold tracking-tight text-foreground' : 'text-lg font-semibold tracking-tight text-foreground'}>{profile.name}</h2>
+                    <p className={isLightMode ? 'mt-1 text-sm text-muted-foreground' : 'mt-1 text-sm text-muted-foreground'}>{profile.bank}</p>
-                    <h2 className={isLightMode ? 'text-lg font-semibold tracking-tight text-slate-900' : 'text-lg font-semibold tracking-tight text-white'}>{profile.name}</h2>
-                    <p className={isLightMode ? 'mt-1 text-sm text-slate-500' : 'mt-1 text-sm text-slate-400'}>{profile.bank}</p>
```

## src/app/templates/page.tsx

```diff
+                <p className={isLightMode ? 'text-lg font-semibold text-foreground' : 'text-lg font-semibold text-foreground'}>{template.name}</p>
+                <p className={isLightMode ? 'text-sm text-muted-foreground' : 'text-sm text-muted-foreground'}>{template.type}</p>
-                <p className={isLightMode ? 'text-lg font-semibold text-slate-900' : 'text-lg font-semibold text-white'}>{template.name}</p>
-                <p className={isLightMode ? 'text-sm text-slate-500' : 'text-sm text-slate-400'}>{template.type}</p>
+            <div className={`space-y-3 text-sm ${isLightMode ? 'text-muted-foreground' : 'text-foreground'}`}>
-            <div className={`space-y-3 text-sm ${isLightMode ? 'text-slate-600' : 'text-slate-300'}`}>
+              <p className={`mb-2 text-[11px] font-medium uppercase tracking-[0.2em] ${isLightMode ? 'text-muted-foreground' : 'text-muted-foreground'}`}>Champs</p>
-              <p className={`mb-2 text-[11px] font-medium uppercase tracking-[0.2em] ${isLightMode ? 'text-slate-500' : 'text-slate-400'}`}>Champs</p>
```

## src/components/brand-logo.tsx

```diff
+export { Logo as BrandLogo } from "./logo";
-import Image from 'next/image';
-import lightLogo from '../../assets/white mode logo.png';
-import darkLogo from '../../assets/dark mode logo.png';
-/** Both assets stay mounted so a theme change never flashes the wrong logo. */
-export function BrandLogo() {
-  return <span className="alias-logo">
-    <Image src={lightLogo} alt="Alias Convert PDF — Alias Informatique" className="alias-logo-light" sizes="220px" priority />
-    <Image src={darkLogo} alt="Alias Convert PDF — Alias Informatique" className="alias-logo-dark" sizes="220px" priority />
-  </span>;
-}
-
```

## src/components/resource-editor.tsx

```diff
+    {resource==='export-templates' && <div className="space-y-3"><h3>Colonnes d’export, dans l’ordre du fichier</h3>{fields.map((f,i)=><div className="grid grid-cols-2 gap-2 border-b border-border pb-3" key={i}><Input aria-label="Titre de colonne" required className={inputClass} value={f.fieldName} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,fieldName:e.target.value}:x))}/><select aria-label="Valeur source" className={inputClass} value={f.sourceField} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,sourceField:e.target.value,format:null}:x))}>{sources.map(s=><option key={s}>{s}</option>)}</select><select aria-label="Format" className={inputClass} value={f.format||''} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,format:e.target.value||null}:x))}>{['',...(['Date','ValueDate'].includes(f.sourceField)?['dd/MM/yyyy','yyyy-MM-dd','ddMMyyyy','yyyyMMdd']:['0.00','0.000','0.##'])].map(s=><option key={s} value={s}>{s||'Par défaut'}</option>)}</select><div className="flex gap-3"><label><input type="checkbox" checked={f.required} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,required:e.target.checked}:x))}/> Obligatoire</label><button type="button" onClick={()=>set('fields',fields.filter((_,j)=>j!==i))}>Retirer</button></div></div>)}<button type="button" onClick={()=>set('fields',[...fields,{fieldName:'Référence',sourceField:'Reference',position:fields.length,format:null,required:false}])}>+ Ajouter une colonne</button></div>}
-    {resource==='export-templates' && <div className="space-y-3"><h3>Colonnes d’export, dans l’ordre du fichier</h3>{fields.map((f,i)=><div className="grid grid-cols-2 gap-2 border-b border-slate-500/30 pb-3" key={i}><Input aria-label="Titre de colonne" required className={inputClass} value={f.fieldName} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,fieldName:e.target.value}:x))}/><select aria-label="Valeur source" className={inputClass} value={f.sourceField} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,sourceField:e.target.value,format:null}:x))}>{sources.map(s=><option key={s}>{s}</option>)}</select><select aria-label="Format" className={inputClass} value={f.format||''} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,format:e.target.value||null}:x))}>{['',...(['Date','ValueDate'].includes(f.sourceField)?['dd/MM/yyyy','yyyy-MM-dd','ddMMyyyy','yyyyMMdd']:['0.00','0.000','0.##'])].map(s=><option key={s} value={s}>{s||'Par défaut'}</option>)}</select><div className="flex gap-3"><label><input type="checkbox" checked={f.required} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,required:e.target.checked}:x))}/> Obligatoire</label><button type="button" onClick={()=>set('fields',fields.filter((_,j)=>j!==i))}>Retirer</button></div></div>)}<button type="button" onClick={()=>set('fields',[...fields,{fieldName:'Référence',sourceField:'Reference',position:fields.length,format:null,required:false}])}>+ Ajouter une colonne</button></div>}
```

## src/components/ui/badge.tsx

```diff
+          "bg-destructive text-destructive-foreground focus-visible:ring-destructive/20 dark:focus-visible:ring-destructive/40 [a&]:hover:bg-destructive/90",
-          "bg-destructive text-white focus-visible:ring-destructive/20 dark:bg-destructive/60 dark:focus-visible:ring-destructive/40 [a&]:hover:bg-destructive/90",
```

## src/components/ui/button.tsx

```diff
+        default: "bg-action text-action-foreground hover:bg-action/90",
-        default: "bg-primary text-primary-foreground hover:bg-primary/90",
```

## src/components/logo.tsx — ajouté

```tsx
import Image from 'next/image';

/** SVG containers preserve the original artwork supplied in assets/. */
export function Logo() {
  return <span className="alias-logo">
    <Image src="/logo-light.svg" alt="Alias Convert PDF — Alias Informatique" width={896} height={469} className="alias-logo-light" unoptimized priority />
    <Image src="/logo-dark.svg" alt="Alias Convert PDF — Alias Informatique" width={896} height={455} className="alias-logo-dark" unoptimized priority />
  </span>;
}

```

## public/logo-light.svg et public/logo-dark.svg — ajoutés

[logo-light.svg](../frontend/Import-pdf-excel-master/public/logo-light.svg) · [logo-dark.svg](../frontend/Import-pdf-excel-master/public/logo-dark.svg)

Conteneurs SVG des PNG fournis dans assets, sans modification du dessin original.

## src/app/globals.css — avant → après

Couleurs locales → variables de thème. Dégradé du titre → couleur unie. Dégradé conservé uniquement pour le bouton principal.

## src/app/globals.css — fichier complet après modification

```css
@import "tailwindcss";
@import "tw-animate-css";
@custom-variant dark (&:is([data-theme="dark"] *));
@layer base {
  * { border-color: var(--border); }
}
:root,[data-theme="light"] {
 --background:#FAFBFC; --foreground:#0E1726; --surface:#FFFFFF; --surface-alt:#EAF6FD;
 --card:var(--surface); --card-foreground:var(--foreground); --popover:var(--surface); --popover-foreground:var(--foreground);
 --border:#E6EBF0; --muted:#EAF6FD; --muted-foreground:#64748B;
 --primary:#1A9FE0; --primary-foreground:#0A0C10; --primary-hover:#0B7FBF;
 --action:#0B7FBF; --action-foreground:#0A0C10; --accent:#22D3EE; --accent-foreground:#0E1726;
 --link:#0874AB; --sidebar:#FFFFFF; --success:#16A34A; --success-text:#137A38;
 --destructive:#DC2626; --destructive-foreground:#FFFFFF; --warning:#F59E0B; --warning-text:#926000;
 --ring:#0B7FBF; --input:#64748B; --halo:#EAF6FD; --shadow:none; --radius:1rem;
 --overlay:rgb(10 12 16 / .6);  color-scheme:light;
}
[data-theme="dark"] {
 --background:#0A0C10; --foreground:#EDF1F6; --surface:#12161C; --surface-alt:#1F252E;
 --border:#1F252E; --muted:#1F252E; --muted-foreground:#8B96A5; --primary:#3DB8F5; --primary-hover:#1A9FE0;
 --action:#1A9FE0; --accent:#67E8F9; --accent-foreground:#0A0C10;
 --link:#3DB8F5; --sidebar:#12161C; --success:#34D399; --success-text:#34D399;
 --destructive:#F87171; --destructive-foreground:#0A0C10; --warning:#FBBF24; --warning-text:#FBBF24;
 --ring:#3DB8F5; --input:#8B96A5; --halo:rgba(26,159,224,.15);  color-scheme:dark;
}
:root {
 --preview-light-bg:#FAFBFC; --preview-light-border:#E6EBF0;
 --preview-dark-bg:#0A0C10; --preview-dark-border:#1F252E;
 --elevation-color:color-mix(in srgb,var(--foreground) 5%,transparent);
}
:root,[data-theme="light"],[data-theme="dark"] {
 --avatar-background:color-mix(in srgb,var(--primary) 12%,var(--card));
 --avatar-foreground:var(--link);
 --avatar-border:color-mix(in srgb,var(--primary) 25%,var(--border));
}
@theme inline {
 --color-background:var(--background); --color-foreground:var(--foreground);
 --color-card:var(--surface); --color-card-foreground:var(--foreground);
 --color-popover:var(--surface); --color-popover-foreground:var(--foreground);
 --color-action:var(--action); --color-action-foreground:var(--action-foreground);
 --color-primary:var(--primary); --color-primary-foreground:var(--primary-foreground);
 --color-secondary:var(--surface-alt); --color-secondary-foreground:var(--foreground);
 --color-muted:var(--surface-alt); --color-muted-foreground:var(--muted-foreground);
 --color-accent:var(--accent); --color-accent-foreground:var(--accent-foreground);
 --color-destructive:var(--destructive); --color-destructive-foreground:var(--destructive-foreground); --color-success:var(--success); --color-warning:var(--warning); --color-border:var(--border); --color-input:var(--input); --color-ring:var(--ring);
 --radius-sm:calc(var(--radius) - 4px); --radius-md:calc(var(--radius) - 2px); --radius-lg:var(--radius); --radius-xl:calc(var(--radius) + 4px);
 --font-sans:var(--font-inter); --font-display:var(--font-manrope);
}
*{box-sizing:border-box}body{margin:0;font-family:var(--font-inter),Arial,sans-serif;font-size:14px;background:var(--background);color:var(--foreground)}h1,h2,h3{font-family:var(--font-manrope),Arial,sans-serif}button,input,select,textarea{font:inherit}button,a,input,select,textarea{touch-action:manipulation}button,a{ -webkit-tap-highlight-color:transparent}button:not(:disabled){cursor:pointer}button:disabled{opacity:.45;cursor:not-allowed}button,a{transition:background-color .16s,color .16s,border-color .16s,transform .16s}a:focus-visible,button:focus-visible,input:focus-visible,select:focus-visible,textarea:focus-visible{outline:3px solid var(--ring);outline-offset:3px}::selection{background:var(--accent);color:var(--accent-foreground)}.theme-root{min-height:100vh;background:var(--background);color:var(--foreground)}.muted{color:var(--muted-foreground)}.accent-text{color:var(--link)}
.app-shell{display:flex;min-height:100vh}.desktop-sidebar{width:258px;position:fixed;inset:0 auto 0 0;display:flex;flex-direction:column;background:var(--sidebar);border-right:1px solid var(--border);padding:28px 18px 16px;z-index:30;overflow-y:auto}.brand{display:flex;align-items:center;gap:11px;padding:0 10px 26px;font-size:23px;font-weight:800;letter-spacing:-.9px}.brand small{display:block;font-size:10px;font-weight:500;color:var(--muted-foreground);letter-spacing:.2px;margin-top:4px}.brand-mark{display:grid;place-items:center;background:var(--primary);border-radius:13px;width:43px;height:43px;color:var(--primary-foreground);box-shadow:0 5px 14px var(--halo)}.sidebar-nav{display:grid;gap:22px}.nav-caption{font-size:9px;font-weight:700;letter-spacing:1.5px;color:var(--muted-foreground);padding:0 13px;margin-bottom:10px}.nav-link{display:flex;align-items:center;gap:11px;padding:11px 12px;border-radius:9px;color:var(--muted-foreground);font-size:12px;font-weight:550;margin:3px 0}.nav-link:hover{background:var(--surface-alt);color:var(--foreground)}.nav-link.active{background:color-mix(in srgb,var(--primary) 13%,transparent);color:var(--link)}.sidebar-bottom{margin-top:auto;padding-top:26px}.workspace-note{display:flex;gap:10px;padding:14px 10px;background:var(--surface-alt);border:1px solid var(--border);border-radius:11px;color:var(--link);font-size:11px}.workspace-note p{font-size:10px;color:var(--muted-foreground);margin-top:4px}.sidebar-profile{display:flex;gap:10px;align-items:center;padding:19px 4px 12px;font-size:12px}.logout-button{display:flex;align-items:center;justify-content:center;gap:8px;width:100%;font-size:11px;color:var(--muted-foreground);padding:8px;border-radius:8px}.logout-button:hover{background:var(--surface-alt)}.profile-avatar{display:inline-flex;flex-shrink:0;align-items:center;justify-content:center;width:46px;height:46px;border-radius:14px;background:var(--avatar-background);color:var(--avatar-foreground);border:1px solid var(--avatar-border);font-size:14px;font-weight:750;letter-spacing:.3px}.avatar-small{width:35px;height:35px;border-radius:50%;font-size:11px}.app-body{margin-left:258px;min-width:0;flex:1;display:flex;flex-direction:column}.topbar{height:76px;display:flex;align-items:center;justify-content:space-between;gap:16px;padding:0 34px;background:var(--surface);border-bottom:1px solid var(--border);font-size:12px;position:sticky;top:0;z-index:20}.breadcrumb-root{color:var(--muted-foreground)}.icon-button{width:37px;height:37px;display:inline-flex;align-items:center;justify-content:center;border:1px solid var(--border);border-radius:10px;background:var(--surface);color:var(--muted-foreground)}.icon-button:hover{background:var(--surface-alt);color:var(--link)}.main-content{padding:32px 34px;max-width:1660px;width:100%;margin:0 auto;flex:1;animation:page-enter .2s ease-out}.app-footer{margin:0 34px;padding:19px 0;border-top:1px solid var(--border);display:flex;justify-content:space-between;gap:12px;color:var(--muted-foreground);font-size:10px}.app-footer a{display:flex;align-items:center;gap:5px}.footer-divider{margin:0 10px;color:var(--border)}.mobile-menu{display:none}.mobile-drawer{height:100dvh;max-height:100dvh;width:290px;max-width:90vw;margin:0;padding:28px 18px;background:var(--sidebar);color:var(--foreground);border:0}.mobile-drawer[open]{display:flex;flex-direction:column}.mobile-drawer::backdrop{background:var(--overlay);backdrop-filter:blur(3px)}.drawer-close{position:absolute;right:10px;top:8px;width:26px;height:26px}.skip-link{position:fixed;left:20px;top:-70px;z-index:100;background:var(--primary);padding:12px;color:var(--primary-foreground);border-radius:8px}.skip-link:focus{top:10px}.app-loading{min-height:100vh;display:flex;align-items:center;justify-content:center;gap:12px}
.ui-page{display:grid;gap:24px;color:var(--foreground)}.ui-hero{padding:4px 0 8px;border:0;border-radius:0;background:none;box-shadow:none}.ui-eyebrow{font-size:10px;font-weight:700;letter-spacing:1.7px;text-transform:uppercase;color:var(--link)}.ui-title{font-size:clamp(26px,3vw,34px);font-weight:750;letter-spacing:-1.2px;margin-top:9px;line-height:1.3}.ui-description{color:var(--muted-foreground);font-size:13px;margin-top:9px;line-height:1.7}.button-primary{display:inline-flex;align-items:center;justify-content:center;gap:8px;padding:11px 17px;border-radius:9px;background:var(--primary);color:var(--primary-foreground);font-size:12px;font-weight:650;box-shadow:0 3px 8px var(--halo)}.button-primary:hover{background:var(--primary-hover);transform:translateY(-1px)}.button-secondary{display:inline-flex;align-items:center;justify-content:center;gap:7px;border:1px solid var(--border);background:var(--surface);padding:9px 13px;border-radius:8px;font-size:12px;font-weight:550}.button-secondary:hover{background:var(--surface-alt)}.ui-card{border:1px solid var(--border);border-radius:12px;background:var(--surface);padding:24px;box-shadow:var(--shadow);min-width:0}.ui-row{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid var(--border);font-size:12px}.ui-row:last-child{border-bottom:0}.ui-value{font-weight:550;color:var(--foreground);overflow-wrap:anywhere}.ui-tag{display:inline-flex;align-items:center;gap:5px;padding:5px 9px;border-radius:6px;background:var(--surface-alt);color:var(--muted-foreground);font-size:10px;font-weight:550}.ui-input{width:100%;min-height:43px;border:1px solid var(--border);border-radius:9px;background:var(--surface);padding:10px 12px;color:var(--foreground);font-size:13px}.ui-input:hover{border-color:var(--muted-foreground)}.ui-label{display:block;margin-bottom:8px;font-size:12px;font-weight:550;color:var(--muted-foreground)}.ui-upload{display:flex;flex-direction:column;align-items:center;justify-content:center;border:2px dashed var(--border);border-radius:13px;padding:32px 24px;background:var(--surface-alt)}.ui-file{width:100%;margin-top:20px;font-size:12px;color:var(--muted-foreground)}.ui-file::file-selector-button{border:0;background:var(--primary);color:var(--primary-foreground);border-radius:7px;padding:10px 14px;margin-right:12px;cursor:pointer}.ui-summary{border:1px solid var(--border);border-radius:10px;padding:17px;background:var(--surface-alt)}.ui-table-head{border-bottom:1px solid var(--border);background:var(--surface-alt);color:var(--muted-foreground);font-size:11px}.ui-table-cell{padding:16px 12px;font-size:12px}.ui-table-row{border-bottom:1px solid var(--border)}.ui-table-row:last-child{border-bottom:0}.ui-table-row:hover{background:var(--surface-alt)}.pagination{display:flex;justify-content:space-between;align-items:center;gap:12px;font-size:11px;color:var(--muted-foreground);padding-top:10px}.load-state{padding:25px;border:1px dashed var(--border);border-radius:12px;background:var(--surface);color:var(--muted-foreground);display:flex;align-items:center;justify-content:center;gap:10px}.loading-dot{height:8px;width:8px;border-radius:50%;background:var(--accent);animation:pulse-dot 1s ease-in-out infinite}.client-card{transition:border-color .18s,box-shadow .18s}.client-card:hover{border-color:color-mix(in srgb,var(--accent) 45%,var(--border));box-shadow:0 6px 24px var(--elevation-color)}.client-contact{display:flex;gap:9px;align-items:center;color:var(--muted-foreground);font-size:12px;margin:12px 0}.client-contact a{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.client-contact a:hover{color:var(--link)}.client-actions{margin-top:18px;padding-top:16px;border-top:1px solid var(--border);display:flex;justify-content:space-between;align-items:center}.danger-button{color:var(--destructive)}.status-positive{font-size:10px;color:var(--success-text);background:color-mix(in srgb,var(--success) 10%,transparent);padding:5px 9px;border-radius:20px;white-space:nowrap}.metric-value{font-size:32px;font-weight:750;letter-spacing:-1px;margin-top:15px}.metric-icon{padding:10px;border-radius:10px;background:var(--surface-alt);color:var(--link)}
@keyframes page-enter{from{opacity:0;transform:translateY(5px)}to{opacity:1;transform:translateY(0)}}@keyframes pulse-dot{50%{opacity:.35}}@media(max-width:1100px){.desktop-sidebar{width:232px}.app-body{margin-left:232px}.main-content{padding:26px 24px}.topbar{padding:0 24px}}@media(max-width:850px){.desktop-sidebar{display:none}.app-body{margin-left:0}.mobile-menu{display:inline-flex}.main-content{padding:24px 18px}.topbar{height:66px;padding:0 18px}.breadcrumb-root{display:none}.app-footer{margin:0 18px}}@media(max-width:520px){.topbar-create{display:none}.ui-card{padding:18px}.app-footer{flex-wrap:wrap}.pagination{flex-wrap:wrap}}@media(prefers-reduced-motion:reduce){*,*::before,*::after{animation:none!important;transition:none!important;scroll-behavior:auto!important}}
.app-dialog{margin:auto;max-height:88dvh;width:min(740px,calc(100% - 32px));overflow:auto;border:1px solid var(--border);border-radius:17px;padding:26px;background:var(--surface);color:var(--foreground);box-shadow:0 24px 80px var(--overlay)}.app-dialog::backdrop{background:var(--overlay);backdrop-filter:blur(3px)}.app-dialog[open]{animation:page-enter .18s ease-out}.app-dialog input[type=checkbox]{accent-color:var(--primary)}
.auth-layout{display:grid;grid-template-columns:1.05fr 1fr;min-height:100dvh}.auth-story{display:flex;flex-direction:column;justify-content:space-between;background:var(--background);color:var(--foreground);padding:42px;position:relative;overflow:hidden}.auth-story::after{content:'';position:absolute;border:1px solid var(--border);width:560px;height:560px;right:-300px;bottom:-240px;border-radius:50%;box-shadow:0 0 0 80px var(--halo),0 0 0 160px var(--halo);pointer-events:none}.auth-story .brand{padding:0}.auth-story .brand small{color:var(--muted-foreground)}.auth-story-content{max-width:540px;margin:70px auto;position:relative;z-index:1}.auth-story-content .ui-eyebrow{color:var(--link)}.auth-story h1{font-size:clamp(32px,3.7vw,54px);letter-spacing:-2px;font-weight:750;line-height:1.2;margin:24px 0}.auth-story h1 span{color:var(--link)}.auth-story-content>p:not(.ui-eyebrow){color:var(--muted-foreground);font-size:14px;line-height:1.9;max-width:440px}.auth-flow{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:27px 24px;background:var(--surface);border:1px solid var(--border);border-radius:15px;margin:35px 0;color:var(--link)}.auth-flow span{display:flex;flex-direction:column;align-items:center;gap:12px}.auth-flow small{color:var(--foreground);font-size:10px}.auth-form-section{position:relative;display:flex;flex-direction:column;justify-content:center;align-items:center;padding:70px 35px 30px;background:var(--surface)}.auth-form-content{width:100%;max-width:390px;margin:auto 0;padding:30px 0 60px}.auth-theme{position:absolute;right:28px;top:25px}.auth-form-content .ui-input{min-height:47px;background:var(--background)}.auth-form-content .button-primary{min-height:46px}.auth-mobile-brand{display:none}@media(max-width:850px){.auth-layout{grid-template-columns:1fr}.auth-story{display:none}.auth-form-section{min-height:100dvh;padding:60px 24px 24px}.auth-mobile-brand{display:flex;align-items:center;gap:9px;font-weight:750;font-size:21px;color:var(--link);margin-bottom:45px}}
.theme-choice{border:1px solid var(--border);border-radius:10px;padding:12px;text-align:left}.theme-choice.selected{border-color:var(--link);box-shadow:0 0 0 1px var(--accent)}.theme-preview{height:75px;border-radius:7px;display:grid;grid-template-columns:25% 1fr;gap:8px;padding:8px;border:1px solid var(--border)}.preview-light{background:var(--preview-light-bg);color:var(--preview-light-border)}.preview-dark{background:var(--preview-dark-bg);color:var(--preview-dark-border)}.theme-preview>span:first-child{background:currentColor;border-radius:3px}.theme-preview>span:last-child{display:grid;gap:5px}.theme-preview i{display:block;background:currentColor;border-radius:3px}.theme-preview i:first-child{background:var(--primary)}

.client-logo{width:46px;height:46px;object-fit:contain;border-radius:13px;background:var(--card);border:1px solid var(--border);padding:3px}.client-photo-button{position:relative;flex-shrink:0;border-radius:14px}.photo-camera{position:absolute;bottom:-4px;right:-5px;border:2px solid var(--surface);background:var(--primary);color:var(--primary-foreground);border-radius:50%;width:21px;height:21px;display:grid;place-items:center}.photo-editor-preview{display:flex;align-items:center;gap:20px;padding:20px;border:1px solid var(--border);background:var(--surface-alt);border-radius:15px}.photo-editor-preview .client-logo,.photo-editor-preview .profile-avatar,.photo-preview-image{width:100px;height:100px;object-fit:contain;border-radius:15px;flex-shrink:0}.photo-preview-image{background:var(--card);padding:5px}.photo-upload{display:flex;flex-direction:column;align-items:center;gap:10px;padding:25px;border:2px dashed var(--border);border-radius:16px;background:var(--surface-alt);color:var(--link)}.nav-link.active{box-shadow:inset 0 0 0 1px color-mix(in srgb,var(--primary) 30%,transparent)}.brand-mark{background:var(--primary)}@media(max-width:520px){.ui-hero{padding:20px}.photo-editor-preview{flex-wrap:wrap}}

/* Guided conversion, shared by processing and archived standard files. */
.app-dialog.conversion-dialog{width:min(1200px,calc(100% - 32px))}.conversion-steps{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:24px}.conversion-steps li{display:flex;align-items:center;gap:12px;padding:15px;border:1px solid var(--border);border-radius:12px;background:var(--surface-alt);color:var(--muted-foreground)}.conversion-steps strong{display:block;font-size:12px}.conversion-steps small{display:block;font-size:10px;margin-top:5px}.conversion-steps .current{border-color:var(--primary);color:var(--foreground);background:color-mix(in srgb,var(--primary) 10%,var(--surface))}.step-icon{display:grid;place-items:center;flex-shrink:0;width:35px;height:35px;border-radius:10px;background:var(--surface);color:var(--link)}.done .step-icon{color:var(--success-text)}.conversion-section{padding:20px;border:1px solid var(--border);border-radius:18px;background:var(--surface);min-width:0}.conversion-heading{display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;margin-bottom:20px}.conversion-heading h3{font-weight:700;font-size:15px}.conversion-heading .ui-description{font-size:12px;margin-top:6px}.mapping-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:9px}.mapping-field{display:grid;grid-template-columns:24px 1fr 18px 1fr;align-items:center;gap:10px;background:var(--surface-alt);border:1px solid var(--border);border-radius:10px;padding:12px;font-size:12px;overflow-wrap:anywhere}.mapping-field small{display:block;font-weight:400;font-size:10px;color:var(--muted-foreground);margin-top:4px}.mapping-index{color:var(--link);font-size:10px}.conversion-metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}.conversion-metric{border:1px solid var(--border);border-radius:13px;background:var(--surface-alt);padding:17px}.conversion-metric>span{color:var(--muted-foreground);font-size:11px;display:block}.conversion-metric strong{display:block;font-variant-numeric:tabular-nums;font-size:clamp(16px,1.8vw,23px);margin-top:12px;overflow-wrap:anywhere}.amount-credit{color:var(--success-text)}.amount-debit{color:var(--destructive)}.conversion-note{display:flex;align-items:flex-start;gap:8px;color:var(--muted-foreground);font-size:11px;line-height:1.7;margin-top:16px}.conversion-note svg{flex-shrink:0;margin-top:2px}.conversion-toolbar{display:flex;gap:12px;margin-bottom:18px}.conversion-search{display:flex;align-items:center;gap:10px;flex:1;border:1px solid var(--border);border-radius:9px;padding:10px;color:var(--muted-foreground)}.conversion-search input{background:transparent;outline:none;width:100%;color:var(--foreground);font-size:12px}.conversion-search:focus-within{outline:2px solid var(--ring)}.conversion-toolbar select{width:220px}.conversion-table-scroll{overflow:auto;max-height:440px}.conversion-table{width:100%;border-collapse:separate;border-spacing:0;font-size:12px}.conversion-table th{position:sticky;top:0;background:var(--surface-alt);color:var(--muted-foreground);font-size:11px;font-weight:550;text-align:left;padding:13px 12px;z-index:1}.conversion-table td{padding:14px 12px;border-bottom:1px solid var(--border);font-variant-numeric:tabular-nums}.conversion-table tr:hover td{background:var(--surface-alt)}.conversion-table td:nth-child(3){min-width:220px}.conversion-table td:not(:nth-child(3)){white-space:nowrap}.conversion-table th:nth-child(n+4),.conversion-table td:nth-child(n+4){text-align:right}.conversion-section .pagination{margin-top:16px}@media(max-width:700px){.conversion-steps{grid-template-columns:1fr;gap:7px}.conversion-steps li{padding:10px}.conversion-steps small{display:none}.mapping-grid{grid-template-columns:1fr}.conversion-metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.conversion-toolbar{flex-direction:column}.conversion-toolbar select{width:100%}.conversion-section{padding:14px}.app-dialog.conversion-dialog{padding:16px}}
/* Shared shadcn and legacy surface refinements. */
.workflow-card{background:var(--surface)}
.ui-input:disabled{opacity:.6;cursor:not-allowed}
.ui-input,.conversion-table{font-variant-numeric:tabular-nums}
.ui-page>section,.ui-page>div{min-width:0}
.mobile-drawer{overflow-y:auto}
@media(max-width:520px){.ui-hero{padding:0 0 4px}.main-content{padding:22px 16px}.topbar{padding:0 16px}.ui-title{font-size:27px}}
.auth-form-content [data-slot="input"]{min-height:47px;background:var(--background)}
.auth-form-content [data-slot="button"]{min-height:46px}
.ui-page>.conversion-steps{margin-bottom:0}

.conversion-workbench{display:grid;gap:18px}
.conversion-actions{display:flex;flex-wrap:wrap;justify-content:flex-end;gap:8px}
.conversion-quality-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;margin-top:12px}
.conversion-quality-grid>div{border:1px solid var(--border);border-radius:10px;background:color-mix(in srgb,var(--surface-alt) 70%,var(--surface));padding:12px}
.conversion-quality-grid strong{display:block;font-size:17px;font-variant-numeric:tabular-nums}
.conversion-quality-grid span{display:block;margin-top:5px;color:var(--muted-foreground);font-size:11px}
.conversion-filter{display:flex;align-items:center;gap:9px;width:250px;border:1px solid var(--border);border-radius:9px;padding:0 10px;color:var(--muted-foreground);background:var(--surface)}
.conversion-filter select{min-height:40px;width:100%;border:0;background:transparent;color:var(--foreground);outline:0;font-size:12px}
.conversion-table-editable th:last-child,.conversion-table-editable td:last-child{text-align:left;min-width:96px}
.conversion-table-editable input,.conversion-table-editable textarea{width:100%;min-width:92px;border:1px solid var(--border);border-radius:7px;background:var(--surface);color:var(--foreground);padding:8px;font-size:12px;line-height:1.4}
.conversion-table-editable textarea{min-width:260px;resize:vertical}
.conversion-table-editable input:focus,.conversion-table-editable textarea:focus{outline:2px solid var(--ring);outline-offset:1px}
.conversion-table-editable .row-edited td{background:color-mix(in srgb,var(--primary) 7%,var(--surface))}
.row-actions{display:flex;gap:6px}
.row-actions .icon-button{width:32px;height:32px;border-radius:8px}
.conversion-file-strip{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;border:1px solid var(--border);border-radius:14px;background:var(--surface);padding:16px}
.processing-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}
.processing-card{display:block;min-width:0;border:1px solid var(--border);border-radius:14px;background:var(--surface);padding:16px}
.processing-card .ui-input{margin:8px 0 10px}
.processing-card-summary ol{margin:9px 0 0;padding-left:18px;color:var(--muted-foreground);font-size:12px;line-height:1.8}
@media(max-width:900px){.processing-grid{grid-template-columns:1fr}.conversion-actions{justify-content:flex-start}.conversion-quality-grid{grid-template-columns:1fr}.conversion-filter{width:100%}}
@media(max-width:700px){.conversion-file-strip{flex-direction:column}.conversion-table-editable textarea{min-width:200px}}
.workspace-document{display:grid;gap:16px}
.workspace-document-header{display:flex;align-items:flex-start;justify-content:space-between;gap:16px}
.workspace-document-meta{display:grid;gap:8px;text-align:right;color:var(--muted-foreground);font-size:11px;white-space:nowrap}
.workspace-document-actions{display:flex;flex-wrap:wrap;align-items:center;gap:8px;padding:12px;border:1px solid var(--border);border-radius:10px;background:color-mix(in srgb,var(--surface-alt) 62%,var(--surface))}
@media(max-width:700px){.workspace-document-header{flex-direction:column}.workspace-document-meta{text-align:left;white-space:normal}.workspace-document-actions [data-slot="button"]{flex:1 1 140px}}

/* The shell stays in the viewport; navigation and page content scroll independently. */
.app-shell{height:100dvh;min-height:0;overflow:hidden}
.desktop-sidebar{height:100dvh;overflow:hidden;padding:24px 14px 12px}
.desktop-sidebar .brand,.mobile-drawer .brand{flex-shrink:0;padding-bottom:24px}
.sidebar-nav{display:flex;flex-direction:column;gap:20px;flex:1;min-height:0;overflow-y:auto;overflow-x:hidden;overscroll-behavior:contain;padding:2px 4px 12px;scrollbar-gutter:stable}
.sidebar-nav>div{flex-shrink:0}
.sidebar-bottom{flex-shrink:0;margin-top:12px;padding-top:10px;border-top:1px solid var(--border)}
.sidebar-bottom .workspace-note{display:none}
.sidebar-profile{padding:8px 6px 12px}
.app-body{height:100dvh;min-height:0;overflow:hidden}
.topbar{position:relative;flex-shrink:0}
.app-scroll{flex:1;min-height:0;display:flex;flex-direction:column;overflow-y:auto;overflow-x:hidden;overscroll-behavior-y:contain;scrollbar-gutter:stable;scroll-padding-top:24px}
.app-scroll:focus-visible,.sidebar-nav:focus-visible{outline:2px solid var(--ring);outline-offset:-2px}
.main-content{flex:1 0 auto}
.app-footer{flex-shrink:0}
.mobile-drawer{overflow:hidden;padding:30px 14px 12px}
.app-scroll,.sidebar-nav,.app-dialog,.conversion-table-scroll,[data-slot="table-container"]{scrollbar-width:thin;scrollbar-color:color-mix(in srgb,var(--muted-foreground) 35%,transparent) transparent}
.app-scroll:hover,.sidebar-nav:hover{scrollbar-color:color-mix(in srgb,var(--muted-foreground) 65%,transparent) transparent}
.app-scroll::-webkit-scrollbar,.sidebar-nav::-webkit-scrollbar,.app-dialog::-webkit-scrollbar{width:6px;height:6px}
.app-scroll::-webkit-scrollbar-track,.sidebar-nav::-webkit-scrollbar-track,.app-dialog::-webkit-scrollbar-track{background:transparent}
.app-scroll::-webkit-scrollbar-thumb,.sidebar-nav::-webkit-scrollbar-thumb,.app-dialog::-webkit-scrollbar-thumb{background:color-mix(in srgb,var(--muted-foreground) 40%,transparent);border-radius:20px}
.app-scroll::-webkit-scrollbar-button,.sidebar-nav::-webkit-scrollbar-button,.app-dialog::-webkit-scrollbar-button{display:none;width:0;height:0}
.accounts-section-heading{display:flex;align-items:center;justify-content:space-between;gap:12px;padding-bottom:16px;border-bottom:1px solid var(--border)}
.account-card{transition:border-color .2s,box-shadow .2s}
.account-card:hover{border-color:color-mix(in srgb,var(--primary) 45%,var(--border));box-shadow:0 6px 24px var(--elevation-color)}
.account-bank-icon{display:grid;place-items:center;width:44px;height:44px;flex-shrink:0;border-radius:12px;background:color-mix(in srgb,var(--primary) 12%,var(--surface));color:var(--link);border:1px solid color-mix(in srgb,var(--primary) 20%,var(--border))}
.account-number{padding:16px;border:1px solid var(--border);border-radius:10px;background:color-mix(in srgb,var(--surface-alt) 65%,var(--surface))}
.accounts-empty{display:flex;flex-direction:column;align-items:center;text-align:center;padding:48px 24px;border:1px dashed var(--border);border-radius:16px;background:var(--surface)}
@media(max-width:520px){.accounts-section-heading{align-items:flex-start;flex-direction:column}.account-card [data-slot="card-header"],.account-card [data-slot="card-content"],.account-card [data-slot="card-footer"]{padding-left:18px;padding-right:18px}.app-scroll{scrollbar-gutter:auto}}
@media(max-height:500px){.desktop-sidebar,.mobile-drawer{padding-top:12px}.desktop-sidebar .brand,.mobile-drawer .brand{padding-bottom:12px}.sidebar-bottom{margin-top:6px;padding-top:4px}.sidebar-profile{padding:4px 6px}.sidebar-nav{gap:12px}.logout-button{padding:6px}}
.treatment-navigation{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;margin-bottom:24px}
.treatment-navigation button{display:flex;align-items:center;gap:12px;text-align:left;padding:15px;border:1px solid var(--border);border-radius:12px;background:var(--surface-alt);color:var(--muted-foreground);font-size:12px}
.treatment-navigation button span{display:grid;place-items:center;flex-shrink:0;width:30px;height:30px;border-radius:9px;background:var(--surface)}
.treatment-navigation button.selected{border-color:var(--primary);background:color-mix(in srgb,var(--primary) 10%,var(--surface));color:var(--foreground)}
.treatment-panel{scroll-margin-top:24px;min-width:0}
@media(max-width:700px){.treatment-navigation{grid-template-columns:1fr;gap:7px}.treatment-navigation button{padding:10px}.treatment-panel .conversion-section{padding:12px}}

/* Alias Informatique: shared identity, responsive navigation and theme controls. */
.alias-logo{display:block;width:190px;max-width:100%;flex-shrink:0;border-radius:12px;overflow:hidden}
.alias-logo img{width:100%;height:auto;object-fit:contain}
.alias-logo-dark{display:none}
[data-theme="dark"] .alias-logo-light{display:none}
[data-theme="dark"] .alias-logo-dark{display:block}
.brand{justify-content:center;padding:0 0 16px!important}
.desktop-sidebar{width:264px;transition:width .2s ease}
.app-body{margin-left:264px;transition:margin-left .2s ease}
.sidebar-mode{display:flex;align-items:center;gap:10px;padding:0 8px 16px;color:var(--muted-foreground);font-size:11px;flex-shrink:0}
.sidebar-toggle{flex-shrink:0}
.nav-link{min-height:44px;border-radius:12px;font-size:12px}
.nav-link svg{flex-shrink:0}
.nav-link.active{color:var(--link);background:color-mix(in srgb,var(--primary) 10%,var(--surface));box-shadow:inset 0 0 0 1px color-mix(in srgb,var(--primary) 25%,var(--border))}
.nav-caption{font-size:9px;letter-spacing:1.4px}
.sidebar-compact .desktop-sidebar{width:88px;padding-left:10px;padding-right:10px}
.sidebar-compact .app-body{margin-left:88px}
.sidebar-compact .alias-logo{width:62px}
.sidebar-compact .sidebar-mode{justify-content:center;padding-left:0;padding-right:0}
.sidebar-compact .sidebar-mode>span,.sidebar-compact .nav-caption,.sidebar-compact .nav-label,.sidebar-compact .nav-chevron,.sidebar-compact .sidebar-profile>span:not(.profile-avatar),.sidebar-compact .sidebar-profile>svg,.sidebar-compact .logout-button>span{display:none}
.sidebar-compact .nav-link{justify-content:center;padding:10px 0}
.sidebar-compact .sidebar-nav{gap:14px;padding-left:0;padding-right:0;scrollbar-gutter:auto}
.sidebar-compact .sidebar-nav>div+div{border-top:1px solid var(--border);padding-top:10px}
.sidebar-compact .sidebar-profile{justify-content:center;padding:6px 0 12px}
.sidebar-compact .logout-button{min-height:40px}
.theme-switch{display:inline-flex;align-items:center;padding:3px;border:1px solid var(--border);border-radius:12px;background:var(--surface);flex-shrink:0}
.theme-switch button{display:grid;place-items:center;width:36px;height:36px;border-radius:8px;color:var(--muted-foreground)}
.theme-switch button:hover{background:var(--surface-alt);color:var(--foreground)}
.theme-switch button[aria-pressed="true"]{background:var(--surface-alt);color:var(--link);box-shadow:inset 0 0 0 1px var(--border)}
.button-primary,.primary-action{background:linear-gradient(110deg,var(--action),var(--primary),var(--accent));color:var(--action-foreground);border-radius:12px;box-shadow:none}
.button-primary:hover,.primary-action:hover{background:linear-gradient(110deg,var(--action),var(--primary),var(--accent));filter:brightness(.97)}
.ui-card,.conversion-section{border-radius:18px;box-shadow:none}
.ui-input{border-color:color-mix(in srgb,var(--input) 70%,var(--border));border-radius:12px}
.ui-upload{background:var(--surface);border-width:1px;border-radius:20px;box-shadow:0 0 65px 12px var(--halo)}
.danger-button,.amount-debit{color:var(--destructive)}
.auth-story{background:var(--background);color:var(--foreground);border-right:1px solid var(--border)}
.auth-story::after{border-color:var(--border);box-shadow:0 0 100px 40px var(--halo)}
.auth-story .brand{justify-content:flex-start}
.auth-story .alias-logo{width:220px}
.auth-story-content .ui-eyebrow{color:var(--link)}
.auth-story h1 span{color:var(--link)}
.auth-story-content>p:not(.ui-eyebrow),.auth-flow small{color:var(--muted-foreground)}
.auth-flow{background:var(--surface);border-color:var(--border);color:var(--link)}
.auth-mobile-brand .alias-logo{width:200px}
.mobile-drawer::backdrop,.app-dialog::backdrop{background:var(--overlay)}
.theme-preview{border-color:var(--border)}
.preview-light{background:var(--preview-light-bg);color:var(--preview-light-border)}
.preview-dark{background:var(--preview-dark-bg);color:var(--preview-dark-border)}
.preview-system{background:var(--background);color:var(--border)}
.theme-preview i:first-child{background:var(--primary)}
@media(max-width:850px){
 .app-body,.sidebar-compact .app-body{margin-left:0}
 .mobile-drawer{width:290px;padding-top:42px;transition:width .2s ease}
 .sidebar-compact .mobile-drawer{width:88px}
 .sidebar-compact .mobile-drawer .alias-logo{width:60px}
 .drawer-close{width:32px;height:32px;right:12px}
 .topbar{gap:8px}
 .topbar>div{gap:8px}
}
@media(max-width:520px){
 .topbar{padding:0 12px}
 .topbar .theme-switch button{width:30px;height:36px}
 .topbar .theme-switch{padding:2px}
 .topbar .avatar-small{width:30px;height:30px}
 .topbar .breadcrumb-root{display:none}
}

```
