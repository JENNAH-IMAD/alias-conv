export function exportName(bank: string, account: string | null | undefined, id: string, extension: string, date?: string | null) {
  const part = (s: string) => s.trim().replace(/[^\p{L}\p{N}_-]+/gu, '_').replace(/^_+|_+$/g, '').slice(0, 45) || 'releve';
  return `${part(bank)}_${part(account || 'compte')}_${(date || new Date().toISOString()).slice(0, 10)}_${id.replaceAll('-', '').slice(0, 8)}.${extension}`;
}

export function validateExportName(name: string, extension: string) {
  const value = name.trim();
  if (!value || value.length > 180 || /[<>:"/\\|?*\x00-\x1f]/.test(value) || /[. ]$/.test(value) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(value)) throw new Error('Choisissez un nom de fichier valide, sans chemin ni caractères spéciaux.');
  if (!value.toLowerCase().endsWith(`.${extension.toLowerCase()}`)) throw new Error(`Conservez l’extension .${extension}.`);
  if (value.length <= extension.length + 1) throw new Error('Le nom du fichier est obligatoire.');
  return value;
}

type SaveHandle = { name: string; createWritable(): Promise<{ write(blob: Blob): Promise<void>; close(): Promise<void>; abort(): Promise<void> }> };
type PickerWindow = Window & { showSaveFilePicker?: (options: { suggestedName: string; id: string }) => Promise<SaveHandle> };

export function hasSavePicker() { return typeof window !== 'undefined' && window.isSecureContext && !!(window as PickerWindow).showSaveFilePicker; }

export async function saveExport(name: string, getBlob: () => Promise<Blob>, chooseFolder: boolean): Promise<string | null> {
  try {
    // Request the destination while the click still has transient user activation.
    const handle = chooseFolder && hasSavePicker() ? await (window as PickerWindow).showSaveFilePicker!({ suggestedName: name, id: 'alias-exports' }) : null;
    const blob = await getBlob();
    if (handle) {
      const writable = await handle.createWritable();
      try { await writable.write(blob); await writable.close(); }
      catch (error) { await writable.abort().catch(() => {}); throw error; }
      return `« ${handle.name} » enregistré dans le dossier que vous avez choisi.`;
    }
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a'); link.href = url; link.download = name;
    document.body.appendChild(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
    return `Téléchargement de « ${name} » lancé. Retrouvez-le dans les téléchargements du navigateur (dossier défini dans ses paramètres).`;
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return null;
    throw error;
  }
}
