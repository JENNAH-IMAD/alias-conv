type ExportFileHandle = {
  name: string;
  createWritable: () => Promise<{
    write: (data: Blob) => Promise<void>;
    close: () => Promise<void>;
    abort: () => Promise<void>;
  }>;
};
type PickerWindow = Window & {
  showSaveFilePicker?: (options: {
    suggestedName: string;
    id: string;
    types: { description: string; accept: Record<string, string[]> }[];
  }) => Promise<ExportFileHandle>;
};
export type ExportDestination = { name: string; handle?: ExportFileHandle };

export function exportFileName(value: string, extension: string): string {
  const name = value.trim();
  if (
    !name ||
    name.length > 180 ||
    /[<>:"/\\|?*]/.test(name) ||
    Array.from(name).some((character) => character.charCodeAt(0) < 32) ||
    /[. ]$/.test(name) ||
    /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(name)
  ) {
    throw new Error('Choisissez un nom de fichier valide, sans caractères < > : " / \\ | ? *.');
  }
  return name.toLowerCase().endsWith(`.${extension.toLowerCase()}`) ? name : `${name}.${extension}`;
}

export function canChooseExportLocation(): boolean {
  return (
    typeof window !== 'undefined' &&
    window.isSecureContext &&
    typeof (window as PickerWindow).showSaveFilePicker === 'function'
  );
}

// Call from the button action before any network await to preserve user activation.
export async function chooseExportDestination(
  name: string,
  extension: string,
  chooseLocation = true,
): Promise<ExportDestination> {
  if (!chooseLocation || !canChooseExportLocation()) return { name };
  const handle = await (window as PickerWindow).showSaveFilePicker!({
    suggestedName: name,
    id: 'alias-exports',
    types: [
      {
        description: extension === 'csv' ? 'Fichier CSV' : 'Fichier texte',
        accept: { [extension === 'csv' ? 'text/csv' : 'text/plain']: [`.${extension}`] },
      },
    ],
  });
  return { name: handle.name, handle };
}

export async function saveExportBlob(blob: Blob, destination: ExportDestination): Promise<void> {
  if (destination.handle) {
    const writable = await destination.handle.createWritable();
    try {
      await writable.write(blob);
      await writable.close();
    } catch (error) {
      await writable.abort().catch(() => {});
      throw error;
    }
    return;
  }
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = destination.name.replace(/[\\/\r\n]/g, '_');
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 10000);
}
