import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { authGuard } from './core/guards/auth.guard';
import { driverWorkspaceGuard } from './core/guards/driver-workspace.guard';

const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    canActivateChild: [driverWorkspaceGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'my-trips', loadChildren: () => import('./modules/driver-workspace/driver-workspace.module').then(m => m.DriverWorkspaceModule) },
      { path: 'dashboard', loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule) },
      { path: 'fleet', loadChildren: () => import('./modules/fleet-management/fleet-management.module').then(m => m.FleetManagementModule) },
      { path: 'vehicles', loadChildren: () => import('./modules/vehicles/vehicles.module').then(m => m.VehiclesModule) },
      { path: 'drivers', loadChildren: () => import('./modules/drivers/drivers.module').then(m => m.DriversModule) },
      { path: 'customers', loadChildren: () => import('./modules/customers/customers.module').then(m => m.CustomersModule) },
      { path: 'routes', loadChildren: () => import('./modules/routes/routes.module').then(m => m.RoutesModule) },
      { path: 'bookings', loadChildren: () => import('./modules/bookings/bookings.module').then(m => m.BookingsModule) },
      { path: 'payments', loadChildren: () => import('./modules/payments/payments.module').then(m => m.PaymentsModule) },
      { path: 'reports', loadChildren: () => import('./modules/reports/reports.module').then(m => m.ReportsModule) },
      { path: 'gps-tracking', loadChildren: () => import('./modules/gps-tracking/gps-tracking.module').then(m => m.GpsTrackingModule) },
      { path: 'tracking', redirectTo: 'gps-tracking/live', pathMatch: 'full' },
      { path: 'driver-allowance-rules', loadChildren: () => import('./modules/driver-allowance-rules/driver-allowance-rules.module').then(m => m.DriverAllowanceRulesModule) },
      { path: 'users', loadChildren: () => import('./modules/users/users.module').then(m => m.UsersModule) },
      { path: 'platform', loadChildren: () => import('./modules/platform-admin/platform-admin.module').then(m => m.PlatformAdminModule) },
      { path: 'settings', loadChildren: () => import('./modules/settings/settings.module').then(m => m.SettingsModule) },
      { path: 'fuel-logs', loadChildren: () => import('./modules/fuel-logs/fuel-logs.module').then(m => m.FuelLogsModule) },
      { path: 'maintenance', redirectTo: 'fleet/maintenance', pathMatch: 'full' },
      { path: 'maintenance/service-records', loadChildren: () => import('./modules/maintenance/maintenance.module').then(m => m.MaintenanceModule) },
      { path: 'audit-logs', loadChildren: () => import('./modules/audit-logs/audit-logs.module').then(m => m.AuditLogsModule) },
      { path: 'profile', loadChildren: () => import('./modules/profile/profile.module').then(m => m.ProfileModule) }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
