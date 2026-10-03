import { Component, Input } from '@angular/core';

export type StatusBadgeTone = 'active' | 'warning';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.scss',
})
export class StatusBadgeComponent {
  @Input({ required: true }) label!: string;
  @Input() tone: StatusBadgeTone = 'active';
}
