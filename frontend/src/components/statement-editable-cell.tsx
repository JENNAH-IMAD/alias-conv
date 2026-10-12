'use client';
import { useRef, useState } from 'react';

export type CellKind = 'text' | 'date' | 'money' | 'direction' | 'currency';

// Edit the original value, never reverse-parse a rounded or transliterated export.
export function StatementEditableCell({
  value,
  display,
  label,
  kind = 'text',
  disabled,
  onApply,
  onPending,
  multiline = false,
  signed = false,
}: {
  value: string | number | null | undefined;
  display: React.ReactNode;
  label: string;
  kind?: CellKind;
  disabled?: boolean;
  multiline?: boolean;
  signed?: boolean;
  onApply: (value: string | null) => boolean;
  onPending: (pending: boolean) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [buffer, setBuffer] = useState('');
  const [error, setError] = useState('');
  const cancelled = useRef(false);
  function close() {
    cancelled.current = true;
    setEditing(false);
    setError('');
    onPending(false);
  }
  function apply() {
    if (cancelled.current) return;
    let next = buffer;
    if (kind === 'money') {
      next = next.replace(/[\s\u00a0\u202f]/g, '').replace(',', '.');
      if (next && !(signed ? /^-?\d+(?:\.\d+)?$/ : /^\d+(?:\.\d+)?$/).test(next)) {
        setError(signed ? 'Saisissez un montant valide.' : 'Saisissez un montant positif.');
        return;
      }
    }
    if (
      kind === 'date' &&
      next &&
      (!/^\d{4}-\d{2}-\d{2}$/.test(next) ||
        next.startsWith('0000-') ||
        !Number.isFinite(Date.parse(next)) ||
        new Date(next).toISOString().slice(0, 10) !== next)
    ) {
      setError('Saisissez une date complète valide.');
      return;
    }
    if (kind === 'currency') {
      next = next.trim().toUpperCase();
      if (next && !/^[A-Z]{3}$/.test(next)) {
        setError('Utilisez trois lettres, par exemple MAD.');
        return;
      }
    }
    if (next === String(value ?? '') || onApply(next || null)) close();
    else {
      cancelled.current = true;
      close();
    }
  }
  if (!editing || disabled)
    return (
      <button
        type="button"
        className="statement-cell-button"
        data-kind={kind}
        data-multiline={multiline || undefined}
        disabled={disabled}
        aria-label={`Modifier ${label}`}
        title={disabled ? undefined : 'Cliquer pour modifier dans le tableau'}
        onClick={() => {
          cancelled.current = false;
          setBuffer(String(value ?? ''));
          setEditing(true);
          onPending(true);
        }}
      >
        {display || <span className="conversion-empty-cell">vide</span>}
      </button>
    );
  const props = {
    autoFocus: true,
    'aria-label': label,
    'aria-invalid': !!error,
    className: 'statement-cell-input',
    value: buffer,
    onChange: (
      event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>,
    ) => {
      setBuffer(event.target.value);
      setError('');
    },
    onKeyDown: (event: React.KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        cancelled.current = true;
        close();
      }
      if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        apply();
      }
    },
  };
  return (
    <div
      className="statement-cell-editor"
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) apply();
      }}
    >
      {kind === 'direction' ? (
        <select {...props}>
          <option value="">Non renseigné</option>
          <option value="DEBIT">Débit</option>
          <option value="CREDIT">Crédit</option>
          <option value="UNKNOWN">À vérifier</option>
        </select>
      ) : multiline ? (
        <textarea {...props} rows={3} maxLength={10000} />
      ) : (
        <input
          {...props}
          type={kind === 'date' ? 'date' : 'text'}
          inputMode={kind === 'money' ? 'decimal' : undefined}
          maxLength={kind === 'currency' ? 3 : 10000}
        />
      )}
      {error && (
        <span className="statement-cell-error" role="alert">
          {error}
        </span>
      )}
      <span className="statement-cell-hint">Entrée : appliquer · Échap : annuler</span>
    </div>
  );
}
