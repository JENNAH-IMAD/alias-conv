'use client';

import { useState } from 'react';
import Link from 'next/link';
import { ArrowRight, BookOpen } from 'lucide-react';
import { Button } from './ui/button';
import { ConversionSteps } from './conversion-steps';
import { useAll, useApi } from '@/services/use-api';
import { request, type Account, type Bank, type Client } from '@/services/api';
import type {
  EntryModelDossier,
  EntryModelPreview,
  EntryOutputTemplate,
} from '@/services/entry-models';
import type { StatementDetail } from '@/services/statement-exports';

export type EntryFlowSelection = {
  clientId: string;
  bankAccountId: string;
  exportTemplateId: string;
  abbreviation: string;
  modelId: string;
  outputTemplateId?: string;
};
const fold = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]/gi, '')
    .toUpperCase();

export function AccountingEntryModelFlow({
  detail,
  initial,
  onReview,
  onImport,
  onReload,
  extractId,
  embedded = false,
  onBack,
}: {
  detail: StatementDetail;
  initial: EntryFlowSelection;
  onReview: (selection: EntryFlowSelection, preview: EntryModelPreview) => void;
  onImport: () => void;
  onReload: () => void;
  extractId?: string;
  embedded?: boolean;
  onBack?: () => void;
}) {
  const clients = useAll<Client>('/clients');
  const accounts = useAll<Account>('/bank-accounts');
  const banks = useAll<Bank>('/banks');
  const formats = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const [formatChoice, setFormatChoice] = useState(initial.outputTemplateId || '');
  const [clientId, setClientId] = useState(initial.clientId || detail.clientId || '');
  const models = useApi<EntryModelDossier>(
    clientId ? `/accounting/entry-models/${clientId}` : null,
  );
  const [choice, setChoice] = useState(initial.modelId || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [source, setSource] = useState(detail);
  const available = (accounts.data || []).filter((a) => a.clientId === clientId);
  const sameBank = (a: Account) =>
    fold(a.bank) === fold(detail.statement.bankName) ||
    (!!detail.statement.bankCode &&
      banks.data?.some(
        (b) => b.id === a.bankId && fold(b.code) === fold(detail.statement.bankCode || ''),
      ));
  const entries = (models.data?.models || []).filter((m) => m.active);
  const eligibleEntries = entries.filter((model) => {
    const modelAccount = available.find((item) => item.id === model.bankAccountId);
    return initial.bankAccountId
      ? model.bankAccountId === initial.bankAccountId
      : !!modelAccount && sameBank(modelAccount);
  });
  const seeded = eligibleEntries.filter(
    (m) => m.bankAccountId === initial.bankAccountId && m.abbreviation === initial.abbreviation,
  );
  const selected =
    eligibleEntries.find((m) => m.id === choice) ||
    (!choice && seeded.length === 1 ? seeded[0] : undefined);
  const account = available.find((a) => a.id === selected?.bankAccountId);
  const compatibilityError = !selected
    ? ''
    : !account
      ? 'Le compte bancaire de cette écriture est indisponible pour cette entreprise.'
      : !sameBank(account)
        ? `Cette écriture concerne ${account.bank}, alors que le document importé concerne ${detail.statement.bankName}. Choisissez une écriture de ${detail.statement.bankName}, ou importez un document de ${account.bank}.`
        : account.currency.trim().toUpperCase() !==
            (detail.statement.currency || '').trim().toUpperCase()
          ? `Cette écriture utilise ${account.currency}, mais le document est en ${detail.statement.currency}. Choisissez une écriture dans la devise du document.`
          : selected.bankAccountBased && selected.accountCode !== account.accountCode
            ? 'Le compte comptable de la banque a changé. Actualisez les paramètres de cette écriture.'
            : '';
  const selection: EntryFlowSelection = {
    clientId,
    bankAccountId: selected?.bankAccountId || '',
    exportTemplateId: selected?.exportTemplateId || '',
    abbreviation: selected?.abbreviation || '',
    modelId: selected?.id || '',
    ...(formatChoice ? { outputTemplateId: formatChoice } : {}),
  };
  const outputFormatError =
    formatChoice &&
    !formats.loading &&
    !formats.data?.some((t) => t.id === formatChoice && t.active)
      ? 'Le modèle d’affichage choisi est indisponible ou inactif. Choisissez un format actif.'
      : '';
  const loading =
    clients.loading || accounts.loading || banks.loading || models.loading || formats.loading;
  const failure =
    error || clients.error || accounts.error || banks.error || models.error || formats.error;
  async function proceed() {
    if (!selected || !account || compatibilityError || outputFormatError || busy) return;
    setBusy(true);
    setError('');
    try {
      if (source.clientId !== clientId || source.bankAccountId !== account.id) {
        const associated = await request<StatementDetail>(
          `/statement-imports/${detail.id}/association`,
          {
            method: 'PUT',
            body: JSON.stringify({ version: source.version, clientId, bankAccountId: account.id }),
          },
        );
        setSource(associated);
      }
      const preview = await request<EntryModelPreview>('/accounting/entry-models/preview', {
        method: 'POST',
        body: JSON.stringify({
          clientId,
          statementId: detail.id,
          exportTemplateId: selection.exportTemplateId,
          abbreviation: selection.abbreviation,
          modelId: selection.modelId,
          ...(selection.outputTemplateId ? { outputTemplateId: selection.outputTemplateId } : {}),
          ...(extractId ? { extractId } : {}),
          decisions: {},
        }),
      });
      onReview(selection, preview);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className={embedded ? 'entry-model-inline' : 'ui-card conversion-model-step'}
      aria-label="Choix de l’écriture de l’entreprise"
    >
      {!embedded && (
        <>
          <ConversionSteps
            current={2}
            accountingMode
            extractMode={detail.statement.conversionType === 'EXTRACT'}
            disabled={busy}
            onNavigate={() => onImport()}
          />
          <div className="model-step-heading">
            <span className="model-step-icon">
              <BookOpen size={26} />
            </span>
            <div>
              <p className="conversion-eyebrow">ÉCRITURES COMPTABLES</p>
              <h2>Choisissez le modèle d’écritures</h2>
              <p>
                Le compte, les mots du libellé et le taux de TVA de l’écriture choisie seront appliqués
                au document. Vérifiez ensuite directement le résultat.
              </p>
            </div>
          </div>
          <div className="model-step-document">
            <strong>{detail.sourceFileName || detail.statement.bankName}</strong>
            <span>
              {detail.statement.bankName} · {detail.statement.currency}
            </span>
          </div>
        </>
      )}
      {embedded && (
        <header className="entry-model-inline-heading">
          <div className="model-step-icon">
            <BookOpen size={21} />
          </div>
          <div>
            <h3>Écritures comptables de l’entreprise</h3>
            <p>Choisissez une écriture enregistrée pour cette banque et ce relevé.</p>
          </div>
        </header>
      )}
      <>
        {(!embedded || !detail.clientId) && (
          <label className="conversion-template-label">
            Entreprise
            <select
              aria-label="Entreprise des écritures"
              className="ui-input mt-2"
              value={clientId}
              disabled={busy || clients.loading}
              onChange={(e) => {
                setClientId(e.target.value);
                setChoice('');
                setError('');
              }}
            >
              <option value="">Choisir une entreprise…</option>
              {clients.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {embedded && detail.clientId && (
          <p className="entry-model-account-context">
            {clients.data?.find((item) => item.id === clientId)?.name || 'Entreprise sélectionnée'}
            {initial.bankAccountId && account
              ? ` · ${account.bank} · ${account.accountName}`
              : ` · ${detail.statement.bankName}`}
          </p>
        )}
        <label className="conversion-template-label">
          Écriture comptable
          <select
            aria-label="Écriture de l’entreprise"
            className="ui-input mt-2"
            value={selected?.id || ''}
            disabled={busy || loading || !clientId}
            onChange={(e) => setChoice(e.target.value)}
          >
            <option value="">Choisir une écriture enregistrée…</option>
            {eligibleEntries.map((m) => (
              <option key={m.id} value={m.id}>
                {m.abbreviation} — {m.name} · {m.accountCode} · TVA {m.taxRate} % ·{' '}
                {available.find((a) => a.id === m.bankAccountId)?.bank || 'Compte indisponible'} ·{' '}
                {m.keywords.join(', ')}
              </option>
            ))}
          </select>
        </label>
        {selected && (
          <div className="model-step-document">
            <strong>
              {selected.abbreviation} · {account?.accountName}
            </strong>
            <span>
              {selected.taxRate} % · {selected.keywords.join(', ')}
            </span>
          </div>
        )}
        {!embedded && <label className="conversion-template-label">
          Modèle d’affichage et d’exportation
          <select
            aria-label="Modèle d’affichage des écritures"
            className="ui-input mt-2"
            value={formatChoice}
            disabled={busy || formats.loading}
            onChange={(e) => setFormatChoice(e.target.value)}
          >
            <option value="">Format enregistré dans l’écriture</option>
            {formats.data?.map((t) => (
              <option key={t.id} value={t.id} disabled={!t.active}>
                {t.name} · .{t.output.fileExtension || 'txt'}
                {!t.active ? ' (inactif)' : ''}
              </option>
            ))}
          </select>
        </label>}
        {!embedded && formatChoice && (
          <p className="muted text-sm">
            Le format choisi définit les colonnes de consultation et le fichier téléchargé pour ce
            traitement. L’écriture enregistrée reste inchangée.
          </p>
        )}
        {!loading && !failure && clientId && !eligibleEntries.length && (
          <p className="conversion-alert warning">
            Aucune écriture active ne correspond à ce compte bancaire.{' '}
            <Link className="underline" href={`/entry-models?clientId=${clientId}`}>
              Créer une écriture pour cette entreprise
            </Link>
          </p>
        )}
        <div className="entry-model-actions">
          <Link
            className="underline text-sm"
            href={`/entry-models?clientId=${clientId}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            Gérer les écritures de cette entreprise
          </Link>
          <Button
            size="sm"
            variant="outline"
            disabled={busy || loading}
            onClick={() => {
              models.reload();
              accounts.reload();
              banks.reload();
              formats.reload();
              onReload();
            }}
          >
            Actualiser les écritures
          </Button>
        </div>
      </>
      {!loading && compatibilityError && (
        <p className="conversion-alert warning" role="alert">
          {compatibilityError}
        </p>
      )}
      {!loading && outputFormatError && (
        <p className="conversion-alert warning" role="alert">
          {outputFormatError}
        </p>
      )}
      {loading && <p role="status">Chargement des écritures…</p>}
      {failure && (
        <div className="conversion-alert error" role="alert">
          {failure}
          <Button
            variant="outline"
            onClick={() => {
              clients.reload();
              accounts.reload();
              banks.reload();
              models.reload();
              formats.reload();
              onReload();
              setError('');
            }}
          >
            Réessayer
          </Button>
        </div>
      )}
      <div className="model-step-footer">
        {embedded ? (
          <Button variant="outline" disabled={busy} onClick={onBack}>
            Retour aux modèles d’export
          </Button>
        ) : (
          <Button variant="outline" disabled={busy} onClick={onImport}>
            Retour à l’import
          </Button>
        )}
        <Button
          permission="documents.write"
          disabled={busy || loading || !selected || !!compatibilityError || !!outputFormatError}
          onClick={() => void proceed()}
        >
          <ArrowRight size={16} />
          {busy ? 'Préparation…' : 'Vérifier le résultat'}
        </Button>
      </div>
    </section>
  );
}
