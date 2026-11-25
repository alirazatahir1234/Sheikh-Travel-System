import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { TRAVEL_PERMISSIONS } from './models/travel.models';

/**
 * TravelNexus routes, mounted at /travel (parent route requires Travel.View + module travel-agency).
 * Later phases add enquiries, visa cases, Umrah groups, bookings, finance and reports here.
 */
export const TRAVEL_NEXUS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'customers' },
  {
    path: 'customers',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.customerView] },
    loadComponent: () => import('./pages/customers/customer-list.component').then(m => m.TravelCustomerListComponent),
    title: 'Travel customers'
  },
  {
    path: 'customers/new',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.customerManage] },
    loadComponent: () => import('./pages/customers/customer-form.component').then(m => m.TravelCustomerFormComponent),
    title: 'New travel customer'
  },
  {
    path: 'customers/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.customerView] },
    loadComponent: () => import('./pages/customers/customer-detail.component').then(m => m.TravelCustomerDetailComponent),
    title: 'Travel customer'
  },
  {
    path: 'customers/:id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.customerManage] },
    loadComponent: () => import('./pages/customers/customer-form.component').then(m => m.TravelCustomerFormComponent),
    title: 'Edit travel customer'
  },
  {
    path: 'settings',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.settings] },
    loadComponent: () => import('./pages/settings/travel-settings.component').then(m => m.TravelSettingsComponent),
    title: 'TravelNexus settings'
  }
];
