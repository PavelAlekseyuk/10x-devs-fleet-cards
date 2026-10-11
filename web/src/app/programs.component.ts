import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Subscription } from 'rxjs';
import { ButtonComponent } from './shared/ui/button.component';
import { StatusBadgeComponent } from './shared/ui/status-badge.component';
import { LanguageService, TranslationKey } from './language.service';
import {
  FleetProgram,
  ProgramAssignment,
  ProgramComparison,
  ProgramsResponse,
  ProgramsService,
} from './programs.service';

type PeriodMode = 'last-month' | 'last-30-days' | 'custom';
type ChangeAction = 'program' | 'end';
type SortKey = 'hypotheticalNetTotal' | 'hypotheticalGrossTotal' | 'netSavings' | 'grossSavings';
type SortDirection = 'asc' | 'desc';

interface ProgramHistoryEntry {
  id: string;
  programId: string | null;
  startsAt: string;
  endsAt: string | null;
}

@Component({
  selector: 'app-programs',
  standalone: true,
  imports: [ButtonComponent, StatusBadgeComponent],
  templateUrl: './programs.component.html',
  styleUrl: './programs.component.scss',
})
export class ProgramsComponent implements OnInit {
  protected readonly language = inject(LanguageService);
  protected readonly programs = signal<FleetProgram[]>([]);
  protected readonly assignments = signal<ProgramAssignment[]>([]);
  protected readonly currentAssignment = signal<ProgramAssignment | null>(null);
  protected readonly comparison = signal<ProgramComparison | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly comparisonLoading = signal(false);
  protected readonly comparisonError = signal(false);
  protected readonly saving = signal(false);
  protected readonly mutationError = signal(false);
  protected readonly changed = signal(false);
  protected readonly dialogOpen = signal(false);
  protected readonly periodMode = signal<PeriodMode>('last-month');
  protected readonly customFrom = signal(dateDaysAgoLocal(30));
  protected readonly customTo = signal(localDateValue(new Date()));
  protected readonly action = signal<ChangeAction>('program');
  protected readonly selectedProgramId = signal('');
  protected readonly startsAtValue = signal(localDateTimeValue(new Date(Date.now() + 1_000)));
  protected readonly minimumStartsAt = signal(localDateTimeValue(new Date(Date.now() + 1_000)));
  protected readonly invalidPeriod = signal(false);
  protected readonly invalidSchedule = signal(false);
  protected readonly historyEntries = computed(() => makeHistoryEntries(this.assignments()));
  protected readonly sortKey = signal<SortKey | null>(null);
  protected readonly sortDirection = signal<SortDirection>('asc');
  protected readonly sortedComparisonPrograms = computed(() => {
    const programs = this.comparison()?.programs ?? [];
    const key = this.sortKey();
    if (!key) {
      return programs;
    }
    const factor = this.sortDirection() === 'asc' ? 1 : -1;
    return [...programs].sort((left, right) => (left[key] - right[key]) * factor);
  });

  @ViewChild('changeDialog') private changeDialog?: ElementRef<HTMLDialogElement>;

  private readonly destroyRef = inject(DestroyRef);
  private activeLoad?: Subscription;
  private activeComparison?: Subscription;
  private activeMutation?: Subscription;

  ngOnInit(): void {
    this.loadData();
  }

  protected get programOptions(): FleetProgram[] {
    return this.programs();
  }

  protected openChangeDialog(): void {
    this.periodMode.set('last-month');
    this.action.set('program');
    this.selectedProgramId.set('');
    const minimumStartsAt = localDateTimeValue(new Date(Date.now() + 1_000));
    this.minimumStartsAt.set(minimumStartsAt);
    this.startsAtValue.set(minimumStartsAt);
    this.invalidPeriod.set(false);
    this.invalidSchedule.set(false);
    this.mutationError.set(false);
    this.dialogOpen.set(true);
    const dialog = this.changeDialog?.nativeElement;
    if (dialog && !dialog.open) {
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    }
    this.loadComparison();
  }

  protected closeChangeDialog(): void {
    const dialog = this.changeDialog?.nativeElement;
    if (dialog?.open) {
      if (typeof dialog.close === 'function') {
        dialog.close();
      } else {
        dialog.removeAttribute('open');
      }
    }
    this.dialogOpen.set(false);
  }

  protected onDialogClose(): void {
    this.dialogOpen.set(false);
  }

  protected onDialogClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closeChangeDialog();
    }
  }

  protected onPeriodModeChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (value === 'last-month' || value === 'last-30-days' || value === 'custom') {
      this.periodMode.set(value);
      this.comparisonError.set(false);
      this.invalidPeriod.set(false);
      this.comparison.set(null);
      this.selectedProgramId.set('');
      if (value !== 'custom') {
        this.loadComparison();
      }
    }
  }

  protected onCustomDateChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.name === 'customFrom') {
      this.customFrom.set(input.value);
    } else {
      this.customTo.set(input.value);
    }
    this.comparisonError.set(false);
    this.invalidPeriod.set(false);
    this.comparison.set(null);
    this.selectedProgramId.set('');
  }

  protected toggleSort(key: SortKey): void {
    if (this.sortKey() !== key) {
      this.sortKey.set(key);
      this.sortDirection.set('asc');
    } else if (this.sortDirection() === 'asc') {
      this.sortDirection.set('desc');
    } else {
      this.sortKey.set(null);
      this.sortDirection.set('asc');
    }
  }

  protected ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
    if (this.sortKey() !== key) {
      return 'none';
    }
    return this.sortDirection() === 'asc' ? 'ascending' : 'descending';
  }

  protected sortIndicator(key: SortKey): string {
    if (this.sortKey() !== key) {
      return '';
    }
    return this.sortDirection() === 'asc' ? '▲' : '▼';
  }

  protected onActionChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (value === 'program' || value === 'end') {
      this.action.set(value);
      this.invalidSchedule.set(false);
    }
  }

  protected onProgramChange(event: Event): void {
    this.selectedProgramId.set((event.target as HTMLSelectElement).value);
    this.invalidSchedule.set(false);
  }

  protected onStartsAtChange(event: Event): void {
    this.startsAtValue.set((event.target as HTMLInputElement).value);
    this.invalidSchedule.set(false);
  }

  protected retryLoad(): void {
    this.loadData();
  }

  protected retryComparison(): void {
    this.loadComparison();
  }

  protected compareCustomPeriod(): void {
    this.loadComparison();
  }

  protected confirmChange(event: Event): void {
    event.preventDefault();
    const startsAt = new Date(this.startsAtValue());
    const now = Date.now();
    if (
      Number.isNaN(startsAt.getTime()) ||
      startsAt.getTime() <= now ||
      (this.action() === 'program' &&
        !this.programs().some((program) => program.id === this.selectedProgramId()))
    ) {
      this.invalidSchedule.set(true);
      return;
    }

    this.activeMutation?.unsubscribe();
    this.saving.set(true);
    this.mutationError.set(false);
    this.changed.set(false);
    const request =
      this.action() === 'end'
        ? this.service.scheduleEnd(startsAt.toISOString())
        : this.service.scheduleProgram(this.selectedProgramId(), startsAt.toISOString());
    this.activeMutation = request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        this.changed.set(true);
        this.closeChangeDialog();
        this.loadData();
      },
      error: () => {
        this.saving.set(false);
        this.mutationError.set(true);
      },
    });
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat(this.locale, { style: 'currency', currency: 'PLN' }).format(value);
  }

  protected formatPercent(value: number): string {
    return new Intl.NumberFormat(this.locale, {
      style: 'percent',
      maximumFractionDigits: 2,
      signDisplay: 'exceptZero',
    }).format(value === 0 ? 0 : -value);
  }

  protected formatDateTime(value: string | Date): string {
    return new Intl.DateTimeFormat(this.locale, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Europe/Warsaw',
    }).format(new Date(value));
  }

  protected fuelLabel(value: string): string {
    const keyByFuelType: Record<string, TranslationKey> = {
      '95': 'fuel95',
      '98': 'fuel98',
      Diesel: 'fuelDiesel',
      '95+': 'fuel95Plus',
      '98+': 'fuel98Plus',
      'Diesel+': 'fuelDieselPlus',
    };
    const key = keyByFuelType[value];
    return key ? this.language.text(key) : value;
  }

  protected programName(programId: string | null): string {
    if (!programId) {
      return this.language.text('unassigned');
    }
    return this.programs().find((program) => program.id === programId)?.name ?? programId;
  }

  protected currentProgramName(): string {
    const assignment = this.currentAssignment();
    return assignment
      ? this.programName(assignment.programId)
      : this.language.text('noCurrentProgram');
  }

  protected comparePeriodLabel(): string {
    const period = this.resolvePeriod();
    return period ? `${this.formatDateTime(period.from)} – ${this.formatDateTime(period.to)}` : '';
  }

  protected scheduleErrorLabel(): TranslationKey {
    return this.action() === 'program' && !this.selectedProgramId()
      ? 'programSelectionRequired'
      : 'invalidEffectiveDate';
  }

  private readonly service = inject(ProgramsService);

  private get locale(): string {
    return this.language.language() === 'PL' ? 'pl-PL' : 'en-GB';
  }

  private loadData(): void {
    this.activeLoad?.unsubscribe();
    this.loading.set(true);
    this.loadError.set(false);
    this.activeLoad = forkJoin({
      response: this.service.getPrograms(),
      assignments: this.service.getAssignments(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ response, assignments }) => {
          this.setProgramData(response, assignments);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.loadError.set(true);
        },
      });
  }

  private setProgramData(response: ProgramsResponse, assignments: ProgramAssignment[]): void {
    this.programs.set(response.programs);
    this.currentAssignment.set(response.currentAssignment);
    this.assignments.set(
      [...assignments].sort(
        (left, right) => new Date(left.startsAt).getTime() - new Date(right.startsAt).getTime(),
      ),
    );
  }

  private loadComparison(): void {
    const period = this.resolvePeriod();
    if (!period || period.from.getTime() >= period.to.getTime()) {
      this.invalidPeriod.set(true);
      this.comparison.set(null);
      return;
    }
    this.activeComparison?.unsubscribe();
    this.comparisonLoading.set(true);
    this.comparisonError.set(false);
    this.invalidPeriod.set(false);
    this.comparison.set(null);
    this.activeComparison = this.service
      .getComparison(period.from.toISOString(), period.to.toISOString())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (comparison) => {
          this.comparison.set(comparison);
          this.comparisonLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.comparisonLoading.set(false);
          this.comparisonError.set(true);
          if (error.status === 400) {
            this.invalidPeriod.set(true);
          }
        },
      });
  }

  private resetSort(): void {
    this.sortKey.set(null);
    this.sortDirection.set('asc');
  }

  private resolvePeriod(): { from: Date; to: Date } | null {
    const now = new Date();
    if (this.periodMode() === 'last-30-days') {
      return { from: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1_000), to: now };
    }
    if (this.periodMode() === 'last-month') {
      const { year, month } = getDatePartsInTimeZone(now, 'Europe/Warsaw');
      const currentMonthStart = dateAtWarsawMidnight(year, month, 1);
      const previousMonth = month === 1 ? 12 : month - 1;
      const previousYear = month === 1 ? year - 1 : year;
      return {
        from: dateAtWarsawMidnight(previousYear, previousMonth, 1),
        to: currentMonthStart,
      };
    }

    const fromParts = parseDate(this.customFrom());
    const toParts = parseDate(this.customTo());
    if (!fromParts || !toParts || compareDates(fromParts, toParts) > 0) {
      return null;
    }
    const from = dateAtWarsawMidnight(fromParts.year, fromParts.month, fromParts.day);
    const nextDay = incrementDate(toParts);
    const requestedTo = dateAtWarsawMidnight(nextDay.year, nextDay.month, nextDay.day);
    const to = requestedTo.getTime() > now.getTime() ? now : requestedTo;
    return { from, to };
  }
}

function makeHistoryEntries(assignments: ProgramAssignment[]): ProgramHistoryEntry[] {
  const sorted = [...assignments].sort(
    (left, right) => new Date(left.startsAt).getTime() - new Date(right.startsAt).getTime(),
  );
  const entries: ProgramHistoryEntry[] = [];
  let previous: ProgramAssignment | undefined;

  for (const assignment of sorted) {
    if (
      previous?.endsAt &&
      new Date(previous.endsAt).getTime() < new Date(assignment.startsAt).getTime()
    ) {
      entries.push({
        id: `gap-${previous.id}-${assignment.id}`,
        programId: null,
        startsAt: previous.endsAt,
        endsAt: assignment.startsAt,
      });
    }
    entries.push({
      id: assignment.id,
      programId: assignment.programId,
      startsAt: assignment.startsAt,
      endsAt: assignment.endsAt,
    });
    previous = assignment;
  }

  if (previous?.endsAt) {
    entries.push({
      id: `gap-after-${previous.id}`,
      programId: null,
      startsAt: previous.endsAt,
      endsAt: null,
    });
  }

  return entries;
}

function localDateValue(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function localDateTimeValue(date: Date): string {
  return `${localDateValue(date)}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(
    date.getSeconds(),
  )}`;
}

function dateDaysAgoLocal(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return localDateValue(date);
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function parseDate(value: string): { year: number; month: number; day: number } | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) {
    return null;
  }
  const [, yearText, monthText, dayText] = match;
  const year = Number(yearText);
  const month = Number(monthText);
  const day = Number(dayText);
  const date = new Date(utcMillis(year, month - 1, day));
  if (
    year < 1 ||
    year > 9998 ||
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  ) {
    return null;
  }
  return { year, month, day };
}

function compareDates(
  left: { year: number; month: number; day: number },
  right: { year: number; month: number; day: number },
): number {
  return left.year - right.year || left.month - right.month || left.day - right.day;
}

function incrementDate(date: { year: number; month: number; day: number }): {
  year: number;
  month: number;
  day: number;
} {
  const next = new Date(utcMillis(date.year, date.month - 1, date.day + 1));
  return { year: next.getUTCFullYear(), month: next.getUTCMonth() + 1, day: next.getUTCDate() };
}

function getDatePartsInTimeZone(date: Date, timeZone: string): { year: number; month: number } {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: 'numeric',
  }).formatToParts(date);
  return {
    year: Number(parts.find((part) => part.type === 'year')?.value),
    month: Number(parts.find((part) => part.type === 'month')?.value),
  };
}

function dateAtWarsawMidnight(year: number, month: number, day: number): Date {
  const intendedUtc = utcMillis(year, month - 1, day);
  let candidate = intendedUtc;
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Europe/Warsaw',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  });

  for (let attempt = 0; attempt < 3; attempt++) {
    const parts = formatter.formatToParts(new Date(candidate));
    const values = new Map(parts.map((part) => [part.type, Number(part.value)]));
    const representedUtc = utcMillis(
      values.get('year') ?? year,
      (values.get('month') ?? month) - 1,
      values.get('day') ?? day,
      values.get('hour') ?? 0,
      values.get('minute') ?? 0,
      values.get('second') ?? 0,
    );
    candidate += intendedUtc - representedUtc;
  }
  return new Date(candidate);
}

function utcMillis(
  year: number,
  zeroBasedMonth: number,
  day: number,
  hour = 0,
  minute = 0,
  second = 0,
): number {
  const date = new Date(0);
  date.setUTCFullYear(year, zeroBasedMonth, day);
  date.setUTCHours(hour, minute, second, 0);
  return date.getTime();
}
