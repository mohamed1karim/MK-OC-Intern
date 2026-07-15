import { Pipe, PipeTransform } from '@angular/core';

// Angular's built-in `currency` pipe places the symbol/code according to
// CLDR locale data, which for an unmapped code like EGP means it's
// prefixed with no space ("EGP10.00") — not something a pipe parameter can
// override, since placement isn't one of its options. This formats the
// number ourselves and appends " EGP" as a suffix instead.
@Pipe({ name: 'egp', standalone: true })
export class EgpCurrencyPipe implements PipeTransform {
  transform(value: number | string | null | undefined): string {
    const num = typeof value === 'string' ? parseFloat(value) : value;
    if (num === null || num === undefined || Number.isNaN(num)) return '';

    return `${num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} EGP`;
  }
}
