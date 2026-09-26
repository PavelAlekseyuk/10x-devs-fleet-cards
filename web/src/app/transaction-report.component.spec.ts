import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';
import { TransactionReportComponent } from './transaction-report.component';
import { TransactionReport } from './transaction-report.service';

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
  totals: { netTotal: 90, vatTotal: 20.7, grossTotal: 999.99, rowCount: 1 },
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

  it('requests the selected year and month when the month picker changes', async () => {
    const fixture = createFixture();
    reportRequest().flush({ ...reportResponse, transactions: [] });
    await fixture.whenStable();

    const monthInput = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[type="month"]',
    )!;
    monthInput.value = '2026-09';
    monthInput.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    const request = reportRequest();
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('09');
    request.flush({ ...reportResponse, transactions: [] });
    await fixture.whenStable();
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