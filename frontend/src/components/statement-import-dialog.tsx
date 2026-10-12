'use client';
import { useState } from 'react';
import { CheckCircle2, FileText, Upload, ShieldCheck, FolderOpen } from 'lucide-react';
import { ConversionSteps } from './conversion-steps';
import { ProcessingProgress } from './processing-progress';
import { Button } from './ui/button';
import { Account, Bank, Client, request } from '@/services/api';
import { useAll } from '@/services/use-api';
import { StatementDetail } from '@/services/statement-exports';
import { validatePdf } from '@/lib/pdf-validation';
import { createUuid } from '@/lib/create-uuid';

export function StatementImportDialog({
  onClose,
  onImported,
  initialClientId = '',
}: {
  onClose: () => void;
  onImported: (detail: StatementDetail) => void;
  initialClientId?: string;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [bankId, setBankId] = useState('');
  const [currency, setCurrency] = useState('MAD');
  const [clientId, setClientId] = useState(initialClientId);
  const [accountId, setAccountId] = useState('');
  const [busy, setBusy] = useState(false);
  const [processingId, setProcessingId] = useState('');
  const [error, setError] = useState('');
  const [dragging, setDragging] = useState(false);
  const banks = useAll<Bank>('/banks');
  const clients = useAll<Client>('/clients');
  const accounts = useAll<Account>('/bank-accounts');
  function choose(value: File | undefined) {
    const invalid = value ? validatePdf(value) : null;
    setError(invalid || '');
    setFile(invalid ? null : value || null);
  }
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!file || !bankId || busy) return;
    const id = createUuid();
    setProcessingId(id);
    setBusy(true);
    setError('');
    const input = new FormData();
    input.set('file', file);
    input.set('processingId', id);
    input.set('bankId', bankId);
    input.set('currency', currency);
    if (clientId && accountId) {
      input.set('clientId', clientId);
      input.set('bankAccountId', accountId);
    }
    try {
      const saved = await request<StatementDetail>('/statement-imports/pdf', {
        method: 'POST',
        body: input,
      });
      onImported(saved);
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  }
  return (
    <section className="ui-card conversion-import-page">
      <header className="import-page-header">
        <div>
          <p className="conversion-eyebrow">NOUVELLE CONVERSION</p>
          <h1>Importer un relevé PDF</h1>
          <p className="muted">Un document, un compte, une conversion prête à vérifier.</p>
        </div>
        <Button variant="outline" disabled={busy} onClick={onClose}>
          Retour
        </Button>
      </header>
      <form onSubmit={submit} className="import-page-form">
        <ConversionSteps current={1} />
        {busy && (
          <p className="muted text-sm">
            Lecture automatique du PDF, puis revue des mouvements et des soldes.
          </p>
        )}
        {busy && file && (
          <div className="processing-file">
            <FileText size={20} />
            <div>
              <strong title={file.name}>{file.name}</strong>
              <span>
                {banks.data?.find((bank) => bank.id === bankId)?.name} · {currency}
              </span>
            </div>
          </div>
        )}
        <fieldset disabled={busy} hidden={busy} className="import-page-fields">
          <label
            className={`conversion-upload import-dropzone ${file ? 'has-file' : ''} ${dragging ? 'is-dragging' : ''}`}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragging(false);
              if (!busy) choose(e.dataTransfer.files[0]);
            }}
          >
            <span className="import-upload-icon">
              {file ? <CheckCircle2 size={28} /> : <Upload size={28} />}
            </span>
            <strong>{file?.name || 'Déposez votre relevé PDF ici'}</strong>
            <span>
              {file
                ? `${file.size < 1024 * 1024 ? `${Math.max(1, Math.ceil(file.size / 1024))} Ko` : `${(file.size / 1024 / 1024).toLocaleString('fr-FR', { maximumFractionDigits: 2 })} Mo`} · Cliquez pour changer de fichier`
                : 'ou cliquez pour choisir un fichier'}
            </span>
            <small>PDF natif ou scanné · 20 Mo maximum</small>
            <input
              aria-label="Fichier PDF du relevé"
              type="file"
              accept=".pdf,application/pdf"
              onChange={(e) => choose(e.target.files?.[0])}
            />
          </label>
          <div className="import-bank-fields grid gap-4 sm:grid-cols-2">
            <label className="text-sm">
              Banque du relevé
              <select
                required
                disabled={!!accountId}
                aria-label="Banque du relevé"
                className="ui-input mt-2"
                value={bankId}
                onChange={(e) => setBankId(e.target.value)}
              >
                <option value="">
                  {banks.loading ? 'Chargement des banques…' : 'Choisir une banque…'}
                </option>
                {banks.data?.map((bank) => (
                  <option key={bank.id} value={bank.id}>
                    {bank.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-sm">
              Devise du relevé
              <input
                required
                aria-label="Devise du relevé"
                readOnly={!!accountId}
                className="ui-input mt-2"
                pattern="[A-Z]{3}"
                maxLength={3}
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              />
            </label>
          </div>
          <section className="import-assignment-flat">
            <div className="assignment-heading">
              <FolderOpen size={17} />
              <span>
                Classer dans un dossier client <small>Facultatif</small>
              </span>
            </div>
            <p className="muted text-xs">
              Choisissez le client et son compte, ou classez le relevé après la conversion. Les 5
              derniers relevés ajoutés à chaque compte sont conservés ; le plus ancien est supprimé
              définitivement au 6e ajout.
            </p>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="text-sm">
                Client
                <select
                  className="ui-input mt-2"
                  aria-label="Client du relevé"
                  value={clientId}
                  onChange={(e) => {
                    setClientId(e.target.value);
                    setAccountId('');
                  }}
                >
                  <option value="">Sans affectation — à classer ensuite</option>
                  {clients.data?.map((client) => (
                    <option key={client.id} value={client.id}>
                      {client.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-sm">
                Compte bancaire
                <select
                  className="ui-input mt-2"
                  aria-label="Compte bancaire du relevé"
                  required={!!clientId}
                  disabled={!clientId}
                  value={accountId}
                  onChange={(e) => {
                    const account = accounts.data?.find((a) => a.id === e.target.value);
                    setAccountId(e.target.value);
                    if (account) {
                      setBankId(account.bankId);
                      setCurrency(account.currency);
                    }
                  }}
                >
                  <option value="">Choisir le compte du client…</option>
                  {accounts.data
                    ?.filter((a) => a.clientId === clientId)
                    .map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.bank} · {account.accountName} · {account.accountNumber}
                      </option>
                    ))}
                </select>
              </label>
            </div>
          </section>
          <div className="conversion-clean">
            <ShieldCheck size={18} />
            <span>Le PDF original sera conservé pour contrôler les données extraites.</span>
          </div>
        </fieldset>
        {processingId && (
          <ProcessingProgress key={processingId} id={processingId} failed={!busy && !!error} />
        )}
        {(error || banks.error || clients.error || accounts.error) && (
          <p role="alert" className="conversion-alert error">
            {error || banks.error || clients.error || accounts.error}
          </p>
        )}
        {banks.error && (
          <Button type="button" variant="outline" onClick={banks.reload}>
            Recharger les banques
          </Button>
        )}
        <div className="import-footer">
          <p className="muted text-xs">
            {busy
              ? 'Gardez cette page ouverte pendant le traitement.'
              : !file
                ? 'Ajoutez un PDF pour commencer.'
                : !bankId
                  ? 'Choisissez la banque pour continuer.'
                  : clientId && !accountId
                    ? 'Choisissez le compte bancaire du client.'
                    : 'Tout est prêt pour convertir votre relevé.'}
          </p>
          <div className="flex justify-end gap-3">
            <Button type="button" variant="outline" disabled={busy} onClick={onClose}>
              Annuler
            </Button>
            <Button
              disabled={busy || !file || !bankId || !!banks.error || (!!clientId && !accountId)}
            >
              <Upload size={16} />
              {busy ? 'Lecture et adaptation…' : 'Importer et continuer'}
            </Button>
          </div>
        </div>
      </form>
    </section>
  );
}
