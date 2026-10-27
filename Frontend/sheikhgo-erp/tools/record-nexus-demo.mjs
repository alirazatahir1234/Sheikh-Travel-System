/**
 * Records a SheikhGo Nexus marketing + ERP walkthrough as WebM segments, then stitches MP4.
 *
 * Prereqs: `ng serve` on SHOT_BASE (default http://localhost:4200), Playwright chromium.
 *
 * Usage:
 *   node tools/record-nexus-demo.mjs
 *   OUT=../../docs/demo node tools/record-nexus-demo.mjs
 */
import { chromium } from 'playwright';
import { mkdir, readdir, writeFile, rm } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ERP_ROOT = join(__dirname, '..');

const BASE = process.env.SHOT_BASE ?? 'http://localhost:4200';
const EMAIL = process.env.SHOT_EMAIL ?? 'admin@sheikhtravel.com';
const PASSWORD = process.env.SHOT_PASSWORD ?? 'Pass@123';
const OUT_DIR =
  process.env.OUT ?? join(ERP_ROOT, 'public', 'website', 'demo');
const SEG_DIR = join(OUT_DIR, '_segments');
const FINAL_MP4 = join(OUT_DIR, 'sheikhgo-nexus-walkthrough.mp4');

const VIEWPORT = { width: 1920, height: 1080 };

/** ERP routes to show live (aligned with capture-nexus-modules.mjs + bookings/whatsapp). */
const ERP_LIVE = [
  { path: '/dashboard', wait: 4000 },
  { path: '/gps-tracking/live', wait: 6000 },
  { path: '/gps-tracking/alerts', wait: 4000 },
  { path: '/gps-tracking/geofences', wait: 4000 },
  { path: '/gps-tracking/trips', wait: 3500 },
  { path: '/gps-tracking', wait: 3500 },
  { path: '/vehicles', wait: 3200 },
  { path: '/drivers', wait: 3200 },
  { path: '/trips', wait: 3200 },
  { path: '/bookings', wait: 3500 },
  { path: '/fuel-logs', wait: 3200 },
  { path: '/fleet/maintenance', wait: 3200 },
  { path: '/reports', wait: 3500 },
  { path: '/ai', wait: 3500 },
  { path: '/customers', wait: 3200 },
  { path: '/notifications', wait: 3200 },
  { path: '/whatsapp', wait: 3500 },
];

async function smoothScroll(page, deltaY, steps = 12, pauseMs = 80) {
  const step = deltaY / steps;
  for (let i = 0; i < steps; i++) {
    await page.mouse.wheel(0, step);
    await page.waitForTimeout(pauseMs);
  }
}

async function scrollToSelector(page, selector, timeout = 15000) {
  const el = page.locator(selector).first();
  await el.waitFor({ state: 'visible', timeout });
  await el.scrollIntoViewIfNeeded();
  await page.waitForTimeout(600);
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

function ffmpeg(args) {
  const r = spawnSync('ffmpeg', args, { encoding: 'utf8' });
  if (r.status !== 0) {
    console.error(r.stderr?.slice(-2000));
    throw new Error(`ffmpeg failed: ${args.join(' ')}`);
  }
}

async function makeTitleCard(outPath, line1, line2, durationSec = 2.8) {
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
  h1 { font-size: 72px; font-weight: 700; letter-spacing: -0.02em; text-align: center; max-width: 90%; }
  p { margin-top: 24px; font-size: 34px; color: #94a3b8; text-align: center; max-width: 85%; }
  .mark { position: absolute; bottom: 48px; font-size: 22px; color: #475569; }
</style></head>
<body>
  <div class="kicker">SheikhGo</div>
  <h1>${esc(line1)}</h1>
  <p>${esc(line2)}</p>
  <div class="mark">NEXUS · Fleet &amp; travel operations</div>
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

async function loadExistingSegments() {
  const names = (await readdir(SEG_DIR)).filter(n => /^\d{2}-.+\.webm$/.test(n)).sort();
  return names.map(n => join(SEG_DIR, n));
}

async function main() {
  const stitchOnly = process.env.STITCH_ONLY === '1';
  if (!stitchOnly) {
    await rm(SEG_DIR, { recursive: true, force: true });
  }
  await mkdir(SEG_DIR, { recursive: true });
  await mkdir(OUT_DIR, { recursive: true });

  let segments = stitchOnly ? await loadExistingSegments() : [];

  if (stitchOnly && segments.length === 0) {
    throw new Error('STITCH_ONLY=1 but no segment webms in ' + SEG_DIR);
  }

  if (!stitchOnly) console.log('Recording: intro / home / NEXUS centerpiece…');
  if (!stitchOnly) {
  segments.push(
    await recordSegment('01-marketing-home', async page => {
      await page.goto(`${BASE}/`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(3000);
      await scrollToSelector(page, '#see-the-platform');
      await page.waitForTimeout(4500);
      await scrollToSelector(page, '#platform-modules');
      await smoothScroll(page, 1200, 10, 80);
      await page.waitForTimeout(2000);
      await scrollToSelector(page, '#platform-features');
      await page.waitForTimeout(2500);
      await scrollToSelector(page, '#platform-modules');
      await smoothScroll(page, 2400, 20, 75);
      await page.waitForTimeout(3000);
      await scrollToSelector(page, '#top');
      await page.waitForTimeout(1200);
    }),
  );

  console.log('Recording: AI Solutions, Live Tracking, GPS Alerts (marketing)…');
  segments.push(
    await recordSegment('02-marketing-product-pages', async page => {
      for (const path of ['/ai-solutions', '/live-tracking', '/gps-alerts']) {
        await page.goto(`${BASE}${path}`, { waitUntil: 'networkidle' });
        await page.waitForTimeout(2800);
        await smoothScroll(page, 1800, 16, 70);
        await page.waitForTimeout(2600);
      }
    }),
  );

  console.log('Recording: live ERP modules…');
  segments.push(
    await recordSegment('03-erp-live', async page => {
      await login(page);
      for (const mod of ERP_LIVE) {
        try {
          await page.goto(`${BASE}${mod.path}`, { waitUntil: 'domcontentloaded' });
          await page.waitForTimeout(mod.wait);
        } catch (err) {
          console.warn('skip module', mod.path, err.message);
        }
      }
    }),
  );

  console.log('Recording: module gallery montage (14 captures)…');
  segments.push(
    await recordSegment('04-module-gallery', async page => {
      await page.goto(`${BASE}/`, { waitUntil: 'networkidle' });
      await scrollToSelector(page, '#platform-modules');
      const cards = page.locator('.nexus-module-card');
      const count = await cards.count();
      const limit = Math.min(count, 14);
      for (let i = 0; i < limit; i++) {
        await cards.nth(i).scrollIntoViewIfNeeded();
        await page.waitForTimeout(1100);
      }
      await page.waitForTimeout(1500);
    }),
  );

  console.log('Recording: request demo CTA…');
  segments.push(
    await recordSegment('05-cta', async page => {
      await page.goto(`${BASE}/request-demo`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(3500);
    }),
  );
  }

  const openTitle = ['SheikhGo NEXUS', 'Fleet intelligence platform'];
  /** Title card after each recorded segment (same order as webm files). */
  const sectionTitles = [
    ['Home & NEXUS centerpiece', 'Redesigned fleet intelligence story'],
    ['Marketing depth', 'AI Solutions · Live tracking · GPS Alerts'],
    ['Live demo tenant', 'Dashboard · GPS · Bookings · AI · WhatsApp · Reports'],
    ['14 platform modules', 'Screenshots from the running app'],
    ['Request a demo', 'Start your fleet workflow review'],
  ];

  const mp4Parts = [];
  mp4Parts.push(join(SEG_DIR, 'title-open.mp4'));
  await makeTitleCard(mp4Parts[0], openTitle[0], openTitle[1], 3.2);

  for (let i = 0; i < segments.length; i++) {
    const mp4 = join(SEG_DIR, `part-${i + 1}.mp4`);
    webmToMp4(segments[i], mp4);
    mp4Parts.push(mp4);
    const [t1, t2] = sectionTitles[i] ?? ['SheikhGo NEXUS', ''];
    const titleMp4 = join(SEG_DIR, `title-${i + 1}.mp4`);
    await makeTitleCard(titleMp4, t1, t2);
    mp4Parts.push(titleMp4);
  }

  mp4Parts.push(join(SEG_DIR, 'title-close.mp4'));
  await makeTitleCard(
    mp4Parts[mp4Parts.length - 1],
    'sheikhgo.com',
    'Web ERP · Mobile fleet · Shared API',
    3.5,
  );

  const listFile = join(SEG_DIR, 'concat.txt');
  const listBody = mp4Parts.map(p => `file '${p.replace(/'/g, "'\\''")}'`).join('\n');
  await writeFile(listFile, listBody);

  console.log('Stitching final MP4…');
  concatMp4(listFile, FINAL_MP4);

  const manifest = {
    generatedAt: new Date().toISOString(),
    baseUrl: BASE,
    output: FINAL_MP4,
    segments: segments.map(s => s.replace(ERP_ROOT + '/', '')),
    erpLiveRoutes: ERP_LIVE.map(m => m.path),
    notes: [
      'Bookings/WhatsApp shown only if enabled for demo tenant.',
      'Dedicated finance module route not in current ERP shell — covered via Reports/operations narrative.',
      'Module gallery uses 14 PNG captures on the home page.',
    ],
  };
  await writeFile(join(OUT_DIR, 'sheikhgo-nexus-walkthrough.manifest.json'), JSON.stringify(manifest, null, 2));

  console.log('Done:', FINAL_MP4);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
