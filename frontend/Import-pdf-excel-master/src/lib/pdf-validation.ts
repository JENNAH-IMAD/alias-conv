export const MAX_PDF_BYTES = 20 * 1024 * 1024;

export function validatePdf(file: Pick<File, 'name' | 'size' | 'type'>): string | null {
  if (!/\.pdf$/i.test(file.name) || (file.type && file.type !== 'application/pdf')) {
    return 'Choisissez un fichier au format PDF.';
  }
  if (file.size === 0) return 'Ce fichier est vide. Choisissez un autre relevé.';
  if (file.size > MAX_PDF_BYTES) return 'Le fichier dépasse la limite de 20 Mo.';
  return null;
}
