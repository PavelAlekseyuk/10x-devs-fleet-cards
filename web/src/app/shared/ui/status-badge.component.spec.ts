import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatusBadgeComponent, StatusBadgeTone } from './status-badge.component';

@Component({
  standalone: true,
  imports: [StatusBadgeComponent],
  template: '<app-status-badge [label]="label" [tone]="tone" />',
})
class StatusBadgeHostComponent {
  label = 'Active';
  tone: StatusBadgeTone = 'active';
}

describe('StatusBadgeComponent', () => {
  let fixture: ComponentFixture<StatusBadgeHostComponent>;
  let host: StatusBadgeHostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatusBadgeHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(StatusBadgeHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function badge(): HTMLSpanElement {
    return fixture.nativeElement.querySelector('.status-badge') as HTMLSpanElement;
  }

  it('renders its label with the active tone', () => {
    expect(badge().textContent?.trim()).toBe('Active');
    expect(badge().getAttribute('data-tone')).toBe('active');
    expect(badge().classList.contains('status-badge--active')).toBe(true);
  });

  it('renders the warning tone', () => {
    host.label = 'Archived';
    host.tone = 'warning';
    fixture.detectChanges();

    expect(badge().textContent?.trim()).toBe('Archived');
    expect(badge().getAttribute('data-tone')).toBe('warning');
    expect(badge().classList.contains('status-badge--warning')).toBe(true);
  });
});
