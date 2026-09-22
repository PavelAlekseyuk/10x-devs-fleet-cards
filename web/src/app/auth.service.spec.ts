import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads CSRF before signing in and stores the returned role', async () => {
    const promise = service.login({ UserName: 'anna.nowak', Password: 'Anna123!' });
    const csrf = http.expectOne('/api/auth/csrf');
    csrf.flush(null);
    await Promise.resolve();
    const login = http.expectOne('/api/auth/login');
    login.flush({ userName: 'anna.nowak', permission: 'ReadWrite' });

    expect(await promise).toBe(true);
    expect(service.user()?.permission).toBe('ReadWrite');
  });

  it('uses one generic error for invalid credentials', async () => {
    const promise = service.login({ UserName: 'unknown.user', Password: 'wrong-password' });
    http.expectOne('/api/auth/csrf').flush(null);
    await Promise.resolve();
    http.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(await promise).toBe(false);
    expect(service.error()).toBe('We could not sign you in with those details.');
  });
});