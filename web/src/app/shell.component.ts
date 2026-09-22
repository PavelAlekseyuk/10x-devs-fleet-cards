import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { LanguageService } from './language.service';

type SectionKey = 'overview' | 'cars' | 'cards' | 'transactions' | 'programs';
interface NavigationItem { key: SectionKey; icon: string; }
interface Client { legalName: string; nip: string; jurisdiction: string; address: string; }

@Component({ selector: 'app-shell', standalone: true, imports: [RouterLink], templateUrl: './shell.component.html', styleUrl: './app.scss' })
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly router = inject(Router);
  protected readonly activeSection: SectionKey;
  protected client: Client | null = null;
  protected readonly navigation: NavigationItem[] = [
    { key: 'overview', icon: '◒' }, { key: 'cars', icon: '▱' },
    { key: 'cards', icon: '▭' }, { key: 'transactions', icon: '↗' }, { key: 'programs', icon: '✦' },
  ];

  constructor(route: ActivatedRoute, private readonly http: HttpClient) {
    this.activeSection = (route.snapshot.data['section'] as SectionKey) ?? 'overview';
    void this.loadClient();
  }

  protected get visibleNavigation(): NavigationItem[] {
    return this.auth.user()?.permission === 'ReadOnly' ? this.navigation.filter((item) => item.key !== 'programs') : this.navigation;
  }
  protected get activeTitle(): string { return this.language.text(this.activeSection === 'cars' ? 'vehicles' : this.activeSection); }
  protected get initials(): string { return (this.auth.user()?.userName ?? 'User').split('.').map((part) => part[0]?.toUpperCase() ?? '').join(''); }
  protected get displayName(): string { return (this.auth.user()?.userName ?? 'User').split('.').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' '); }
  protected canWrite(): boolean { return this.auth.user()?.permission === 'ReadWrite'; }

  protected async logout(): Promise<void> { await this.auth.logout(); await this.router.navigateByUrl('/login'); }

  private async loadClient(): Promise<void> {
    try { this.client = await firstValueFrom(this.http.get<Client>('/api/client')); }
    catch { await this.router.navigateByUrl('/login'); }
  }
}