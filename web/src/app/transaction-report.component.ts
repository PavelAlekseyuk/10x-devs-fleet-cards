import { Component, DestroyRef, inject, linkedSignal, OnInit, signal } from '@angular/core';
import { ButtonComponent } from './shared/ui/button.component';
import { StatusBadgeComponent } from './shared/ui/status-badge.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { LanguageService, TranslationKey } from './language.service';
import { formatMonthYear } from './month-year';
import { TransactionReport, TransactionReportRow, TransactionReportService } from './transaction-report.service';
import { UserSettingsService } from './user-settings.service';

const fuelTranslationKeys: Record<string, TranslationKey> = {
  '95': 'fuel95',
  '98': 'fuel98',
  Diesel: 'fuelDiesel',
  '95+': 'fuel95Plus',
  '98+': 'fuel98Plus',
  'Diesel+': 'fuelDieselPlus',
};

const statusTranslationKeys: Record<string, TranslationKey> = {
  completed: 'transactionCompleted',
  recalculated: 'transactionRecalculated',
  refunded: 'transactionRefunded',
  cancelled: 'transactionCancelled',
};

const monthTranslationKeys: TranslationKey[] = [
  'monthJanuary',
  'monthFebruary',
  'monthMarch',
  'monthApril',
  'monthMay',
  'monthJune',
  'monthJuly',
  'monthAugust',
  'monthSeptember',
  'monthOctober',
  'monthNovember',
  'monthDecember',
];

@Component({
  selector: 'app-transaction-report',
  standalone: true,
  imports: [ButtonComponent, StatusBadgeComponent],
  templateUrl: './transaction-report.component.html',
  styleUrl: './transaction-report.component.scss',
})
export class TransactionReportComponent implements OnInit {
  protected readonly language = inject(LanguageService);
  protected readonly reports = inject(TransactionReportService);
  protected readonly settings = inject(UserSettingsService);
  protected readonly timeZoneOptions = getTimeZoneOptions();
  protected readonly report = signal<TransactionReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly invalidMonth = signal(false);
  protected readonly monthTranslationKeys = monthTranslationKeys;
  protected readonly monthDraft = linkedSignal(() => this.reports.selectedMonth().slice(5));
  protected readonly yearDraft = linkedSignal(() => this.reports.selectedMonth().slice(0, 4));
  protected readonly invalidTimeZone = signal(false);
  protected readonly timeZoneDraft = signal(this.settings.timeZone());

  private readonly destroyRef = inject(DestroyRef);
  private activeRequest?: Subscription;

  ngOnInit(): void {
    this.loadReport();
  }

  protected onMonthChange(event: Event): void {
    this.monthDraft.set((event.target as HTMLSelectElement).value);
    this.commitPeriod();
  }

  protected onYearInput(event: Event): void {
    this.yearDraft.set((event.target as HTMLInputElement).value);
    this.invalidMonth.set(!this.isValidYear(this.yearDraft()));
  }

  protected onYearChange(): void {
    this.commitPeriod();
  }

  protected moveMonth(offset: number): void {
    if (this.reports.moveSelectedMonth(offset)) {
      this.monthDraft.set(this.reports.selectedMonth().slice(5));
      this.yearDraft.set(this.reports.selectedMonth().slice(0, 4));
      this.invalidMonth.set(false);
      this.loadReport();
    }
  }

  protected onTimeZoneInput(event: Event): void {
    this.timeZoneDraft.set((event.target as HTMLInputElement).value);
    this.invalidTimeZone.set(false);
  }

  protected applyTimeZone(): void {
    if (!this.settings.setTimeZone(this.timeZoneDraft())) {
      this.invalidTimeZone.set(true);
      return;
    }

    this.timeZoneDraft.set(this.settings.timeZone());
    this.invalidTimeZone.set(false);
    this.loadReport();
  }

  protected retry(): void {
    this.loadReport();
  }

  protected formatMonth(month: string): string {
    return formatMonthYear(month, this.locale);
  }

  protected formatUserTime(value: string): string {
    return new Intl.DateTimeFormat(this.locale, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: this.settings.timeZone(),
    }).format(new Date(value));
  }

  protected formatPointOfSaleTime(value: string): string {
    const wallClock = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})/.exec(value)?.[1];
    if (!wallClock) {
      return value;
    }

    return new Intl.DateTimeFormat(this.locale, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'UTC',
    }).format(new Date(`${wallClock}Z`));
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat(this.locale, { style: 'currency', currency: 'PLN' }).format(value);
  }

  protected formatQuantity(value: number): string {
    const quantity = new Intl.NumberFormat(this.locale, { maximumFractionDigits: 3 }).format(value);
    return `${quantity} ${this.language.text('litersUnit')}`;
  }

  protected formatVatRate(value: number): string {
    return new Intl.NumberFormat(this.locale, {
      style: 'percent',
      maximumFractionDigits: 1,
    }).format(value);
  }

  protected fuelLabel(fuelType: string): string {
    const key = fuelTranslationKeys[fuelType];
    return key ? this.language.text(key) : fuelType;
  }

  protected statusLabel(status: string): string {
    const key = statusTranslationKeys[status.toLowerCase()];
    return this.language.text(key ?? 'transactionStatusOther');
  }

  protected isWarningStatus(status: string): boolean {
    return status.toLowerCase() === 'refunded' || status.toLowerCase() === 'cancelled';
  }

  private get locale(): string {
    return this.language.language() === 'PL' ? 'pl-PL' : 'en-GB';
  }

  private isValidYear(year: string): boolean {
    return /^\d{4}$/.test(year) && Number(year) >= 1 && Number(year) <= 9998;
  }

  private commitPeriod(): void {
    const period = `${this.yearDraft()}-${this.monthDraft()}`;
    if (!this.isValidYear(this.yearDraft())) {
      this.invalidMonth.set(true);
      return;
    }

    const changed = period !== this.reports.selectedMonth();
    this.invalidMonth.set(!this.reports.setSelectedMonth(period));
    if (!this.invalidMonth() && changed) {
      this.loadReport();
    }
  }

  private loadReport(): void {
    this.activeRequest?.unsubscribe();
    this.report.set(null);
    this.loading.set(true);
    this.error.set(false);
    this.activeRequest = this.reports
      .getReport()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (report) => {
          this.report.set(report);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        },
      });
  }
}

function getTimeZoneOptions(): string[] {
  const intlWithTimeZones = Intl as typeof Intl & {
    supportedValuesOf?: (key: 'timeZone') => string[];
  };
  const supported = intlWithTimeZones.supportedValuesOf?.('timeZone') ?? [
    'Europe/Warsaw',
    'Europe/London',
    'America/New_York',
    'Asia/Tokyo',
  ];
  return [...new Set(['Europe/Warsaw', 'UTC', ...supported])].sort((left, right) =>
    left.localeCompare(right),
  );
}

export type { TransactionReportRow };