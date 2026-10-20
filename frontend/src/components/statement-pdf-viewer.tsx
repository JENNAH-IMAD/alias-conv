'use client';
import { useEffect, useRef, useState } from 'react';
import type { PDFDocumentProxy, RenderTask } from 'pdfjs-dist';
import { ChevronLeft, ChevronRight, ZoomIn, ZoomOut, RotateCcw } from 'lucide-react';
import { API_URL } from '@/services/api';
import { useApi } from '@/services/use-api';
import type { PdfTextDocument } from '@/services/statement-exports';
import type { PdfSelection } from '@/lib/pdf-selection';
import { Button } from './ui/button';

type Box = { text: string; left: number; top: number; width: number; height: number };
export function StatementPdfViewer({
  id,
  hasExtraction,
  onSelection,
  sourcePath,
  readOnly = false,
  selectable = true,
}: {
  id: string;
  hasExtraction: boolean;
  onSelection: (selection: PdfSelection | null) => void;
  sourcePath?: string;
  readOnly?: boolean;
  selectable?: boolean;
}) {
  const documentPath = sourcePath ?? `/statement-imports/${id}/original`;
  const raw = useApi<PdfTextDocument>(hasExtraction ? `/statement-imports/${id}/extraction` : null);
  const [pdf, setPdf] = useState<PDFDocumentProxy | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [zoom, setZoom] = useState(1);
  const [width, setWidth] = useState(500);
  const [boxes, setBoxes] = useState<Box[]>([]);
  const [selected, setSelected] = useState<number[]>([]);
  const [size, setSize] = useState({ width: 500, height: 707 });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const canvas = useRef<HTMLCanvasElement>(null);
  const container = useRef<HTMLDivElement>(null);
  useEffect(() => {
    let frame = 0;
    const observer = new ResizeObserver(([entry]) => {
      // contentRect already excludes padding. Hidden tabs must keep their last useful width.
      if (entry.contentRect.width < 1) return;
      const nextWidth = Math.max(1, Math.floor(entry.contentRect.width));
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() =>
        setWidth((current) => (current === nextWidth ? current : nextWidth)),
      );
    });
    if (container.current) observer.observe(container.current);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    };
  }, []);
  useEffect(() => {
    let disposed = false;
    let task: ReturnType<(typeof import('pdfjs-dist'))['getDocument']> | undefined;
    async function load() {
      try {
        const library = await import('pdfjs-dist');
        if (disposed) return;
        library.GlobalWorkerOptions.workerSrc = '/pdfjs/pdf.worker.min.mjs';
        task = library.getDocument({
          url: `${API_URL}${documentPath}`,
          withCredentials: true,
          cMapUrl: '/pdfjs/cmaps/',
          cMapPacked: true,
          standardFontDataUrl: '/pdfjs/standard_fonts/',
          wasmUrl: '/pdfjs/wasm/',
        });
        const document = await task.promise;
        if (!disposed) {
          setPdf(document);
          setError('');
        }
      } catch {
        if (!disposed) {
          setError('Impossible d’afficher le PDF. Réessayez ou ouvrez-le dans un nouvel onglet.');
          setLoading(false);
        }
      }
    }
    void load();
    return () => {
      disposed = true;
      void task?.destroy();
    };
  }, [documentPath, retry]);
  useEffect(() => {
    if (!pdf || raw.loading) return;
    let disposed = false;
    let render: RenderTask | undefined;
    async function draw() {
      setLoading(true);
      try {
        const page = await pdf!.getPage(pageNumber);
        if (disposed || !canvas.current) return;
        const base = page.getViewport({ scale: 1 });
        const viewport = page.getViewport({ scale: (width / base.width) * zoom });
        const ratio = Math.min(window.devicePixelRatio || 1, 2);
        // Render separately so a zoom, resize or page change does not clear the visible PDF.
        const buffer = document.createElement('canvas');
        buffer.width = Math.ceil(viewport.width * ratio);
        buffer.height = Math.ceil(viewport.height * ratio);
        render = page.render({ canvas: buffer, viewport, transform: [ratio, 0, 0, ratio, 0, 0] });
        await render.promise;
        if (disposed || !canvas.current) return;
        const element = canvas.current;
        const context = element.getContext('2d');
        if (!context) throw new Error('PDF canvas unavailable');
        element.width = buffer.width;
        element.height = buffer.height;
        context.drawImage(buffer, 0, 0);
        setSize({ width: viewport.width, height: viewport.height });
        const extracted = raw.data?.pages.find((p) => p.number === pageNumber);
        let regions: Box[];
        if (readOnly || !selectable) {
          regions = [];
        } else if (extracted?.boxes.length) {
          regions = extracted.boxes.map((box) => ({
            text: box.text,
            left: (box.left / extracted.width) * 100,
            top: (box.top / extracted.height) * 100,
            width: ((box.right - box.left) / extracted.width) * 100,
            height: ((box.bottom - box.top) / extracted.height) * 100,
          }));
        } else {
          const content = await page.getTextContent();
          const { Util } = await import('pdfjs-dist');
          regions = content.items.flatMap((item) => {
            if (!('str' in item) || !item.str.trim()) return [];
            const matrix = Util.transform(base.transform, item.transform);
            const height = Math.hypot(matrix[2], matrix[3]);
            return [
              {
                text: item.str,
                left: (matrix[4] / base.width) * 100,
                top: ((matrix[5] - height) / base.height) * 100,
                width: (item.width / base.width) * 100,
                height: (height / base.height) * 100,
              },
            ];
          });
        }
        if (!disposed) {
          setBoxes(regions);
          setLoading(false);
          setError('');
        }
      } catch (cause) {
        if (!disposed && (cause as Error).name !== 'RenderingCancelledException') {
          setError(
            'Cette page ne peut pas être affichée. Ouvrez le PDF original pour la consulter.',
          );
          setLoading(false);
        }
      }
    }
    void draw();
    return () => {
      disposed = true;
      render?.cancel();
    };
  }, [pdf, pageNumber, width, zoom, raw.data, raw.loading, readOnly, selectable]);
  function choose(index: number) {
    const indices = selected.includes(index)
      ? selected.filter((i) => i !== index)
      : [...selected, index];
    setSelected(indices);
    const picked = indices
      .map((i) => boxes[i])
      .sort((a, b) => (Math.abs(a.top - b.top) < 0.7 ? a.left - b.left : a.top - b.top));
    onSelection(
      picked.length
        ? {
            text: picked.map((box) => box.text).join(' '),
            page: pageNumber,
            left: Math.min(...picked.map((b) => b.left)),
            top: Math.min(...picked.map((b) => b.top)),
            bottom: Math.max(...picked.map((b) => b.top + b.height)),
          }
        : null,
    );
  }
  function navigate(page: number) {
    setPageNumber(page);
    setSelected([]);
    onSelection(null);
  }
  return (
    <section
      className="statement-pdf-viewer"
      aria-busy={loading}
      aria-label={readOnly ? 'Relevé bancaire converti' : 'Relevé bancaire original'}
    >
      <div className="pdf-viewer-heading">
        <div>
          <h2>{readOnly ? 'Votre relevé converti' : 'Votre relevé original'}</h2>
          <p>
            {readOnly
              ? 'Copie structurée du relevé source, ou rapport des mouvements sélectionnés.'
              : selectable
                ? 'Cliquez sur un texte pour le sélectionner. Plusieurs clics combinent les valeurs.'
                : 'Consultez le document source. Corrigez les données directement dans le tableau.'}
          </p>
        </div>
      </div>
      <div className="pdf-viewer-toolbar">
        <Button
          type="button"
          size="icon-sm"
          variant="outline"
          aria-label="Page précédente du PDF"
          disabled={!pdf || pageNumber === 1 || loading}
          onClick={() => navigate(pageNumber - 1)}
        >
          <ChevronLeft />
        </Button>
        <span>
          Page {pageNumber} / {pdf?.numPages ?? '…'}
        </span>
        <Button
          type="button"
          size="icon-sm"
          variant="outline"
          aria-label="Page suivante du PDF"
          disabled={!pdf || pageNumber >= pdf.numPages || loading}
          onClick={() => navigate(pageNumber + 1)}
        >
          <ChevronRight />
        </Button>
        <Button
          type="button"
          size="icon-sm"
          variant="outline"
          aria-label="Réduire le zoom du PDF"
          disabled={zoom <= 0.75}
          onClick={() => setZoom((z) => Math.max(0.75, z - 0.25))}
        >
          <ZoomOut />
        </Button>
        <span>{Math.round(zoom * 100)} %</span>
        <Button
          type="button"
          size="icon-sm"
          variant="outline"
          aria-label="Agrandir le PDF"
          disabled={zoom >= 2}
          onClick={() => setZoom((z) => Math.min(2, z + 0.25))}
        >
          <ZoomIn />
        </Button>
        <Button
          type="button"
          size="icon-sm"
          variant="outline"
          aria-label="Ajuster le PDF à la largeur"
          onClick={() => setZoom(1)}
        >
          <RotateCcw />
        </Button>
      </div>
      {error && (
        <div role="alert" className="pdf-viewer-error">
          <p>{error}</p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              setPdf(null);
              setLoading(true);
              setRetry((r) => r + 1);
            }}
          >
            Réessayer
          </Button>
          <a href={`${API_URL}${documentPath}`} target="_blank" rel="noopener noreferrer">
            Ouvrir le PDF
          </a>
        </div>
      )}
      <div
        ref={container}
        className="pdf-viewer-scroll"
        tabIndex={0}
        aria-label="Document PDF avec sélection des textes"
      >
        {loading && !error && (
          <p role="status" className="pdf-viewer-loading pdf-render-status">
            Affichage du relevé…
          </p>
        )}
        <div className="pdf-page" style={{ width: size.width, height: size.height }}>
          <canvas
            ref={canvas}
            style={{ width: size.width, height: size.height }}
            aria-label={`Page ${pageNumber} du relevé bancaire`}
          />
          {!loading &&
            !error &&
            !readOnly &&
            selectable &&
            boxes.map((box, index) => (
              <button
                type="button"
                key={index}
                className={`pdf-text-region ${selected.includes(index) ? 'selected' : ''}`}
                style={{
                  left: `${box.left}%`,
                  top: `${box.top}%`,
                  width: `${Math.max(box.width, 0.5)}%`,
                  height: `${Math.max(box.height, 0.7)}%`,
                }}
                title={box.text}
                aria-label={`Sélectionner ${box.text}`}
                aria-pressed={selected.includes(index)}
                onClick={() => choose(index)}
              />
            ))}
        </div>
        {!readOnly && selectable && !loading && !error && !boxes.length && (
          <p className="pdf-viewer-loading">
            Aucun texte sélectionnable sur cette page. Vous pouvez saisir la valeur à corriger
            manuellement.
          </p>
        )}
      </div>
      {!readOnly && selectable && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={!selected.length}
          onClick={() => {
            setSelected([]);
            onSelection(null);
          }}
        >
          Effacer la sélection
        </Button>
      )}
    </section>
  );
}
