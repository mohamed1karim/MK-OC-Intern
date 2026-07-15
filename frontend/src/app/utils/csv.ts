// Shared by the Products and Orders pages' "Export CSV" buttons. Builds a
// CSV string client-side and triggers a browser download — no server
// endpoint or library needed, and CSV opens directly in Excel, which is
// exactly what "export to Excel" needs without pulling in a full .xlsx
// dependency for something this simple.

// Escapes a single field per RFC 4180: wrap in quotes if it contains a
// comma, quote, or newline, and double up any interior quotes.
function escapeCsvField(value: unknown): string {
  const str = value === null || value === undefined ? '' : String(value);
  if (/[",\n]/.test(str)) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

export function toCsv(headers: string[], rows: unknown[][]): string {
  const lines = [headers, ...rows].map((row) => row.map(escapeCsvField).join(','));
  return lines.join('\r\n');
}

export function downloadCsv(filename: string, headers: string[], rows: unknown[][]): void {
  const csv = toCsv(headers, rows);
  // Leading BOM so Excel opens it as UTF-8 instead of guessing the wrong
  // codepage for any accented characters in product/user names.
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();

  URL.revokeObjectURL(url);
}
