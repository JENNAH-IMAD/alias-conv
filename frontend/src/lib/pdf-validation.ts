export function validatePdf(file: Pick<File, 'name' | 'size' | 'type'>): string | null {
  if (!/\.pdf$/i.test(file.name) || (file.type && file.type !== 'application/pdf')) {
    return 'Choisissez un fichier au format PDF.';
  }
  if (file.size === 0) return 'Ce fichier est vide. Choisissez un autre relevé.';
  return null;
}
