export interface NavItem {
  id: string;
  label: string;
  icon: string;
  route: string;
  adminOnly?: boolean;
  moduleKey?: string;
  queryParams?: Record<string, string>;
}

export interface NavGroup {
  id: string;
  label: string;
  icon: string;
  items: NavItem[];
  collapsible: boolean;
}

export interface ResolvedMenu {
  groups: NavGroup[];
  standaloneItems: NavItem[];
  isDriverLayout: boolean;
}
