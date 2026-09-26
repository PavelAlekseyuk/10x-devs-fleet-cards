import { Injectable, signal } from '@angular/core';

export const DEFAULT_TIME_ZONE = 'Europe/Warsaw';

@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private readonly timeZoneState = signal(this.readTimeZone());
  readonly timeZone = this.timeZoneState.asReadonly();

  setTimeZone(timeZone: string): boolean {
    const normalized = timeZone.trim();
    if (!this.isValidTimeZone(normalized)) {
      return false;
    }

    this.timeZoneState.set(normalized);
    localStorage.setItem('fleetcards-time-zone', normalized);
    return true;
  }

  private readTimeZone(): string {
    const saved = localStorage.getItem('fleetcards-time-zone');
    return saved && this.isValidTimeZone(saved) ? saved : DEFAULT_TIME_ZONE;
  }

  private isValidTimeZone(timeZone: string): boolean {
    if (!timeZone) {
      return false;
    }

    try {
      new Intl.DateTimeFormat('en-US', { timeZone });
      return true;
    } catch {
      return false;
    }
  }
}