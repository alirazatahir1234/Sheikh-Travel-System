/**
 * Captures SheikhGo Nexus ERP module screenshots for the marketing site.
 * Usage: node tools/capture-nexus-modules.mjs
 */
import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const BASE = process.env.SHOT_BASE ?? 'http://localhost:4200';
const EMAIL = process.env.SHOT_EMAIL ?? 'admin@sheikhtravel.com';
const PASSWORD = process.env.SHOT_PASSWORD ?? 'Pass@123';
const OUT = 'public/website/nexus/modules';

/** slug → ERP route; caption is used on the website */
const SHOTS = [
  { slug: 'dashboard', caption: 'Dashboard', path: '/dashboard', wait: 4000 },
  { slug: 'live-tracking', caption: 'Live Tracking', path: '/gps-tracking/live', wait: 6500 },
  { slug: 'gps-alerts', caption: 'GPS Alerts', path: '/gps-tracking/alerts', wait: 4000 },
  { slug: 'geofencing', caption: 'Geofencing', path: '/gps-tracking/geofences', wait: 4500 },
  { slug: 'trip-history', caption: 'Trip History', path: '/gps-tracking/trips', wait: 4000 },
  { slug: 'fleet-tracking', caption: 'Fleet Tracking', path: '/gps-tracking', wait: 4000 },
  { slug: 'vehicles', caption: 'Vehicles', path: '/vehicles', wait: 3500 },
  { slug: 'drivers', caption: 'Drivers', path: '/drivers', wait: 3500 },
  { slug: 'trips', caption: 'Trips', path: '/trips', wait: 3500 },
  { slug: 'bookings', caption: 'Bookings', path: '/bookings', wait: 3500 },
  { slug: 'fuel', caption: 'Fuel', path: '/fuel-logs', wait: 3500 },
  { slug: 'maintenance', caption: 'Maintenance', path: '/fleet/maintenance', wait: 3500 },
  { slug: 'reports', caption: 'Reports', path: '/reports', wait: 4000 },
  { slug: 'ai-assistant', caption: 'AI Assistant', path: '/ai', wait: 4000 },
  { slug: 'customers', caption: 'Customers', path: '/customers', wait: 3500 },
  { slug: 'notifications', caption: 'Notifications', path: '/notifications', wait: 3500 },
];

const browser = await chromium.launch({ channel: 'chromium' });
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
  ignoreHTTPSErrors: true,
});
const page = await context.newPage();

await mkdir(OUT, { recursive: true });

const manifest = [];

try {
  await page.goto(`${BASE}/auth/login`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.fill('#login-email', EMAIL);
  await page.fill('#login-password', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForURL(url => !url.pathname.startsWith('/auth'), { timeout: 45000 });
  console.log('signed in ->', page.url());
} catch (err) {
  console.error('login failed', err.message);
  await browser.close();
  process.exit(1);
}

for (const shot of SHOTS) {
  try {
    await page.goto(`${BASE}${shot.path}`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await page.waitForTimeout(shot.wait);
    const file = `${OUT}/${shot.slug}.png`;
    await page.screenshot({ path: file, fullPage: false });
    manifest.push({ ...shot, file: `website/nexus/modules/${shot.slug}.png`, ok: true });
    console.log('captured', shot.slug, '<-', page.url());
  } catch (err) {
    manifest.push({ ...shot, ok: false, error: err.message });
    console.error('failed', shot.slug, err.message);
  }
}

await browser.close();
console.log(JSON.stringify({ count: manifest.filter(m => m.ok).length, manifest }, null, 2));
