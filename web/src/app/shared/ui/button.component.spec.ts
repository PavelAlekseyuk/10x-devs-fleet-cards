import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ButtonComponent, ButtonType, ButtonVariant } from './button.component';

@Component({
  standalone: true,
  imports: [ButtonComponent],
  template: `
    <app-button [type]="type" [disabled]="disabled" [variant]="variant">
      Continue
    </app-button>
  `,
})
class ButtonHostComponent {
  type: ButtonType = 'button';
  disabled = false;
  variant: ButtonVariant = 'primary';
}

describe('ButtonComponent', () => {
  let fixture: ComponentFixture<ButtonHostComponent>;
  let host: ButtonHostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ButtonHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ButtonHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function nativeButton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  }

  it('preserves native button and submit types', () => {
    expect(nativeButton().type).toBe('button');

    host.type = 'submit';
    fixture.detectChanges();

    expect(nativeButton().type).toBe('submit');
  });

  it('uses projected visible text as the native button name', () => {
    const button = nativeButton();

    expect(button.textContent?.trim()).toBe('Continue');
    expect(button.hasAttribute('aria-label')).toBe(false);
  });

  it('preserves the native disabled behavior', () => {
    expect(nativeButton().disabled).toBe(false);

    host.disabled = true;
    fixture.detectChanges();

    expect(nativeButton().disabled).toBe(true);
    expect(nativeButton().hasAttribute('disabled')).toBe(true);
  });

  it.each([
    ['primary', 'button--primary'],
    ['quiet', 'button--quiet'],
    ['text', 'button--text'],
  ] as const)('applies the %s variant', (variant, expectedClass) => {
    host.variant = variant;
    fixture.detectChanges();

    expect(nativeButton().classList.contains(expectedClass)).toBe(true);
  });
});
