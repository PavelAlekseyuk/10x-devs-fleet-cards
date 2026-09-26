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

  it('starts with the current month in the default reporting timezone', () => {
    const expected = new Intl.DateTimeFormat('en-US', {
      timeZone: 'Europe/Warsaw',
      year: 'numeric',
      month: '2-digit',
    }).formatToParts(new Date());
    const year = expected.find((part) => part.type === 'year')?.value;
    const month = expected.find((part) => part.type === 'month')?.value;

    expect(reports.selectedMonth()).toBe(`${year}-${month}`);
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
});