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
        path: 'fleet-management',
        loadComponent: () => import('./pages/fleet/fleet.page').then(m => m.FleetPage),
      },
      {
        path: 'gps-tracking',
        loadComponent: () => import('./pages/gps/gps.page').then(m => m.GpsPage),
      },
      {
        path: 'features',
        loadComponent: () => import('./pages/features/features.page').then(m => m.FeaturesPage),
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
      },
      {
        path: 'privacy-policy',
        loadComponent: () => import('./pages/legal/privacy.page').then(m => m.PrivacyPage),
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
