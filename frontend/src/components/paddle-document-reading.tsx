'use client';
import { useState } from 'react';
import { Download, FileJson, Table2, FileText } from 'lucide-react';
import { PdfTextDocument } from '@/services/statement-exports';
import { Button } from './ui/button';

export function PaddleDocumentReading({
  document,
  pageIndex,
}: {
  document: PdfTextDocument;
  pageIndex: number;
}) {
  const [view, setView] = useState<'tables' | 'markdown' | 'json'>('tables');
  const page = document.pages[pageIndex];
  function download(format: 'json' | 'md') {
    const content =
      format === 'json'
        ? JSON.stringify({ layoutParsingResults: document.pages.map((p) => p.rawResult) }, null, 2)
        : document.pages.map((p) => p.markdown || '').join('\n\n---\n\n');
    const url = URL.createObjectURL(
      new Blob([content], {
        type: format === 'json' ? 'application/json' : 'text/markdown;charset=utf-8',
      }),
    );
    const link = window.document.createElement('a');
    link.href = url;
    link.download = `Alias-extraction.${format}`;
    window.document.body.appendChild(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 10000);
  }
  return (
    <div className="space-y-4 min-w-0">
      <div className="flex flex-wrap items-center gap-2">
        {(
          [
            ['tables', 'Tableaux', Table2],
            ['markdown', 'Markdown', FileText],
            ['json', 'JSON', FileJson],
          ] as const
        ).map(([key, label, Icon]) => (
          <Button
            key={key}
            variant={view === key ? 'default' : 'outline'}
            type="button"
            aria-pressed={view === key}
            onClick={() => setView(key)}
          >
            <Icon size={16} />
            {label}
          </Button>
        ))}
        <div className="flex flex-wrap gap-2 sm:ml-auto">
          <Button
            permission="documents.export"
            type="button"
            variant="outline"
            onClick={() => download('json')}
          >
            <Download size={14} />
            Extraire le JSON
          </Button>
          <Button
            permission="documents.export"
            type="button"
            variant="outline"
            onClick={() => download('md')}
          >
            <Download size={14} />
            Extraire le Markdown
          </Button>
        </div>
      </div>
      {view === 'tables' && (
        <div className="space-y-4" aria-label="Tableaux extraits par PaddleOCR">
          {page.tables?.length ? (
            page.tables.map((table, index) => (
              <div
                className="conversion-table-scroll max-h-[32rem]"
                tabIndex={0}
                key={index}
                aria-label={`Tableau ${index + 1} de la page ${page.number}`}
              >
                <table className="conversion-table">
                  <thead>
                    <tr>
                      {Array.from(
                        { length: Math.max(...table.rows.map((r) => r.length)) },
                        (_, column) => (
                          <th key={column}>Colonne {column + 1}</th>
                        ),
                      )}
                    </tr>
                  </thead>
                  <tbody>
                    {table.rows.map((row, r) => (
                      <tr key={r}>
                        {row.map((cell, c) => (
                          <td key={c} className="whitespace-pre-wrap min-w-28">
                            {cell}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))
          ) : (
            <p className="conversion-alert warning">
              Aucun tableau structuré n’a été reconnu sur cette page. Consultez le Markdown et le
              PDF original.
            </p>
          )}
        </div>
      )}
      {view === 'markdown' && (
        <pre
          className="conversion-details p-4 max-h-[32rem] overflow-auto whitespace-pre-wrap break-words text-sm"
          tabIndex={0}
          aria-label="Markdown extrait"
        >
          {page.markdown || 'Aucun Markdown disponible.'}
        </pre>
      )}
      {view === 'json' && (
        <pre
          className="conversion-details p-4 max-h-[32rem] overflow-auto text-xs"
          tabIndex={0}
          aria-label="JSON PaddleOCR brut"
        >
          {JSON.stringify(page.rawResult, null, 2)}
        </pre>
      )}
      <p className="muted text-xs">
        Lecture brute de PaddleOCR-VL-1.6, avant adaptation bancaire. Les téléchargements
        contiennent toutes les pages. Les montants des cellules débit/crédit servent à l’export ;
        les montants dans les libellés restent du texte.
      </p>
    </div>
  );
}
