import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission.guard';
import { TRAVEL_PERMISSIONS } from './models/travel.models';

/**
 * TravelNexus routes, mounted at /travel (parent route requires Travel.View + module travel-agency).
 * Later phases add bookings, finance and reports here.
 */
export const TRAVEL_NEXUS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.dashboardView] },
    loadComponent: () => import('./pages/dashboard/travel-dashboard.component').then(m => m.TravelDashboardComponent),
    title: 'TravelNexus dashboard'
  },
  {
    path: 'reminders',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.reminderView] },
    loadComponent: () => import('./pages/reminders/reminders-page.component').then(m => m.TravelRemindersPageComponent),
    title: 'Reminders'
  },
  {
    path: 'reports',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.reportView, TRAVEL_PERMISSIONS.financeView, TRAVEL_PERMISSIONS.profitabilityView] },
    loadComponent: () => import('./pages/reports/reports-page.component').then(m => m.TravelReportsPageComponent),
    title: 'Reports & Analytics'
  },
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
    path: 'packages',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.packageView] },
    loadComponent: () => import('./pages/packages/package-list.component').then(m => m.TravelPackageListComponent),
    title: 'Packages'
  },
  {
    path: 'packages/new',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.packageManage] },
    loadComponent: () => import('./pages/packages/package-editor.component').then(m => m.TravelPackageEditorComponent),
    title: 'New package'
  },
  {
    path: 'packages/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.packageView] },
    loadComponent: () => import('./pages/packages/package-editor.component').then(m => m.TravelPackageEditorComponent),
    title: 'Package'
  },
  {
    path: 'groups',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.groupView] },
    loadComponent: () => import('./pages/groups/group-list.component').then(m => m.TravelGroupListComponent),
    title: 'Umrah groups'
  },
  {
    path: 'groups/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.groupView] },
    loadComponent: () => import('./pages/groups/group-detail.component').then(m => m.TravelGroupDetailComponent),
    title: 'Umrah group'
  },
  {
    path: 'bookings',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.bookingView] },
    loadComponent: () => import('./pages/bookings/booking-list.component').then(m => m.TravelBookingListComponent),
    title: 'Bookings'
  },
  {
    path: 'bookings/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.bookingView] },
    loadComponent: () => import('./pages/bookings/booking-detail.component').then(m => m.TravelBookingDetailComponent),
    title: 'Booking'
  },
  {
    path: 'suppliers',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.supplierView] },
    loadComponent: () => import('./pages/suppliers/supplier-list.component').then(m => m.TravelSupplierListComponent),
    title: 'Suppliers'
  },
  {
    path: 'suppliers/bills/new',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.supplierBillManage] },
    loadComponent: () => import('./pages/suppliers/bill-editor.component').then(m => m.TravelBillEditorComponent),
    title: 'New supplier bill'
  },
  {
    path: 'suppliers/bills/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.financeView] },
    loadComponent: () => import('./pages/suppliers/bill-detail.component').then(m => m.TravelBillDetailComponent),
    title: 'Supplier bill'
  },
  {
    path: 'suppliers/bills/:id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.supplierBillManage] },
    loadComponent: () => import('./pages/suppliers/bill-editor.component').then(m => m.TravelBillEditorComponent),
    title: 'Edit supplier bill'
  },
  {
    path: 'suppliers/payments/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.financeView] },
    loadComponent: () => import('./pages/suppliers/supplier-payment-detail.component').then(m => m.TravelSupplierPaymentDetailComponent),
    title: 'Supplier payment'
  },
  {
    path: 'suppliers/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.supplierView] },
    loadComponent: () => import('./pages/suppliers/supplier-detail.component').then(m => m.TravelSupplierDetailComponent),
    title: 'Supplier'
  },
  {
    path: 'finance',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.financeView] },
    loadComponent: () => import('./pages/finance/finance-page.component').then(m => m.TravelFinancePageComponent),
    title: 'Invoices & Payments'
  },
  {
    path: 'finance/invoices/new',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.invoiceManage] },
    loadComponent: () => import('./pages/finance/invoice-editor.component').then(m => m.TravelInvoiceEditorComponent),
    title: 'New invoice'
  },
  {
    path: 'finance/invoices/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.financeView] },
    loadComponent: () => import('./pages/finance/invoice-detail.component').then(m => m.TravelInvoiceDetailComponent),
    title: 'Invoice'
  },
  {
    path: 'finance/invoices/:id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.invoiceManage] },
    loadComponent: () => import('./pages/finance/invoice-editor.component').then(m => m.TravelInvoiceEditorComponent),
    title: 'Edit invoice'
  },
  {
    path: 'finance/payments/:id',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.financeView] },
    loadComponent: () => import('./pages/finance/payment-detail.component').then(m => m.TravelPaymentDetailComponent),
    title: 'Payment'
  },
  {
    path: 'finance/statements/:customerId',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.financeView] },
    loadComponent: () => import('./pages/finance/statement.component').then(m => m.TravelStatementComponent),
    title: 'Statement'
  },
  {
    path: 'commissions',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.commissionView] },
    loadComponent: () => import('./pages/commissions/commissions-page.component').then(m => m.TravelCommissionsPageComponent),
    title: 'Commissions'
  },
  {
    path: 'profitability',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.profitabilityView] },
    loadComponent: () => import('./pages/profitability/profitability-page.component').then(m => m.TravelProfitabilityPageComponent),
    title: 'Profitability'
  },
  {
    path: 'settings',
    canActivate: [permissionGuard],
    data: { permissions: [TRAVEL_PERMISSIONS.settings] },
    loadComponent: () => import('./pages/settings/travel-settings.component').then(m => m.TravelSettingsComponent),
    title: 'TravelNexus settings'
  }
];
