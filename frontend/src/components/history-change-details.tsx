'use client';
import { useMemo, useState } from 'react';
import { ArrowRight, FilePenLine, ListChecks } from 'lucide-react';
import { Button } from './ui/button';

type Row = { label: string; before: string; after: string; comparison: boolean };
type Operation = { title: string; kind: 'Ajoutée' | 'Modifiée' | 'Supprimée'; rows: Row[] };
type ObjectValue = Record<string, unknown>;
const labels: Record<string, string> = {
  status: 'Statut',
  version: 'Version',
  bankname: 'Banque',
  bankcode: 'Code banque',
  statementnumber: 'Référence du relevé',
  currency: 'Devise',
  periodstart: 'Début de période',
  periodend: 'Fin de période',
  transactioncount: 'Nombre de mouvements',
  sourcefilename: 'Fichier source',
  importedbyname: 'Importé par',
  modifiedbyname: 'Modifié par',
  validatedat: 'Validation',
  filedat: 'Classement',
  accountnumber: 'Numéro de compte',
  accountholder: 'Titulaire du compte',
  openingbalance: 'Solde initial',
  closingbalance: 'Solde final',
  totaldebit: 'Total des débits',
  totalcredit: 'Total des crédits',
  date: 'Date d’opération',
  valuedate: 'Date de valeur',
  description: 'Libellé',
  reference: 'Référence',
  debit: 'Débit',
  credit: 'Crédit',
  amount: 'Montant',
  direction: 'Sens',
  balance: 'Solde',
  journal: 'Journal',
  account: 'Compte comptable',
  analytic: 'Code analytique',
  recordtype: 'Type de ligne',
  conversiontype: 'Document',
  openingbalancedate: 'Date du solde initial',
  closingbalancedate: 'Date du solde final',
  openingbalancevaluedate: 'Date de valeur du solde initial',
  closingbalancevaluedate: 'Date de valeur du solde final',
  openingbalancedescription: 'Libellé du solde initial',
  closingbalancedescription: 'Libellé du solde final',
  name: 'Nom du modèle',
  isactive: 'Modèle actif',
  createdbyname: 'Créé par',
};
const vocabulary: Record<string, string> = {
  VALIDATED: 'Validé',
  NEEDS_REVIEW: 'À vérifier',
  EXPORTED: 'Exporté',
  FAILED: 'En erreur',
  STATEMENT: 'Relevé bancaire',
  EXTRACT: 'Extrait bancaire',
  DEBIT: 'Débit',
  CREDIT: 'Crédit',
};
function unpack(value: unknown): unknown {
  for (let depth = 0; depth < 3 && typeof value === 'string'; depth++) {
    const trimmed = value.trim();
    if (!/^[{[\"]/.test(trimmed)) break;
    try {
      value = JSON.parse(trimmed);
    } catch {
      break;
    }
  }
  return value;
}
function object(value: unknown): ObjectValue {
  const parsed = unpack(value);
  return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
    ? (parsed as ObjectValue)
    : {};
}
function keyed(value: unknown): ObjectValue {
  return Object.fromEntries(
    Object.entries(object(value)).map(([key, item]) => [key.toLowerCase(), item]),
  );
}
function format(value: unknown, key: string): string {
  if (value === null || value === undefined || value === '') return 'Non renseigné';
  if (typeof value === 'boolean') return value ? 'Oui' : 'Non';
  const text = String(value);
  if (vocabulary[text]) return vocabulary[text];
  if (/date|period|at$/.test(key) && /^\d{4}-\d{2}-\d{2}/.test(text)) {
    const date = new Date(text.length === 10 ? `${text}T12:00:00` : text);
    if (!Number.isNaN(date.getTime()))
      return text.length === 10 ? date.toLocaleDateString('fr-FR') : date.toLocaleString('fr-FR');
  }
  if (/balance$|debit$|credit$|amount$/.test(key) && Number.isFinite(Number(value)))
    return Number(value).toLocaleString('fr-FR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  return text;
}
function scalarRows(before: ObjectValue, after: ObjectValue, comparison: boolean): Row[] {
  const rows: Row[] = [];
  for (const key of new Set([...Object.keys(before), ...Object.keys(after)])) {
    if (!labels[key] || (comparison && JSON.stringify(before[key]) === JSON.stringify(after[key])))
      continue;
    if ([before[key], after[key]].some((value) => value !== null && typeof value === 'object'))
      continue;
    if (!comparison && (after[key] === null || after[key] === undefined || after[key] === ''))
      continue;
    rows.push({
      label: labels[key],
      before: format(before[key], key),
      after: format(after[key], key),
      comparison,
    });
  }
  return rows;
}
function describe(raw: string, snapshot: boolean) {
  const input = keyed(raw);
  let before: ObjectValue = {},
    after: ObjectValue = {};
  const notes: string[] = [];
  if (snapshot) after = input;
  else
    for (const [key, value] of Object.entries(input)) {
      const pair = keyed(value);
      if ('before' in pair || 'after' in pair) {
        before[key] = pair.before;
        after[key] = pair.after;
      } else after[key] = value;
    }
  const normalizedBefore = keyed(before.normalizedjson);
  const normalizedAfter = keyed(after.normalizedjson);
  before = { ...normalizedBefore, ...before };
  after = { ...normalizedAfter, ...after };
  const comparison = !snapshot;
  const rows = scalarRows(before, after, comparison);
  for (const [key, label] of [
    ['clientid', 'L’association au dossier client'],
    ['bankaccountid', 'L’association au compte bancaire'],
  ] as const) {
    if (before[key] !== after[key] && (before[key] || after[key]))
      notes.push(`${label} a été ${after[key] ? 'mise à jour' : 'retirée'}.`);
  }
  for (const [key, label] of [
    ['mapping', 'Les réglages de lecture'],
    ['mappingjson', 'Les réglages de lecture'],
    ['aliases', 'Les synonymes des colonnes'],
    ['aliasesjson', 'Les synonymes des colonnes'],
  ] as const) {
    if (after[key] && JSON.stringify(before[key]) !== JSON.stringify(after[key]))
      notes.push(
        `${label} ${snapshot ? 'sont enregistrés dans ce modèle' : 'ont été mis à jour'}.`,
      );
  }
  const oldTransactions = Array.isArray(before.transactions) ? before.transactions.map(keyed) : [];
  const newTransactions = Array.isArray(after.transactions) ? after.transactions.map(keyed) : [];
  const keyFor = (item: ObjectValue, index: number) => String(item.id ?? item.sequence ?? index);
  const oldMap = new Map(oldTransactions.map((item, index) => [keyFor(item, index), item]));
  const newMap = new Map(newTransactions.map((item, index) => [keyFor(item, index), item]));
  const operations: Operation[] = [];
  for (const key of new Set([...oldMap.keys(), ...newMap.keys()])) {
    const oldItem = oldMap.get(key),
      newItem = newMap.get(key);
    const operationRows = scalarRows(oldItem ?? {}, newItem ?? {}, !!oldItem && !!newItem);
    if (!operationRows.length) continue;
    const item = newItem ?? oldItem!;
    operations.push({
      title: `Opération ${item.sequence ?? (newTransactions.indexOf(item) + 1 || oldTransactions.indexOf(item) + 1)}${item.description ? ` · ${item.description}` : ''}`,
      kind: !oldItem ? 'Ajoutée' : !newItem ? 'Supprimée' : 'Modifiée',
      rows: newItem ? operationRows : scalarRows({}, oldItem!, false),
    });
  }
  if (
    (before.extracts || after.extracts) &&
    JSON.stringify(before.extracts) !== JSON.stringify(after.extracts)
  )
    notes.push('Les sélections d’opérations enregistrées ont été mises à jour.');
  return { rows, notes: [...new Set(notes)], operations };
}

function Values({ rows }: { rows: Row[] }) {
  return (
    <div className="history-readable-values">
      {rows.map((row, index) => (
        <div className="history-readable-row" key={`${row.label}-${index}`}>
          <span className="history-field-label">{row.label}</span>
          {row.comparison ? (
            <div className="history-value-comparison">
              <div>
                <small>Avant</small>
                <span>{row.before}</span>
              </div>
              <ArrowRight size={15} aria-hidden="true" />
              <div>
                <small>Après</small>
                <strong>{row.after}</strong>
              </div>
            </div>
          ) : (
            <strong className="history-current-value">{row.after}</strong>
          )}
        </div>
      ))}
    </div>
  );
}

export function HistoryChangeDetails({
  value,
  snapshot = false,
}: {
  value: string;
  snapshot?: boolean;
}) {
  const { rows, notes, operations } = useMemo(() => describe(value, snapshot), [value, snapshot]);
  const [limit, setLimit] = useState(10);
  return (
    <div className="history-readable">
      <h4>
        <FilePenLine size={17} />
        {snapshot ? 'Réglages du modèle' : 'Détail des changements'}
      </h4>
      {!!rows.length && <Values rows={rows} />}
      {notes.map((note) => (
        <p className="history-change-note" key={note}>
          {note}
        </p>
      ))}
      {!!operations.length && (
        <section className="history-operations" aria-label="Opérations concernées">
          <h4>
            <ListChecks size={17} />
            Opérations concernées <span>{operations.length.toLocaleString('fr-FR')}</span>
          </h4>
          <div className="history-operation-summary">
            {(['Ajoutée', 'Modifiée', 'Supprimée'] as const).map((kind) => {
              const count = operations.filter((operation) => operation.kind === kind).length;
              return count ? (
                <span key={kind} data-kind={kind}>
                  {count} {kind.toLowerCase()}
                  {count > 1 ? 's' : ''}
                </span>
              ) : null;
            })}
          </div>
          {operations.slice(0, limit).map((operation, index) => (
            <details className="history-operation" key={index}>
              <summary>
                <span>{operation.title}</span>
                <span data-kind={operation.kind}>{operation.kind}</span>
              </summary>
              <Values rows={operation.rows} />
            </details>
          ))}
          {operations.length > limit && (
            <Button size="sm" variant="outline" onClick={() => setLimit(limit + 10)}>
              Afficher 10 opérations de plus ({operations.length - limit} restantes)
            </Button>
          )}
        </section>
      )}
      {!rows.length && !notes.length && !operations.length && (
        <p className="history-change-note">
          Cette action a été enregistrée. Aucun détail supplémentaire n’est disponible pour cette
          entrée.
        </p>
      )}
    </div>
  );
}
