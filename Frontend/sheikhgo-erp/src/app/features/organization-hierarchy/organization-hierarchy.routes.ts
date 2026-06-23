import { Routes } from '@angular/router';

export const ORGANIZATION_HIERARCHY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/hierarchy-configuration/hierarchy-configuration.component').then(
        m => m.HierarchyConfigurationComponent
      ),
    title: 'Organization Hierarchy',
  },
];
