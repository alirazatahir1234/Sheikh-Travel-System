import { Routes } from '@angular/router';
import { WebsiteShellComponent } from './website-shell.component';

export const WEBSITE_ROUTES: Routes = [
  {
    path: '',
    component: WebsiteShellComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./pages/home/home.page').then(m => m.HomePage),
      },
      {
        path: 'logistics-fleet',
        loadComponent: () =>
          import('./pages/logistics-fleet/logistics-fleet.page').then(m => m.LogisticsFleetPage),
      },
      {
        path: 'fleet-management',
        loadComponent: () => import('./pages/fleet/fleet.page').then(m => m.FleetPage),
      },
      {
        path: 'gps-tracking',
        loadComponent: () => import('./pages/gps/gps.page').then(m => m.GpsPage),
      },
      {
        path: 'vehicle-rental',
        loadComponent: () =>
          import('./pages/vehicle-rental/vehicle-rental.page').then(m => m.VehicleRentalPage),
      },
      {
        path: 'transportation-operations',
        loadComponent: () =>
          import('./pages/transportation-operations/transportation-operations.page').then(
            m => m.TransportationOperationsPage,
          ),
      },
      {
        path: 'ai-fleet-insights',
        loadComponent: () =>
          import('./pages/ai-fleet-insights/ai-fleet-insights.page').then(m => m.AiFleetInsightsPage),
      },
      {
        path: 'gps-alerts',
        loadComponent: () => import('./pages/gps-alerts/gps-alerts.page').then(m => m.GpsAlertsPage),
      },
      {
        path: 'ai-solutions',
        loadComponent: () =>
          import('./pages/ai-solutions/ai-solutions.page').then(m => m.AiSolutionsPage),
      },
      {
        path: 'live-tracking',
        loadComponent: () =>
          import('./pages/live-tracking/live-tracking.page').then(m => m.LiveTrackingPage),
      },
      {
        path: 'driver-performance',
        loadComponent: () =>
          import('./pages/driver-performance/driver-performance.page').then(m => m.DriverPerformancePage),
      },
      {
        path: 'driver-ratings',
        loadComponent: () =>
          import('./pages/driver-ratings/driver-ratings.page').then(m => m.DriverRatingsPage),
      },
      {
        path: 'predictive-maintenance',
        loadComponent: () =>
          import('./pages/predictive-maintenance/predictive-maintenance.page').then(m => m.PredictiveMaintenancePage),
      },
      {
        path: 'accident-risk',
        loadComponent: () =>
          import('./pages/accident-risk/accident-risk.page').then(m => m.AccidentRiskPage),
      },
      {
        path: 'client-tracking',
        loadComponent: () =>
          import('./pages/client-tracking/client-tracking.page').then(m => m.ClientTrackingPage),
      },
      {
        path: 'electric-fleet',
        loadComponent: () =>
          import('./pages/electric-fleet/electric-fleet.page').then(m => m.ElectricFleetPage),
      },
      {
        path: 'document-tracker',
        loadComponent: () =>
          import('./pages/document-tracker/document-tracker.page').then(m => m.DocumentTrackerPage),
      },
      {
        path: 'anomaly-detection',
        loadComponent: () =>
          import('./pages/anomaly-detection/anomaly-detection.page').then(m => m.AnomalyDetectionPage),
      },
      {
        path: 'features',
        loadComponent: () => import('./pages/features/features.page').then(m => m.FeaturesPage),
      },
      {
        path: 'services',
        loadComponent: () => import('./pages/services/services.page').then(m => m.ServicesPage),
      },
      {
        path: 'about',
        loadComponent: () => import('./pages/about/about.page').then(m => m.AboutPage),
      },
      {
        path: 'contact',
        loadComponent: () => import('./pages/contact/contact.page').then(m => m.ContactPage),
      },
      {
        path: 'request-demo',
        loadComponent: () =>
          import('./pages/request-demo/request-demo.page').then(m => m.RequestDemoPage),
        data: { intent: 'demo' },
      },
      {
        path: 'fleet-assessment',
        loadComponent: () =>
          import('./pages/request-demo/request-demo.page').then(m => m.RequestDemoPage),
        data: { intent: 'assessment' },
      },
      { path: 'book-a-demo', redirectTo: 'request-demo', pathMatch: 'full' },
      {
        path: 'privacy-policy',
        loadComponent: () => import('./pages/legal/privacy.page').then(m => m.PrivacyPage),
      },
      {
        path: 'delete-account',
        loadComponent: () =>
          import('./pages/legal/delete-account.page').then(m => m.DeleteAccountPage),
      },
      {
        path: 'terms-and-conditions',
        loadComponent: () => import('./pages/legal/terms.page').then(m => m.TermsPage),
      },
      {
        path: 'cookie-policy',
        loadComponent: () => import('./pages/legal/cookie.page').then(m => m.CookiePage),
      },
    ],
  },
];
