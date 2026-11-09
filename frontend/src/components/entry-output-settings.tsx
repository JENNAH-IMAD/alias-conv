'use client';
import { ArrowUp, ArrowDown, Plus, Trash2 } from 'lucide-react';
import { Button } from './ui/button';
import { entryFields, type EntryModelOutput } from '@/services/entry-models';

export function EntryOutputSettings({
  value,
  onChange,
  disabled,
}: {
  value: EntryModelOutput;
  onChange: (value: EntryModelOutput) => void;
  disabled: boolean;
}) {
  function move(index: number, direction: number) {
    const columns = [...value.columns];
    const next = index + direction;
    [columns[index], columns[next]] = [columns[next], columns[index]];
    onChange({ ...value, columns });
  }
  return (
    <fieldset disabled={disabled} className="entry-output-settings">
      <legend>Affichage des résultats et colonnes du fichier</legend>
      <p className="muted text-sm">
        Ce modèle est indépendant des formats Sage et des anciens modèles de relevés. L’ordre
        ci-dessous définit l’affichage et l’export.
      </p>
      {value.columns.map((column, index) => (
        <div className="entry-output-column" key={index}>
          <label>
            Champ
            <select
              className="ui-input mt-2"
              aria-label={`Champ colonne ${index + 1}`}
              value={column.field}
              onChange={(e) =>
                onChange({
                  ...value,
                  columns: value.columns.map((c, i) =>
                    i === index
                      ? { ...c, field: e.target.value, label: entryFields[e.target.value] }
                      : c,
                  ),
                })
              }
            >
              {Object.entries(entryFields)
                .filter(
                  ([field]) =>
                    field === column.field || !value.columns.some((c) => c.field === field),
                )
                .map(([field, label]) => (
                  <option key={field} value={field}>
                    {label}
                  </option>
                ))}
            </select>
          </label>
          <label>
            En-tête
            <input
              className="ui-input mt-2"
              aria-label={`En-tête colonne ${index + 1}`}
              value={column.label}
              required
              maxLength={80}
              onChange={(e) =>
                onChange({
                  ...value,
                  columns: value.columns.map((c, i) =>
                    i === index ? { ...c, label: e.target.value } : c,
                  ),
                })
              }
            />
          </label>
          <label className="entry-model-check">
            <input
              type="checkbox"
              checked={column.display}
              onChange={(e) =>
                onChange({
                  ...value,
                  columns: value.columns.map((c, i) =>
                    i === index ? { ...c, display: e.target.checked } : c,
                  ),
                })
              }
            />{' '}
            Afficher
          </label>
          <label className="entry-model-check">
            <input
              type="checkbox"
              checked={column.export}
              onChange={(e) =>
                onChange({
                  ...value,
                  columns: value.columns.map((c, i) =>
                    i === index ? { ...c, export: e.target.checked } : c,
                  ),
                })
              }
            />{' '}
            Exporter
          </label>
          <div className="flex gap-1">
            <Button
              type="button"
              size="icon"
              variant="outline"
              aria-label={`Monter colonne ${index + 1}`}
              disabled={disabled || index === 0}
              onClick={() => move(index, -1)}
            >
              <ArrowUp size={15} />
            </Button>
            <Button
              type="button"
              size="icon"
              variant="outline"
              aria-label={`Descendre colonne ${index + 1}`}
              disabled={disabled || index === value.columns.length - 1}
              onClick={() => move(index, 1)}
            >
              <ArrowDown size={15} />
            </Button>
            <Button
              type="button"
              size="icon"
              variant="outline"
              aria-label={`Supprimer colonne ${index + 1}`}
              disabled={disabled || value.columns.length === 1}
              onClick={() =>
                onChange({ ...value, columns: value.columns.filter((_, i) => i !== index) })
              }
            >
              <Trash2 size={15} />
            </Button>
          </div>
        </div>
      ))}
      <Button
        type="button"
        variant="outline"
        disabled={disabled || value.columns.length >= Object.keys(entryFields).length}
        onClick={() => {
          const field = Object.keys(entryFields).find(
            (f) => !value.columns.some((c) => c.field === f),
          );
          if (field)
            onChange({
              ...value,
              columns: [
                ...value.columns,
                { field, label: entryFields[field], display: true, export: true },
              ],
            });
        }}
      >
        <Plus size={15} /> Ajouter une colonne
      </Button>
      <div className="entry-model-fields">
        <label>
          Type de fichier
          <select
            aria-label="Type de fichier"
            className="ui-input mt-2"
            value={value.fileExtension || 'txt'}
            onChange={(e) =>
              onChange({ ...value, fileExtension: e.target.value as 'txt' | 'csv' | 'tsv' })
            }
          >
            <option value="txt">Fichier texte (.txt)</option>
            <option value="csv">Fichier CSV (.csv)</option>
            <option value="tsv">Fichier TSV (.tsv)</option>
          </select>
        </label>
        <label>
          Séparateur du fichier
          <select
            aria-label="Séparateur du fichier"
            className="ui-input mt-2"
            value={value.delimiter}
            onChange={(e) => onChange({ ...value, delimiter: e.target.value })}
          >
            {[
              [';', 'Point-virgule'],
              ['\t', 'Tabulation'],
              ['|', 'Barre verticale'],
              [',', 'Virgule'],
            ].map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
        </label>
        <label>
          Encodage
          <select
            aria-label="Encodage"
            className="ui-input mt-2"
            value={value.encoding}
            onChange={(e) => onChange({ ...value, encoding: e.target.value })}
          >
            <option value="utf-8">UTF-8</option>
            <option value="windows-1252">Windows-1252</option>
          </select>
        </label>
        <label>
          Format des nombres
          <select
            aria-label="Format des nombres"
            className="ui-input mt-2"
            value={value.numberCulture}
            onChange={(e) => onChange({ ...value, numberCulture: e.target.value })}
          >
            <option value="fr-FR">Virgule décimale : 100,00</option>
            <option value="en-US">Point décimal : 100.00</option>
          </select>
        </label>
        <label>
          Format de date
          <select
            aria-label="Format de date"
            className="ui-input mt-2"
            value={value.dateFormat || 'dd/MM/yyyy'}
            onChange={(e) => onChange({ ...value, dateFormat: e.target.value })}
          >
            <option value="dd/MM/yyyy">dd/MM/yyyy</option>
            <option value="ddMMyy">ddMMyy</option>
            <option value="ddMMyyyy">ddMMyyyy</option>
            <option value="yyMMdd">yyMMdd</option>
            <option value="yyyyMMdd">yyyyMMdd</option>
            <option value="yyyy-MM-dd">yyyy-MM-dd</option>
            <option value="dd-MM-yyyy">dd-MM-yyyy</option>
            <option value="MM/dd/yyyy">MM/dd/yyyy</option>
          </select>
        </label>
        <label className="entry-model-check">
          <input
            type="checkbox"
            checked={value.includeHeader}
            onChange={(e) => onChange({ ...value, includeHeader: e.target.checked })}
          />{' '}
          Inclure les en-têtes dans le fichier
        </label>
      </div>
      {!value.columns.some((c) => c.display) || !value.columns.some((c) => c.export) ? (
        <p role="alert" className="conversion-alert warning">
          Conservez au moins une colonne pour l’affichage et une colonne pour l’export.
        </p>
      ) : null}
    </fieldset>
  );
}
