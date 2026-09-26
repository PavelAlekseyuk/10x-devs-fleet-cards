import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { ShellComponent } from './shell.component';
import { TransactionReportService } from './transaction-report.service';

const clientResponse = {
  legalName: 'Baltic Logistics Sp. z o.o.',
  nip: '5250001234',
  jurisdiction: 'Poland',
  address: 'ul. Prosta 12, 00-850 Warszawa',
};

const summaryResponse = {
  activeVehicleCount: 4,
  activeCardCount: 3,
  periodTotals: { netTotal: 100, vatTotal: 23, grossTotal: 123, rowCount: 2 },
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

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Piotr Wisniewski');
    expect(text).toContain('Baltic Logistics Sp. z o.o.');
    expect(text).toContain('123.00');
    expect(text).toContain('4');
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
});