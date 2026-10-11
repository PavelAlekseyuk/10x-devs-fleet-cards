import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';
import { AdminDemoComponent } from './admin-demo.component';

describe('AdminDemoComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AdminDemoComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createFixture() {
    const fixture = TestBed.createComponent(AdminDemoComponent);
    fixture.autoDetectChanges();
    return fixture;
  }

  function setInput(element: HTMLElement, name: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
    input.value = value;
    input.dispatchEvent(new Event(name === 'count' ? 'input' : 'change', { bubbles: true }));
  }

  function submit(element: HTMLElement): void {
    element
      .querySelector<HTMLFormElement>('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  }

  it('posts the inclusive UTC date range and requested count', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    setInput(element, 'from', '2026-07-01');
    setInput(element, 'to', '2026-07-03');
    setInput(element, 'count', '12');
    submit(element);

    const request = http.expectOne('/api/admin/demo-transactions');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      from: '2026-07-01T00:00:00.000Z',
      to: '2026-07-04T00:00:00.000Z',
      count: 12,
    });
    request.flush({ generatedCount: 12 });
    await fixture.whenStable();
    expect(element.textContent).toContain('Demo transactions generated: 12');
  });

  it('validates the date range and transaction count before posting', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    setInput(element, 'from', '2026-07-04');
    setInput(element, 'to', '2026-07-03');
    setInput(element, 'count', '1001');
    submit(element);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Choose a valid date range',
    );
    http.expectNone('/api/admin/demo-transactions');
  });

  it('shows localized page strings and reports request failures', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    const language = TestBed.inject(LanguageService);
    language.language.set('PL');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Dane demonstracyjne');
    expect(element.textContent).toContain('Generuj transakcje');

    submit(element);
    const request = http.expectOne('/api/admin/demo-transactions');
    request.flush({}, { status: 500, statusText: 'Server error' });
    await fixture.whenStable();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Nie udało się wygenerować',
    );
  });
});
