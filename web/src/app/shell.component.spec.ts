import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
  it('binds the authenticated user and hides ReadOnly program controls', async () => {
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
    auth.user.set({ userName: 'piotr.wisniewski', permission: 'ReadOnly' });
    const fixture = TestBed.createComponent(ShellComponent);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/client').flush({
      legalName: 'Baltic Logistics Sp. z o.o.',
      nip: '5250001234',
      jurisdiction: 'Poland',
      address: 'ul. Prosta 12, 00-850 Warszawa',
    });
    await Promise.resolve();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Piotr Wisniewski');
    expect(text).toContain('Baltic Logistics Sp. z o.o.');
    expect(text).not.toContain('Programs');
    expect(text).not.toContain('Issue virtual card');
    http.verify();
  });
});