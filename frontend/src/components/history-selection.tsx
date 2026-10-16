'use client';
import { Fragment, ReactNode, useState } from 'react';
import { Trash2 } from 'lucide-react';
import { useAuth } from './auth-context';
import { BankDialog } from './bank-dialogs';
import { Button } from './ui/button';
import { request } from '@/services/api';

type Selection = { all: boolean; ids: string[]; excludeIds: string[] };
export function HistorySelection<T extends { id: string }>({
  items,
  total,
  loading,
  kind,
  filters = {},
  onDeleted,
  children,
}: {
  items: T[];
  total: number;
  loading: boolean;
  kind: 'statements' | 'profiles';
  filters?: Record<string, string>;
  onDeleted: (count: number) => void;
  children: (item: T, controls: ReactNode) => ReactNode;
}) {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';
  const [all, setAll] = useState(false);
  // Explicit selections, or exclusions when all matching pages are selected.
  const [ids, setIds] = useState(new Set<string>());
  const [pending, setPending] = useState<{ selection: Selection; count: number } | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const selected = (id: string) => (all ? !ids.has(id) : ids.has(id));
  const count = all ? Math.max(0, total - ids.size) : ids.size;
  const pageSelected = items.length > 0 && items.every((item) => selected(item.id));
  const pagePartial = !pageSelected && items.some((item) => selected(item.id));
  function toggle(id: string) {
    setIds((previous) => {
      const next = new Set(previous);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }
  function choose(selection: Selection, size: number) {
    setError('');
    setMessage('');
    setPending({ selection, count: size });
  }
  async function remove() {
    if (!pending || busy || !isAdmin) return;
    setBusy(true);
    setError('');
    try {
      const result = await request<{ deleted: number }>(`/history/${kind}/delete-selection`, {
        method: 'POST',
        body: JSON.stringify({ ...pending.selection, ...filters }),
      });
      setPending(null);
      setAll(false);
      setIds(new Set());
      setMessage(`${result.deleted} action(s) supprimée(s) de l’historique.`);
      onDeleted(result.deleted);
    } catch (cause) {
      setError((cause as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      {isAdmin && total > 0 && (
        <div className="history-selection-bar">
          <label className="history-selection-check">
            <input
              type="checkbox"
              checked={pageSelected}
              ref={(element) => {
                if (element) element.indeterminate = pagePartial;
              }}
              disabled={loading || busy || !!pending}
              onChange={() =>
                setIds((previous) => {
                  const next = new Set(previous);
                  for (const item of items) {
                    if (all ? pageSelected : !pageSelected) next.add(item.id);
                    else next.delete(item.id);
                  }
                  return next;
                })
              }
            />{' '}
            Sélectionner cette page
          </label>
          <Button
            variant="outline"
            size="sm"
            disabled={loading || busy || !!pending}
            onClick={() => {
              setAll(true);
              setIds(new Set());
            }}
          >
            Tout sélectionner ({total})
          </Button>
          {count > 0 && (
            <>
              <span className="muted text-sm" role="status">
                {count} action(s) sélectionnée(s){all ? ' sur toutes les pages' : ''}
              </span>
              <Button
                variant="ghost"
                size="sm"
                disabled={busy || !!pending}
                onClick={() => {
                  setAll(false);
                  setIds(new Set());
                }}
              >
                Désélectionner
              </Button>
              <Button
                variant="danger"
                size="sm"
                disabled={loading || busy || !!pending}
                onClick={() =>
                  choose({ all, ids: all ? [] : [...ids], excludeIds: all ? [...ids] : [] }, count)
                }
              >
                <Trash2 size={14} /> Supprimer la sélection
              </Button>
            </>
          )}
        </div>
      )}
      {message && (
        <p role="status" className="muted text-sm">
          {message}
        </p>
      )}
      {items.map((item) => (
        <Fragment key={item.id}>
          {children(
            item,
            isAdmin ? (
              <div className="history-row-actions">
                <input
                  type="checkbox"
                  aria-label="Sélectionner cette action"
                  checked={selected(item.id)}
                  disabled={busy || !!pending}
                  onChange={() => toggle(item.id)}
                />
                <Button
                  variant="danger"
                  size="sm"
                  aria-label="Supprimer cette action"
                  disabled={busy || !!pending}
                  onClick={() => choose({ all: false, ids: [item.id], excludeIds: [] }, 1)}
                >
                  <Trash2 size={14} />
                  Supprimer
                </Button>
              </div>
            ) : null,
          )}
        </Fragment>
      ))}
      {isAdmin && pending && (
        <BankDialog title="Supprimer de l’historique" busy={busy} onClose={() => setPending(null)}>
          <p>
            Supprimer définitivement {pending.count} action(s) de l’historique
            {pending.selection.all ? ', sur toutes les pages de la liste actuelle' : ''} ?
          </p>
          <p className="muted text-sm mt-2">
            Les relevés, PDF, modèles et fichiers exportés sont conservés.
          </p>
          {error && (
            <p role="alert" className="text-destructive mt-3">
              {error}
            </p>
          )}
          <div className="flex flex-wrap justify-end gap-3 mt-6">
            <Button variant="outline" disabled={busy} onClick={() => setPending(null)}>
              Annuler
            </Button>
            <Button variant="danger" disabled={busy} onClick={() => void remove()}>
              {busy ? 'Suppression…' : 'Confirmer la suppression'}
            </Button>
          </div>
        </BankDialog>
      )}
    </>
  );
}
