import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

export type FleetCardState = 'Active' | 'Blocked' | 'Stolen' | 'Archived';

export interface FleetCard {
  id: string;
  maskedPan: string;
  state: FleetCardState;
  assignedCarId: string;
  assignedVehicleRegistrationPlate: string | null;
}

export interface FleetCardStateHistory {
  state: FleetCardState;
  occurredAt: string;
}

interface FleetCardApiResponse extends FleetCard {
  providerToken: string;
}

@Injectable({ providedIn: 'root' })
export class CardManagementService {
  constructor(private readonly http: HttpClient) {}

  getCards(): Observable<FleetCard[]> {
    return this.http.get<FleetCardApiResponse[]>('/api/cards').pipe(
      map((cards) => cards.map((card) => this.toCard(card))),
    );
  }

  issueCard(vehicleId: string): Observable<FleetCard> {
    return this.http.post<FleetCardApiResponse>('/api/cards', { vehicleId }).pipe(
      map((card) => this.toCard(card)),
    );
  }

  changeState(cardId: string, state: FleetCardState): Observable<FleetCard> {
    return this.http.put<FleetCardApiResponse>(`/api/cards/${cardId}/state`, { state }).pipe(
      map((card) => this.toCard(card)),
    );
  }

  getStateHistory(cardId: string): Observable<FleetCardStateHistory[]> {
    return this.http.get<FleetCardStateHistory[]>(`/api/cards/${cardId}/state-history`);
  }

  private toCard(card: FleetCardApiResponse): FleetCard {
    return {
      id: card.id,
      maskedPan: card.maskedPan,
      state: card.state,
      assignedCarId: card.assignedCarId,
      assignedVehicleRegistrationPlate: card.assignedVehicleRegistrationPlate,
    };
  }
}