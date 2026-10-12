'use client';
import { Button } from '@/components/ui/button';
import Image from 'next/image';
import { useEffect, useState } from 'react';
import { Camera, Upload, Trash2 } from 'lucide-react';
import { request } from '@/services/api';
import { Avatar } from './avatar';
import { BankDialog } from './bank-dialogs';
type PhotoOwner = {
  id: string;
  name: string;
  photoVersion: string | null;
};
export function ClientAvatar({
  client,
  resource = 'clients',
}: {
  client: PhotoOwner;
  resource?: 'clients' | 'banks';
}) {
  const [photo, setPhoto] = useState<{
    version: string;
    url: string;
  } | null>(null);
  useEffect(() => {
    let active = true;
    if (client.photoVersion)
      request<{
        dataUrl: string | null;
      }>(`/${resource}/${client.id}/${resource === 'banks' ? 'logo' : 'photo'}`)
        .then((r) => {
          if (active && r.dataUrl) setPhoto({ version: client.photoVersion!, url: r.dataUrl });
        })
        .catch(() => {});
    return () => {
      active = false;
    };
  }, [client.id, client.photoVersion, resource]);
  return photo && photo.version === client.photoVersion ? (
    <Image
      unoptimized
      src={photo.url}
      alt={`Logo de ${client.name}`}
      width={46}
      height={46}
      className="client-logo"
      onError={() => setPhoto(null)}
    />
  ) : (
    <Avatar name={client.name} />
  );
}
export function ClientPhotoEditor({
  client,
  onClose,
  onSaved,
  resource = 'clients',
}: {
  client: PhotoOwner;
  onClose: () => void;
  onSaved: () => void;
  resource?: 'clients' | 'banks';
}) {
  const [preview, setPreview] = useState<string | null>(null);
  const [file, setFile] = useState<Blob | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [reading, setReading] = useState(false);
  async function choose(selected: File | undefined) {
    if (!selected) return;
    setError('');
    setFile(null);
    setPreview(null);
    if (!['image/png', 'image/jpeg'].includes(selected.type) || selected.size > 2 * 1024 * 1024) {
      setError('Choisissez une image PNG ou JPEG de 2 Mo maximum.');
      return;
    }
    setReading(true);
    try {
      const bitmap = await createImageBitmap(selected);
      try {
        if (bitmap.width > 12000 || bitmap.height > 12000)
          throw Error('Image trop grande (12 000 pixels maximum par côté).');
        const scale = Math.min(1, 768 / Math.max(bitmap.width, bitmap.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(bitmap.width * scale));
        canvas.height = Math.max(1, Math.round(bitmap.height * scale));
        const ctx = canvas.getContext('2d');
        if (!ctx) throw Error('Impossible de préparer l’image.');
        ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        const blob = await new Promise<Blob>((resolve, reject) =>
          canvas.toBlob(
            (b) => (b ? resolve(b) : reject(Error('Image illisible.'))),
            selected.type,
            0.9,
          ),
        );
        if (blob.size > 2 * 1024 * 1024) throw Error('Image trop volumineuse après préparation.');
        setFile(blob);
        setPreview(canvas.toDataURL(selected.type, 0.9));
      } finally {
        bitmap.close();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Image illisible.');
    } finally {
      setReading(false);
    }
  }
  async function save(remove = false) {
    setError('');
    setBusy(true);
    try {
      if (remove)
        await request(`/${resource}/${client.id}/${resource === 'banks' ? 'logo' : 'photo'}`, {
          method: 'DELETE',
        });
      else {
        if (!file) return;
        const form = new FormData();
        form.append('file', file, file.type === 'image/png' ? 'logo.png' : 'photo.jpg');
        await request(`/${resource}/${client.id}/${resource === 'banks' ? 'logo' : 'photo'}`, {
          method: 'PUT',
          body: form,
        });
      }
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
      title={resource === 'banks' ? 'Logo de la banque' : 'Photo ou logo du client'}
      onClose={onClose}
      busy={busy || reading}
    >
      <div className="space-y-5">
        <div className="photo-editor-preview">
          {preview ? (
            <Image
              unoptimized
              src={preview}
              alt="Aperçu de la nouvelle photo"
              width={128}
              height={128}
              className="photo-preview-image"
            />
          ) : (
            <ClientAvatar client={client} resource={resource} />
          )}
          <div>
            <h3 className="font-semibold">{client.name}</h3>
            <p className="ui-description">
              {resource === 'banks'
                ? 'Ajoutez le logo de votre banque.'
                : 'Logo d’entreprise ou photo de profil.'}
            </p>
          </div>
        </div>
        <label className="photo-upload">
          <Upload size={23} />
          <span className="font-semibold">Choisir une image</span>
          <span className="muted text-xs">PNG ou JPEG · 2 Mo maximum</span>
          <input
            type="file"
            accept="image/png,image/jpeg"
            disabled={busy || reading}
            onChange={(e) => {
              void choose(e.target.files?.[0]);
              e.target.value = '';
            }}
            className="ui-file"
            aria-label={resource === 'banks' ? 'Logo de la banque' : 'Photo ou logo du client'}
          />
        </label>
        <p className="muted text-xs">
          L’image est ajustée à 768 pixels maximum, sans recadrage, pour préserver votre logo.
        </p>
        {reading && <p role="status">Préparation de l’aperçu…</p>}
        {error && (
          <p role="alert" className="danger-button">
            {error}
          </p>
        )}
        <div className="flex flex-wrap justify-between gap-3">
          {client.photoVersion && (
            <Button
              variant="outline"
              className="danger-button"
              disabled={busy || reading}
              onClick={() => save(true)}
            >
              <Trash2 size={15} />{' '}
              {resource === 'banks' ? 'Supprimer le logo' : 'Supprimer la photo'}
            </Button>
          )}
          <Button className="ml-auto" disabled={busy || reading || !file} onClick={() => save()}>
            <Camera size={16} />
            {busy
              ? 'Enregistrement…'
              : resource === 'banks'
                ? 'Enregistrer le logo'
                : 'Enregistrer la photo'}
          </Button>
        </div>
      </div>
    </BankDialog>
  );
}
