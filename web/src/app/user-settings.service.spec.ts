import { TestBed } from '@angular/core/testing';
import { DEFAULT_TIME_ZONE, UserSettingsService } from './user-settings.service';

describe('UserSettingsService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [UserSettingsService] });
  });

  it('uses the CET-compatible default and persists a valid IANA timezone', () => {
    const settings = TestBed.inject(UserSettingsService);

    expect(settings.timeZone()).toBe(DEFAULT_TIME_ZONE);
    expect(settings.setTimeZone('America/Los_Angeles')).toBe(true);
    expect(settings.timeZone()).toBe('America/Los_Angeles');
    expect(localStorage.getItem('fleetcards-time-zone')).toBe('America/Los_Angeles');
  });

  it('restores a persisted timezone and rejects invalid values without replacing it', () => {
    localStorage.setItem('fleetcards-time-zone', 'Asia/Tokyo');
    const settings = TestBed.inject(UserSettingsService);

    expect(settings.timeZone()).toBe('Asia/Tokyo');
    expect(settings.setTimeZone('Not/A_Timezone')).toBe(false);
    expect(settings.timeZone()).toBe('Asia/Tokyo');
    expect(localStorage.getItem('fleetcards-time-zone')).toBe('Asia/Tokyo');
  });
});