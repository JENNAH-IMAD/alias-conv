'use client';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { usePathname } from 'next/navigation';
import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from 'react';
import {
  FolderKanban,
  History,
  LayoutDashboard,
  Building2,
  FileSliders,
  ArrowRightLeft,
  Menu,
  X,
  ChevronRight,
  Plus,
  UsersRound,
  LockKeyhole,
  ScanText,
  NotebookPen,
} from 'lucide-react';
import { useAuth, Can } from './auth-context';
import { ThemeMenu, ThemeToggle } from './theme-menu';
import { Logo } from './logo';
import { PageHeader } from './page-header';
import Image from 'next/image';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';
const groups = [
  {
    name: 'ESPACE DE TRAVAIL',
    items: [
      { href: '/dashboard', label: 'Vue d’ensemble', icon: LayoutDashboard },
      { href: '/client-workspace', label: 'Espace clients', icon: FolderKanban },
      { href: '/conversions', label: 'Conversion & export', icon: ArrowRightLeft },
      { href: '/entry-models', label: 'Modèles d’écritures', icon: NotebookPen },
      { href: '/banks', label: 'Banques et comptes', icon: Building2 },
    ],
  },
  {
    name: 'RÉFÉRENTIELS',
    items: [
      { href: '/templates', label: 'Modèles d’exportation', icon: FileSliders },
      { href: '/history', label: 'Historique', icon: History },
      { href: '/users', label: 'Utilisateurs & permissions', icon: UsersRound },
      { href: '/settings', label: 'Configuration OCR', icon: ScanText },
    ],
  },
];
type NavigationGroup = (typeof groups)[number] & { items: (typeof groups)[number]['items'][number][] };

function SidebarNavigation({
  visibleGroups,
  pathname,
  bankFallbackHref,
  compact,
  mobile,
  onNavigate,
}: {
  visibleGroups: NavigationGroup[];
  pathname: string;
  bankFallbackHref: string;
  compact: boolean;
  mobile: boolean;
  onNavigate: () => void;
}) {
  const navRef = useRef<HTMLElement>(null);
  const indicatorRef = useRef<HTMLSpanElement>(null);
  const linksKey = visibleGroups.flatMap((group) => group.items.map((item) => item.href)).join('|');

  useLayoutEffect(() => {
    const nav = navRef.current;
    const indicator = indicatorRef.current;
    if (!nav || !indicator) return;

    const updateIndicator = () => {
      const activeLink = nav.querySelector<HTMLElement>('.nav-link.active');
      if (!activeLink) {
        indicator.style.visibility = 'hidden';
        return;
      }
      indicator.style.height = `${activeLink.offsetHeight}px`;
      indicator.style.transform = `translateY(${activeLink.offsetTop}px)`;
      indicator.style.visibility = 'visible';
    };

    updateIndicator();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', updateIndicator);
      return () => window.removeEventListener('resize', updateIndicator);
    }
    const observer = new ResizeObserver(updateIndicator);
    observer.observe(nav);
    nav.querySelectorAll('.nav-link').forEach((link) => observer.observe(link));
    return () => observer.disconnect();
  }, [pathname, linksKey, mobile, compact]);

  const isNavActive = (href: string) =>
    pathname === href || (href === '/banks' && ['/clients', '/bank-accounts'].includes(pathname));

  return (
    <nav
      ref={navRef}
      id={mobile ? 'mobile-navigation' : 'desktop-navigation'}
      aria-label="Navigation principale"
      className="sidebar-nav"
      tabIndex={0}
    >
      <span ref={indicatorRef} className="nav-active-indicator" aria-hidden="true" />
      {visibleGroups.map((group) => (
        <div key={group.name}>
          <p className="nav-caption">{group.name}</p>
          <div>
            {group.items.map(({ href, label, icon: Icon }) => (
              <Link
                key={href}
                href={href === '/banks' ? bankFallbackHref : href}
                aria-label={label}
                title={compact && !mobile ? label : undefined}
                aria-current={isNavActive(href) ? 'page' : undefined}
                className={`nav-link ${isNavActive(href) ? 'active' : ''}`}
                onClick={onNavigate}
              >
                <span className="nav-icon" aria-hidden="true">
                  <Icon size={19} strokeWidth={1.8} />
                </span>
                <span className="nav-label">{label}</span>
                {isNavActive(href) && (
                  <ChevronRight size={15} className="nav-chevron ml-auto" aria-hidden="true" />
                )}
              </Link>
            ))}
          </div>
        </div>
      ))}
    </nav>
  );
}
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
  const previousPathname = useRef(pathname);
  const { user, can } = useAuth();
  const routePermission: Record<string, string> = {
    '/dashboard': 'dashboard.read',
    '/clients': 'clients.read',
    '/banks': 'banks.read',
    '/bank-accounts': 'accounts.read',
    '/templates': 'templates.read',
    '/client-workspace': 'documents.read',
    '/conversions': 'documents.read',
    '/entry-models': 'documents.read',
    '/entry-model-results': 'documents.read',
    '/settings': 'admin',
    '/history': 'history.read',
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
          : item.href === '/banks'
            ? can('banks.read') || can('accounts.read') || can('clients.read')
            : can(routePermission[item.href]),
      ),
    }))
    .filter((group) => group.items.length);
  const drawer = useRef<HTMLDialogElement>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  useLayoutEffect(() => {
    if (previousPathname.current === pathname) return;
    previousPathname.current = pathname;
    const resetScroll = () => {
      window.scrollTo(0, 0);
      document.scrollingElement?.scrollTo(0, 0);
      document.querySelector<HTMLElement>('.app-scroll')?.scrollTo(0, 0);
    };
    resetScroll();
    const frame = window.requestAnimationFrame(resetScroll);
    return () => window.cancelAnimationFrame(frame);
  }, [pathname]);
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
  const active =
    pathname === '/clients'
      ? { label: 'Clients' }
      : pathname === '/banks'
        ? { label: 'Banques' }
        : pathname === '/bank-accounts'
          ? { label: 'Comptes bancaires' }
          : pathname === '/profile'
            ? { label: 'Mon profil' }
          : groups.flatMap((g) => g.items).find((i) => i.href === pathname);
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
      <SidebarNavigation
        visibleGroups={visibleGroups}
        pathname={pathname}
        bankFallbackHref={can('accounts.read') ? '/bank-accounts' : '/clients'}
        compact={compact}
        mobile={mobile}
        onNavigate={() => drawer.current?.close()}
      />
    </>
  );
  return (
    <div className={`app-shell app-shell-browser-scroll ${compact ? 'sidebar-compact' : ''}`}>
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
            <ThemeToggle />
            <ThemeMenu />
          </div>
        </header>
        <div className="app-scroll" role="region" aria-label="Contenu de la page">
          <main id="main-content" className="main-content workspace-route-content" tabIndex={-1}>
            {allowed ? (
              children
            ) : (
              <div className="ui-page">
                <PageHeader
                  eyebrow="Espace de travail"
                  title="Accès limité"
                  description="Votre profil ne permet pas de consulter cette page. Contactez un administrateur pour modifier vos permissions."
                  icon={LockKeyhole}
                />
              </div>
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
