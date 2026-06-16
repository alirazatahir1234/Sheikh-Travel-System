import { Component } from '@angular/core';

/**
 * Fleet hub dashboard route (`/fleet/dashboard`). Renders the shared dashboard content
 * inside the fleet hub's own layout shell ({@link FleetLayoutComponent}).
 */
@Component({
  selector: 'app-fleet-dashboard',
  template: `<fleet-dashboard-content></fleet-dashboard-content>`
})
export class FleetDashboardComponent {}
