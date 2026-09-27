import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, Subscription } from 'rxjs';
import { AuthService } from './auth.service';
import { CardManagementComponent } from './card-management.component';
import { FleetVehiclesComponent } from './fleet-vehicles.component';
import { LanguageService, TranslationKey } from './language.service';
import { DashboardSummary, TransactionReportService } from './transaction-report.service';
import { TransactionReportComponent } from './transaction-report.component';
import { UserSettingsService } from './user-settings.service';

type SectionKey = 'overview' | 'cars' | 'cards' | 'transactions' | 'programs';
interface NavigationItem { key: SectionKey; icon: string; }
interface Client { legalName: string; nip: string; jurisdiction: string; address: string; }

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, FleetVehiclesComponent, CardManagementComponent, TransactionReportComponent],
  templateUrl: './shell.component.html',
  styleUrl: './app.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly router = inject(Router);
  protected readonly reports = inject(TransactionReportService);
  protected readonly settings = inject(UserSettingsService);
  protected readonly activeSection: SectionKey;
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly summaryLoading = signal(true);
  protected readonly summaryError = signal(false);
  protected client: Client | null = null;
  protected readonly navigation: NavigationItem[] = [
    { key: 'overview', icon: '◒' }, { key: 'cars', icon: '▱' },
    { key: 'cards', icon: '▭' }, { key: 'transactions', icon: '↗' }, { key: 'programs', icon: '✦' },
  ];

  private readonly destroyRef = inject(DestroyRef);
  private activeSummaryRequest?: Subscription;

  constructor(route: ActivatedRoute, private readonly http: HttpClient) {
    this.activeSection = (route.snapshot.data['section'] as SectionKey) ?? 'overview';
    void this.loadClient();
    if (this.activeSection === 'overview') {
      this.loadSummary();
    }
  }

  protected get visibleNavigation(): NavigationItem[] {
    return this.auth.user()?.permission === 'ReadOnly' ? this.navigation.filter((item) => item.key !== 'programs') : this.navigation;
  }
  protected get activeTitle(): string { return this.language.text(this.activeSection === 'cars' ? 'vehicles' : this.activeSection); }
  protected get summaryStateLabel(): TranslationKey {
    if (this.summaryLoading()) {
      return 'summaryLoading';
    }
    return this.summaryError() ? 'summaryUnavailable' : 'summaryCurrent';
  }
  protected get initials(): string { return (this.auth.user()?.userName ?? 'User').split('.').map((part) => part[0]?.toUpperCase() ?? '').join(''); }
  protected get displayName(): string { return (this.auth.user()?.userName ?? 'User').split('.').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' '); }
  protected canWrite(): boolean { return this.auth.user()?.permission === 'ReadWrite'; }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat(this.locale, { style: 'currency', currency: 'PLN' }).format(value);
  }

  protected formatSelectedMonth(): string {
    return new Intl.DateTimeFormat(this.locale, {
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date(`${this.reports.selectedMonth()}-01T00:00:00Z`));
  }

  protected retrySummary(): void {
    this.loadSummary();
  }

  protected moveSummaryMonth(offset: number): void {
    if (this.reports.moveSelectedMonth(offset)) {
      this.loadSummary();
    }
  }

  protected async logout(): Promise<void> { await this.auth.logout(); await this.router.navigateByUrl('/login'); }

  private async loadClient(): Promise<void> {
    try { this.client = await firstValueFrom(this.http.get<Client>('/api/client')); }
    catch { await this.router.navigateByUrl('/login'); }
  }

  private loadSummary(): void {
    this.activeSummaryRequest?.unsubscribe();
    this.summary.set(null);
    this.summaryLoading.set(true);
    this.summaryError.set(false);
    this.activeSummaryRequest = this.reports
      .getSummary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => {
          this.summary.set(summary);
          this.summaryLoading.set(false);
        },
        error: () => {
          this.summaryLoading.set(false);
          this.summaryError.set(true);
        },
      });
  }

  private get locale(): string {
    return this.language.language() === 'PL' ? 'pl-PL' : 'en-GB';
  }
}