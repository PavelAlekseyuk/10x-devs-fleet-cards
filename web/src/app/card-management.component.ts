import { Component, inject, OnInit } from '@angular/core';
import { AuthService } from './auth.service';
import {
  CardManagementService,
  FleetCard,
  FleetCardAssignment,
  FleetCardState,
} from './card-management.service';
import { FleetVehicle, FleetVehiclesService } from './fleet-vehicles.service';
import { LanguageService, TranslationKey } from './language.service';

const cardStates: FleetCardState[] = ['Active', 'Blocked', 'Stolen', 'Archived'];

const stateTranslationKeys: Record<FleetCardState, TranslationKey> = {
  Active: 'cardStateActive',
  Blocked: 'cardStateBlocked',
  Stolen: 'cardStateStolen',
  Archived: 'cardStateArchived',
};

@Component({
  selector: 'app-card-management',
  standalone: true,
  templateUrl: './card-management.component.html',
  styleUrl: './card-management.component.scss',
})
export class CardManagementComponent implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly states = cardStates;
  protected cards: FleetCard[] = [];
  protected vehicles: FleetVehicle[] = [];
  protected selectedIssueVehicleId = '';
  protected readonly assignmentSelections: Record<string, string> = {};
  protected readonly assignmentHistory: Record<string, FleetCardAssignment[]> = {};
  protected loadingCards = true;
  protected loadingVehicles = true;
  protected savingIssue = false;
  protected pendingCardId: string | null = null;
  protected openHistoryCardId: string | null = null;
  protected error: string | null = null;
  protected issueError: string | null = null;

  private readonly cardsService = inject(CardManagementService);
  private readonly vehiclesService = inject(FleetVehiclesService);

  ngOnInit(): void {
    this.loadCards();
    this.loadVehicles();
  }

  protected get canWrite(): boolean {
    return this.auth.user()?.permission === 'ReadWrite';
  }

  protected stateLabel(state: FleetCardState): string {
    return this.language.text(stateTranslationKeys[state]);
  }

  protected updateIssueVehicle(event: Event): void {
    this.selectedIssueVehicleId = (event.target as HTMLSelectElement).value;
  }

  protected issueCard(): void {
    if (!this.canWrite || this.savingIssue) {
      return;
    }
    if (!this.vehicles.some((vehicle) => vehicle.id === this.selectedIssueVehicleId)) {
      this.issueError = this.language.text('cardIssueVehicleRequired');
      return;
    }

    this.issueError = null;
    this.savingIssue = true;
    this.cardsService.issueCard(this.selectedIssueVehicleId).subscribe({
      next: (card) => {
        this.cards = [card, ...this.cards];
        this.selectedIssueVehicleId = '';
        this.savingIssue = false;
      },
      error: () => {
        this.savingIssue = false;
        this.issueError = this.language.text('cardIssueError');
      },
    });
  }

  protected changeCardState(card: FleetCard, event: Event): void {
    if (!this.canWrite || this.pendingCardId) {
      return;
    }
    const state = (event.target as HTMLSelectElement).value;
    if (!cardStates.includes(state as FleetCardState) || state === card.state) {
      return;
    }

    this.pendingCardId = card.id;
    this.error = null;
    this.cardsService.changeState(card.id, state as FleetCardState).subscribe({
      next: (updatedCard) => this.finishCardMutation(updatedCard),
      error: () => this.failCardMutation(),
    });
  }

  protected updateAssignmentSelection(card: FleetCard, event: Event): void {
    this.assignmentSelections[card.id] = (event.target as HTMLSelectElement).value;
  }

  protected selectedAssignment(card: FleetCard): string {
    return this.assignmentSelections[card.id] ?? card.assignedCarId ?? '';
  }

  protected reassignCard(card: FleetCard): void {
    if (!this.canWrite || this.pendingCardId) {
      return;
    }
    const vehicleId = this.selectedAssignment(card);
    if (!this.vehicles.some((vehicle) => vehicle.id === vehicleId)) {
      this.error = this.language.text('cardReassignVehicleRequired');
      return;
    }
    if (vehicleId === card.assignedCarId) {
      return;
    }

    this.pendingCardId = card.id;
    this.error = null;
    this.cardsService.reassignCard(card.id, vehicleId).subscribe({
      next: (updatedCard) => {
        this.assignmentSelections[card.id] = updatedCard.assignedCarId ?? '';
        delete this.assignmentHistory[card.id];
        this.finishCardMutation(updatedCard);
        if (this.openHistoryCardId === card.id) {
          this.loadAssignmentHistory(card.id);
        }
      },
      error: () => this.failCardMutation(),
    });
  }

  protected toggleAssignmentHistory(card: FleetCard): void {
    if (this.openHistoryCardId === card.id) {
      this.openHistoryCardId = null;
      return;
    }
    this.openHistoryCardId = card.id;
    if (this.assignmentHistory[card.id]) {
      return;
    }

    this.loadAssignmentHistory(card.id);
  }

  private loadAssignmentHistory(cardId: string): void {
    this.error = null;
    this.cardsService.getAssignmentHistory(cardId).subscribe({
      next: (history) => (this.assignmentHistory[cardId] = history),
      error: () => {
        this.openHistoryCardId = null;
        this.error = this.language.text('cardHistoryError');
      },
    });
  }

  protected formatDate(value: string): string {
    const locale = this.language.language() === 'PL' ? 'pl-PL' : 'en-GB';
    return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(
      new Date(value),
    );
  }

  private loadCards(): void {
    this.cardsService.getCards().subscribe({
      next: (cards) => {
        this.cards = cards;
        this.loadingCards = false;
      },
      error: () => {
        this.loadingCards = false;
        this.error = this.language.text('cardLoadError');
      },
    });
  }

  private loadVehicles(): void {
    this.vehiclesService.getVehicles('', false).subscribe({
      next: (vehicles) => {
        this.vehicles = vehicles.filter((vehicle) => !vehicle.isArchived);
        this.loadingVehicles = false;
      },
      error: () => {
        this.loadingVehicles = false;
        this.error = this.language.text('cardLoadError');
      },
    });
  }

  private finishCardMutation(card: FleetCard): void {
    this.cards = this.cards.map((current) => (current.id === card.id ? card : current));
    this.pendingCardId = null;
  }

  private failCardMutation(): void {
    this.pendingCardId = null;
    this.error = this.language.text('cardMutationError');
  }
}