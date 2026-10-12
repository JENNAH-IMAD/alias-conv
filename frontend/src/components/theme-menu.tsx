'use client';
import { DropdownMenu } from 'radix-ui';
import { Check, ChevronDown, LogOut, LoaderCircle, Moon, Sun } from 'lucide-react';
import Image from 'next/image';
import { useTheme } from './theme-context';
import { useAuth } from './auth-context';

export function ThemeMenu() {
  const { isLightMode, setTheme } = useTheme();
  const { logout, loggingOut, logoutError } = useAuth();
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        className="alias-menu-trigger mini-menu-trigger"
        aria-label="Apparence et session"
        title="Apparence et session"
      >
        <Image src="/alias-icon.svg" alt="" width={20} height={20} />
        <ChevronDown size={12} aria-hidden="true" />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          className="alias-theme-menu mini-theme-menu"
          aria-label="Apparence et session"
          align="end"
          sideOffset={8}
          collisionPadding={12}
        >
          <DropdownMenu.Label className="alias-menu-heading">Apparence</DropdownMenu.Label>
          <DropdownMenu.RadioGroup value={isLightMode ? 'light' : 'dark'} onValueChange={setTheme}>
            {[
              { value: 'light', label: 'Clair', icon: Sun },
              { value: 'dark', label: 'Sombre', icon: Moon },
            ].map(({ value, label, icon: Icon }) => (
              <DropdownMenu.RadioItem
                key={value}
                value={value}
                className="alias-theme-item"
                onSelect={(event) => event.preventDefault()}
              >
                <span className="alias-theme-icon">
                  <Icon size={16} aria-hidden="true" />
                </span>
                <strong>{label}</strong>
                <DropdownMenu.ItemIndicator className="alias-theme-check">
                  <Check size={15} aria-hidden="true" />
                </DropdownMenu.ItemIndicator>
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>
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
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
