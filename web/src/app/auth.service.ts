import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LanguageService } from './language.service';

export interface CurrentUser {
  userName: string;
  permission: 'ReadWrite' | 'ReadOnly';
}

export interface LoginCredentials {
  UserName: string;
  Password: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly user = signal<CurrentUser | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  private readonly language = inject(LanguageService);

  constructor(private readonly http: HttpClient) {}

  async restoreSession(): Promise<CurrentUser | null> {
    try {
      const user = await firstValueFrom(this.http.get<CurrentUser>('/api/auth/me'));
      this.user.set(user);
      return user;
    } catch {
      this.user.set(null);
      return null;
    }
  }

  async login(credentials: LoginCredentials): Promise<boolean> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.http.get('/api/auth/csrf'));
      const user = await firstValueFrom(
        this.http.post<CurrentUser>('/api/auth/login', credentials),
      );
      this.user.set(user);
      return true;
    } catch (error) {
      this.user.set(null);
      this.error.set(this.messageFor(error));
      return false;
    } finally {
      this.loading.set(false);
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.user.set(null);
    }
  }

  private messageFor(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 429) {
      return this.language.text('throttledError');
    }
    return this.language.text('genericError');
  }
}
