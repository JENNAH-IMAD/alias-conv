'use client';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { usePathname } from 'next/navigation';
import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import {
  Archive,
  History,
  LayoutDashboard,
  ContactRound,
  Landmark,
  Wallet,
  FileOutput,
  ArrowRightLeft,
  Menu,
  X,
  ChevronRight,
  Plus,
  RefreshCw,
  Users,
  LockKeyhole,
  Settings2,
} from 'lucide-react';
import { useAuth, Can } from './auth-context';
import { ThemeMenu } from './theme-menu';
import { Logo } from './logo';
import Image from 'next/image';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';
const groups = [
  {
    name: 'ESPACE DE TRAVAIL',
    items: [
      { href: '/dashboard', label: 'Vue d’ensemble', icon: LayoutDashboard },
      { href: '/client-workspace', label: 'Espace clients', icon: Archive },
      { href: '/history', label: 'Historique', icon: History },
      { href: '/conversions', label: 'Conversion & export', icon: ArrowRightLeft },
    ],
  },
  {
    name: 'RÉFÉRENTIELS',
    items: [
      { href: '/clients', label: 'Clients', icon: ContactRound },
      { href: '/banks', label: 'Banques', icon: Landmark },
      { href: '/bank-accounts', label: 'Comptes bancaires', icon: Wallet },
      { href: '/templates', label: 'Modèles d’exportation', icon: FileOutput },
      { href: '/users', label: 'Utilisateurs & permissions', icon: Users },
      { href: '/settings', label: 'Configuration OCR', icon: Settings2 },
    ],
  },
];
function subscribeSidebar(listener: () => void) {
  window.addEventListener('storage', listener);
  window.addEventListener('sidebar-change', listener);
  return () => {
    window.removeEventListener('storage', listener);
    window.removeEventListener('sidebar-change', listener);
  };
}
let sidebarFallback = false;
let sidebarStorageUnavailable = false;
function sidebarSnapshot() {
  if (sidebarStorageUnavailable) return sidebarFallback;
  try {
    return localStorage.getItem('alias-sidebar') === 'compact';
  } catch {
    sidebarStorageUnavailable = true;
    return sidebarFallback;
  }
}
export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { user, can } = useAuth();
  const routePermission: Record<string, string> = {
    '/dashboard': 'dashboard.read',
    '/clients': 'clients.read',
    '/banks': 'banks.read',
    '/bank-accounts': 'accounts.read',
    '/templates': 'templates.read',
    '/client-workspace': 'documents.read',
    '/conversions': 'documents.read',
    '/settings': 'admin',
    '/history': 'documents.read',
  };
  const allowed = ['/users', '/settings'].includes(pathname)
    ? user?.role === 'Admin'
    : !routePermission[pathname] || can(routePermission[pathname]);
  const visibleGroups = groups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) =>
        ['/users', '/settings'].includes(item.href)
          ? user?.role === 'Admin'
          : can(routePermission[item.href]),
      ),
    }))
    .filter((group) => group.items.length);
  const drawer = useRef<HTMLDialogElement>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const compact = useSyncExternalStore(subscribeSidebar, sidebarSnapshot, () => false);
  const toggleSidebar = () => {
    sidebarFallback = !compact;
    try {
      localStorage.setItem('alias-sidebar', compact ? 'standard' : 'compact');
    } catch {
      sidebarStorageUnavailable = true;
    }
    window.dispatchEvent(new Event('sidebar-change'));
  };
  useEffect(() => {
    const desktop = window.matchMedia('(min-width: 851px)');
    const closeMobileMenu = () => {
      if (desktop.matches) drawer.current?.close();
    };
    desktop.addEventListener('change', closeMobileMenu);
    return () => desktop.removeEventListener('change', closeMobileMenu);
  }, []);
  const active = groups.flatMap((g) => g.items).find((i) => i.href === pathname);
  const navigation = (mobile = false) => (
    <>
      <div className="sidebar-header">
        <Link
          href="/dashboard"
          className="brand"
          aria-label="Alias — Accueil"
          onClick={() => drawer.current?.close()}
        >
          <Logo />
          <span className="sidebar-brand-icon" aria-hidden="true">
            <Image src="/alias-icon.svg" alt="" width={36} height={36} />
          </span>
        </Link>
        <p className="sidebar-tagline">Relevés & comptabilité</p>
        {mobile ? (
          <button
            type="button"
            className="icon-button drawer-close"
            aria-label="Fermer le menu"
            onClick={() => drawer.current?.close()}
          >
            <X size={20} aria-hidden="true" />
          </button>
        ) : (
          <button
            type="button"
            className="icon-button sidebar-toggle"
            aria-label={compact ? 'Déplier le menu' : 'Réduire le menu'}
            aria-expanded={!compact}
            aria-controls="desktop-navigation"
            title={compact ? 'Menu standard' : 'Menu compact'}
            onClick={toggleSidebar}
          >
            <svg
              width="22"
              height="22"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path className="sidebar-toggle-arrow" d="m14 6-6 6 6 6" />
            </svg>
          </button>
        )}
      </div>
      <nav
        id={mobile ? 'mobile-navigation' : 'desktop-navigation'}
        aria-label="Navigation principale"
        className="sidebar-nav"
        tabIndex={0}
      >
        {visibleGroups.map((g) => (
          <div key={g.name}>
            <p className="nav-caption">{g.name}</p>
            <div>
              {g.items.map(({ href, label, icon: Icon }) => (
                <Link
                  key={href}
                  href={href}
                  aria-label={label}
                  title={compact && !mobile ? label : undefined}
                  aria-current={pathname === href ? 'page' : undefined}
                  className={`nav-link ${pathname === href ? 'active' : ''}`}
                  onClick={() => drawer.current?.close()}
                >
                  <span className="nav-icon" aria-hidden="true">
                    <Icon size={19} strokeWidth={1.8} />
                  </span>
                  <span className="nav-label">{label}</span>
                  {pathname === href && (
                    <ChevronRight size={15} className="nav-chevron ml-auto" aria-hidden="true" />
                  )}
                </Link>
              ))}
            </div>
          </div>
        ))}
      </nav>
    </>
  );
  return (
    <div
      className={`app-shell ${compact ? 'sidebar-compact' : ''} ${pathname === '/conversions' ? 'app-shell-browser-scroll' : ''}`}
    >
      <a href="#main-content" className="skip-link">
        Aller au contenu
      </a>
      <aside className="desktop-sidebar">{navigation()}</aside>
      <dialog
        ref={drawer}
        id="mobile-menu"
        aria-label="Navigation principale"
        className="mobile-drawer"
        onClose={() => setDrawerOpen(false)}
      >
        {navigation(true)}
      </dialog>
      <div className="app-body">
        <header className="topbar">
          <nav
            aria-label="Fil d’Ariane"
            className="topbar-breadcrumb flex min-w-0 items-center gap-3"
          >
            <button
              className="icon-button mobile-menu"
              aria-label="Ouvrir le menu"
              aria-controls="mobile-menu"
              aria-expanded={drawerOpen}
              onClick={() => {
                drawer.current?.showModal();
                setDrawerOpen(true);
              }}
            >
              <Menu size={21} />
            </button>
            <span className="breadcrumb-root">Espace de travail</span>
            <ChevronRight size={14} className="breadcrumb-root" />
            <span className="truncate font-medium">{active?.label || 'Alias'}</span>
          </nav>
          <div className="topbar-actions flex shrink-0 items-center gap-3">
            <Button
              variant="outline"
              size="icon"
              aria-label="Actualiser la page"
              title="Actualiser la page"
              onClick={() => {
                if (confirmDiscard(hasUnsavedChanges())) window.location.reload();
              }}
            >
              <RefreshCw size={17} />
            </Button>
            <Can permission="documents.write">
              <Button asChild className="primary-action navbar-import-button">
                <Link
                  href="/conversions?import=1"
                  aria-label="Importer un PDF"
                  onClick={(event) => {
                    if (!confirmDiscard(hasUnsavedChanges())) event.preventDefault();
                  }}
                >
                  <Plus size={17} />
                  <span className="navbar-import-label">Importer un PDF</span>
                  <span className="navbar-import-short" aria-hidden="true">
                    PDF
                  </span>
                </Link>
              </Button>
            </Can>
            <ThemeMenu />
          </div>
        </header>
        <div
          className="app-scroll"
          key={pathname}
          tabIndex={0}
          role="region"
          aria-label="Contenu de la page"
        >
          <main id="main-content" className="main-content" tabIndex={-1}>
            {allowed ? (
              children
            ) : (
              <section className="ui-card">
                <LockKeyhole className="accent-text mb-4" />
                <h1 className="ui-title">Accès limité</h1>
                <p className="muted mt-3">
                  Votre profil ne permet pas de consulter cette page. Contactez un administrateur
                  pour modifier vos permissions.
                </p>
              </section>
            )}
          </main>
          <footer className="app-footer">
            <span>
              Alias <span className="footer-divider">/</span> Gestion des relevés bancaires
            </span>
          </footer>
        </div>
      </div>
    </div>
  );
}
