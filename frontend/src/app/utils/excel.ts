// Shared by the Products, Orders, and Stock History pages' "Export Excel"
// buttons. Builds a real .xlsx workbook client-side via SheetJS — no
// server endpoint needed, and unlike CSV, opens in Excel with correct
// column types (numbers stay numbers) instead of everything as text.
import * as XLSX from 'xlsx';

export function downloadExcel(filename: string, headers: string[], rows: unknown[][]): void {
  const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Sheet1');
  XLSX.writeFile(workbook, filename);
}
