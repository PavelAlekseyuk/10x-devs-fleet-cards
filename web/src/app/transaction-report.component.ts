import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ButtonComponent } from './shared/ui/button.component';
import { StatusBadgeComponent } from './shared/ui/status-badge.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { LanguageService, TranslationKey } from './language.service';
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
  protected readonly invalidTimeZone = signal(false);
  protected readonly timeZoneDraft = signal(this.settings.timeZone());

  private readonly destroyRef = inject(DestroyRef);
  private activeRequest?: Subscription;

  ngOnInit(): void {
    this.loadReport();
  }

  protected onMonthChange(event: Event): void {
    const month = (event.target as HTMLInputElement).value;
    this.invalidMonth.set(!this.reports.setSelectedMonth(month));
    if (!this.invalidMonth()) {
      this.loadReport();
    }
  }

  protected moveMonth(offset: number): void {
    if (this.reports.moveSelectedMonth(offset)) {
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
    return new Intl.DateTimeFormat(this.locale, {
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date(`${month}-01T00:00:00Z`));
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