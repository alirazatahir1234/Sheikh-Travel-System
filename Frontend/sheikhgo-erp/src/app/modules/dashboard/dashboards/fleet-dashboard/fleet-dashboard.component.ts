import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FleetDashboardContentComponent } from '../../../fleet-management/fleet-dashboard/fleet-dashboard-content.component';

/**
 * Fleet dashboard view inside the Admin Portal `/dashboard` selector. Renders only the
 * widgets grid — navigation stays in the main STCC shell sidebar.
 */
@Component({
    selector: 'app-dashboard-fleet',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FleetDashboardContentComponent],
    template: `<fleet-dashboard-content></fleet-dashboard-content>`
})
export class FleetDashboardComponent {}
