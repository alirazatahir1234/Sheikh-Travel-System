import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

import { UiStatusBadgeComponent } from '../components/ui/status-badge/ui-status-badge.component';
import { UiPageHeaderComponent } from '../components/ui/page-header/ui-page-header.component';

import { FleetStatusBadgeComponent } from './status-badge/fleet-status-badge.component';
import { FleetPageHeaderComponent } from './page-header/fleet-page-header.component';
import { FleetExpiryCardComponent } from './expiry-card/fleet-expiry-card.component';
import { FleetTimelineComponent } from './timeline/fleet-timeline.component';

const COMPONENTS = [
  FleetStatusBadgeComponent,
  FleetPageHeaderComponent,
  FleetExpiryCardComponent,
  FleetTimelineComponent
];

@NgModule({
  declarations: [...COMPONENTS],
  imports: [CommonModule, MatIconModule, UiStatusBadgeComponent, UiPageHeaderComponent],
  exports: [...COMPONENTS]
})
export class FleetUiModule {}
