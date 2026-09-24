import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { FleetVehiclesComponent } from './fleet-vehicles.component';
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

const archivedVehicle: FleetVehicle = {
  ...activeVehicle,
  id: 'vehicle-2',
  registrationPlate: 'KR 98765',
  isArchived: true,
};

describe('FleetVehiclesComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FleetVehiclesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createFixture(permission: 'ReadWrite' | 'ReadOnly' = 'ReadWrite') {
    TestBed.inject(AuthService).user.set({ userName: 'test.user', permission });
    return TestBed.createComponent(FleetVehiclesComponent);
  }

  function flushInitial(fixture: ReturnType<typeof createFixture>, vehicles = [activeVehicle]) {
    const request = http.expectOne((item) => item.url === '/api/cars');
    expect(request.request.params.get('search')).toBe('');
    expect(request.request.params.get('includeArchived')).toBe('false');
    request.flush(vehicles);
    fixture.detectChanges();
  }

  it('loads active vehicles by default and shows their fuel and description', () => {
    const fixture = createFixture();
    fixture.detectChanges();
    flushInitial(fixture);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('WA 12345');
    expect(text).toContain('Volvo FH');
    expect(text).toContain('Diesel');
    expect(text).not.toContain('KR 98765');
  });

  it('requests archived vehicles when the archived filter is selected', () => {
    const fixture = createFixture();
    fixture.detectChanges();
    flushInitial(fixture);

    const archivedButton = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).find((button) => button.textContent?.includes('Archived'));
    archivedButton?.click();
    fixture.detectChanges();

    const request = http.expectOne((item) => item.url === '/api/cars');
    expect(request.request.params.get('includeArchived')).toBe('true');
    request.flush([activeVehicle, archivedVehicle]);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('KR 98765');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('WA 12345');
  });

  it('uses the search API query when search is submitted', async () => {
    const fixture = createFixture();
    fixture.detectChanges();
    flushInitial(fixture);

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '#vehicle-search',
    )!;
    input.value = '  WA 12 ';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLFormElement>('.vehicle-search')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const request = http.expectOne((item) => item.url === '/api/cars');
    expect(request.request.params.get('search')).toBe('WA 12');
    expect(request.request.params.get('includeArchived')).toBe('false');
    request.flush([activeVehicle]);
  });

  it('hides all mutation controls for ReadOnly users', () => {
    const fixture = createFixture('ReadOnly');
    fixture.detectChanges();
    flushInitial(fixture);

    const actionLabels = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '.vehicle-heading button, .row-actions button',
      ),
    ).map((button) => button.textContent?.trim());
    expect(actionLabels).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).querySelector('.row-actions')).toBeNull();
  });

  it('validates required plate and fuel type before creating', () => {
    const fixture = createFixture();
    fixture.detectChanges();
    flushInitial(fixture);
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.vehicle-heading .primary-button')!
      .click();
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLFormElement>('.vehicle-form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Enter a registration plate and choose a fuel type.',
    );
    http.expectNone((request) => request.method === 'POST' && request.url === '/api/cars');
  });

  it('creates and edits vehicles with the expected request bodies', async () => {
    const fixture = createFixture();
    fixture.detectChanges();
    flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;

    element.querySelector<HTMLButtonElement>('.vehicle-heading .primary-button')!.click();
    fixture.detectChanges();
    element.querySelector<HTMLInputElement>('[name="registrationPlate"]')!.value = 'GD 24680';
    element
      .querySelector<HTMLInputElement>('[name="registrationPlate"]')!
      .dispatchEvent(new Event('input'));
    const fuel = element.querySelector<HTMLSelectElement>('[name="fuelType"]')!;
    fuel.value = '98+';
    fuel.dispatchEvent(new Event('change'));
    const make = element.querySelector<HTMLInputElement>('[name="make"]')!;
    make.value = 'Volvo';
    make.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    element
      .querySelector<HTMLFormElement>('.vehicle-form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const createRequest = http.expectOne('/api/cars');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual({
      registrationPlate: 'GD 24680',
      fuelType: '98+',
      make: 'Volvo',
      model: null,
    });
    createRequest.flush({ ...activeVehicle, registrationPlate: 'GD 24680', fuelType: '98+' });
    fixture.detectChanges();
    const refreshed = http.expectOne((request) => request.url === '/api/cars');
    refreshed.flush([activeVehicle]);
    fixture.detectChanges();

    element.querySelector<HTMLButtonElement>('.row-actions .text-action')!.click();
    fixture.detectChanges();
    element.querySelector<HTMLInputElement>('[name="model"]')!.value = 'FH 500';
    element.querySelector<HTMLInputElement>('[name="model"]')!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    element
      .querySelector<HTMLFormElement>('.vehicle-form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const updateRequest = http.expectOne('/api/cars/vehicle-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.model).toBe('FH 500');
    updateRequest.flush({ ...activeVehicle, model: 'FH 500' });
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/cars').flush([activeVehicle]);
  });

  it('archives active and restores archived vehicles', () => {
    const fixture = createFixture();
    fixture.detectChanges();
    flushInitial(fixture);
    const element = fixture.nativeElement as HTMLElement;

    element.querySelector<HTMLButtonElement>('.danger-action')!.click();
    const archiveRequest = http.expectOne('/api/cars/vehicle-1/archive');
    expect(archiveRequest.request.method).toBe('POST');
    archiveRequest.flush({ ...activeVehicle, isArchived: true });
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/cars').flush([]);
    fixture.detectChanges();

    element.querySelector<HTMLButtonElement>('.filter-control button:last-child')!.click();
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/cars').flush([archivedVehicle]);
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.row-actions .text-action')!.click();
    const restoreRequest = http.expectOne('/api/cars/vehicle-2/restore');
    expect(restoreRequest.request.method).toBe('POST');
    restoreRequest.flush({ ...archivedVehicle, isArchived: false });
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/cars').flush([activeVehicle]);
  });

  it('uses English and Polish labels for vehicle controls and fuel choices', () => {
    const language = TestBed.inject(LanguageService);
    expect(language.text('vehicleSearch')).toBe('Search vehicles');
    expect(language.text('fuelDiesel')).toBe('Diesel');
    language.language.set('PL');
    expect(language.text('vehicleSearch')).toBe('Szukaj pojazdów');
    expect(language.text('fuelDiesel')).toBe('Olej napędowy');
    expect(language.text('vehicleArchive')).toBe('Archiwizuj');
  });
});