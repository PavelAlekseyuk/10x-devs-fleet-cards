import { Component, signal } from '@angular/core';

interface FleetCard {
  readonly name: string;
  readonly role: string;
  readonly status: string;
  readonly accent: string;
  readonly stats: readonly [string, string][];
}

@Component({
  selector: 'app-root',
  imports: [],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly cards = signal<readonly FleetCard[]>([
    {
      name: 'Aurora',
      role: 'Scout vessel',
      status: 'Ready',
      accent: 'violet',
      stats: [
        ['Speed', '92'],
        ['Range', '78'],
        ['Crew', '04'],
      ],
    },
    {
      name: 'Waypoint',
      role: 'Command frigate',
      status: 'On mission',
      accent: 'blue',
      stats: [
        ['Speed', '64'],
        ['Range', '96'],
        ['Crew', '18'],
      ],
    },
    {
      name: 'Northstar',
      role: 'Support cruiser',
      status: 'Maintenance',
      accent: 'orange',
      stats: [
        ['Speed', '51'],
        ['Range', '84'],
        ['Crew', '12'],
      ],
    },
  ]);

  protected selectedCard = signal<string | null>(null);

  protected selectCard(name: string): void {
    this.selectedCard.set(name);
  }
}
