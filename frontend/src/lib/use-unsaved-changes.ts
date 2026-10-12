'use client';

import { useEffect } from 'react';
let pendingEditors = 0;
export function hasUnsavedChanges() {
  return pendingEditors > 0;
}

export function confirmDiscard(changed: boolean) {
  return (
    !changed ||
    window.confirm('Des corrections ne sont pas enregistrées. Quitter sans les enregistrer ?')
  );
}

export function useUnsavedChanges(changed: boolean) {
  useEffect(() => {
    if (!changed) return;
    pendingEditors++;
    const unload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    const navigate = (event: MouseEvent) => {
      if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
        return;
      const link =
        event.target instanceof Element ? event.target.closest<HTMLAnchorElement>('a[href]') : null;
      if (!link || link.target === '_blank' || link.hasAttribute('download')) return;
      const destination = new URL(link.href, window.location.href);
      if (
        destination.pathname === window.location.pathname &&
        destination.search === window.location.search
      )
        return;
      if (!confirmDiscard(true)) {
        event.preventDefault();
        event.stopPropagation();
      }
    };
    window.addEventListener('beforeunload', unload);
    document.addEventListener('click', navigate, true);
    return () => {
      pendingEditors--;
      window.removeEventListener('beforeunload', unload);
      document.removeEventListener('click', navigate, true);
    };
  }, [changed]);
}
