import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [LanguageService] });
  });

  it('defaults to English and persists the Polish selection', () => {
    const service = TestBed.inject(LanguageService);

    expect(service.language()).toBe('EN');
    service.toggle();

    expect(service.language()).toBe('PL');
    expect(localStorage.getItem('fleetcards-language')).toBe('PL');
  });

  it('provides matching English and Polish report and status labels', () => {
    const service = TestBed.inject(LanguageService);

    expect(service.text('reportTimeZone')).toBe('Reporting timezone');
    expect(service.text('transactionRefunded')).toBe('Refunded');
    service.language.set('PL');
    expect(service.text('reportTimeZone')).toBe('Strefa czasowa raportu');
    expect(service.text('transactionRefunded')).toBe('Zwrócona');
    expect(service.text('vat')).toBe('VAT');
  });
});