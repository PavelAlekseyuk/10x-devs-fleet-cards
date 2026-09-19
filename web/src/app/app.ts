import { Component } from '@angular/core';

type SectionKey = 'overview' | 'cars' | 'cards' | 'transactions' | 'programs';
interface NavigationItem { key: SectionKey; label: string; icon: string; }
interface PreviewRow { name: string; detail: string; status: string; }

@Component({ selector: 'app-root', templateUrl: './app.html', styleUrl: './app.scss' })
export class App {
  protected activeSection: SectionKey = 'overview';
  protected language = 'EN';
  protected readonly navigation: NavigationItem[] = [
    { key: 'overview', label: 'Overview', icon: '◒' }, { key: 'cars', label: 'Fleet vehicles', icon: '▱' },
    { key: 'cards', label: 'Fleet cards', icon: '▭' }, { key: 'transactions', label: 'Transactions', icon: '↗' }, { key: 'programs', label: 'Programs', icon: '✦' }
  ];
  protected readonly previewRows: PreviewRow[] = [
    { name: 'WA 12345', detail: 'Volvo FH · Diesel', status: 'Active' }, { name: 'PO 98765', detail: 'Toyota Corolla · Petrol 95', status: 'Active' },
    { name: 'GD 44210', detail: 'Scania R450 · Diesel+', status: 'Blocked' }, { name: 'WI 00318', detail: 'Ford Transit · Petrol 95+', status: 'Active' }
  ];
  protected get activeTitle(): string { return this.navigation.find(item => item.key === this.activeSection)?.label ?? 'Overview'; }
  protected selectSection(section: string, event?: Event): void { event?.preventDefault(); this.activeSection = section as SectionKey; }
  protected toggleLanguage(): void { this.language = this.language === 'EN' ? 'PL' : 'EN'; }
}
