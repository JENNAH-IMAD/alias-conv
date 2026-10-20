'use client';
import { useEffect, useId, useRef, useState } from 'react';
import {
  BookOpen,
  ChevronUp,
  Copy,
  Download,
  ExternalLink,
  FileText,
  Sparkles,
} from 'lucide-react';
import { Button } from '@/components/ui/button';

type DocumentType = 'STATEMENT' | 'EXTRACT';
export function DashboardPromptActions() {
  return (
    <div className="overview-inline-prompts">
      <div className="overview-inline-prompt-grid">
        <section aria-label="Prompt relevé">
          <h2>
            <Sparkles size={14} />
            Prompt relevé
          </h2>
          <p className="overview-inline-prompt-description">Structurer un relevé complet.</p>
          <PromptContent type="STATEMENT" compact />
        </section>
        <section aria-label="Prompt extrait">
          <h2>
            <Sparkles size={14} />
            Prompt extrait
          </h2>
          <p className="overview-inline-prompt-description">
            Traiter un extrait bancaire existant.
          </p>
          <PromptContent type="EXTRACT" compact />
        </section>
      </div>
    </div>
  );
}

export function DashboardModelDownloads() {
  return (
    <div className="overview-inline-models">
      <a href="/resources/Relever.ema" download="releve.ema">
        <Download size={14} aria-hidden="true" />
        Fichier d’importation Sage 100 · releve.ema
      </a>
    </div>
  );
}
export function DashboardConversionResources({ initialType }: { initialType?: DocumentType }) {
  if (initialType) return <PromptSwitcher initialType={initialType} />;
  return (
    <div className="conversion-prompts-grid" aria-label="Prompts de conversion">
      <SeparatePromptCard type="STATEMENT" />
      <SeparatePromptCard type="EXTRACT" />
    </div>
  );
}

function SeparatePromptCard({ type }: { type: DocumentType }) {
  const titleId = useId();
  return (
    <section
      className="overview-card overview-resources conversion-prompt-card"
      aria-labelledby={titleId}
    >
      <p className="conversion-eyebrow">
        {type === 'EXTRACT' ? 'PROMPT EXTRAIT' : 'PROMPT RELEVÉ'}
      </p>
      <div className="overview-card-heading">
        <div>
          <h2 id={titleId}>
            <Sparkles size={18} />
            {type === 'EXTRACT'
              ? 'Préparer un extrait avec ChatGPT'
              : 'Préparer un relevé avec ChatGPT'}
          </h2>
          <p>
            {type === 'EXTRACT'
              ? 'Pour traiter un extrait bancaire PDF existant.'
              : 'Pour traiter un relevé bancaire PDF complet.'}
          </p>
        </div>
      </div>
      <PromptContent type={type} />
    </section>
  );
}

function PromptSwitcher({ initialType }: { initialType: DocumentType }) {
  const [type, setType] = useState<DocumentType>(initialType);
  const titleId = useId();
  return (
    <section className="overview-card overview-resources" aria-labelledby={titleId}>
      <div className="overview-card-heading">
        <div>
          <h2 id={titleId}>
            <Sparkles size={18} />
            {type === 'EXTRACT'
              ? 'Préparer un extrait avec ChatGPT'
              : 'Préparer un relevé avec ChatGPT'}
          </h2>
          <p>Deux prompts dédiés pour conserver les champs du document original.</p>
        </div>
        <span className="overview-icon teal">
          <FileText size={20} />
        </span>
      </div>
      <div className="conversion-prompt-choice" role="group" aria-label="Document à préparer">
        <button
          type="button"
          aria-pressed={type === 'STATEMENT'}
          onClick={() => setType('STATEMENT')}
        >
          Relevé bancaire
        </button>
        <button type="button" aria-pressed={type === 'EXTRACT'} onClick={() => setType('EXTRACT')}>
          Extrait bancaire
        </button>
      </div>
      <PromptContent key={type} type={type} />
    </section>
  );
}

function PromptContent({ type, compact = false }: { type: DocumentType; compact?: boolean }) {
  const path =
    type === 'EXTRACT'
      ? '/resources/prompt-conversion-extrait.txt'
      : '/resources/prompt-conversion-releve.txt';
  const [prompt, setPrompt] = useState('');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const field = useRef<HTMLTextAreaElement>(null);
  const fieldId = useId();
  useEffect(() => {
    const controller = new AbortController();
    fetch(path, { signal: controller.signal, cache: 'no-store' })
      .then((response) => {
        if (!response.ok)
          throw new Error(
            'Impossible de charger le prompt. Utilisez « Copier le prompt » pour réessayer.',
          );
        return response.text();
      })
      .then(setPrompt)
      .catch((error) => {
        if (!controller.signal.aborted) setMessage((error as Error).message);
      });
    return () => controller.abort();
  }, [path]);
  const chatGptUrl = prompt ? `https://chatgpt.com/?${new URLSearchParams({ q: prompt })}` : null;
  async function loadPrompt() {
    if (prompt) return prompt;
    const response = await fetch(path, { cache: 'no-store' });
    if (!response.ok)
      throw new Error(
        'Le prompt ne peut pas être chargé. Réessayez ou téléchargez le fichier texte.',
      );
    const text = await response.text();
    setPrompt(text);
    return text;
  }
  async function copyPrompt() {
    setBusy(true);
    setMessage('');
    try {
      const text = await loadPrompt();
      if (navigator.clipboard?.writeText) {
        try {
          await navigator.clipboard.writeText(text);
          setMessage('Prompt copié. Joignez votre PDF source et le modèle PDF dans ChatGPT.');
          return;
        } catch {
          /* Manual selection remains available. */
        }
      }
      setOpen(true);
      requestAnimationFrame(() => {
        field.current?.focus();
        field.current?.select();
        let copied = false;
        try {
          copied = document.execCommand('copy');
        } catch {
          /* Manual copy remains available. */
        }
        setMessage(
          copied ? 'Prompt copié.' : 'Texte sélectionné : utilisez Ctrl+C pour le copier.',
        );
      });
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function togglePrompt() {
    if (open) {
      setOpen(false);
      return;
    }
    try {
      await loadPrompt();
      setOpen(true);
    } catch (error) {
      setMessage((error as Error).message);
    }
  }
  if (compact)
    return (
      <>
        <div className="overview-inline-prompt-buttons">
          {chatGptUrl ? (
            <Button size="sm" asChild>
              <a
                href={chatGptUrl}
                target="_blank"
                rel="noopener noreferrer"
                aria-label="Ouvrir ChatGPT avec le prompt"
              >
                <ExternalLink size={14} />
                Ouvrir ChatGPT
              </a>
            </Button>
          ) : (
            <Button size="sm" disabled>
              <ExternalLink size={14} />
              Ouvrir ChatGPT
            </Button>
          )}
          <Button
            variant="outline"
            size="sm"
            onClick={copyPrompt}
            disabled={busy}
            aria-label="Copier le prompt"
          >
            <Copy size={14} />
            {busy ? 'Chargement…' : 'Copier'}
          </Button>
        </div>
        <div className="overview-inline-prompt-links">
          <button type="button" onClick={togglePrompt} aria-expanded={open} aria-controls={fieldId}>
            {open ? (
              <ChevronUp size={14} aria-hidden="true" />
            ) : (
              <BookOpen size={14} aria-hidden="true" />
            )}
            {open ? 'Masquer' : 'Lire le prompt'}
          </button>
          <a href={path} download aria-label="Télécharger le prompt (.txt)">
            <Download size={14} aria-hidden="true" />
            Télécharger
          </a>
        </div>
        {open && (
          <textarea
            className="overview-inline-prompt-text"
            ref={field}
            id={fieldId}
            aria-label="Prompt de conversion pour ChatGPT"
            readOnly
            value={prompt}
            rows={6}
          />
        )}
        {message && (
          <p className="overview-inline-prompt-status" role="status">
            {message}
          </p>
        )}
      </>
    );
  return (
    <>
      <p className="conversion-prompt-description">
        {type === 'EXTRACT'
          ? 'Structurez un extrait existant en conservant toutes les opérations fournies, sans créer de nouveaux mouvements.'
          : 'Structurez l’ensemble du relevé en conservant ses opérations, ses dates et les soldes présents dans la source.'}
      </p>
      <div className="overview-resources-actions">
        {chatGptUrl ? (
          <Button asChild>
            <a href={chatGptUrl} target="_blank" rel="noopener noreferrer">
              <ExternalLink size={16} />
              Ouvrir ChatGPT avec le prompt
            </a>
          </Button>
        ) : (
          <Button disabled>
            <ExternalLink size={16} />
            Ouvrir ChatGPT avec le prompt
          </Button>
        )}
        <Button onClick={copyPrompt} disabled={busy}>
          <Copy size={16} />
          {busy ? 'Chargement…' : 'Copier le prompt'}
        </Button>
      </div>
      <div className="conversion-shared-model">
        <FileText size={20} aria-hidden="true" />
        <div>
          <strong>Le relevé original définit la structure du PDF</strong>
          <p>Mêmes champs, mêmes colonnes et mêmes valeurs que dans le document source.</p>
        </div>
        <Button variant="outline" asChild>
          <a href="/resources/Relever.ema" download>
            <Download size={16} />
            Modèle EMA · Relever.ema
          </a>
        </Button>
      </div>
      <div className="overview-resources-links">
        <button type="button" onClick={togglePrompt} aria-expanded={open} aria-controls={fieldId}>
          {open ? 'Masquer le prompt' : 'Lire le prompt'}
        </button>
        <a href={path} download>
          Télécharger le prompt (.txt)
        </a>
      </div>
      {open && (
        <textarea
          ref={field}
          id={fieldId}
          aria-label="Prompt de conversion pour ChatGPT"
          readOnly
          value={prompt}
          rows={12}
        />
      )}
      <p role="status" className="overview-resources-status">
        {message ||
          'Joignez votre relevé original dans ChatGPT, puis importez le PDF structuré dans Alias. Choisissez ensuite le modèle d’exportation pour le CSV/TXT.'}
      </p>
    </>
  );
}
