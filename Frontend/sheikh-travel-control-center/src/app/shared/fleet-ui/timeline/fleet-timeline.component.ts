import { Component, Input } from '@angular/core';

export interface FleetTimelineEvent {
  title: string;
  detail?: string;
  timestamp?: string | Date;
  icon?: string;
  tone?: 'success' | 'warning' | 'danger' | 'primary' | 'muted';
}

@Component({
  selector: 'fleet-timeline',
  template: `
    <ol class="fleet-timeline">
      <li class="fleet-timeline__item" *ngFor="let e of events" [attr.data-tone]="e.tone || 'primary'">
        <span class="fleet-timeline__marker">
          <mat-icon *ngIf="e.icon">{{ e.icon }}</mat-icon>
        </span>
        <div class="fleet-timeline__body">
          <p class="fleet-timeline__title">{{ e.title }}</p>
          <p class="fleet-timeline__detail" *ngIf="e.detail">{{ e.detail }}</p>
          <p class="fleet-timeline__time" *ngIf="e.timestamp">{{ e.timestamp | date: 'medium' }}</p>
        </div>
      </li>
      <li class="fleet-timeline__empty" *ngIf="!events?.length">No activity yet.</li>
    </ol>
  `,
  styleUrls: ['./fleet-timeline.component.scss']
})
export class FleetTimelineComponent {
  @Input() events: FleetTimelineEvent[] = [];
}
