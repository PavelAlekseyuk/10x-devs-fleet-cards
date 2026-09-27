import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { LanguageService } from './language.service';
import { ShellComponent } from './shell.component';
import { TransactionReport, TransactionReportService } from './transaction-report.service';

const clientResponse = {
  legalName: 'Baltic Logistics Sp. z o.o.',
  nip: '5250001234',
  jurisdiction: 'Poland',
  address: 'ul. Prosta 12, 00-850 Warszawa',
};

const summaryResponse = {
  activeVehicleCount: 4,
  activeCardCount: 3,
  periodTotals: { netTotal: 100, vatTotal: 23, grossTotal: 123, grossSavingsTotal: 12, rowCount: 2 },
  periodStartUtc: '2026-08-31T22:00:00Z',
  periodEndUtc: '2026-09-30T22:00:00Z',
};

describe('ShellComponent', () => {
  beforeEach(() => localStorage.clear());

  function createFixture(permission: 'ReadOnly' | 'ReadWrite' = 'ReadOnly') {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { section: 'overview' } } } },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.user.set({ userName: 'piotr.wisniewski', permission });
    const fixture = TestBed.createComponent(ShellComponent);
    fixture.autoDetectChanges();
    return { fixture, http: TestBed.inject(HttpTestingController) };
  }

  function flushClient(http: HttpTestingController): void {
    http.expectOne('/api/client').flush(clientResponse);
  }

  it('binds ReadOnly users, hides mutations, and links the live period report', async () => {
    const { fixture, http } = createFixture();
    flushClient(http);
    const reportService = TestBed.inject(TransactionReportService);
    const [year, month] = reportService.selectedMonth().split('-');
    const summary = http.expectOne((request) => request.url === '/api/dashboard/summary');
    expect(summary.request.params.get('year')).toBe(year);
    expect(summary.request.params.get('month')).toBe(month);
    expect(summary.request.params.get('timeZone')).toBe('Europe/Warsaw');
    summary.flush(summaryResponse);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const text = element.textContent ?? '';
    expect(text).toContain('Piotr Wisniewski');
    expect(text).toContain('Baltic Logistics Sp. z o.o.');
    expect(text).toContain('123.00');
    expect(text).toContain('4');
    const savingsMetric = element.querySelector('.metric-card.accent-gold')!;
    expect(savingsMetric.querySelector('span')?.textContent).toBe('Gross savings');
    expect(savingsMetric.querySelector('strong')?.textContent).toContain('12.00');
    const selectedMonth = TestBed.inject(TransactionReportService).selectedMonth();
    const expectedMonth = new Intl.DateTimeFormat('en-GB', {
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date(`${selectedMonth}-01T00:00:00Z`));
    expect(savingsMetric.querySelector('small')?.textContent).toContain(expectedMonth);

    expect(text).not.toContain('Programs');
    expect(text).not.toContain('Issue virtual card');
    expect((fixture.nativeElement as HTMLElement).querySelector('.period-link')?.getAttribute('href'))
      .toBe('/transactions');
    http.verify();
  });

  it('does not retain preview values when the dashboard summary fails', async () => {
    const { fixture, http } = createFixture('ReadWrite');
    flushClient(http);
    http.expectOne((request) => request.url === '/api/dashboard/summary').flush(
      'failure',
      { status: 500, statusText: 'Server Error' },
    );
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Could not load the period summary.');
    expect(element.textContent).toContain('Summary unavailable');
    expect(element.textContent).not.toContain('326,60 zł');
    expect(element.querySelector('.spend-total')).toBeNull();
    http.verify();
  });

  it('reloads the overview for both month controls and shares the selected report month', async () => {
    const { fixture, http } = createFixture();
    flushClient(http);
    const reports = TestBed.inject(TransactionReportService);
    reports.setSelectedMonth('2026-09');
    http.expectOne((request) => request.url === '/api/dashboard/summary').flush(summaryResponse);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLButtonElement>('[aria-label="Next month"]')!.click();
    expect(reports.selectedMonth()).toBe('2026-10');
    const octoberSummary = http.expectOne((request) => request.url === '/api/dashboard/summary');
    expect(octoberSummary.request.params.get('year')).toBe('2026');
    expect(octoberSummary.request.params.get('month')).toBe('10');
    octoberSummary.flush(summaryResponse);
    await fixture.whenStable();

    reports.getReport().subscribe();
    const octoberReport = http.expectOne((request) => request.url === '/api/transactions/report');
    expect(octoberReport.request.params.get('year')).toBe('2026');
    expect(octoberReport.request.params.get('month')).toBe('10');
    octoberReport.flush({
      year: 2026,
      month: 10,
      timeZoneId: 'Europe/Warsaw',
      periodStartUtc: '2026-09-30T22:00:00Z',
      periodEndUtc: '2026-10-31T23:00:00Z',
      transactions: [],
      totals: summaryResponse.periodTotals,
    } satisfies TransactionReport);

    element.querySelector<HTMLButtonElement>('[aria-label="Previous month"]')!.click();
    expect(reports.selectedMonth()).toBe('2026-09');
    const septemberSummary = http.expectOne((request) => request.url === '/api/dashboard/summary');
    expect(septemberSummary.request.params.get('year')).toBe('2026');
    expect(septemberSummary.request.params.get('month')).toBe('09');
    septemberSummary.flush(summaryResponse);
    await fixture.whenStable();
    http.verify();
  });

  it('shows explicit net, gross, and savings labels in English and Polish', async () => {
    const { fixture, http } = createFixture();
    flushClient(http);
    http.expectOne((request) => request.url === '/api/dashboard/summary').flush(summaryResponse);
    await fixture.whenStable();

    const language = TestBed.inject(LanguageService);
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Gross spend');
    expect(element.textContent).toContain('Gross savings');
    expect(element.textContent).toContain('Net spend');
    expect(element.textContent).toContain('12.00');

    language.language.set('PL');
    await fixture.whenStable();
    expect(element.textContent).toContain('Wydatki brutto');
    expect(element.textContent).toContain('Oszczędności brutto');
    expect(element.textContent).toContain('Wydatki netto');
    http.verify();
  });
});