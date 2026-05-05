import { Routes } from '@angular/router';
import { PortalShellComponent } from './layout/portal-shell.component';

export const routes: Routes = [
  {
    path: '',
    component: PortalShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./pages/dashboard/dashboard-page.component').then((m) => m.DashboardPageComponent)
      },
      {
        path: 'book',
        loadComponent: () =>
          import('./pages/book-ride/book-ride-page.component').then((m) => m.BookRidePageComponent)
      },
      {
        path: 'bookings',
        loadComponent: () =>
          import('./pages/my-bookings/my-bookings-page.component').then((m) => m.MyBookingsPageComponent)
      },
      {
        path: 'bookings/:id',
        loadComponent: () =>
          import('./pages/booking-detail/booking-detail-page.component').then(
            (m) => m.BookingDetailPageComponent
          )
      },
      {
        path: 'payments',
        loadComponent: () =>
          import('./pages/payments/payments-page.component').then((m) => m.PaymentsPageComponent)
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./pages/profile/profile-page.component').then((m) => m.ProfilePageComponent)
      },
      {
        path: 'help',
        loadComponent: () => import('./pages/help/help-page.component').then((m) => m.HelpPageComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
