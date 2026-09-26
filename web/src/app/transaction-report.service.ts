import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSettingsService } from './user-settings.service';

export interface TransactionReportTotals {
  netTotal: number;
  vatTotal: number;
  grossTotal: number;
  rowCount: number;
}

export interface TransactionReportRow {
  id: string;
  lineageId: string;
  version: number | null;
  correctsTransactionId: string | null;
  cardId: string;
  vehicleId: string | null;
  vehiclePlate: string;
  occurredAtUtc: string;
  userOccurredAt: string;
  posOccurredAtLocal: string;
  posTimeZoneId: string;
  stationName: string;
  fuelType: string;
  quantity: number;
  unitPrice: number;
  discount: number;
  netTotal: number;
  vatRate: number;
  vatAmount: number;
  grossTotal: number;
  status: string;
}

export interface TransactionReport {
  year: number;
  month: number;
  timeZoneId: string;
  periodStartUtc: string;
  periodEndUtc: string;
  transactions: TransactionReportRow[];
  totals: TransactionReportTotals;
}

export interface DashboardSummary {
  activeVehicleCount: number;
  activeCardCount: number;
  periodTotals: TransactionReportTotals;
  periodStartUtc: string;
  periodEndUtc: string;
}

@Injectable({ providedIn: 'root' })
export class TransactionReportService {
  private readonly http = inject(HttpClient);
  private readonly settings = inject(UserSettingsService);
  private readonly selectedMonthState = signal(getCurrentMonth(this.settings.timeZone()));
  readonly selectedMonth = this.selectedMonthState.asReadonly();

  getReport(): Observable<TransactionReport> {
    return this.http.get<TransactionReport>('/api/transactions/report', {
      params: this.createPeriodParams(),
    });
  }

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>('/api/dashboard/summary', {
      params: this.createPeriodParams(),
    });
  }

  setSelectedMonth(month: string): boolean {
    const match = /^(\d{4})-(0[1-9]|1[0-2])$/.exec(month);
    if (!match) {
      return false;
    }

    const year = Number(match[1]);
    if (year < 1 || year > 9998) {
      return false;
    }

    this.selectedMonthState.set(month);
    return true;
  }

  moveSelectedMonth(offset: number): boolean {
    if (!Number.isInteger(offset)) {
      return false;
    }

    const [year, month] = this.selectedMonth().split('-').map(Number);
    const monthIndex = year * 12 + month - 1 + offset;
    const nextYear = Math.floor(monthIndex / 12);
    const nextMonth = (monthIndex % 12) + 1;
    return this.setSelectedMonth(`${String(nextYear).padStart(4, '0')}-${String(nextMonth).padStart(2, '0')}`);
  }

  private createPeriodParams(): HttpParams {
    const [year, month] = this.selectedMonth().split('-');
    return new HttpParams()
      .set('year', year)
      .set('month', month)
      .set('timeZone', this.settings.timeZone());
  }
}

function getCurrentMonth(timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
  }).formatToParts(new Date());
  const year = parts.find((part) => part.type === 'year')?.value ?? '0001';
  const month = parts.find((part) => part.type === 'month')?.value ?? '01';
  return `${year.padStart(4, '0')}-${month}`;
}