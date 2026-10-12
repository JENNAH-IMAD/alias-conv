'use client';
import { FormEvent, useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { ExportField, ExportTemplate, request } from '@/services/api';
import {
  descriptionSources,
  newExportTemplate,
  sageX3Fields,
} from '@/services/export-template-defaults';

const sources = [
  ['Date', 'Date'],
  ['ValueDate', 'Date de valeur'],
  ['Journal', 'Journal'],
  ['Account', 'Compte comptable'],
  ['Description', 'Libellé'],
  ['MovementDescription', 'Libellé des mouvements (colonne vide sur les soldes)'],
  ['DescriptionWithBalances', 'Libellé des mouvements et des soldes'],
  ['Debit', 'Débit'],
  ['Credit', 'Crédit'],
  ['Reference', 'Référence'],
  ['Amount', 'Montant'],
  ['Direction', 'Sens'],
  ['Analytic', 'Analytique'],
  ['Balance', 'Solde'],
  ['OpeningBalance', 'Solde initial'],
  ['ClosingBalance', 'Solde final'],
  ['BankCode', 'Code banque'],
  ['Currency', 'Devise'],
  ['StatementNumber', 'Numéro de relevé'],
  ['RecordType', 'Type de ligne'],
  ['Sequence', 'Numéro de ligne'],
];
const dateFormats = [
  'ddMMyy',
  'ddMMyyyy',
  'yyMMdd',
  'yyyyMMdd',
  'yyyy-MM-dd',
  'dd/MM/yyyy',
  'dd-MM-yyyy',
  'MM/dd/yyyy',
];
const emptyField = (position: number): ExportField => ({
  fieldName: '',
  sourceField: 'Description',
  position,
  format: null,
  required: false,
});

export function ExportTemplateEditor({
  initial,
  onClose,
  onSaved,
  duplicate = false,
  defaultType = 'CUSTOMCSV',
}: {
  initial: ExportTemplate | null;
  onClose: () => void;
  onSaved: (saved: ExportTemplate) => void;
  duplicate?: boolean;
  defaultType?: ExportTemplate['type'];
}) {
  const [model, setModel] = useState<Omit<ExportTemplate, 'id'>>(() =>
    initial
      ? {
          ...initial,
          includeHeader: initial.type === 'SAGE100' ? false : initial.includeHeader,
          name: duplicate ? `${initial.name} (copie)` : initial.name,
          fields: initial.fields
            .map((field) => ({ ...field }))
            .sort((a, b) => a.position - b.position),
        }
      : newExportTemplate(defaultType),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const update = <K extends keyof typeof model>(key: K, value: (typeof model)[K]) =>
    setModel((previous) => ({ ...previous, [key]: value }));
  function updateField(index: number, patch: Partial<ExportField>) {
    setModel((previous) => ({
      ...previous,
      fields: previous.fields.map((field, i) => (i === index ? { ...field, ...patch } : field)),
    }));
  }
  function moveField(index: number, direction: number) {
    const fields = [...model.fields];
    [fields[index], fields[index + direction]] = [fields[index + direction], fields[index]];
    // Swap the positions only when the user explicitly changes the order.
    const positions = model.fields.map((field) => field.position);
    update(
      'fields',
      fields.map((field, i) => ({ ...field, position: positions[i] })),
    );
  }
  async function save(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      const { ...input } = model;
      const saved = await request<ExportTemplate>(
        '/export-templates' + (initial && !duplicate ? '/' + initial.id : ''),
        {
          method: initial && !duplicate ? 'PUT' : 'POST',
          body: JSON.stringify(input),
        },
      );
      onSaved(saved);
      onClose();
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  }
  return (
    <BankDialog
      title={
        duplicate ? 'Dupliquer le modèle' : initial ? 'Modifier le modèle' : 'Ajouter un modèle'
      }
      onClose={onClose}
      busy={busy}
      wide
      className="conversion-template-dialog"
    >
      <form onSubmit={save} className="space-y-5">
        <p className="muted text-sm">
          {initial && !duplicate
            ? 'Les modifications s’appliquent aux prochains exports de ce modèle. Les fichiers déjà archivés restent conservés. Dupliquez le modèle pour créer une variante.'
            : 'Configurez votre propre format : colonnes, ordre, sources, dates, nombres, séparateur et encodage.'}
        </p>
        <fieldset disabled={busy} className="space-y-5">
          <div className="template-editor-guide">
            <strong>Votre modèle en 3 étapes</strong>
            <p>1. Nommez le format · 2. Réglez le fichier · 3. Choisissez les colonnes</p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <label>
              Nom du modèle
              <Input
                required
                maxLength={200}
                className="ui-input mt-2"
                value={model.name}
                onChange={(e) => update('name', e.target.value)}
              />
            </label>
            <label>
              Type
              <select
                aria-label="Type"
                className="ui-input mt-2"
                value={model.type}
                onChange={(e) => {
                  const type = e.target.value as ExportTemplate['type'];
                  setModel((previous) => ({
                    ...previous,
                    type,
                    includeHeader: type === 'SAGE100' ? false : previous.includeHeader,
                    fileExtension: type === 'CUSTOMCSV' ? 'csv' : 'txt',
                    ...(!initial &&
                    previous.fields.every((field) => !field.fieldName.trim()) &&
                    type === 'SAGEX3'
                      ? {
                          fields: sageX3Fields(),
                          encoding: 'ascii',
                          numberCulture: 'en-US',
                          includeHeader: false,
                        }
                      : {}),
                  }));
                }}
              >
                <option value="CUSTOMCSV">CSV personnalisé</option>
                <option value="SAGE100">Sage 100</option>
                <option value="SAGEX3">Sage X3</option>
              </select>
            </label>
          </div>
          {model.type === 'SAGEX3' && (
            <section className="template-balance-settings">
              <div>
                <strong>Lignes de solde Sage X3</strong>
                <p>
                  Solde initial : numéro 1 · mouvements : 2 · solde final : 3. Le numéro interne
                  augmente à chaque ligne.
                </p>
              </div>
              <label>
                Libellé sur les lignes de solde
                <select
                  aria-label="Libellé sur les lignes de solde"
                  className="ui-input mt-2"
                  value={
                    model.fields.find((field) => descriptionSources.includes(field.sourceField))
                      ?.sourceField || 'Description'
                  }
                  onChange={(event) =>
                    update(
                      'fields',
                      model.fields.map((field) =>
                        descriptionSources.includes(field.sourceField)
                          ? { ...field, sourceField: event.target.value }
                          : field,
                      ),
                    )
                  }
                >
                  <option value="Description">
                    Retirer la colonne sur les soldes (par défaut)
                  </option>
                  <option value="MovementDescription">Conserver la colonne, libellé vide</option>
                  <option value="DescriptionWithBalances">
                    Conserver les libellés lus dans le relevé
                  </option>
                </select>
              </label>
              <p>
                Le format standard contient 10 valeurs par mouvement et 9 par solde. Vous pouvez
                ajouter, retirer, renommer et réordonner toutes les colonnes ci-dessous.
              </p>
            </section>
          )}
          <label className="block">
            Description
            <textarea
              maxLength={2000}
              className="ui-input mt-2"
              value={model.description}
              onChange={(e) => update('description', e.target.value)}
            />
          </label>
          <div className="grid gap-4 sm:grid-cols-3">
            <label>
              Séparateur
              <select
                aria-label="Séparateur"
                className="ui-input mt-2"
                value={model.delimiter}
                onChange={(e) => update('delimiter', e.target.value)}
              >
                {[
                  [';', 'Point-virgule'],
                  [',', 'Virgule'],
                  ['\t', 'Tabulation'],
                  ['|', 'Barre verticale'],
                  [' ', 'Espace'],
                ].map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Encodage
              <select
                aria-label="Encodage"
                className="ui-input mt-2"
                value={model.encoding}
                onChange={(e) => update('encoding', e.target.value)}
              >
                <option value="utf-8">UTF-8</option>
                <option value="windows-1252">Windows-1252</option>
                <option value="ascii">ASCII</option>
              </select>
            </label>
            <label>
              Format des nombres
              <select
                aria-label="Format des nombres"
                className="ui-input mt-2"
                value={model.numberCulture}
                onChange={(e) => update('numberCulture', e.target.value)}
              >
                <option value="fr-FR">Français (123,45)</option>
                <option value="en-US">Anglais (123.45)</option>
              </select>
            </label>
          </div>
          <p className="template-settings-help">
            Le séparateur délimite les colonnes. L’encodage détermine les caractères acceptés. Le
            format des nombres choisit la virgule ou le point décimal. Reprenez les réglages
            attendus par votre logiciel comptable.
          </p>
          <p className="text-sm opacity-70">Extension : .{model.fileExtension}</p>
          <div className="flex flex-wrap gap-6">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={model.type !== 'SAGE100' && model.includeHeader}
                disabled={model.type === 'SAGE100'}
                onChange={(e) => update('includeHeader', e.target.checked)}
              />
              {model.type === 'SAGE100' ? 'Sage 100 : valeurs sans en-tête' : 'Ligne d’en-tête'}
            </label>
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={model.isActive}
                onChange={(e) => update('isActive', e.target.checked)}
              />
              Modèle actif
            </label>
          </div>
          <h3 className="font-semibold">Colonnes ({model.fields.length}/30)</h3>
          <div className="space-y-3">
            {model.fields.map((field, index) => (
              <div key={field.position} className="rounded-xl border p-4">
                <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <strong>Colonne {index + 1}</strong>
                  <div className="flex gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      aria-label={`Monter la colonne ${index + 1}`}
                      disabled={index === 0}
                      onClick={() => moveField(index, -1)}
                    >
                      ↑
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      aria-label={`Descendre la colonne ${index + 1}`}
                      disabled={index === model.fields.length - 1}
                      onClick={() => moveField(index, 1)}
                    >
                      ↓
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      disabled={model.fields.length === 1}
                      onClick={() =>
                        update(
                          'fields',
                          model.fields.filter((_, i) => i !== index),
                        )
                      }
                    >
                      Retirer la colonne {index + 1}
                    </Button>
                  </div>
                </div>
                <div className="grid gap-4 sm:grid-cols-3">
                  <label>
                    Libellé {index + 1}
                    <Input
                      required
                      maxLength={100}
                      className="ui-input mt-2"
                      value={field.fieldName}
                      onChange={(e) => updateField(index, { fieldName: e.target.value })}
                    />
                  </label>
                  <label>
                    Source {index + 1}
                    <select
                      aria-label={`Source ${index + 1}`}
                      className="ui-input mt-2"
                      value={field.sourceField}
                      onChange={(e) =>
                        updateField(index, { sourceField: e.target.value, format: null })
                      }
                    >
                      {sources.map(([value, label]) => (
                        <option key={value} value={value}>
                          {label}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Format {index + 1}
                    <select
                      aria-label={`Format ${index + 1}`}
                      className="ui-input mt-2"
                      value={field.format ?? ''}
                      onChange={(e) => updateField(index, { format: e.target.value || null })}
                    >
                      <option value="">Par défaut</option>
                      {(field.sourceField === 'Date' || field.sourceField === 'ValueDate'
                        ? dateFormats
                        : ['0.00', '0.000', '0.##']
                      ).map((format) => (
                        <option key={format} value={format}>
                          {format}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
                <label className="mt-3 flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={field.required}
                    onChange={(e) => updateField(index, { required: e.target.checked })}
                  />
                  Colonne {index + 1} obligatoire
                </label>
              </div>
            ))}
          </div>
          <Button
            type="button"
            variant="outline"
            disabled={model.fields.length >= 30}
            onClick={() =>
              update('fields', [
                ...model.fields,
                emptyField(Math.max(...model.fields.map((field) => field.position)) + 1),
              ])
            }
          >
            Ajouter une colonne
          </Button>
        </fieldset>
        {error && (
          <p role="alert" className="text-destructive">
            {error}
          </p>
        )}
        <div className="flex justify-end gap-3">
          <Button type="button" variant="outline" disabled={busy} onClick={onClose}>
            Annuler
          </Button>
          <Button disabled={busy}>{busy ? 'Enregistrement…' : 'Enregistrer'}</Button>
        </div>
      </form>
    </BankDialog>
  );
}
