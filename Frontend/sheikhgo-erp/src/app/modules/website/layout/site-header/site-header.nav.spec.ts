import { isNavItemActive } from '../../../../core/navigation/nav-route-active.util';

/** Marketing header flat links — longest-route wins (same rules as ERP nav). */
const HEADER_FLAT_NAV = [
  { id: 'home', route: '/' },
  { id: 'platform', route: '/features' },
  { id: 'about', route: '/about' },
  { id: 'contact', route: '/contact' },
] as const;

describe('site header flat nav active matching', () => {
  it('highlights Platform on /features, not Contact', () => {
    const path = '/features';
    expect(isNavItemActive({ id: 'platform', route: '/features' }, path, {}, HEADER_FLAT_NAV)).toBe(true);
    expect(isNavItemActive({ id: 'contact', route: '/contact' }, path, {}, HEADER_FLAT_NAV)).toBe(false);
  });

  it('highlights Contact only on /contact', () => {
    expect(isNavItemActive({ id: 'contact', route: '/contact' }, '/contact', {}, HEADER_FLAT_NAV)).toBe(true);
    expect(isNavItemActive({ id: 'platform', route: '/features' }, '/contact', {}, HEADER_FLAT_NAV)).toBe(false);
  });
});
