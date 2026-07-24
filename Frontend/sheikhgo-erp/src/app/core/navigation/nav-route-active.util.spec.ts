import { isNavItemActive } from './nav-route-active.util';

describe('isNavItemActive', () => {
  const platformHome = { id: 'platform-home', route: '/platform' };
  const modules = { id: 'modules', route: '/platform/module-management' };
  const plans = { id: 'plans', route: '/platform/subscription-management' };
  const all = [platformHome, modules, plans];

  it('selects the longest matching route under /platform/...', () => {
    expect(
      isNavItemActive(modules, '/platform/module-management', {}, all)
    ).toBe(true);
    expect(
      isNavItemActive(platformHome, '/platform/module-management', {}, all)
    ).toBe(false);
  });

  it('selects platform home only on exact /platform', () => {
    expect(isNavItemActive(platformHome, '/platform', {}, all)).toBe(true);
    expect(isNavItemActive(modules, '/platform', {}, all)).toBe(false);
  });

  it('respects alias item suppression', () => {
    expect(
      isNavItemActive(platformHome, '/platform', {}, all, new Set(['platform-home']))
    ).toBe(false);
  });

  it('matches query-param items exactly', () => {
    const billing = {
      id: 'billing',
      route: '/platform/subscription-management',
      queryParams: { tab: 'billing' }
    };
    const items = [...all, billing];
    expect(
      isNavItemActive(billing, '/platform/subscription-management', { tab: 'billing' }, items)
    ).toBe(true);
    expect(
      isNavItemActive(billing, '/platform/subscription-management', {}, items)
    ).toBe(false);
  });
});
