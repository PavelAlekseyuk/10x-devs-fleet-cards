import { Component, Input } from '@angular/core';

export type ButtonVariant = 'primary' | 'quiet' | 'text';
export type ButtonType = 'button' | 'submit' | 'reset';

@Component({
  selector: 'app-button',
  standalone: true,
  templateUrl: './button.component.html',
  styleUrl: './button.component.scss',
})
export class ButtonComponent {
  @Input() variant: ButtonVariant = 'primary';
  @Input() type: ButtonType = 'button';
  @Input() disabled = false;
}
