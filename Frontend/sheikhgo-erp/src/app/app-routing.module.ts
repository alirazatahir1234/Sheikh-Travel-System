import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { authGuard } from './core/guards/auth.guard';
import { driverWorkspaceGuard } from './core/guards/driver-workspace.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { guestCanMatch } from './core/guards/guest.guard';
import { WebsiteShellComponent } from './modules/website/website-shell.component';

const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule) },
  { path: 'login', redirectTo: 'auth/login', pathMatch: 'full' },

  // Always-public legal pages (Play Store / compliance — available while signed in too)
  {
    path: 'privacy-policy',
    component: WebsiteShellComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./modules/website/pages/legal/privacy.page').then(m => m.PrivacyPage),
      },
    ],
  },
  {
    path: 'terms-and-conditions',
    component: WebsiteShellComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./modules/website/pages/legal/terms.page').then(m => m.TermsPage),
      },
    ],
  },
  {
    path: 'cookie-policy',
    component: WebsiteShellComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./modules/website/pages/legal/cookie.page').then(m => m.CookiePage),
      },
    ],
  },

  // Public marketing site (guests only — avoids clashing with ERP paths like gps-tracking)
  {
    path: '',
    component: WebsiteShellComponent,
    canMatch: [guestCanMatch],
    children: [
      {
        path: '',
        loadComponent: () => import('./modules/website/pages/home/home.page').then(m => m.HomePage),
      },
      {
        path: 'fleet-management',
        loadComponent: () => import('./modules/website/pages/fleet/fleet.page').then(m => m.FleetPage),
      },
      {
        path: 'gps-tracking',
        loadComponent: () => import('./modules/website/pages/gps/gps.page').then(m => m.GpsPage),
      },
      {
        path: 'features',
        loadComponent: () => import('./modules/website/pages/features/features.page').then(m => m.FeaturesPage),
      },
      // Guest-only marketing aliases (ERP `/platform` remains auth-protected)
      { path: 'platform', redirectTo: 'features', pathMatch: 'full' },
      { path: 'solutions', redirectTo: 'about', pathMatch: 'full' },
      {
        path: 'about',
        loadComponent: () => import('./modules/website/pages/about/about.page').then(m => m.AboutPage),
      },
      {
        path: 'contact',
        loadComponent: () => import('./modules/website/pages/contact/contact.page').then(m => m.ContactPage),
      },
      {
        path: 'request-demo',
        loadComponent: () =>
          import('./modules/website/pages/request-demo/request-demo.page').then(m => m.RequestDemoPage),
      },
    ],
  },

  // Authenticated ERP shell
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    canActivateChild: [driverWorkspaceGuard],
    children: [
      // Signed-in `/` → dashboard (marketing shell is skipped via guestCanMatch)
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'my-trips', loadChildren: () => import('./modules/driver-workspace/driver-workspace.module').then(m => m.DriverWorkspaceModule) },
      {
        path: 'dashboard',
        canActivate: [permissionGuard],
        data: { permissions: ['Platform.Dashboard.View'] },
        loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule)
      },
      {
        path: 'fleet',
        canActivate: [permissionGuard],
        data: { permissions: ['Vehicle.View'] },
        loadChildren: () => import('./modules/fleet-management/fleet-management.module').then(m => m.FleetManagementModule)
      },
      {
        path: 'vehicles',
        canActivate: [permissionGuard],
        data: { permissions: ['Vehicle.View'] },
        loadChildren: () => import('./modules/vehicles/vehicles.module').then(m => m.VehiclesModule)
      },
      {
        path: 'drivers',
        canActivate: [permissionGuard],
        data: { permissions: ['Driver.View'] },
        loadChildren: () => import('./modules/drivers/drivers.module').then(m => m.DriversModule)
      },
      {
        path: 'customers',
        canActivate: [permissionGuard],
        data: { permissions: ['Customer.View'] },
        loadChildren: () => import('./modules/customers/customers.module').then(m => m.CustomersModule)
      },
      {
        path: 'routes',
        canActivate: [permissionGuard],
        data: { permissions: ['Route.View'] },
        loadChildren: () => import('./modules/routes/routes.module').then(m => m.RoutesModule)
      },
      {
        path: 'bookings',
        canActivate: [permissionGuard],
        data: { permissions: ['Booking.View'] },
        loadChildren: () => import('./modules/bookings/bookings.module').then(m => m.BookingsModule)
      },
      {
        path: 'trips',
        canActivate: [permissionGuard],
        data: { permissions: ['Trip.View'] },
        loadChildren: () => import('./modules/trips/trips.module').then(m => m.TripsModule)
      },
      {
        path: 'payments',
        canActivate: [permissionGuard],
        data: { permissions: ['Payment.View'] },
        loadChildren: () => import('./modules/payments/payments.module').then(m => m.PaymentsModule)
      },
      {
        path: 'reports',
        canActivate: [permissionGuard],
        data: { permissions: ['Report.View'] },
        loadChildren: () => import('./modules/reports/reports.module').then(m => m.ReportsModule)
      },
      {
        path: 'gps-tracking',
        canActivate: [permissionGuard],
        data: { permissions: ['GPS.View'] },
        loadChildren: () => import('./modules/gps-tracking/gps-tracking.module').then(m => m.GpsTrackingModule)
      },
      { path: 'tracking', redirectTo: 'gps-tracking/live', pathMatch: 'full' },
      {
        path: 'driver-allowance-rules',
        canActivate: [permissionGuard],
        data: { permissions: ['Driver.Manage'] },
        loadChildren: () => import('./modules/driver-allowance-rules/driver-allowance-rules.module').then(m => m.DriverAllowanceRulesModule)
      },
      {
        path: 'users',
        canActivate: [permissionGuard],
        data: { permissions: ['Platform.Users.View'] },
        loadChildren: () => import('./modules/users/users.module').then(m => m.UsersModule)
      },
      {
        path: 'platform',
        canActivate: [permissionGuard],
        data: {
          permissions: [
            'Platform.Tenants.View',
            'Platform.Roles.View',
            'Platform.Users.View',
            'Platform.Branches.Manage',
            'Platform.Departments.Manage',
            'Platform.Settings.View',
            'Platform.AuditLogs.View',
            'Platform.Migrations.View',
            'Platform.System.Reset'
          ]
        },
        loadChildren: () => import('./modules/platform-admin/platform-admin.module').then(m => m.PlatformAdminModule)
      },
      {
        path: 'settings',
        canActivate: [permissionGuard],
        data: { permissions: ['Platform.Settings.View'] },
        loadChildren: () => import('./modules/settings/settings.module').then(m => m.SettingsModule)
      },
      {
        path: 'fuel-logs',
        canActivate: [permissionGuard],
        data: { permissions: ['Fuel.View'] },
        loadChildren: () => import('./modules/fuel-logs/fuel-logs.module').then(m => m.FuelLogsModule)
      },
      { path: 'maintenance', redirectTo: 'fleet/maintenance', pathMatch: 'full' },
      {
        path: 'maintenance/service-records',
        canActivate: [permissionGuard],
        data: { permissions: ['Maintenance.View'] },
        loadChildren: () => import('./modules/maintenance/maintenance.module').then(m => m.MaintenanceModule)
      },
      {
        path: 'audit-logs',
        redirectTo: 'platform/audit-center',
        pathMatch: 'full'
      },
      {
        path: 'notifications',
        canActivate: [permissionGuard],
        data: { permissions: ['Notification.View'] },
        loadChildren: () => import('./modules/notifications/notifications.module').then(m => m.NotificationsModule)
      },
      {
        path: 'ai',
        canActivate: [permissionGuard],
        data: { permissions: ['Ai.View'] },
        loadChildren: () => import('./modules/ai/ai.module').then(m => m.AiModule)
      },
      {
        path: 'website',
        canActivate: [permissionGuard],
        data: { permissions: ['Website.View'] },
        loadChildren: () => import('./modules/website-admin/website-admin.module').then(m => m.WebsiteAdminModule)
      },
      { path: 'profile', loadChildren: () => import('./modules/profile/profile.module').then(m => m.ProfileModule) }
    ]
  },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes), WebsiteShellComponent],
  exports: [RouterModule]
})
export class AppRoutingModule { }
