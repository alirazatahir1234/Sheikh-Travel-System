/**
 * Records a logged-in SheikhGo Nexus ERP walkthrough (shell + modules), stitches MP4.
 *
 * Prereqs: `ng serve` on SHOT_BASE (default http://localhost:4200), Playwright chromium, ffmpeg.
 *
 * Usage:
 *   node tools/record-nexus-erp-demo.mjs
 */
import { chromium } from 'playwright';
import { mkdir, writeFile, rm } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ERP_ROOT = join(__dirname, '..');

const BASE = process.env.SHOT_BASE ?? 'http://localhost:4200';
const EMAIL = process.env.SHOT_EMAIL ?? 'admin@sheikhtravel.com';
const PASSWORD = process.env.SHOT_PASSWORD ?? 'Pass@123';
const OUT_DIR = process.env.OUT ?? join(ERP_ROOT, 'public', 'website', 'demo');
const SEG_DIR = join(OUT_DIR, '_erp_segments');
const FINAL_MP4 = join(OUT_DIR, 'sheikhgo-nexus-erp-walkthrough.mp4');

const VIEWPORT = { width: 1920, height: 1080 };

/** ERP routes after login — dwell times tuned for map/data loads. */
const ERP_MODULES = [
  { path: '/dashboard', wait: 4500, label: 'Dashboard' },
  { path: '/gps-tracking/live', wait: 7000, label: 'Live tracking' },
  { path: '/gps-tracking/alerts', wait: 4500, label: 'GPS alerts' },
  { path: '/gps-tracking/geofences', wait: 4500, label: 'Geofencing' },
  { path: '/gps-tracking/trips', wait: 4000, label: 'Trip history' },
  { path: '/gps-tracking', wait: 3800, label: 'Fleet tracking' },
  { path: '/vehicles', wait: 3500, label: 'Vehicles' },
  { path: '/drivers', wait: 3500, label: 'Drivers' },
  { path: '/trips', wait: 3500, label: 'Trips' },
  { path: '/bookings', wait: 4000, label: 'Bookings' },
  { path: '/fuel-logs', wait: 3500, label: 'Fuel' },
  { path: '/fleet/maintenance', wait: 3500, label: 'Maintenance' },
  { path: '/payments', wait: 3500, label: 'Payments' },
  { path: '/invoices', wait: 3500, label: 'Invoices' },
  { path: '/settlements', wait: 3200, label: 'Settlements' },
  { path: '/reports', wait: 4500, label: 'Reports' },
  { path: '/customers', wait: 3500, label: 'Customers' },
  { path: '/notifications', wait: 3200, label: 'Notifications' },
  { path: '/whatsapp', wait: 4000, label: 'WhatsApp' },
  { path: '/ai', wait: 4000, label: 'AI assistant' },
  { path: '/mcp', wait: 3500, label: 'MCP console' },
];

async function smoothScroll(page, deltaY, steps = 10, pauseMs = 70) {
  const step = deltaY / steps;
  for (let i = 0; i < steps; i++) {
    await page.mouse.wheel(0, step);
    await page.waitForTimeout(pauseMs);
  }
}

async function recordSegment(slug, run) {
  const browser = await chromium.launch({ channel: 'chromium', headless: true });
  const context = await browser.newContext({
    viewport: VIEWPORT,
    deviceScaleFactor: 1,
    ignoreHTTPSErrors: true,
    recordVideo: { dir: SEG_DIR, size: VIEWPORT },
  });
  const page = await context.newPage();
  page.setDefaultTimeout(60000);

  try {
    await run(page);
  } finally {
    await page.waitForTimeout(800);
    const video = page.video();
    await context.close();
    await browser.close();
    if (!video) throw new Error(`no video for segment ${slug}`);
    const webmPath = await video.path();
    const dest = join(SEG_DIR, `${slug}.webm`);
    const { rename } = await import('node:fs/promises');
    await rename(webmPath, dest);
    return dest;
  }
}

async function login(page) {
  await page.goto(`${BASE}/auth/login`, { waitUntil: 'networkidle' });
  await page.fill('#login-email', EMAIL);
  await page.fill('#login-password', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForURL(url => !url.pathname.startsWith('/auth'), { timeout: 45000 });
}

async function expandSidebar(page) {
  await page.evaluate(() => {
    localStorage.setItem('stb_sidebar_pinned', 'true');
  });
  const pin = page.locator('.stb-pin-btn').first();
  if (await pin.isVisible().catch(() => false)) {
    const expanded = await page.locator('.stb-rail.is-expanded').count();
    if (!expanded) {
      await pin.click();
      await page.waitForTimeout(600);
    }
  }
  await page.waitForSelector('.stb-rail-nav', { timeout: 20000 });
  await page.waitForTimeout(1200);
}

async function scrollSidebarNav(page) {
  const nav = page.locator('.stb-rail-nav').first();
  if (!(await nav.isVisible().catch(() => false))) return;
  await nav.evaluate(el => {
    el.scrollTop = 0;
  });
  await page.waitForTimeout(800);
  await nav.evaluate(el => {
    el.scrollTop = el.scrollHeight;
  });
  await page.waitForTimeout(1200);
  await nav.evaluate(el => {
    el.scrollTop = 0;
  });
  await page.waitForTimeout(600);
}

function ffmpeg(args) {
  const r = spawnSync('ffmpeg', args, { encoding: 'utf8' });
  if (r.status !== 0) {
    console.error(r.stderr?.slice(-2000));
    throw new Error(`ffmpeg failed: ${args.join(' ')}`);
  }
}

async function makeTitleCard(outPath, line1, line2, durationSec = 2.6) {
  const pngPath = outPath.replace(/\.mp4$/i, '.png');
  const browser = await chromium.launch({ channel: 'chromium', headless: true });
  const page = await browser.newPage({ viewport: VIEWPORT });
  const esc = s =>
    s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
  await page.setContent(`<!DOCTYPE html>
<html><head><meta charset="utf-8"/>
<style>
  * { box-sizing: border-box; margin: 0; }
  body {
    width: 1920px; height: 1080px;
    background: radial-gradient(ellipse 80% 60% at 50% 40%, #0f2847 0%, #07111f 55%, #030810 100%);
    font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
    display: flex; flex-direction: column; align-items: center; justify-content: center;
    color: #fff;
  }
  .kicker { letter-spacing: 0.2em; text-transform: uppercase; font-size: 18px; color: #64748b; margin-bottom: 20px; }
  h1 { font-size: 68px; font-weight: 700; letter-spacing: -0.02em; text-align: center; max-width: 90%; }
  p { margin-top: 24px; font-size: 32px; color: #94a3b8; text-align: center; max-width: 85%; }
  .mark { position: absolute; bottom: 48px; font-size: 22px; color: #475569; }
</style></head>
<body>
  <div class="kicker">SheikhGo NEXUS</div>
  <h1>${esc(line1)}</h1>
  <p>${esc(line2)}</p>
  <div class="mark">Web ERP · Live tenant demo</div>
</body></html>`);
  await page.screenshot({ path: pngPath, type: 'png' });
  await browser.close();
  ffmpeg([
    '-y',
    '-loop',
    '1',
    '-i',
    pngPath,
    '-t',
    String(durationSec),
    '-c:v',
    'libx264',
    '-pix_fmt',
    'yuv420p',
    '-r',
    '30',
    outPath,
  ]);
}

function webmToMp4(inPath, outPath) {
  ffmpeg([
    '-y',
    '-i',
    inPath,
    '-c:v',
    'libx264',
    '-pix_fmt',
    'yuv420p',
    '-crf',
    '23',
    '-preset',
    'fast',
    '-an',
    outPath,
  ]);
}

function concatMp4(listPath, outPath) {
  ffmpeg(['-y', '-f', 'concat', '-safe', '0', '-i', listPath, '-c', 'copy', outPath]);
}

async function main() {
  await rm(SEG_DIR, { recursive: true, force: true });
  await mkdir(SEG_DIR, { recursive: true });
  await mkdir(OUT_DIR, { recursive: true });

  const segments = [];

  console.log('Recording: sign-in + ERP shell sidebar…');
  segments.push(
    await recordSegment('01-shell', async page => {
      await login(page);
      await page.goto(`${BASE}/dashboard`, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2500);
      await expandSidebar(page);
      await scrollSidebarNav(page);
      await page.waitForTimeout(1500);
    }),
  );

  console.log('Recording: GPS & fleet operations…');
  const gpsFleet = ERP_MODULES.slice(1, 12);
  segments.push(
    await recordSegment('02-gps-fleet', async page => {
      await login(page);
      await expandSidebar(page);
      for (const mod of gpsFleet) {
        try {
          await page.goto(`${BASE}${mod.path}`, { waitUntil: 'domcontentloaded' });
          await page.waitForTimeout(mod.wait);
          if (mod.path.includes('live')) {
            await smoothScroll(page, 400, 8, 60);
            await page.waitForTimeout(800);
          }
        } catch (err) {
          console.warn('skip', mod.path, err.message);
        }
      }
    }),
  );

  console.log('Recording: finance, reports, comms, AI…');
  const rest = ERP_MODULES.slice(12);
  segments.push(
    await recordSegment('03-finance-ai', async page => {
      await login(page);
      await expandSidebar(page);
      for (const mod of rest) {
        try {
          await page.goto(`${BASE}${mod.path}`, { waitUntil: 'domcontentloaded' });
          await page.waitForTimeout(mod.wait);
        } catch (err) {
          console.warn('skip', mod.path, err.message);
        }
      }
    }),
  );

  const sectionTitles = [
    ['Operations shell', 'Sidebar navigation · Dashboard overview'],
    ['GPS & fleet', 'Live tracking · Alerts · Bookings · Maintenance'],
    ['Finance & intelligence', 'Reports · WhatsApp · AI · MCP'],
  ];

  const mp4Parts = [];
  mp4Parts.push(join(SEG_DIR, 'title-open.mp4'));
  await makeTitleCard(mp4Parts[0], 'Nexus ERP walkthrough', 'Logged-in fleet operations demo', 3);

  for (let i = 0; i < segments.length; i++) {
    const mp4 = join(SEG_DIR, `part-${i + 1}.mp4`);
    webmToMp4(segments[i], mp4);
    mp4Parts.push(mp4);
    const [t1, t2] = sectionTitles[i];
    const titleMp4 = join(SEG_DIR, `title-${i + 1}.mp4`);
    await makeTitleCard(titleMp4, t1, t2);
    mp4Parts.push(titleMp4);
  }

  mp4Parts.push(join(SEG_DIR, 'title-close.mp4'));
  await makeTitleCard(mp4Parts[mp4Parts.length - 1], 'sheikhgo.com', 'Request a demo · Start your fleet review', 3);

  const listFile = join(SEG_DIR, 'concat.txt');
  const listBody = mp4Parts.map(p => `file '${p.replace(/'/g, "'\\''")}'`).join('\n');
  await writeFile(listFile, listBody);

  console.log('Stitching final MP4…');
  concatMp4(listFile, FINAL_MP4);

  const manifest = {
    generatedAt: new Date().toISOString(),
    baseUrl: BASE,
    output: FINAL_MP4.replace(ERP_ROOT + '/', ''),
    kind: 'erp-application',
    segments: segments.map(s => s.replace(ERP_ROOT + '/', '')),
    erpRoutes: ERP_MODULES.map(m => m.path),
    notes: [
      'Marketing home/ai-solutions pages intentionally omitted.',
      'Bookings/WhatsApp/MCP depend on demo tenant modules.',
    ],
  };
  await writeFile(
    join(OUT_DIR, 'sheikhgo-nexus-erp-walkthrough.manifest.json'),
    JSON.stringify(manifest, null, 2),
  );

  console.log('Done:', FINAL_MP4);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
