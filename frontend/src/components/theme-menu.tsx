'use client';
import Link from 'next/link';
import { DropdownMenu } from 'radix-ui';
import { ChevronDown, CircleUserRound, LoaderCircle, LogOut, Moon, Sun, UserRound } from 'lucide-react';
import { useTheme } from './theme-context';
import { useAuth } from './auth-context';

export function ThemeToggle() {
  const { setTheme, isLightMode } = useTheme();
  const nextTheme = isLightMode ? 'dark' : 'light';
  const LabelIcon = isLightMode ? Moon : Sun;
  return (
    <button
      type="button"
      className="alias-menu-trigger mini-menu-trigger appearance-toggle"
      aria-label={`Activer le mode ${isLightMode ? 'sombre' : 'clair'}`}
      title={`Activer le mode ${isLightMode ? 'sombre' : 'clair'}`}
      onClick={() => setTheme(nextTheme)}
    >
      <LabelIcon size={17} aria-hidden="true" />
    </button>
  );
}

export function ThemeMenu() {
  const { user, logout, loggingOut, logoutError } = useAuth();
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        type="button"
        className="alias-menu-trigger mini-menu-trigger account-menu-trigger"
        aria-label="Ouvrir le menu du compte"
        title="Mon compte"
      >
        <span className="account-menu-avatar" aria-hidden="true">
          {user?.name.trim().charAt(0).toLocaleUpperCase() || <CircleUserRound size={17} />}
        </span>
        <ChevronDown size={12} aria-hidden="true" />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          className="alias-theme-menu mini-theme-menu"
          aria-label="Menu du compte"
          align="end"
          sideOffset={8}
          collisionPadding={12}
        >
          {user && (
            <DropdownMenu.Label className="account-menu-identity">
              <strong>{user.name}</strong>
              <span>{user.email}</span>
            </DropdownMenu.Label>
          )}
          <DropdownMenu.Separator className="alias-menu-separator" />
          <DropdownMenu.Item asChild className="account-menu-item">
            <Link href="/profile">
              <UserRound size={16} aria-hidden="true" />
              Gérer mon profil
            </Link>
          </DropdownMenu.Item>
          {user && (
            <>
              <DropdownMenu.Separator className="alias-menu-separator" />
              <DropdownMenu.Item
                className="mini-logout-item"
                disabled={loggingOut}
                onSelect={(event) => {
                  event.preventDefault();
                  void logout();
                }}
              >
                {loggingOut ? (
                  <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />
                ) : (
                  <LogOut size={16} aria-hidden="true" />
                )}
                {loggingOut ? 'Déconnexion en cours' : 'Se déconnecter'}
              </DropdownMenu.Item>
              {logoutError && (
                <p role="alert" className="mini-menu-error">
                  {logoutError}
                </p>
              )}
            </>
          )}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
