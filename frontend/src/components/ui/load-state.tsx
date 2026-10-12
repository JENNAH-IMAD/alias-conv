import { Alert, AlertTitle, AlertDescription } from './alert';
import { Skeleton } from './skeleton';
import { Inbox } from 'lucide-react';
export function LoadState({
  error,
  loading,
  empty,
}: {
  error: string;
  loading: boolean;
  empty?: boolean;
}) {
  return (
    <>
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Les données ne sont pas disponibles</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      {loading && (
        <div className="space-y-3 rounded-xl border border-border bg-card p-5" role="status">
          <span className="sr-only">Chargement des données…</span>
          <Skeleton className="h-4 w-1/3" />
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      )}
      {!loading && !error && empty && (
        <div className="load-state flex-col">
          <Inbox size={28} />
          <p>Aucun élément à afficher.</p>
          <p className="text-xs">Ajoutez un élément ou ajustez vos filtres pour commencer.</p>
        </div>
      )}
    </>
  );
}
