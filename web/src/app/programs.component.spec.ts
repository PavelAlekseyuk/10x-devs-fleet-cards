import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';
import { ProgramsComponent } from './programs.component';
import { ProgramAssignment, ProgramComparison, ProgramsResponse } from './programs.service';

const programsResponse: ProgramsResponse = {
  programs: [
    {
      id: 'program-basic',
      name: 'Nebula Basic',
      activeFrom: '2026-01-01T00:00:00Z',
      activeTo: null,
      rules: [
        { fuelType: 'Diesel', percent: 0.05 },
        { fuelType: '95', percent: -0.02 },
        { fuelType: '98', percent: 0 },
      ],
    },
    {
      id: 'program-diesel',
      name: 'Orion Diesel',
      activeFrom: '2026-01-01T00:00:00Z',
      activeTo: null,
      rules: [{ fuelType: 'Diesel', percent: 0.1 }],
    },
  ],
  currentAssignment: {
    id: 'assignment-current',
    programId: 'program-basic',
    startsAt: '2026-01-01T00:00:00Z',
    endsAt: '2026-01-10T00:00:00Z',
  },
};

const assignmentsResponse: ProgramAssignment[] = [
  {
    id: 'assignment-current',
    programId: 'program-basic',
    startsAt: '2026-01-01T00:00:00Z',
    endsAt: '2026-01-10T00:00:00Z',
  },
  {
    id: 'assignment-next',
    programId: 'program-diesel',
    startsAt: '2026-01-12T00:00:00Z',
    endsAt: null,
  },
];

const comparisonResponse: ProgramComparison = {
  fromUtc: '2026-08-31T22:00:00Z',
  toUtc: '2026-09-30T22:00:00Z',
  baseline: { netTotal: 100, vatTotal: 23, grossTotal: 123 },
  programs: [
    {
      programId: 'program-diesel',
      programName: 'Orion Diesel',
      hypotheticalNetTotal: 90,
      hypotheticalGrossTotal: 110.7,
      netSavings: 10,
      grossSavings: 12.3,
    },
    {
      programId: 'program-basic',
      programName: 'Nebula Basic',
      hypotheticalNetTotal: 95,
      hypotheticalGrossTotal: 116.85,
      netSavings: 5,
      grossSavings: 6.15,
    },
  ],
};

describe('ProgramsComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ProgramsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createFixture() {
    const fixture = TestBed.createComponent(ProgramsComponent);
    fixture.autoDetectChanges();
    return fixture;
  }

  function loadPrograms(): void {
    http.expectOne('/api/programs').flush(programsResponse);
    http.expectOne('/api/programs/assignments').flush(assignmentsResponse);
  }

  function openDialog(element: HTMLElement): void {
    element.querySelector<HTMLButtonElement>('app-button button')!.click();
  }

  function fillEffectiveDate(element: HTMLElement, value = '2030-04-05T12:30:00'): void {
    const input = element.querySelector<HTMLInputElement>('input[type="datetime-local"]')!;
    input.value = value;
    input.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function submitDialog(element: HTMLElement): void {
    element
      .querySelector<HTMLFormElement>('.change-form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  }

  it('renders the seeded programs, assignment history, and unassigned gaps in both languages', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Nebula Basic');
    expect(element.textContent).toContain('Orion Diesel');
    expect(element.textContent).toContain('-5%');
    expect(element.textContent).toContain('+2%');
    expect(element.textContent).toContain('0%');
    expect(element.textContent).toContain('Unassigned');
    expect(element.textContent).toContain('No successor scheduled');
    expect(element.querySelector('button')?.textContent).not.toContain('Add new');

    TestBed.inject(LanguageService).language.set('PL');
    await fixture.whenStable();
    expect(element.textContent).toContain('Dostępne programy');
    expect(element.textContent).toContain('Brak przypisania');
  });

  it('shows the actual baseline and hypothetical net, gross, and savings values', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    openDialog(element);

    const request = http.expectOne((entry) => entry.url === '/api/programs/comparison');
    expect(request.request.params.get('from')).toMatch(/Z$/);
    expect(request.request.params.get('to')).toMatch(/Z$/);
    request.flush(comparisonResponse);
    await fixture.whenStable();

    const text = element.querySelector('.comparison-table')?.textContent ?? '';
    expect(text).toContain('Current actual cost');
    expect(text).toContain('100.00');
    expect(text).toContain('123.00');
    expect(text).toContain('90.00');
    expect(text).toContain('110.70');
    expect(text).toContain('12.30');
  });

  it('sorts program rows by cycling ascending, descending, and default while pinning the baseline', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    openDialog(element);
    http.expectOne((entry) => entry.url === '/api/programs/comparison').flush(comparisonResponse);
    await fixture.whenStable();

    const headers = () =>
      Array.from(element.querySelectorAll<HTMLTableCellElement>('.comparison-table thead th'));
    const buttons = () =>
      Array.from(element.querySelectorAll<HTMLButtonElement>('.comparison-table .sort-button'));
    const rowNames = () =>
      Array.from(element.querySelectorAll('.comparison-table tbody tr')).map((row) =>
        row.textContent?.includes('Current actual cost')
          ? 'baseline'
          : row.querySelector('.program-choice span')?.textContent?.trim(),
      );
    const sortValues = () =>
      headers()
        .slice(1)
        .map((header) => header.getAttribute('aria-sort'));

    expect(buttons().length).toBe(4);
    expect(headers()[0].hasAttribute('aria-sort')).toBe(false);
    expect(sortValues()).toEqual(['none', 'none', 'none', 'none']);
    expect(rowNames()).toEqual(['baseline', 'Orion Diesel', 'Nebula Basic']);

    for (let index = 0; index < 4; index++) {
      buttons()[index].click();
      await fixture.whenStable();
      const expected = ['none', 'none', 'none', 'none'];
      expected[index] = 'ascending';
      expect(sortValues()).toEqual(expected);
      expect(rowNames()).toEqual(
        ['baseline', 'Orion Diesel', 'Nebula Basic'].map((name, position) => {
          // Orion has lower cost and higher savings, so ascending order differs by column kind.
          return index < 2 ? name : ['baseline', 'Nebula Basic', 'Orion Diesel'][position];
        }),
      );

      buttons()[index].click();
      await fixture.whenStable();
      expected[index] = 'descending';
      expect(sortValues()).toEqual(expected);
      expect(rowNames()).toEqual(
        index < 2
          ? ['baseline', 'Nebula Basic', 'Orion Diesel']
          : ['baseline', 'Orion Diesel', 'Nebula Basic'],
      );

      buttons()[index].click();
      await fixture.whenStable();
      expect(sortValues()).toEqual(['none', 'none', 'none', 'none']);
      expect(rowNames()).toEqual(['baseline', 'Orion Diesel', 'Nebula Basic']);
    }
  });

  it('keeps radio selection bound to the correct program after sorting', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    openDialog(element);
    http.expectOne((entry) => entry.url === '/api/programs/comparison').flush(comparisonResponse);
    await fixture.whenStable();

    element.querySelector<HTMLButtonElement>('.comparison-table .sort-button')!.click();
    await fixture.whenStable();
    const radios = element.querySelectorAll<HTMLInputElement>('input[name="programChoice"]');
    expect(radios[0].value).toBe('program-diesel');
    radios[0].click();
    await fixture.whenStable();

    const select = element.querySelector<HTMLSelectElement>('.change-options select')!;
    expect(select.value).toBe('program-diesel');
    expect(
      element.querySelector<HTMLInputElement>('input[name="programChoice"]:checked')!.value,
    ).toBe('program-diesel');
  });
  it('requests a custom Europe/Warsaw date range as UTC instants', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    openDialog(element);
    http.expectOne((entry) => entry.url === '/api/programs/comparison').flush(comparisonResponse);
    await fixture.whenStable();

    const periodSelect = element.querySelector<HTMLSelectElement>('.comparison-controls select')!;
    periodSelect.value = 'custom';
    periodSelect.dispatchEvent(new Event('change', { bubbles: true }));
    await fixture.whenStable();

    element.querySelector<HTMLInputElement>('input[name="customFrom"]')!.value = '2026-07-01';
    element
      .querySelector<HTMLInputElement>('input[name="customFrom"]')!
      .dispatchEvent(new Event('change', { bubbles: true }));
    element.querySelector<HTMLInputElement>('input[name="customTo"]')!.value = '2026-07-02';
    element
      .querySelector<HTMLInputElement>('input[name="customTo"]')!
      .dispatchEvent(new Event('change', { bubbles: true }));
    element.querySelector<HTMLButtonElement>('.custom-period app-button button')!.click();

    const request = http.expectOne((entry) => entry.url === '/api/programs/comparison');
    expect(request.request.params.get('from')).toBe('2026-06-30T22:00:00.000Z');
    expect(request.request.params.get('to')).toBe('2026-07-02T22:00:00.000Z');
    request.flush(comparisonResponse);
    await fixture.whenStable();
  });

  it('schedules a selected program for a future effective date', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    openDialog(element);
    http.expectOne((entry) => entry.url === '/api/programs/comparison').flush(comparisonResponse);
    await fixture.whenStable();

    element.querySelector<HTMLSelectElement>('.change-options select')!.value = 'program-diesel';
    element
      .querySelector<HTMLSelectElement>('.change-options select')!
      .dispatchEvent(new Event('change', { bubbles: true }));
    fillEffectiveDate(element);
    submitDialog(element);

    const request = http.expectOne('/api/programs/assignments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      programId: 'program-diesel',
      startsAt: new Date('2030-04-05T12:30:00').toISOString(),
    });
    request.flush(null);
    http.expectOne('/api/programs').flush(programsResponse);
    http.expectOne('/api/programs/assignments').flush(assignmentsResponse);
    await fixture.whenStable();
    expect(element.textContent).toContain('The program change has been scheduled.');
  });

  it('schedules an end without a successor', async () => {
    const fixture = createFixture();
    loadPrograms();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    openDialog(element);
    http.expectOne((entry) => entry.url === '/api/programs/comparison').flush(comparisonResponse);
    await fixture.whenStable();

    element.querySelector<HTMLInputElement>('input[name="changeAction"][value="end"]')!.click();
    fillEffectiveDate(element);
    submitDialog(element);

    const request = http.expectOne('/api/programs/assignments/end');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      endsAt: new Date('2030-04-05T12:30:00').toISOString(),
    });
    request.flush(null);
    http.expectOne('/api/programs').flush(programsResponse);
    http.expectOne('/api/programs/assignments').flush(assignmentsResponse);
    await fixture.whenStable();
  });
});
