import {
  isMcpConsoleEnabled,
  isMcpConsoleNavItem,
  stripMcpConsoleFromMenu
} from './mcp-console-access.util';
import { ResolvedMenu } from './nav-models';

describe('mcp-console-access.util (SG-22)', () => {
  it('isMcpConsoleEnabled is true only when enableMcpConsole is true', () => {
    expect(isMcpConsoleEnabled({ enableMcpConsole: true })).toBe(true);
    expect(isMcpConsoleEnabled({ enableMcpConsole: false })).toBe(false);
    expect(isMcpConsoleEnabled({})).toBe(false);
  });

  it('isMcpConsoleNavItem matches console id and /mcp routes only', () => {
    expect(isMcpConsoleNavItem({ id: 'mcp-console', route: '/mcp' })).toBe(true);
    expect(isMcpConsoleNavItem({ id: 'x', route: '/mcp/tools' })).toBe(true);
    expect(isMcpConsoleNavItem({ id: 'ai-center', route: '/ai' })).toBe(false);
    expect(isMcpConsoleNavItem({ id: 'dashboard', route: '/dashboard' })).toBe(false);
  });

  it('stripMcpConsoleFromMenu removes MCP Console and keeps AI Assistant', () => {
    const menu: ResolvedMenu = {
      groups: [
        {
          id: 'administration',
          label: 'Administration',
          icon: 'admin_panel_settings',
          collapsible: true,
          items: [
            { id: 'ai-center', label: 'SheikhGo AI Assistant', icon: 'smart_toy', route: '/ai' },
            { id: 'mcp-console', label: 'MCP Console', icon: 'hub', route: '/mcp' },
            { id: 'settings', label: 'Settings', icon: 'tune', route: '/settings' }
          ]
        }
      ],
      standaloneItems: [
        { id: 'mcp-console', label: 'MCP Console', icon: 'hub', route: '/mcp' }
      ],
      isDriverLayout: false
    };

    const stripped = stripMcpConsoleFromMenu(menu);
    const routes = stripped.groups.flatMap(g => g.items.map(i => i.route));
    expect(routes).toContain('/ai');
    expect(routes).toContain('/settings');
    expect(routes).not.toContain('/mcp');
    expect(stripped.standaloneItems ?? []).toEqual([]);
  });
});
