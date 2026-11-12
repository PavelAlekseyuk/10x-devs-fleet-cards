import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';
import { TransactionReportComponent } from './transaction-report.component';
import { TransactionReport, TransactionReportService } from './transaction-report.service';

const reportResponse: TransactionReport = {
  year: 2026,
  month: 9,
  timeZoneId: 'Europe/Warsaw',
  periodStartUtc: '2026-08-31T22:00:00Z',
  periodEndUtc: '2026-09-30T22:00:00Z',
  transactions: [
    {
      id: 'transaction-1',
      lineageId: 'lineage-1',
      version: 1,
      correctsTransactionId: null,
      cardId: 'card-1',
      vehicleId: 'vehicle-1',
      vehiclePlate: 'WA 12345',
      occurredAtUtc: '2026-09-09T12:00:00Z',
      userOccurredAt: '2026-09-09T14:00:00+02:00',
      posOccurredAtLocal: '2026-09-09T14:00:00+02:00',
      posTimeZoneId: 'Europe/Warsaw',
      stationName: 'Warsaw Central',
      fuelType: 'Diesel',
      quantity: 40,
      unitPrice: 6.5,
      discount: 10,
      netTotal: 90,
      vatRate: 0.23,
      vatAmount: 20.7,
      grossTotal: 110.7,
      status: 'Completed',
    },
  ],
  totals: { netTotal: 90, vatTotal: 20.7, grossTotal: 999.99, grossSavingsTotal: 2.5, rowCount: 1 },
};

describe('TransactionReportComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [TransactionReportComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createFixture() {
    const fixture = TestBed.createComponent(TransactionReportComponent);
    fixture.autoDetectChanges();
    return fixture;
  }

  function reportRequest() {
    return http.expectOne((request) => request.url === '/api/transactions/report');
  }

  it('renders server totals and keeps POS-local time unchanged when the user timezone changes', async () => {
    const fixture = createFixture();
    const initialRequest = reportRequest();
    expect(initialRequest.request.params.get('timeZone')).toBe('Europe/Warsaw');
    initialRequest.flush(reportResponse);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('WA 12345');
    expect(element.textContent).toContain('Warsaw Central');
    expect(element.textContent).toContain('999.99');
    expect(element.textContent).toContain('110.70');
    expect(element.textContent).toContain('Completed');

    const timeZoneInput = element.querySelector<HTMLInputElement>('#report-time-zone')!;
    timeZoneInput.value = 'America/Los_Angeles';
    timeZoneInput.dispatchEvent(new Event('input'));
    element.querySelector<HTMLFormElement>('.timezone-control')!.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
    await fixture.whenStable();

    const refreshedRequest = reportRequest();
    expect(refreshedRequest.request.params.get('timeZone')).toBe('America/Los_Angeles');
    expect(localStorage.getItem('fleetcards-time-zone')).toBe('America/Los_Angeles');
    refreshedRequest.flush(reportResponse);
    await fixture.whenStable();

    const cells = Array.from(element.querySelectorAll('tbody tr:first-child td'));
    expect(cells[0].textContent).toContain('05:00');
    expect(cells[1].textContent).toContain('14:00');
    expect(cells[1].textContent).toContain('Europe/Warsaw');
  });

  it('requests the selected year and month when the period controls change', async () => {
    const reports = TestBed.inject(TransactionReportService);
    reports.setSelectedMonth('2025-01');
    const fixture = createFixture();
    reportRequest().flush({ ...reportResponse, transactions: [] });
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const monthInput = element.querySelector<HTMLSelectElement>('.month-picker select')!;
    const yearInput = element.querySelector<HTMLInputElement>('.year-picker input')!;
    yearInput.value = '2026';
    yearInput.dispatchEvent(new Event('input'));
    yearInput.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    const yearRequest = reportRequest();
    expect(yearRequest.request.params.get('year')).toBe('2026');
    expect(yearRequest.request.params.get('month')).toBe('01');
    yearRequest.flush({ ...reportResponse, transactions: [] });
    monthInput.value = '09';
    monthInput.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    const request = reportRequest();
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('09');
    expect(reports.selectedMonth()).toBe('2026-09');
    request.flush({ ...reportResponse, transactions: [] });
    await fixture.whenStable();
  });

  it('updates all month names on language switches without changing the shared period or loading', async () => {
    const reports = TestBed.inject(TransactionReportService);
    reports.setSelectedMonth('2026-09');
    const fixture = createFixture();
    reportRequest().flush(reportResponse);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const month = element.querySelector<HTMLSelectElement>('.month-picker select')!;
    const labels = () => Array.from(month.options, (option) => option.textContent?.trim());
    expect(month.value).toBe('09');
    expect(labels()).toEqual([
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ]);

    const language = TestBed.inject(LanguageService);
    language.toggle();
    await fixture.whenStable();
    expect(labels()).toEqual([
      'Styczeń',
      'Luty',
      'Marzec',
      'Kwiecień',
      'Maj',
      'Czerwiec',
      'Lipiec',
      'Sierpień',
      'Wrzesień',
      'Październik',
      'Listopad',
      'Grudzień',
    ]);
    expect(element.querySelector('.totals-heading h3')?.textContent).toBe('Wrzesień 2026');
    expect(month.value).toBe('09');
    expect(reports.selectedMonth()).toBe('2026-09');
    expect(element.querySelector('.month-picker')?.textContent).toContain('Miesiąc raportu');
    http.expectNone((request) => request.url === '/api/transactions/report');

    language.toggle();
    await fixture.whenStable();
    expect(labels()[8]).toBe('September');
    expect(element.querySelector('.totals-heading h3')?.textContent).toBe('September 2026');
    expect(reports.selectedMonth()).toBe('2026-09');
    http.expectNone((request) => request.url === '/api/transactions/report');

    language.toggle();
    await fixture.whenStable();
    month.value = '03';
    month.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const request = reportRequest();
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('03');
    expect(reports.selectedMonth()).toBe('2026-03');
    request.flush(reportResponse);
  });

  it('does not commit or request invalid or incomplete years, even when changing month', async () => {
    const reports = TestBed.inject(TransactionReportService);
    reports.setSelectedMonth('2026-01');
    const fixture = createFixture();
    reportRequest().flush(reportResponse);
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    const year = element.querySelector<HTMLInputElement>('.year-picker input')!;
    const month = element.querySelector<HTMLSelectElement>('.month-picker select')!;
    for (const invalid of ['', '2', '202', '0000', '9999', '20a6']) {
      year.value = invalid;
      year.dispatchEvent(new Event('input'));
      year.dispatchEvent(new Event('change'));
      await fixture.whenStable();
      expect(year.getAttribute('aria-invalid')).toBe('true');
      expect(element.querySelector('#report-period-error')?.textContent).toContain('0001 to 9998');
      expect(reports.selectedMonth()).toBe('2026-01');
      http.expectNone((request) => request.url === '/api/transactions/report');
    }

    month.value = '12';
    month.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(reports.selectedMonth()).toBe('2026-01');
    http.expectNone((request) => request.url === '/api/transactions/report');

    year.value = '0001';
    year.dispatchEvent(new Event('input'));
    year.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const request = reportRequest();
    expect(request.request.params.get('year')).toBe('0001');
    expect(request.request.params.get('month')).toBe('12');
    expect(reports.selectedMonth()).toBe('0001-12');
    request.flush(reportResponse);
    await fixture.whenStable();
    expect(year.getAttribute('aria-invalid')).toBe('false');
  });

  it('discards an invalid draft when navigating to a different month', async () => {
    const reports = TestBed.inject(TransactionReportService);
    reports.setSelectedMonth('2026-01');
    const fixture = createFixture();
    reportRequest().flush(reportResponse);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const year = element.querySelector<HTMLInputElement>('.year-picker input')!;
    year.value = '20';
    year.dispatchEvent(new Event('input'));
    element.querySelector<HTMLButtonElement>('[aria-label="Next month"]')!.click();
    await fixture.whenStable();

    const request = reportRequest();
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('02');
    request.flush(reportResponse);
    await fixture.whenStable();
    expect(year.value).toBe('2026');
    expect(year.getAttribute('aria-invalid')).toBe('false');
    expect(reports.selectedMonth()).toBe('2026-02');
  });

  it('navigates year boundaries and preserves the period at the supported limits', async () => {
    const reports = TestBed.inject(TransactionReportService);
    reports.setSelectedMonth('2026-01');
    const fixture = createFixture();
    reportRequest().flush(reportResponse);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const previous = element.querySelector<HTMLButtonElement>('[aria-label="Previous month"]')!;
    const next = element.querySelector<HTMLButtonElement>('[aria-label="Next month"]')!;

    previous.click();
    await fixture.whenStable();
    let request = reportRequest();
    expect(request.request.params.get('year')).toBe('2025');
    expect(request.request.params.get('month')).toBe('12');
    request.flush(reportResponse);
    next.click();
    await fixture.whenStable();
    request = reportRequest();
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('01');
    request.flush(reportResponse);
    await fixture.whenStable();

    const year = element.querySelector<HTMLInputElement>('.year-picker input')!;
    year.value = '0001';
    year.dispatchEvent(new Event('input'));
    year.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    reportRequest().flush(reportResponse);
    previous.click();
    await fixture.whenStable();
    expect(reports.selectedMonth()).toBe('0001-01');
    http.expectNone((item) => item.url === '/api/transactions/report');

    const month = element.querySelector<HTMLSelectElement>('.month-picker select')!;
    year.value = '9998';
    year.dispatchEvent(new Event('input'));
    year.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    reportRequest().flush(reportResponse);
    month.value = '12';
    month.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    request = reportRequest();
    expect(request.request.params.get('year')).toBe('9998');
    expect(request.request.params.get('month')).toBe('12');
    request.flush(reportResponse);
    next.click();
    await fixture.whenStable();
    expect(reports.selectedMonth()).toBe('9998-12');
    http.expectNone((item) => item.url === '/api/transactions/report');
  });

  it('shows an empty state for a period without transactions', async () => {
    const fixture = createFixture();
    reportRequest().flush({
      ...reportResponse,
      transactions: [],
      totals: { ...reportResponse.totals, rowCount: 0 },
    });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No transactions were recorded in this period.',
    );
  });

  it('clears the report and offers retry after a request failure', async () => {
    const fixture = createFixture();
    reportRequest().flush('failure', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Could not load transactions for this period.');
    expect(element.querySelector('table')).toBeNull();
    expect(element.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('uses EN and PL resources for report labels, fuel, and statuses', async () => {
    const fixture = createFixture();
    reportRequest().flush(reportResponse);
    await fixture.whenStable();
    const language = TestBed.inject(LanguageService);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Your local time');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Diesel');
    language.language.set('PL');
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Twój czas lokalny');
    expect(text).toContain('Olej napędowy');
    expect(text).toContain('Zakończona');
  });
});