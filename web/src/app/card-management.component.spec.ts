import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { CardManagementComponent } from './card-management.component';
import { FleetCard, FleetCardStateHistory } from './card-management.service';
import { FleetVehicle } from './fleet-vehicles.service';
import { LanguageService } from './language.service';

const activeVehicle: FleetVehicle = {
  id: 'vehicle-1',
  registrationPlate: 'WA 12345',
  fuelType: 'Diesel',
  make: 'Volvo',
  model: 'FH',
  isArchived: false,
};

const secondVehicle: FleetVehicle = {
  ...activeVehicle,
  id: 'vehicle-2',
  registrationPlate: 'KR 98765',
};

const card: FleetCard = {
  id: 'card-1',
  maskedPan: '•••• 4242',
  state: 'Active',
  assignedCarId: activeVehicle.id,
  assignedVehicleRegistrationPlate: activeVehicle.registrationPlate,
};

const history: FleetCardStateHistory[] = [
  {
    state: 'Blocked',
    occurredAt: '2026-09-02T09:00:00Z',
  },
  {
    state: 'Active',
    occurredAt: '2026-09-01T09:00:00Z',
  },
];

describe('CardManagementComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.removeItem('fleetcards-language');
    await TestBed.configureTestingModule({
      imports: [CardManagementComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createFixture(permission: 'ReadWrite' | 'ReadOnly' = 'ReadWrite') {
    TestBed.inject(AuthService).user.set({ userName: 'test.user', permission });
    return TestBed.createComponent(CardManagementComponent);
  }

  async function flushInitial(
    fixture: ReturnType<typeof createFixture>,
    cards: FleetCard[] = [card],
    vehicles: FleetVehicle[] = [activeVehicle, secondVehicle],
  ) {
    fixture.detectChanges();
    http.expectOne('/api/cards').flush(
      cards.map((value) => ({ ...value, providerToken: 'provider-secret-must-not-render' })),
    );
    const vehicleRequest = http.expectOne((request) => request.url === '/api/cars');
    expect(vehicleRequest.request.params.get('search')).toBe('');
    expect(vehicleRequest.request.params.get('includeArchived')).toBe('false');
    vehicleRequest.flush(vehicles);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads cards and active vehicles without exposing provider tokens', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(card.maskedPan);
    expect(text).toContain(activeVehicle.registrationPlate);
    expect(text).not.toContain('provider-secret-must-not-render');
    expect(text).not.toContain('providerToken');
  });

  it('validates an active vehicle selection before issuing', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;

    element.querySelector<HTMLFormElement>('.issue-form')!.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element.textContent).toContain('Choose an active vehicle before issuing a card.');
    http.expectNone((request) => request.method === 'POST' && request.url === '/api/cards');
  });

  it('issues a card for a selected active vehicle', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const vehicleSelect = element.querySelector<HTMLSelectElement>('#card-issue-vehicle')!;
    vehicleSelect.value = secondVehicle.id;
    vehicleSelect.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    element.querySelector<HTMLFormElement>('.issue-form')!.dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );

    const request = http.expectOne('/api/cards');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ vehicleId: secondVehicle.id });
    request.flush({
      ...card,
      id: 'card-2',
      assignedCarId: secondVehicle.id,
      assignedVehicleRegistrationPlate: secondVehicle.registrationPlate,
      providerToken: 'provider-secret-must-not-render',
    });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.textContent).toContain(secondVehicle.registrationPlate);
    expect(element.textContent).not.toContain('provider-secret-must-not-render');
  });

  it('changes state without sending or clearing the current assignment', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const stateSelect = element.querySelector<HTMLSelectElement>('[aria-label="State: •••• 4242"]')!;
    stateSelect.value = 'Blocked';
    stateSelect.dispatchEvent(new Event('change'));

    const request = http.expectOne('/api/cards/card-1/state');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ state: 'Blocked' });
    request.flush({ ...card, state: 'Blocked', providerToken: 'provider-secret' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element.textContent).toContain('WA 12345');
    expect(element.textContent).toContain('Blocked');
    expect(element.textContent).not.toContain('provider-secret');
  });

  it('selects the persisted card state after a fresh card load', async () => {
    const fixture = createFixture();
    await flushInitial(fixture, [{ ...card, state: 'Blocked' }]);
    const element = fixture.nativeElement as HTMLElement;
    const stateSelect = element.querySelector<HTMLSelectElement>(
      '[aria-label="State: •••• 4242"]',
    )!;

    expect(element.querySelector('.badge')?.textContent).toContain('Blocked');
    expect(stateSelect.value).toBe('Blocked');
  });

  it('does not expose card reassignment controls', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[aria-label="Reassign vehicle: •••• 4242"]')).toBeNull();
    expect(element.textContent).not.toContain('Reassign vehicle');
    http.expectNone((request) => request.url.includes('/assignment'));
  });

  it('loads and renders newest-first card state history', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;
    Array.from(element.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'State history',
    )!.click();

    const request = http.expectOne('/api/cards/card-1/state-history');
    expect(request.request.method).toBe('GET');
    request.flush(history);
    await fixture.whenStable();
    fixture.detectChanges();

    const timeline = element.querySelector('.state-timeline');
    expect(timeline).not.toBeNull();
    expect(Array.from(timeline!.querySelectorAll('li strong')).map((entry) => entry.textContent)).toEqual([
      'Blocked',
      'Active',
    ]);
  });

  it('hides mutation controls from ReadOnly users but leaves history available', async () => {
    const fixture = createFixture('ReadOnly');
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.issue-form')).toBeNull();
    expect(element.querySelector('[aria-label="State: •••• 4242"]')).toBeNull();
    expect(element.querySelector('[aria-label="Reassign vehicle: •••• 4242"]')).toBeNull();
    expect(element.textContent).toContain('State history');
    http.expectNone((request) => request.method === 'POST' || request.method === 'PUT');
  });

  it('renders card controls and state labels in English and Polish', async () => {
    const fixture = createFixture();
    await flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const language = TestBed.inject(LanguageService);

    expect(element.textContent).toContain('Issue a virtual card');
    expect(element.textContent).toContain('Active');
    language.language.set('PL');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.textContent).toContain('Wydaj kartę wirtualną');
    expect(element.textContent).toContain('Aktywna');
    expect(element.textContent).toContain('Historia stanu');
  });
});