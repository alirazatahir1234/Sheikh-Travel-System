import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

interface HubLink {
  label: string;
  description: string;
  icon: string;
  route: string;
  queryParams?: Record<string, string>;
  permissions: string[];
  superAdminOnly?: boolean;
}

interface HubSection {
  id: string;
  title: string;
  description: string;
  links: HubLink[];
}

@Component({
  standalone: false,
  selector: 'app-platform-hub',
  templateUrl: './platform-hub.component.html',
  styleUrls: ['./platform-hub.component.scss']
})
export class PlatformHubComponent {
  readonly sections: HubSection[] = [
    {
      id: 'company',
      title: 'Company',
      description: 'Company directory, provision, and company profile.',
      links: [
        {
          label: 'Companies',
          description: 'List, provision, and manage companies.',
          icon: 'business',
          route: '/platform/tenants',
          permissions: ['Platform.Tenants.View']
        }
      ]
    },
    {
      id: 'organization',
      title: 'Organization',
      description: 'Hierarchy, branches, and departments.',
      links: [
        {
          label: 'Hierarchy',
          description: 'Organization designer and structure.',
          icon: 'account_tree',
          route: '/platform/organization-designer',
          permissions: ['Platform.Branches.Manage', 'Platform.Departments.Manage', 'Platform.Tenants.View']
        },
        {
          label: 'Branches',
          description: 'Branch directory and edits.',
          icon: 'location_city',
          route: '/platform/branches',
          permissions: ['Platform.Branches.Manage']
        },
        {
          label: 'Departments',
          description: 'Department directory.',
          icon: 'domain',
          route: '/platform/departments',
          permissions: ['Platform.Departments.Manage']
        }
      ]
    },
    {
      id: 'identity',
      title: 'Identity & Access',
      description: 'Users, roles, permissions, and access policies.',
      links: [
        {
          label: 'Access Control',
          description: 'Canonical identity hub.',
          icon: 'admin_panel_settings',
          route: '/platform/access-control',
          permissions: ['Platform.Roles.View', 'Platform.Users.View']
        },
        {
          label: 'Users',
          description: 'User directory.',
          icon: 'manage_accounts',
          route: '/users',
          permissions: ['Platform.Users.View']
        },
        {
          label: 'Roles',
          description: 'Role matrix inside Access Control.',
          icon: 'security',
          route: '/platform/access-control',
          queryParams: { tab: 'roles' },
          permissions: ['Platform.Roles.View']
        }
      ]
    },
    {
      id: 'commercial',
      title: 'Commercial',
      description: 'Modules, features, plans, and billing.',
      links: [
        {
          label: 'Modules',
          description: 'Enable or disable tenant modules.',
          icon: 'extension',
          route: '/platform/module-management',
          permissions: ['Platform.Tenants.View', 'Platform.Tenants.Manage']
        },
        {
          label: 'Features',
          description: 'Company feature enablement within modules.',
          icon: 'tune',
          route: '/platform/feature-management',
          permissions: ['Platform.Tenants.View', 'Platform.Tenants.Manage']
        },
        {
          label: 'Menus',
          description: 'Navigation catalog labels, routes, and visibility.',
          icon: 'menu',
          route: '/platform/menu-management',
          permissions: ['Platform.Menus.Manage']
        },
        {
          label: 'Workspaces',
          description: 'Landing workspaces and company enablement.',
          icon: 'workspaces',
          route: '/platform/workspace-management',
          permissions: ['Platform.Workspaces.Manage']
        },
        {
          label: 'Dashboards',
          description: 'Dashboard catalog layouts and widget order.',
          icon: 'dashboard_customize',
          route: '/platform/dashboard-management',
          permissions: ['Platform.Dashboards.View', 'Platform.Dashboards.Manage']
        },
        {
          label: 'Subscriptions',
          description: 'Plans, renewals, and invoices.',
          icon: 'subscriptions',
          route: '/platform/subscription-management',
          permissions: ['Platform.Tenants.View', 'Platform.Tenants.Manage']
        }
      ]
    },
    {
      id: 'system',
      title: 'System',
      description: 'Schema migrations and database reset tools.',
      links: [
        {
          label: 'Migration Manager',
          description: 'View and apply pending schema migrations.',
          icon: 'storage',
          route: '/platform/migrations',
          permissions: ['Platform.Migrations.View', 'Platform.Migrations.Manage'],
          superAdminOnly: true
        },
        {
          label: 'Database Reset',
          description: 'Dev/Staging system maintenance only.',
          icon: 'build_circle',
          route: '/platform/maintenance',
          permissions: ['Platform.System.Reset'],
          superAdminOnly: true
        }
      ]
    },
    {
      id: 'configuration',
      title: 'Configuration',
      description: 'Tenant settings and audit trail.',
      links: [
        {
          label: 'Settings',
          description: 'Tenant configuration categories.',
          icon: 'tune',
          route: '/settings',
          permissions: ['Platform.Settings.View']
        },
        {
          label: 'Audit Logs',
          description: 'Security and change history.',
          icon: 'history',
          route: '/audit-logs',
          permissions: ['Platform.AuditLogs.View']
        }
      ]
    }
  ];

  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

  get visibleSections(): HubSection[] {
    return this.sections
      .map(section => ({
        ...section,
        links: section.links.filter(link => this.canSee(link))
      }))
      .filter(section => section.links.length > 0);
  }

  canSee(link: HubLink): boolean {
    if (link.superAdminOnly && !this.auth.hasRole('SUPER_ADMIN') && !this.auth.hasRole('SuperAdmin')) {
      // Super Admin permission bypass still allows via hasAnyPermission below.
      if (!this.auth.hasAnyPermission(link.permissions)) return false;
    }
    return this.auth.hasAnyPermission(link.permissions);
  }

  open(link: HubLink): void {
    void this.router.navigate([link.route], { queryParams: link.queryParams });
  }
}
