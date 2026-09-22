import { Injectable, signal } from '@angular/core';

export type Language = 'EN' | 'PL';
export type TranslationKey = keyof (typeof translations)['EN'];

const translations = {
  EN: {
    clientPortal: 'Client portal',
    overview: 'Overview',
    vehicles: 'Fleet vehicles',
    cards: 'Fleet cards',
    transactions: 'Transactions',
    programs: 'Programs',
    logout: 'Log out',
    username: 'Username',
    password: 'Password',
    welcome: 'Welcome back',
    signInDescription: 'Use your Fleet Cards account to continue to the client portal.',
    genericError: 'We could not sign you in with those details.',
    throttledError: 'Sign-in is temporarily unavailable. Please try again shortly.',
    readWrite: 'Read-write',
    readOnly: 'Read-only',
    demoEnvironment: 'Demo environment',
    goodMorning: 'Good morning',
    fleetSummary: 'Fleet summary',
    signInButton: 'Sign in',
    clearDecisions: 'Clearer fleet decisions.',
    manageDescription: 'Manage vehicles, virtual cards, and fuel spend from one focused workspace.',
    notifications: 'Notifications',
    issueCard: 'Issue virtual card',
    fleetVehicles: 'Fleet vehicles',
    seededVehicles: 'Seeded vehicles',
    activeCards: 'Active cards',
    virtualFleetCard: 'Virtual fleet card',
    septemberSpend: 'September spend',
    seededTransaction: 'From seeded transaction',
    client: 'Client',
    spendOverview: 'Spend overview',
    fuelSpend: 'Fuel spend',
    last30Days: 'Last 30 days',
    totalPaidWithDiscount: 'Total paid with discount',
    loading: 'Loading...',
    comparePrograms: 'Compare programs',
    clientWorkspace: 'Client workspace',
    authenticatedAccess: 'Authenticated access for',
    addNew: 'Add new',
    seededData: 'Seeded data',
    name: 'Name',
    status: 'Status',
    updated: 'Updated',
    active: 'Active',
    today: 'Today',
    vehiclesCount: '2 vehicles',
    cardCount: '1 card',
    transactionCount: '1 transaction',
    programCount: '1 program',
  },
  PL: {
    clientPortal: 'Portal klienta',
    overview: 'Przegląd',
    vehicles: 'Pojazdy floty',
    cards: 'Karty flotowe',
    transactions: 'Transakcje',
    programs: 'Programy',
    logout: 'Wyloguj',
    username: 'Nazwa użytkownika',
    password: 'Hasło',
    welcome: 'Witaj ponownie',
    signInDescription: 'Użyj konta Fleet Cards, aby przejść do portalu klienta.',
    genericError: 'Nie można zalogować przy użyciu tych danych.',
    throttledError: 'Logowanie jest tymczasowo niedostępne. Spróbuj ponownie za chwilę.',
    readWrite: 'Pełny dostęp',
    readOnly: 'Tylko odczyt',
    demoEnvironment: 'Środowisko demonstracyjne',
    goodMorning: 'Dzień dobry',
    fleetSummary: 'Podsumowanie floty',
    signInButton: 'Zaloguj się',
    clearDecisions: 'Lepsze decyzje flotowe.',
    manageDescription: 'Zarządzaj pojazdami, kartami wirtualnymi i wydatkami na paliwo w jednym miejscu.',
    notifications: 'Powiadomienia',
    issueCard: 'Wydaj kartę wirtualną',
    fleetVehicles: 'Pojazdy floty',
    seededVehicles: 'Pojazdy demonstracyjne',
    activeCards: 'Aktywne karty',
    virtualFleetCard: 'Wirtualna karta flotowa',
    septemberSpend: 'Wydatki we wrześniu',
    seededTransaction: 'Z transakcji demonstracyjnej',
    client: 'Klient',
    spendOverview: 'Przegląd wydatków',
    fuelSpend: 'Wydatki na paliwo',
    last30Days: 'Ostatnie 30 dni',
    totalPaidWithDiscount: 'Łącznie po rabacie',
    loading: 'Ładowanie...',
    comparePrograms: 'Porównaj programy',
    clientWorkspace: 'Obszar klienta',
    authenticatedAccess: 'Dostęp uwierzytelniony dla',
    addNew: 'Dodaj',
    seededData: 'Dane demonstracyjne',
    name: 'Nazwa',
    status: 'Status',
    updated: 'Aktualizacja',
    active: 'Aktywna',
    today: 'Dzisiaj',
    vehiclesCount: '2 pojazdy',
    cardCount: '1 karta',
    transactionCount: '1 transakcja',
    programCount: '1 program',
  },
} as const;

@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly language = signal<Language>(this.readLanguage());

  text(key: TranslationKey): string {
    return translations[this.language()][key];
  }

  toggle(): void {
    const next = this.language() === 'EN' ? 'PL' : 'EN';
    this.language.set(next);
    localStorage.setItem('fleetcards-language', next);
  }

  private readLanguage(): Language {
    return localStorage.getItem('fleetcards-language') === 'PL' ? 'PL' : 'EN';
  }
}