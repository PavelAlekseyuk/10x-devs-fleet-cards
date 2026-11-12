export function formatMonthYear(month: string, locale: string): string {
  const formatted = new Intl.DateTimeFormat(locale, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(`${month}-01T00:00:00Z`));
  return formatted.charAt(0).toLocaleUpperCase(locale) + formatted.slice(1);
}
