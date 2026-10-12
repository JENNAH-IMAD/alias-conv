'use client';
import { useState } from 'react';
import { request, type Page } from '@/services/api';
import { useApi } from '@/services/use-api';
import type { PdfTextDocument, StatementDetail } from '@/services/statement-exports';
import { Button } from './ui/button';
import { useAuth } from './auth-context';
export type ExtractionProfile = {
  id: string;
  name: string;
  bankCode: string;
  currency: string;
  mappingJson: string;
  sampleJson: string;
  aliasesJson: string;
  version: number;
  isActive: boolean;
  createdByName: string;
  modifiedByName: string;
  updatedAt: string;
};
export type ExtractionMapping = NonNullable<PdfTextDocument['lastMapping']>;
const fields: Record<string, string> = {
  Date: 'Date d’opération',
  ValueDate: 'Date de valeur',
  Description: 'Libellé',
  Debit: 'Débit',
  Credit: 'Crédit',
  OpeningBalance: 'Solde initial',
  ClosingBalance: 'Solde final',
};
export function ExtractionProfiles({
  detail,
  mapping,
  onSelect,
  onApplied,
  disabled,
}: {
  detail: StatementDetail;
  mapping: ExtractionMapping;
  onSelect: (mapping: ExtractionMapping) => void;
  onApplied: () => void;
  disabled: boolean;
}) {
  const { can } = useAuth();
  const profiles = useApi<ExtractionProfile[]>('/extraction-profiles');
  const history = useApi<
    Page<{
      id: string;
      profileName: string;
      actorName: string;
      action: string;
      version: number;
      createdAt: string;
    }>
  >('/extraction-profiles/history?pageSize=100');
  const [selected, setSelected] = useState('');
  const [name, setName] = useState('');
  const [active, setActive] = useState(true);
  const [aliases, setAliases] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const current = profiles.data?.find((p) => p.id === selected);
  const matching =
    profiles.data?.filter(
      (p) => p.bankCode === detail.statement.bankCode && p.currency === detail.statement.currency,
    ) ?? [];
  async function act(action: 'save' | 'apply' | 'delete') {
    if (busy || disabled || !can('documents.write')) return;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      if (action === 'apply' && current) {
        await request(`/statement-imports/${detail.id}/adapt`, {
          method: 'POST',
          body: JSON.stringify({ statementVersion: detail.version, profileId: current.id }),
        });
        onApplied();
      } else if (action === 'delete' && current) {
        if (
          !window.confirm(
            'Supprimer ce modèle d’extraction ? Les relevés déjà traités sont conservés.',
          )
        )
          return;
        await request(`/extraction-profiles/${current.id}?version=${current.version}`, {
          method: 'DELETE',
        });
        setSelected('');
        setName('');
        setAliases({});
        profiles.reload();
        history.reload();
      } else if (action === 'save') {
        const body = {
          name,
          statementId: detail.id,
          statementVersion: detail.version,
          mapping,
          isActive: active,
          version: current?.version ?? 1,
          aliases: Object.fromEntries(
            Object.entries(aliases)
              .filter(([, value]) => value.trim())
              .map(([key, value]) => [
                key,
                value
                  .split(';')
                  .map((v) => v.trim())
                  .filter(Boolean),
              ]),
          ),
        };
        const saved = await request<ExtractionProfile>(
          `/extraction-profiles${current ? '/' + current.id : ''}`,
          { method: current ? 'PUT' : 'POST', body: JSON.stringify(body) },
        );
        setSelected(saved.id);
        profiles.reload();
        history.reload();
        setNotice('Modèle enregistré en base de données.');
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <details className="ui-card" open>
      <summary className="cursor-pointer font-semibold">Modèles d’extraction enregistrés</summary>
      <p className="muted text-sm mt-2">
        Configurez les colonnes ci-dessous, puis enregistrez un modèle pour cette banque et cette
        devise. Il restera disponible après suppression des relevés.
      </p>
      {profiles.error && <p role="alert">{profiles.error}</p>}
      <label className="block mt-3">
        Modèle
        <select
          className="ui-input mt-1"
          aria-label="Modèle d’extraction"
          disabled={busy || disabled}
          value={selected}
          onChange={(e) => {
            const profile = matching.find((p) => p.id === e.target.value);
            setSelected(e.target.value);
            setName(profile?.name ?? '');
            setActive(profile?.isActive ?? true);
            setError('');
            setAliases(
              profile
                ? Object.fromEntries(
                    Object.entries(JSON.parse(profile.aliasesJson) as Record<string, string[]>).map(
                      ([key, values]) => [key, values.join('; ')],
                    ),
                  )
                : {},
            );
            if (profile) onSelect(JSON.parse(profile.mappingJson));
          }}
        >
          <option value="">Nouveau modèle</option>
          {matching.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
              {p.isActive ? '' : ' (désactivé)'}
            </option>
          ))}
        </select>
      </label>
      {can('documents.write') && (
        <fieldset disabled={busy || disabled} className="mt-3 space-y-3">
          <label className="block">
            Nom du modèle
            <input
              className="ui-input mt-1"
              maxLength={150}
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </label>
          <details>
            <summary className="cursor-pointer text-sm">Synonymes personnalisés</summary>
            <p className="muted text-xs mt-2">
              Séparez les synonymes par un point-virgule. Un terme doit correspondre à un seul
              champ.
            </p>
            <div className="grid gap-3 mt-3 sm:grid-cols-2">
              {Object.entries(fields).map(([key, label]) => (
                <label key={key} className="text-sm">
                  {label}
                  <input
                    className="ui-input mt-1"
                    aria-label={`Synonymes ${label}`}
                    value={aliases[key] ?? ''}
                    onChange={(e) => setAliases({ ...aliases, [key]: e.target.value })}
                  />
                </label>
              ))}
            </div>
          </details>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />{' '}
            Réutilisation automatique active
          </label>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={!name.trim()} onClick={() => void act('save')}>
              {current ? 'Mettre à jour le modèle' : 'Enregistrer le modèle'}
            </Button>
            {current && (
              <>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={!current.isActive}
                  onClick={() => void act('apply')}
                >
                  Appliquer au relevé
                </Button>
                <Button size="sm" variant="danger" onClick={() => void act('delete')}>
                  Supprimer le modèle
                </Button>
              </>
            )}
          </div>
        </fieldset>
      )}
      {current && (
        <p className="muted text-xs mt-3">
          Créé par {current.createdByName} · modifié par {current.modifiedByName} le{' '}
          {new Date(current.updatedAt).toLocaleString('fr-FR')} · version {current.version}
        </p>
      )}
      {error && (
        <p role="alert" className="text-destructive mt-2">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="text-sm mt-2">
          {notice}
        </p>
      )}
      <details className="mt-3">
        <summary className="cursor-pointer text-sm">Historique des modèles</summary>
        {history.error && <p role="alert">{history.error}</p>}
        <ul className="mt-2 max-h-48 overflow-auto text-xs space-y-2">
          {history.data?.items.map((item) => (
            <li key={item.id}>
              {item.profileName} ·{' '}
              {(
                { CREATED: 'Création', UPDATED: 'Modification', DELETED: 'Suppression' } as Record<
                  string,
                  string
                >
              )[item.action] || item.action}{' '}
              · {item.actorName} · {new Date(item.createdAt).toLocaleString('fr-FR')} · version{' '}
              {item.version}
            </li>
          ))}
        </ul>
      </details>
    </details>
  );
}
