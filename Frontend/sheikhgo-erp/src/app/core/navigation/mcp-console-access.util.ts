import { environment } from '../../../environments/environment';
import { NavItem, ResolvedMenu } from './nav-models';

/** SG-22 — MCP Console is environment-gated (dev/staging only by default). */
export function isMcpConsoleEnabled(
  env: { enableMcpConsole?: boolean } = environment
): boolean {
  return env.enableMcpConsole === true;
}

export function isMcpConsoleNavItem(item: Pick<NavItem, 'id' | 'route'>): boolean {
  const route = item.route ?? '';
  return item.id === 'mcp-console'
    || route === '/mcp'
    || route.startsWith('/mcp/');
}

/** Remove MCP Console links; leave SheikhGo AI Assistant (/ai) intact. */
export function stripMcpConsoleFromMenu(menu: ResolvedMenu): ResolvedMenu {
  return {
    ...menu,
    groups: menu.groups
      .map(g => ({ ...g, items: g.items.filter(i => !isMcpConsoleNavItem(i)) }))
      .filter(g => g.items.length > 0),
    standaloneItems: (menu.standaloneItems ?? []).filter(i => !isMcpConsoleNavItem(i))
  };
}
