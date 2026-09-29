'use client';
import { Monitor, Moon, Sun } from 'lucide-react';
import { useTheme } from './theme-context';

export function ThemeSwitch() {
  const { theme, setTheme } = useTheme();
  return <div className="theme-switch" role="group" aria-label="Apparence">
    {([{ value: 'light', label: 'clair', icon: Sun }, { value: 'dark', label: 'sombre', icon: Moon }, { value: 'system', label: 'système', icon: Monitor }] as const).map(({ value, label, icon: Icon }) =>
      <button key={value} type="button" aria-label={`Activer le thème ${label}`} title={`Thème ${label}`} aria-pressed={theme === value} onClick={() => setTheme(value)}><Icon size={17} aria-hidden="true" /></button>
    )}
  </div>;
}
