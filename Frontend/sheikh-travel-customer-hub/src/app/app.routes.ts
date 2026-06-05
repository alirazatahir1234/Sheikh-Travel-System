import { Routes } from '@angular/router';
import { portalAuthGuard } from './core/guards/portal-auth.guard';
import { PortalShellComponent } from './layout/portal-shell.component';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/home/home.component').then((m) => m.HomeComponent)
  },
  {
    path: 'profile',
    loadComponent: () =>
      import('./pages/profile/profile-page.component').then((m) => m.ProfilePageComponent)
  },
  {
    path: '',
    component: PortalShellComponent,
    children: [
      {
        path: 'book',
        loadComponent: () =>
          import('./pages/book-ride/book-ride-page.component').then((m) => m.BookRidePageComponent)
      },
      {
        path: 'help',
        loadComponent: () => import('./pages/help/help-page.component').then((m) => m.HelpPageComponent)
      },
      {
        path: 'dashboard',
        canActivate: [portalAuthGuard],
        loadComponent: () =>
          import('./pages/dashboard/dashboard-page.component').then((m) => m.DashboardPageComponent)
      },
      {
        path: 'bookings',
        canActivate: [portalAuthGuard],
        loadComponent: () =>
          import('./pages/my-bookings/my-bookings-page.component').then((m) => m.MyBookingsPageComponent)
      },
      {
        path: 'bookings/:id',
        canActivate: [portalAuthGuard],
        loadComponent: () =>
          import('./pages/booking-detail/booking-detail-page.component').then(
            (m) => m.BookingDetailPageComponent
          )
      },
      {
        path: 'payments',
        canActivate: [portalAuthGuard],
        loadComponent: () =>
          import('./pages/payments/payments-page.component').then((m) => m.PaymentsPageComponent)
      },
      {
        path: 'notifications',
        canActivate: [portalAuthGuard],
        loadComponent: () =>
          import('./pages/notifications/notifications-page.component').then(
            (m) => m.NotificationsPageComponent
          )
      },
      {
        path: 'trip-history',
        redirectTo: 'bookings'
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
