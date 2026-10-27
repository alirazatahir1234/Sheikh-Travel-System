import { isNavItemActive } from '../../../../core/navigation/nav-route-active.util';

/** Marketing header flat links — longest-route wins (same rules as ERP nav). */
const HEADER_FLAT_NAV = [
  { id: 'home', route: '/' },
  { id: 'ai-solutions', route: '/ai-solutions' },
  { id: 'live-tracking', route: '/live-tracking' },
  { id: 'features', route: '/features' },
  { id: 'about', route: '/about' },
  { id: 'contact', route: '/contact' },
] as const;

describe('site header flat nav active matching', () => {
  it('highlights Live Tracking on /live-tracking, not Contact', () => {
    const path = '/live-tracking';
    expect(isNavItemActive({ id: 'live-tracking', route: '/live-tracking' }, path, {}, HEADER_FLAT_NAV)).toBe(true);
    expect(isNavItemActive({ id: 'contact', route: '/contact' }, path, {}, HEADER_FLAT_NAV)).toBe(false);
  });

  it('highlights Contact only on /contact', () => {
    expect(isNavItemActive({ id: 'contact', route: '/contact' }, '/contact', {}, HEADER_FLAT_NAV)).toBe(true);
    expect(isNavItemActive({ id: 'live-tracking', route: '/live-tracking' }, '/contact', {}, HEADER_FLAT_NAV)).toBe(
      false
    );
  });
});
