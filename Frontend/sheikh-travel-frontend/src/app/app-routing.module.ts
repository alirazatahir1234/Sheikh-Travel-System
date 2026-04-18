import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { authGuard } from './core/guards/auth.guard';

const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule) },
      { path: 'vehicles', loadChildren: () => import('./modules/vehicles/vehicles.module').then(m => m.VehiclesModule) },
      { path: 'drivers', loadChildren: () => import('./modules/drivers/drivers.module').then(m => m.DriversModule) },
      { path: 'routes', loadChildren: () => import('./modules/routes/routes.module').then(m => m.RoutesModule) },
      { path: 'bookings', loadChildren: () => import('./modules/bookings/bookings.module').then(m => m.BookingsModule) },
      { path: 'payments', loadChildren: () => import('./modules/payments/payments.module').then(m => m.PaymentsModule) },
      { path: 'reports', loadChildren: () => import('./modules/reports/reports.module').then(m => m.ReportsModule) },
      { path: 'tracking', loadChildren: () => import('./modules/tracking/tracking.module').then(m => m.TrackingModule) }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
