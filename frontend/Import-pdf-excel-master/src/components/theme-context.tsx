'use client';

import { useSyncExternalStore } from 'react';
import { ThemeProvider as NextThemeProvider, useTheme as useNextTheme } from 'next-themes';

const subscribe = () => () => {};

function ThemeRoot({ children }: { children: React.ReactNode }) {
  const { resolvedTheme } = useNextTheme();
  const mounted = useSyncExternalStore(subscribe, () => true, () => false);
  return <div className="theme-root" data-theme={mounted ? resolvedTheme : undefined}>{children}</div>;
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return <NextThemeProvider attribute="data-theme" storageKey="bank-converter-theme" defaultTheme="system" enableSystem disableTransitionOnChange>
    <ThemeRoot>{children}</ThemeRoot>
  </NextThemeProvider>;
}

export function useTheme() {
  const { theme, resolvedTheme, setTheme } = useNextTheme();
  const mounted = useSyncExternalStore(subscribe, () => true, () => false);
  const isLightMode = mounted && resolvedTheme === 'light';
  return { theme: mounted ? theme : undefined, setTheme, isLightMode, toggleTheme: () => setTheme(isLightMode ? 'dark' : 'light') };
}
