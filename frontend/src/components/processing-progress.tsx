'use client';
import { Check, Clock3, FileSearch, LoaderCircle } from 'lucide-react';
import { useEffect, useState } from 'react';
import { request } from '@/services/api';

type ProcessingStatus = {
  stage: string;
  percent: number;
  extractedPages: number | null;
  totalPages: number | null;
  activeStage?: string;
};
const steps = [
  {
    key: 'uploading',
    label: 'Envoi du PDF',
    detail: 'Transfert du fichier vers le serveur.',
  },
  {
    key: 'checking',
    label: 'Contrôle du PDF',
    detail: 'Vérification du fichier, du compte et de la devise.',
  },
  {
    key: 'reading',
    label: 'Lecture des pages',
    detail: 'Extraction du texte et des tableaux, avec OCR si nécessaire.',
  },
  {
    key: 'adapting',
    label: 'Organisation des mouvements',
    detail: 'Identification des dates, libellés, montants et soldes.',
  },
  {
    key: 'saving',
    label: 'Préparation de la revue',
    detail: 'Conservation du PDF et contrôle des données extraites.',
  },
];

export function ProcessingProgress({ id, failed = false }: { id: string; failed?: boolean }) {
  const [progress, setProgress] = useState<ProcessingStatus | null>(null);
  const [lastStage, setLastStage] = useState('uploading');
  const [elapsed, setElapsed] = useState(0);
  const [unavailable, setUnavailable] = useState(false);
  useEffect(() => {
    if (failed) {
      const controller = new AbortController();
      // Fast failures can finish before the first polling response. Read the final server stage.
      void request<ProcessingStatus>(`/statement-imports/processing/${id}`, {
        signal: controller.signal,
      })
        .then((next) => {
          if (controller.signal.aborted) return;
          setProgress(next);
          if (next.activeStage) setLastStage(next.activeStage);
          else if (!['done', 'failed', 'cancelled'].includes(next.stage)) setLastStage(next.stage);
        })
        .catch(() => {});
      return () => controller.abort();
    }
    const started = Date.now();
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    let missed = 0;
    async function poll() {
      try {
        const next = await request<ProcessingStatus>(`/statement-imports/processing/${id}`, {
          signal: controller.signal,
        });
        setProgress(next);
        if (next.activeStage) setLastStage(next.activeStage);
        else if (!['done', 'failed', 'cancelled'].includes(next.stage)) setLastStage(next.stage);
        setUnavailable(false);
        missed = 0;
        if (['done', 'failed', 'cancelled'].includes(next.stage)) {
          clearInterval(clock);
          return;
        }
      } catch {
        if (controller.signal.aborted) return;
        // The upload can still be in transit before the server creates its tracking entry.
        missed += 1;
        if (missed >= 5) setUnavailable(true);
      }
      if (!controller.signal.aborted) timer = setTimeout(() => void poll(), 1500);
    }
    const clock = setInterval(() => setElapsed(Math.floor((Date.now() - started) / 1000)), 1000);
    void poll();
    return () => {
      controller.abort();
      clearTimeout(timer);
      clearInterval(clock);
    };
  }, [id, failed]);
  const stage = failed ? 'failed' : (progress?.stage ?? 'uploading');
  const terminal = ['done', 'failed', 'cancelled'].includes(stage);
  const activeStage = stage === 'failed' || stage === 'cancelled' ? lastStage : stage;
  const index =
    stage === 'done'
      ? steps.length
      : activeStage === 'queued' || activeStage === 'collecting'
        ? 2
        : steps.findIndex((step) => step.key === activeStage);
  const title =
    stage === 'uploading'
      ? 'Envoi du PDF'
      : stage === 'queued'
        ? 'En attente du moteur OCR'
        : stage === 'collecting'
          ? 'Récupération du texte et des tableaux'
          : stage === 'failed'
            ? 'Traitement interrompu'
            : stage === 'cancelled'
              ? 'Traitement annulé'
              : stage === 'done'
                ? 'Relevé prêt à vérifier'
                : steps[index]?.label;
  return (
    <section
      className={`processing-progress ${terminal ? 'is-terminal' : 'is-running'}`}
      aria-label="Progression du traitement"
    >
      <div className="processing-heading">
        <span className="processing-symbol">
          <FileSearch size={22} />
        </span>
        <div>
          <p className="ui-eyebrow">TRAITEMENT DU RELEVÉ</p>
          <h3 role="status">{title}</h3>
        </div>
        <strong className="processing-percent">{progress ? `${progress.percent} %` : '…'}</strong>
      </div>
      <div
        className={`processing-track ${!progress && !terminal ? 'indeterminate' : ''}`}
        role="progressbar"
        aria-label="Étapes du traitement terminées"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={progress?.percent}
        aria-valuetext={progress ? `${progress.percent} %, ${title}` : title}
      >
        <span
          style={
            progress ? { width: `${progress.percent}%` } : terminal ? { width: '0%' } : undefined
          }
        />
      </div>
      <div className="processing-meta">
        <span>
          <Clock3 size={14} />
          {Math.floor(elapsed / 60)} min {String(elapsed % 60).padStart(2, '0')} s écoulées
        </span>
        <span>
          {progress?.totalPages
            ? `${progress.extractedPages ?? 0} / ${progress.totalPages} pages lues`
            : 'Préparation du document'}
        </span>
      </div>
      <ol className="processing-steps">
        {steps.map((step, n) => {
          const complete = n < index;
          const current = n === index;
          const pagePercent =
            step.key === 'reading' && stage !== 'queued' && progress?.totalPages
              ? Math.round(Math.min(1, (progress.extractedPages ?? 0) / progress.totalPages) * 100)
              : undefined;
          const percent = complete ? 100 : current ? pagePercent : 0;
          const indeterminate = current && !terminal && percent === undefined;
          const state = complete
            ? 'Terminé'
            : current
              ? terminal
                ? 'Interrompu'
                : 'En cours'
              : 'En attente';
          return (
            <li
              key={step.key}
              className={n < index ? 'complete' : n === index ? 'current' : ''}
              aria-current={n === index ? 'step' : undefined}
            >
              <span className="processing-step-icon">
                {n < index ? (
                  <Check size={15} />
                ) : n === index && !terminal ? (
                  <LoaderCircle size={15} className="animate-spin" />
                ) : (
                  n + 1
                )}
              </span>
              <div className="processing-step-content">
                <strong>{step.label}</strong>
                <p>{step.detail}</p>
                <div
                  className={`processing-track processing-step-track ${indeterminate ? 'indeterminate' : ''}`}
                  role="progressbar"
                  aria-label={step.label}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-valuenow={percent}
                  aria-valuetext={percent === undefined ? state : `${state}, ${percent} %`}
                >
                  <span style={indeterminate ? undefined : { width: `${percent ?? 0}%` }} />
                </div>
                <small>
                  {state}
                  {percent !== undefined && current && !terminal ? ` · ${percent} %` : ''}
                </small>
              </div>
            </li>
          );
        })}
      </ol>
      <p className="processing-note">
        {terminal && stage !== 'done'
          ? 'Le traitement est arrêté. Consultez le message ci-dessous avant de relancer l’import.'
          : unavailable
            ? 'Le suivi est momentanément indisponible. L’import reste en cours ; attendez son résultat.'
            : stage === 'done'
              ? 'Vos données sont prêtes. Vérifiez les mouvements et les soldes avant l’export.'
              : 'Nous préparons vos mouvements automatiquement. Un relevé scanné peut prendre quelques minutes ; la vérification viendra ensuite.'}
      </p>
    </section>
  );
}
