'use client';
import { FormEvent, useEffect, useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { Input } from './ui/input';
import { Button } from './ui/button';
import { all, Bank, Client, request } from '@/services/api';
const schemas = {
  clients: {
    defaults: { country: 'Maroc' },
    fields: [
      ['name', 'Nom'],
      ['legalName', 'Raison sociale'],
      ['ice', 'ICE (15 chiffres)'],
      ['if', 'IF'],
      ['rc', 'RC'],
      ['email', 'Email'],
      ['phone', 'Téléphone'],
      ['country', 'Pays'],
    ],
  },
  'bank-accounts': {
    defaults: { currency: 'MAD', journal: 'BQ', accountCode: '512000' },
    fields: [
      ['clientId', 'Client'],
      ['bankId', 'Banque'],
      ['accountNumber', 'Numéro de compte'],
      ['accountName', 'Intitulé'],
      ['currency', 'Devise'],
      ['journal', 'Journal comptable'],
      ['accountCode', 'Compte comptable'],
    ],
  },
};
export function ResourceEditor({
  resource,
  initial,
  onClose,
  onSaved,
}: {
  resource: keyof typeof schemas;
  initial?: object;
  onClose: () => void;
  onSaved: () => void;
}) {
  const schema = schemas[resource];
  const [values, setValues] = useState<Record<string, unknown>>({ ...schema.defaults, ...initial });
  const [banks, setBanks] = useState<Bank[]>([]);
  const [clients, setClients] = useState<Client[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (resource !== 'bank-accounts') return;
    let active = true;
    Promise.all([all<Bank>('/banks'), all<Client>('/clients')])
      .then(([b, c]) => {
        if (active) {
          setBanks(b);
          setClients(c);
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [resource]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    const payload = Object.fromEntries(schema.fields.map(([key]) => [key, values[key] ?? '']));
    try {
      await request(`/${resource}${values.id ? `/${values.id}` : ''}`, {
        method: values.id ? 'PUT' : 'POST',
        body: JSON.stringify(payload),
      });
      onSaved();
      onClose();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <BankDialog
      title={`${values.id ? 'Modifier' : 'Créer'} · ${resource === 'clients' ? 'Client' : 'Compte bancaire'}`}
      onClose={onClose}
      busy={busy}
    >
      <form onSubmit={submit} className="space-y-5">
        <fieldset disabled={busy} className="grid gap-4 sm:grid-cols-2">
          {schema.fields.map(([key, label]) => (
            <label key={key} className="space-y-2 text-sm">
              <span>{label}</span>
              {key === 'bankId' || key === 'clientId' ? (
                <select
                  className="ui-input"
                  required
                  value={String(values[key] ?? '')}
                  onChange={(e) => setValues((v) => ({ ...v, [key]: e.target.value }))}
                >
                  <option value="">Choisir…</option>
                  {(key === 'bankId' ? banks : clients).map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              ) : (
                <Input
                  required
                  maxLength={2000}
                  type={key === 'email' ? 'email' : 'text'}
                  value={String(values[key] ?? '')}
                  onChange={(e) => setValues((v) => ({ ...v, [key]: e.target.value }))}
                />
              )}
            </label>
          ))}
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
