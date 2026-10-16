'use client';
import { useState } from 'react';
import { ChevronDown, FileText } from 'lucide-react';
import { StatementPdfViewer } from './statement-pdf-viewer';
import { Button } from './ui/button';

export function StatementPdfPanel({
  id,
  version,
  hasRows,
  unsaved = false,
  collapsed = false,
  onToggle,
  extractId,
}: {
  id: string;
  version: number;
  hasRows: boolean;
  unsaved?: boolean;
  collapsed?: boolean;
  onToggle?: () => void;
  extractId?: string;
}) {
  const [view, setView] = useState<'original' | 'converted'>('original');
  return (
    <aside
      className={`statement-pdf-panel ${collapsed ? 'pdf-collapsed' : ''}`}
      aria-label="Consultation et sélection du PDF"
    >
      {onToggle && (
        <button
          type="button"
          className="pdf-display-toggle"
          aria-expanded={!collapsed}
          aria-controls={`pdf-content-${id}`}
          onClick={onToggle}
        >
          <FileText size={18} /> Affichage des PDF <ChevronDown size={18} />
        </button>
      )}
      <div id={`pdf-content-${id}`} hidden={collapsed}>
        <div className="pdf-view-switch" role="group" aria-label="Choisir le PDF à consulter">
          <Button
            type="button"
            size="sm"
            variant={view === 'original' ? 'default' : 'outline'}
            aria-pressed={view === 'original'}
            onClick={() => setView('original')}
          >
            PDF original
          </Button>
          <Button
            type="button"
            size="sm"
            variant={view === 'converted' ? 'default' : 'outline'}
            aria-pressed={view === 'converted'}
            disabled={!hasRows}
            onClick={() => setView('converted')}
          >
            PDF converti
          </Button>
        </div>
        {view === 'converted' && unsaved && (
          <p role="status" className="pdf-view-note">
            Enregistrez vos corrections pour actualiser le PDF converti. Cet aperçu reprend la
            dernière version enregistrée.
          </p>
        )}
        <div hidden={view !== 'original'}>
          <StatementPdfViewer
            id={id}
            hasExtraction={false}
            selectable={false}
            onSelection={() => {}}
          />
        </div>
        {view === 'converted' && (
          <StatementPdfViewer
            key={`${id}:${version}:${extractId ?? ''}`}
            id={id}
            hasExtraction={false}
            readOnly
            selectable={false}
            sourcePath={`/statement-imports/${id}/normalized-pdf?version=${version}&preview=true${extractId ? '&extractId=' + encodeURIComponent(extractId) : ''}`}
            onSelection={() => {}}
          />
        )}
      </div>
    </aside>
  );
}
