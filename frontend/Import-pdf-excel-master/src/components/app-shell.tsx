'use client';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useRef, useSyncExternalStore } from 'react';
import { Archive, LayoutDashboard, ArrowLeftRight, History, Users, Landmark, Wallet, FileOutput, ScanLine, Settings, Menu, X, LogOut, ChevronRight, Plus, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useAuth } from './auth-context';
import { ThemeSwitch } from './theme-switch';
import { BrandLogo } from './brand-logo';
import { can, pagePermission } from '@/lib/permissions';
import { Avatar } from './avatar';
const groups = [
 {name:'ESPACE DE TRAVAIL',items:[{href:'/dashboard',label:'Vue d’ensemble',icon:LayoutDashboard},{href:'/conversions',label:'Conversions',icon:ArrowLeftRight},{href:'/conversions/history',label:'Historique',icon:History},{href:'/client-workspace',label:'Espace clients',icon:Archive}]},
 {name:'RÉFÉRENTIELS',items:[{href:'/clients',label:'Clients',icon:Users},{href:'/banks',label:'Banques',icon:Landmark},{href:'/bank-accounts',label:'Comptes bancaires',icon:Wallet}]},
 {name:'CONFIGURATION',items:[{href:'/templates',label:'Modèles d’export',icon:FileOutput},{href:'/extraction-profiles',label:'Profils d’extraction',icon:ScanLine},{href:'/settings',label:'Paramètres',icon:Settings},{href:'/users',label:'Utilisateurs',icon:Users}]}
];
function subscribeSidebar(listener: () => void) {
 window.addEventListener('storage', listener); window.addEventListener('sidebar-change', listener);
 return () => { window.removeEventListener('storage', listener); window.removeEventListener('sidebar-change', listener); };
}
function sidebarSnapshot() { try { return localStorage.getItem('alias-sidebar') === 'compact'; } catch { return false; } }
export function AppShell({children}:{children:React.ReactNode}) {
 const pathname=usePathname(); const router=useRouter(); const {isAuthenticated,isReady,user,logout}=useAuth(); const drawer=useRef<HTMLDialogElement>(null);
 const compact = useSyncExternalStore(subscribeSidebar, sidebarSnapshot, () => false);
 const toggleSidebar = () => { try { localStorage.setItem('alias-sidebar', compact ? 'standard' : 'compact'); } catch {} window.dispatchEvent(new Event('sidebar-change')); };
 const compactButton = <button type="button" className="icon-button sidebar-toggle" aria-label={compact ? 'Déplier le menu' : 'Réduire le menu'} aria-expanded={!compact} title={compact ? 'Menu standard' : 'Menu compact'} onClick={toggleSidebar}>{compact ? <PanelLeftOpen size={19}/> : <PanelLeftClose size={19}/>}</button>;
 const authRoute=pathname==='/login'||pathname==='/register';
 useEffect(()=>{if(!isReady)return;if(!isAuthenticated&&!authRoute)router.replace('/login');else if(isAuthenticated&&authRoute)router.replace('/dashboard');},[isReady,isAuthenticated,authRoute,router]);
 const active=groups.flatMap(g=>g.items).find(i=>i.href===pathname);
 const navigation=<><Link href="/dashboard" className="brand" aria-label="Alias Convert PDF — Accueil" onClick={()=>drawer.current?.close()}><BrandLogo/></Link><div className="sidebar-mode">{compactButton}<span>Menu standard</span></div><nav aria-label="Navigation principale" className="sidebar-nav" tabIndex={0}>{groups.map(g=><div key={g.name}><p className="nav-caption">{g.name}</p>{g.items.filter(item=>!pagePermission(item.href)||can(user,pagePermission(item.href)!)).map(({href,label,icon:Icon})=><Link key={href} href={href} aria-label={label} title={compact ? label : undefined} aria-current={pathname===href?'page':undefined} className={`nav-link ${pathname===href?'active':''}`} onClick={()=>drawer.current?.close()}><Icon size={19} strokeWidth={1.7}/><span className="nav-label">{label}</span>{pathname===href&&<ChevronRight size={15} className="nav-chevron ml-auto"/>}</Link>)}</div>)}</nav><div className="sidebar-bottom"><div className="workspace-note"><ScanLine size={20}/><div><strong>Du relevé à l’écriture</strong><p>Importez, vérifiez et exportez.</p></div></div><Link href="/settings" className="sidebar-profile" aria-label="Mon profil et mes paramètres" title={compact ? user?.name : undefined} onClick={()=>drawer.current?.close()}><Avatar name={user?.name||'Utilisateur'}/><span className="min-w-0 flex-1"><strong className="block truncate">{user?.name}</strong><small className="muted">{user?.role==='Admin'?'Administrateur':'Utilisateur'}</small></span><Settings size={17}/></Link><button className="logout-button" aria-label="Se déconnecter" title={compact ? "Se déconnecter" : undefined} onClick={()=>{drawer.current?.close();logout();router.replace('/login');}}><LogOut size={16}/><span>Se déconnecter</span></button></div></>;
 if(authRoute)return <>{children}</>;
 if(!isReady||!isAuthenticated)return <div className="app-loading" role="status"><span className="loading-dot"/>Chargement de votre espace…</div>;
 return <div className={`app-shell ${compact ? "sidebar-compact" : ""}`}><a href="#main-content" className="skip-link">Aller au contenu</a><aside className="desktop-sidebar">{navigation}</aside><dialog ref={drawer} aria-label="Navigation principale" className="mobile-drawer"><button className="icon-button drawer-close" aria-label="Fermer le menu" onClick={()=>drawer.current?.close()}><X size={20}/></button>{navigation}</dialog><div className="app-body"><header className="topbar"><div className="flex min-w-0 items-center gap-3"><button className="icon-button mobile-menu" aria-label="Ouvrir le menu" onClick={()=>drawer.current?.showModal()}><Menu size={21}/></button><span className="breadcrumb-root">Espace de travail</span><ChevronRight size={14} className="breadcrumb-root"/><span className="truncate font-medium">{active?.label||'Alias Convert PDF'}</span></div><div className="flex shrink-0 items-center gap-3"><ThemeSwitch/>{can(user, 'documents.write') && <Button asChild className="primary-action hidden sm:inline-flex"><Link href="/conversions"><Plus size={17}/> Nouvelle conversion</Link></Button>}<Link href="/settings" aria-label="Mon profil"><Avatar name={user?.name||'Utilisateur'} small/></Link></div></header><div className="app-scroll" key={pathname} tabIndex={0} role="region" aria-label="Contenu de la page"><main id="main-content" className="main-content" tabIndex={-1}>{pagePermission(pathname) && !can(user, pagePermission(pathname)!) ? <div className="ui-page"><h1 className="ui-title">Accès réservé</h1><p className="ui-description">Vous ne disposez pas des permissions nécessaires. Contactez un administrateur.</p><Link href="/settings" className="accent-text">Mon profil et mes préférences</Link></div> : children}</main><footer className="app-footer"><span>Alias Informatique <span className="footer-divider">/</span> Gestion des relevés bancaires</span><Link href="/settings">Mon espace <ChevronRight size={13}/></Link></footer></div></div></div>;
}


