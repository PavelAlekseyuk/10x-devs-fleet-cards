import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { ButtonComponent } from './shared/ui/button.component';
import { LanguageService } from './language.service';

interface DemoTransactionGenerationResponse {
  generatedCount: number;
}

const MAX_DEMO_TRANSACTION_COUNT = 1000;

@Component({
  selector: 'app-admin-demo',
  standalone: true,
  imports: [ButtonComponent],
  templateUrl: './admin-demo.component.html',
  styleUrl: './admin-demo.component.scss',
})
export class AdminDemoComponent {
  protected readonly language = inject(LanguageService);
  protected readonly fromDate = signal(localDateValue(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
  protected readonly toDate = signal(localDateValue(new Date()));
  protected readonly count = signal(10);
  protected readonly invalid = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal(false);
  protected readonly generatedCount = signal<number | null>(null);

  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private activeRequest?: Subscription;

  protected onInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.name === 'from') {
      this.fromDate.set(input.value);
    } else if (input.name === 'to') {
      this.toDate.set(input.value);
    } else if (input.name === 'count') {
      this.count.set(input.valueAsNumber);
    }
    this.invalid.set(false);
    this.error.set(false);
    this.generatedCount.set(null);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    const from = parseUtcDate(this.fromDate());
    const through = parseUtcDate(this.toDate());
    const count = this.count();
    if (
      !from ||
      !through ||
      from.getTime() > through.getTime() ||
      !Number.isInteger(count) ||
      count < 1 ||
      count > MAX_DEMO_TRANSACTION_COUNT
    ) {
      this.invalid.set(true);
      return;
    }

    const to = new Date(through.getTime());
    to.setUTCDate(to.getUTCDate() + 1);
    this.activeRequest?.unsubscribe();
    this.saving.set(true);
    this.error.set(false);
    this.generatedCount.set(null);
    this.activeRequest = this.http
      .post<DemoTransactionGenerationResponse>('/api/admin/demo-transactions', {
        from: from.toISOString(),
        to: to.toISOString(),
        count,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.saving.set(false);
          this.generatedCount.set(response.generatedCount);
        },
        error: () => {
          this.saving.set(false);
          this.error.set(true);
        },
      });
  }
}

function localDateValue(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function parseUtcDate(value: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null;
  }

  const date = new Date(`${value}T00:00:00.000Z`);
  return Number.isNaN(date.getTime()) || date.toISOString().slice(0, 10) !== value
    ? null
    : date;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}
