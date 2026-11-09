'use client';
import { useSyncExternalStore } from 'react';
import { ThemeProvider as NextThemeProvider, useTheme as useNextTheme } from 'next-themes';
const subscribe = () => () => {};
export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return (
    <NextThemeProvider
      attribute="data-theme"
      storageKey="bank-converter-theme"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      <div className="theme-root">{children}</div>
    </NextThemeProvider>
  );
}
export function useTheme() {
  const { theme, resolvedTheme, setTheme } = useNextTheme();
  const mounted = useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
  const isLightMode = mounted && resolvedTheme === 'light';
  return {
    theme: mounted ? theme : undefined,
    setTheme,
    isLightMode,
  };
}
