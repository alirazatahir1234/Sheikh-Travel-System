/**
 * Captures product screenshots from the running local ERP for the marketing site.
 * Usage: node tools/capture-product-shots.mjs
 */
import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const BASE = process.env.SHOT_BASE ?? 'http://localhost:4200';
const EMAIL = process.env.SHOT_EMAIL ?? 'admin@sheikhtravel.com';
const PASSWORD = process.env.SHOT_PASSWORD ?? 'Pass@123';
const OUT = 'public/website/product';

const SHOTS = [
  { slug: 'live-map', path: '/gps-tracking/live', wait: 6000 },
  { slug: 'dashboard', path: '/dashboard', wait: 4000 },
  { slug: 'vehicles', path: '/vehicles', wait: 3500 },
  { slug: 'trips', path: '/trips', wait: 3500 },
  { slug: 'drivers', path: '/drivers', wait: 3500 },
  { slug: 'reports', path: '/reports', wait: 4000 },
];

const browser = await chromium.launch({ channel: 'chromium' });
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
  ignoreHTTPSErrors: true,
});
const page = await context.newPage();

await mkdir(OUT, { recursive: true });

await page.goto(`${BASE}/auth/login`, { waitUntil: 'networkidle' });
await page.fill('#login-email', EMAIL);
await page.fill('#login-password', PASSWORD);
await page.click('button[type="submit"]');
await page.waitForURL(url => !url.pathname.startsWith('/auth'), { timeout: 30000 });
console.log('signed in ->', page.url());

for (const shot of SHOTS) {
  try {
    await page.goto(`${BASE}${shot.path}`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(shot.wait);
    await page.screenshot({ path: `${OUT}/${shot.slug}.png` });
    console.log('captured', shot.slug, '<-', page.url());
  } catch (err) {
    console.error('failed', shot.slug, err.message);
  }
}

await browser.close();
