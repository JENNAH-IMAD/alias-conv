'use client';
import { useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { hasSavePicker, saveExport, validateExportName } from '@/lib/save-export';

export type ExportRequest = { name: string; getBlob: () => Promise<Blob> };
export function ExportDialog({ request, onClose, onSaved }: { request: ExportRequest; onClose: () => void; onSaved: (message: string) => void }) {
  const [name, setName] = useState(request.name);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const extension = request.name.split('.').pop() || 'csv';
  async function save(choose: boolean) {
    if (busy) return;
    setError('');
    try {
      const valid = validateExportName(name, extension);
      setBusy(true);
      const message = await saveExport(valid, request.getBlob, choose);
      if (message) { onSaved(message); onClose(); }
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <BankDialog title="Enregistrer le fichier" onClose={onClose} busy={busy}>
    <label className="block"><span className="ui-label">Nom du fichier</span><Input value={name} maxLength={180} disabled={busy} onChange={e => setName(e.target.value)} /></label>
    <p className="mt-4 text-sm leading-6 text-muted-foreground">{hasSavePicker() ? 'Choisissez le dossier sur votre ordinateur à l’étape suivante, ou utilisez le dossier de téléchargement du navigateur.' : 'Ce navigateur utilise son dossier de téléchargement. Activez « Toujours demander où enregistrer les fichiers » dans ses paramètres pour choisir le dossier.'}</p>
    <p className="mt-2 text-xs text-muted-foreground">Le nom choisi concerne votre copie téléchargée. Le relevé reste disponible dans l’application. Le navigateur ne communique pas le chemin complet du dossier.</p>
    {error && <p role="alert" className="mt-4 text-sm text-destructive">{error}</p>}
    <div className="mt-6 flex flex-wrap justify-end gap-3"><Button variant="outline" disabled={busy} onClick={onClose}>Annuler</Button><Button variant={hasSavePicker() ? 'outline' : 'default'} disabled={busy} onClick={() => void save(false)}>Télécharger</Button>{hasSavePicker() && <Button disabled={busy} onClick={() => void save(true)}>{busy ? 'Enregistrement…' : 'Choisir le dossier et enregistrer'}</Button>}</div>
  </BankDialog>;
}
