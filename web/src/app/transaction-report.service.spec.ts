import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TransactionReportService } from './transaction-report.service';
import { UserSettingsService } from './user-settings.service';

describe('TransactionReportService', () => {
  let http: HttpTestingController;
  let reports: TransactionReportService;
  let settings: UserSettingsService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    reports = TestBed.inject(TransactionReportService);
    settings = TestBed.inject(UserSettingsService);
  });

  afterEach(() => http.verify());

  it('starts with the previous month in the default reporting timezone', () => {
    const currentParts = new Intl.DateTimeFormat('en-US', {
      timeZone: 'Europe/Warsaw',
      year: 'numeric',
      month: '2-digit',
    }).formatToParts(new Date());
    const year = Number(currentParts.find((part) => part.type === 'year')?.value);
    const month = Number(currentParts.find((part) => part.type === 'month')?.value);
    const previousMonth = new Date(Date.UTC(year, month - 2, 1));
    const expected = `${previousMonth.getUTCFullYear()}-${String(previousMonth.getUTCMonth() + 1).padStart(2, '0')}`;

    expect(reports.selectedMonth()).toBe(expected);
  });

  it('serializes the selected year, month, and timezone for the report endpoint', () => {
    reports.setSelectedMonth('2026-09');
    settings.setTimeZone('America/Los_Angeles');

    reports.getReport().subscribe();
    const request = http.expectOne((item) => item.url === '/api/transactions/report');

    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('09');
    expect(request.request.params.get('timeZone')).toBe('America/Los_Angeles');
    request.flush({});
  });

  it('uses the same selected period for the dashboard summary endpoint', () => {
    reports.setSelectedMonth('2026-01');

    reports.getSummary().subscribe();
    const request = http.expectOne((item) => item.url === '/api/dashboard/summary');

    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('01');
    expect(request.request.params.get('timeZone')).toBe('Europe/Warsaw');
    request.flush({});
  });

  it('validates month selection and navigates across year boundaries', () => {
    expect(reports.setSelectedMonth('2026-13')).toBe(false);
    expect(reports.setSelectedMonth('2026-01')).toBe(true);
    expect(reports.moveSelectedMonth(-1)).toBe(true);
    expect(reports.selectedMonth()).toBe('2025-12');
    expect(reports.moveSelectedMonth(1)).toBe(true);
    expect(reports.selectedMonth()).toBe('2026-01');
  });

  it('retains the shared selection for invalid values and navigation beyond supported years', () => {
    expect(reports.setSelectedMonth('0001-01')).toBe(true);
    expect(reports.moveSelectedMonth(-1)).toBe(false);
    for (const invalid of ['0000-12', '9999-01', '202-01', '2026-1', '2026-00', '2026-13']) {
      expect(reports.setSelectedMonth(invalid)).toBe(false);
      expect(reports.selectedMonth()).toBe('0001-01');
    }
    expect(reports.setSelectedMonth('9998-12')).toBe(true);
    expect(reports.moveSelectedMonth(1)).toBe(false);
    expect(reports.moveSelectedMonth(0.5)).toBe(false);
    expect(reports.selectedMonth()).toBe('9998-12');

    reports.getSummary().subscribe();
    const request = http.expectOne((item) => item.url === '/api/dashboard/summary');
    expect(request.request.params.get('year')).toBe('9998');
    expect(request.request.params.get('month')).toBe('12');
    request.flush({});
  });
});