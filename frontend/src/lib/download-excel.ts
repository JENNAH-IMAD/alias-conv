import * as XLSX from 'xlsx';

export function downloadExcel(
  filename: string,
  columns: string[],
  rows: (string | number | null | undefined)[][],
) {
  const sheet = XLSX.utils.aoa_to_sheet([columns, ...rows]);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, sheet, 'Données');
  XLSX.writeFile(workbook, `${filename.replace(/\.(xlsx|csv|txt|tsv)$/i, '')}.xlsx`, {
    compression: true,
  });
}
