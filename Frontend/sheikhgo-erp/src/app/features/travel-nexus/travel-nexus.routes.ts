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
    path: 'enquiries',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.enquiryView] },
    loadComponent: () => import('./pages/enquiries/enquiry-list.component').then(m => m.TravelEnquiryListComponent),
    title: 'Enquiries'
  },
  {
    path: 'enquiries/new',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.enquiryManage] },
    loadComponent: () => import('./pages/enquiries/enquiry-form.component').then(m => m.TravelEnquiryFormComponent),
    title: 'New enquiry'
  },
  {
    path: 'enquiries/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.enquiryView] },
    loadComponent: () => import('./pages/enquiries/enquiry-detail.component').then(m => m.TravelEnquiryDetailComponent),
    title: 'Enquiry'
  },
  {
    path: 'enquiries/:id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.enquiryManage] },
    loadComponent: () => import('./pages/enquiries/enquiry-form.component').then(m => m.TravelEnquiryFormComponent),
    title: 'Edit enquiry'
  },
  {
    path: 'visa-cases',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.visaView] },
    loadComponent: () => import('./pages/visa-cases/visa-case-list.component').then(m => m.TravelVisaCaseListComponent),
    title: 'Visa cases'
  },
  {
    path: 'visa-cases/new',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.visaManage] },
    loadComponent: () => import('./pages/visa-cases/visa-case-form.component').then(m => m.TravelVisaCaseFormComponent),
    title: 'New visa case'
  },
  {
    path: 'visa-cases/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.visaView] },
    loadComponent: () => import('./pages/visa-cases/visa-case-detail.component').then(m => m.TravelVisaCaseDetailComponent),
    title: 'Visa case'
  },
  {
    path: 'visa-cases/:id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.visaManage] },
    loadComponent: () => import('./pages/visa-cases/visa-case-form.component').then(m => m.TravelVisaCaseFormComponent),
    title: 'Edit visa case'
  },
  {
    path: 'checklists',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.view] },
    loadComponent: () => import('./pages/checklists/checklist-list.component').then(m => m.TravelChecklistListComponent),
    title: 'Document checklists'
  },
  {
    path: 'checklists/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.view] },
    loadComponent: () => import('./pages/checklists/checklist-editor.component').then(m => m.TravelChecklistEditorComponent),
    title: 'Document checklist'
  },
  {
    path: 'settings',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.settings] },
    loadComponent: () => import('./pages/settings/travel-settings.component').then(m => m.TravelSettingsComponent),
    title: 'TravelNexus settings'
  }
];
