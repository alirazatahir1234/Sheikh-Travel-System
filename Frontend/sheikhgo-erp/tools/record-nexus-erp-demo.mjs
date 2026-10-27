/**
 * Records a logged-in SheikhGo Nexus ERP walkthrough (shell + modules), stitches MP4 + BGM.
 *
 * Prereqs: `ng serve` on SHOT_BASE (default http://localhost:4200), Playwright chromium, ffmpeg.
 *
 * Usage:
 *   node tools/record-nexus-erp-demo.mjs
 *   STITCH_ONLY=1 node tools/record-nexus-erp-demo.mjs
 */
import { chromium } from 'playwright';
import { mkdir, writeFile, rm, readdir } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ERP_ROOT = join(__dirname, '..');

const BASE = process.env.SHOT_BASE ?? 'http://localhost:4200';
const EMAIL = process.env.SHOT_EMAIL ?? 'admin@sheikhtravel.com';
const PASSWORD = process.env.SHOT_PASSWORD ?? 'Pass@123';
const OUT_DIR = process.env.OUT ?? join(ERP_ROOT, 'public', 'website', 'demo');
const SEG_DIR = join(OUT_DIR, '_erp_segments');
const FINAL_MP4 = join(OUT_DIR, 'sheikhgo-nexus-erp-walkthrough.mp4');
const FINAL_SILENT = join(SEG_DIR, 'walkthrough-silent.mp4');
const REPO_ROOT = join(ERP_ROOT, '..', '..');
const DEFAULT_BGM = join(REPO_ROOT, 'docs', 'demo', 'audio-src', 'mixkit-motivating-mornings-33.mp3');
const BGM_PATH = process.env.BGM ?? DEFAULT_BGM;
const BGM_VOLUME = process.env.BGM_VOLUME ?? '0.22';

/** Mixkit — Motivating Mornings (#33). Free for commercial use: https://mixkit.co/license/ */
const BGM_SOURCE = {
  title: 'Motivating Mornings',
  provider: 'Mixkit',
  url: 'https://mixkit.co/free-stock-music/motivating-mornings-561/',
  license: 'Mixkit License (free for commercial use)',
  file: 'docs/demo/audio-src/mixkit-motivating-mornings-33.mp3',
};

const VIEWPORT = { width: 1920, height: 1080 };

const COOKIE_CONSENT = JSON.stringify({
  necessary: true,
  analytics: true,
  functional: true,
  marketing: true,
  decidedAt: new Date().toISOString(),
});

async function prepareBrowserContext(context) {
  await context.addInitScript(consentJson => {
    localStorage.setItem('sg_cookie_consent_v1', consentJson);
    localStorage.setItem('stb_sidebar_pinned', 'true');
  }, COOKIE_CONSENT);
}

/** Remove dev-server compile overlays so they never appear in demo footage. */
async function dismissDevOverlays(page) {
  await page.evaluate(() => {
    for (const sel of [
      'vite-error-overlay',
      '#webpack-dev-server-client-overlay',
      '.webpack-dev-server-client-overlay',
    ]) {
      document.querySelector(sel)?.remove();
    }
  });
}

async function waitForErpShell(page) {
  await page.waitForSelector('.stb-rail-nav', { timeout: 45000 }).catch(() => undefined);
  await dismissDevOverlays(page);
  await page.waitForTimeout(400);
}

/** ~10–15 s per module; priority modules at upper range (see docs/demo/SheikhGo-Nexus-ERP-Walkthrough-Script.md). */
const SHOT_DEFAULT = 12_000;
const SHOT_PRIORITY = 14_000;

const ERP_MODULES = [
  { path: '/gps-tracking/live', label: 'Live GPS tracking', shotMs: SHOT_PRIORITY, segment: 'priority' },
  { path: '/gps-tracking/alerts', label: 'Speed & GPS alerts', shotMs: SHOT_PRIORITY, segment: 'priority' },
  { path: '/driver-wallets', label: 'Driver payment wallet', shotMs: SHOT_PRIORITY, segment: 'priority' },
  { path: '/gps-tracking/geofences', label: 'Geofencing', segment: 'operations' },
  { path: '/vehicles', label: 'Vehicles', segment: 'operations' },
  { path: '/bookings', label: 'Bookings', segment: 'operations' },
  { path: '/fleet/maintenance', label: 'Maintenance', segment: 'operations' },
  { path: '/payments', label: 'Payments', segment: 'finance' },
  { path: '/reports', label: 'Reports & analytics', segment: 'finance' },
  { path: '/whatsapp', label: 'WhatsApp inbox', segment: 'finance' },
  { path: '/ai', label: 'AI assistant', segment: 'finance' },
];

const DRIVER_MOCK_IMAGE = 'website/product/drivers.png';

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
  await prepareBrowserContext(context);
  const page = await context.newPage();
  page.setDefaultTimeout(60000);

  try {
    await run(page);
  } finally {
    await page.waitForTimeout(800);
    const video = page.video();
    const dest = join(SEG_DIR, `${slug}.webm`);
    await context.close();
    await browser.close();
    if (!video) throw new Error(`no video for segment ${slug}`);
    await video.saveAs(dest);
    return dest;
  }
}

async function login(page) {
  await page.goto(`${BASE}/auth/login`, { waitUntil: 'networkidle' });
  await dismissDevOverlays(page);
  await page.fill('#login-email', EMAIL);
  await page.fill('#login-password', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForURL(url => !url.pathname.startsWith('/auth'), { timeout: 45000 });
  await waitForErpShell(page);
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

async function showModuleCaption(page, label) {
  await page.evaluate(text => {
    let el = document.getElementById('demo-caption-overlay');
    if (!el) {
      el = document.createElement('div');
      el.id = 'demo-caption-overlay';
      el.style.cssText =
        'position:fixed;bottom:32px;left:50%;transform:translateX(-50%);z-index:99999;' +
        'padding:14px 28px;background:rgba(7,17,31,0.88);color:#f8fafc;font:600 24px system-ui,sans-serif;' +
        'border-radius:10px;border:1px solid rgba(148,163,184,0.35);pointer-events:none;letter-spacing:0.01em;';
      document.body.appendChild(el);
    }
    el.textContent = text;
  }, label);
}

async function visitModule(page, mod) {
  const dwell = mod.shotMs ?? SHOT_DEFAULT;
  await page.goto(`${BASE}${mod.path}`, { waitUntil: 'networkidle' });
  await waitForErpShell(page);
  await page.waitForSelector('.ui-toast, .toast-error', { state: 'hidden', timeout: 8000 }).catch(() => undefined);
  await dismissDevOverlays(page);
  await showModuleCaption(page, mod.label);
  await page.waitForTimeout(dwell);
  if (mod.path.includes('live')) {
    await smoothScroll(page, 400, 8, 60);
    await page.waitForTimeout(900);
  } else if (mod.path === '/reports') {
    await smoothScroll(page, 320, 6, 55);
    await page.waitForTimeout(700);
  }
}

function modulesForSegment(key) {
  return ERP_MODULES.filter(m => m.segment === key);
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
  await makeSceneCard(outPath, { variant: 'title', line1, line2, durationSec });
}

/**
 * Full-screen cards: title, FAQ, illustrative case study, driver mock, CTA.
 */
async function makeSceneCard(outPath, opts) {
  const {
    variant = 'title',
    line1 = '',
    line2 = '',
    line3 = '',
    durationSec = 3,
    imagePublicPath = DRIVER_MOCK_IMAGE,
  } = opts;
  const pngPath = outPath.replace(/\.mp4$/i, '.png');
  const browser = await chromium.launch({ channel: 'chromium', headless: true });
  const page = await browser.newPage({ viewport: VIEWPORT });
  const esc = s =>
    String(s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/"/g, '&quot;');
  const imageFile = join(ERP_ROOT, 'public', imagePublicPath);
  const imageSrc = existsSync(imageFile) ? `file://${imageFile}` : '';

  let body = '';
  if (variant === 'faq') {
    body = `
  <div class="kicker">FAQ</div>
  <h1 class="faq-q">${esc(line1)}</h1>
  <p class="faq-a">${esc(line2)}</p>`;
  } else if (variant === 'case-study') {
    body = `
  <div class="kicker">Illustrative fleet example</div>
  <p class="disclaimer">${esc(line1)}</p>
  <ul class="stats">
    <li><strong>${esc(line2)}</strong><span>idle fuel spend</span></li>
    <li><strong>${esc(line3.split('|')[0] ?? '')}</strong><span>dispatch reconciliation</span></li>
    <li><strong>${esc(line3.split('|')[1] ?? '')}</strong><span>wallet cash-out approvals</span></li>
  </ul>`;
  } else if (variant === 'driver') {
    body = `
  <div class="kicker">SheikhGo Driver</div>
  <div class="phone-row">
    <div class="phone">
      ${imageSrc ? `<img src="${imageSrc}" alt="Driver app"/>` : '<div class="phone-fallback">Driver app</div>'}
    </div>
    <div class="phone-copy">
      <h1>${esc(line1)}</h1>
      <p>${esc(line2)}</p>
    </div>
  </div>`;
  } else if (variant === 'cta') {
    body = `
  <div class="kicker">SheikhGo NEXUS</div>
  <h1 class="cta">${esc(line1)}</h1>
  <p>${esc(line2)}</p>`;
  } else {
    body = `
  <div class="kicker">SheikhGo NEXUS</div>
  <h1>${esc(line1)}</h1>
  <p>${esc(line2)}</p>
  <div class="mark">Web ERP · Live tenant demo</div>`;
  }

  await page.setContent(`<!DOCTYPE html>
<html><head><meta charset="utf-8"/>
<style>
  * { box-sizing: border-box; margin: 0; }
  body {
    width: 1920px; height: 1080px;
    background: radial-gradient(ellipse 80% 60% at 50% 40%, #0f2847 0%, #07111f 55%, #030810 100%);
    font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
    display: flex; flex-direction: column; align-items: center; justify-content: center;
    color: #fff; position: relative; padding: 48px;
  }
  .kicker { letter-spacing: 0.2em; text-transform: uppercase; font-size: 18px; color: #64748b; margin-bottom: 20px; }
  h1 { font-size: 58px; font-weight: 700; letter-spacing: -0.02em; text-align: center; max-width: 92%; line-height: 1.12; }
  h1.faq-q { font-size: 46px; color: #e2e8f0; }
  h1.cta { font-size: 72px; color: #99f6e4; }
  p { margin-top: 24px; font-size: 30px; color: #94a3b8; text-align: center; max-width: 88%; line-height: 1.45; }
  p.faq-a { font-size: 34px; color: #cbd5e1; max-width: 78%; }
  p.disclaimer { font-size: 22px; color: #64748b; font-style: italic; margin-bottom: 36px; }
  .mark { position: absolute; bottom: 48px; font-size: 22px; color: #475569; }
  ul.stats { list-style: none; display: grid; grid-template-columns: repeat(3, 1fr); gap: 28px; margin-top: 12px; width: min(1200px, 92%); }
  ul.stats li { background: rgba(15,40,71,0.65); border: 1px solid rgba(148,163,184,0.25); border-radius: 16px; padding: 28px 24px; text-align: center; }
  ul.stats strong { display: block; font-size: 52px; color: #99f6e4; margin-bottom: 8px; }
  ul.stats span { font-size: 20px; color: #94a3b8; }
  .phone-row { display: flex; align-items: center; gap: 56px; max-width: 1200px; }
  .phone { width: 320px; height: 640px; border-radius: 36px; padding: 14px; background: linear-gradient(145deg,#1e293b,#0f172a); box-shadow: 0 24px 80px rgba(0,0,0,0.45); border: 2px solid rgba(148,163,184,0.35); overflow: hidden; }
  .phone img { width: 100%; height: 100%; object-fit: cover; border-radius: 24px; }
  .phone-fallback { height: 100%; display: grid; place-items: center; color: #64748b; font-size: 22px; }
  .phone-copy { max-width: 520px; text-align: left; }
  .phone-copy h1 { text-align: left; font-size: 48px; }
  .phone-copy p { text-align: left; margin-top: 16px; }
</style></head>
<body>${body}</body></html>`);
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

function probeDurationSec(path) {
  const r = spawnSync(
    'ffprobe',
    [
      '-v',
      'error',
      '-show_entries',
      'format=duration',
      '-of',
      'default=noprint_wrappers=1:nokey=1',
      path,
    ],
    { encoding: 'utf8' },
  );
  if (r.status !== 0) throw new Error(`ffprobe failed for ${path}`);
  return parseFloat(r.stdout.trim());
}

function mixBackgroundMusic(silentVideo, bgmFile, outPath, volume = BGM_VOLUME) {
  const dur = probeDurationSec(silentVideo);
  ffmpeg([
    '-y',
    '-i',
    silentVideo,
    '-stream_loop',
    '-1',
    '-i',
    bgmFile,
    '-filter_complex',
    `[1:a]volume=${volume},afade=t=in:st=0:d=2,afade=t=out:st=${Math.max(0, dur - 3)}:d=3[a]`,
    '-map',
    '0:v:0',
    '-map',
    '[a]',
    '-c:v',
    'copy',
    '-c:a',
    'aac',
    '-b:a',
    '192k',
    '-shortest',
    outPath,
  ]);
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

  if (!stitchOnly) {
    console.log('Recording: sign-in + ERP shell sidebar…');
    segments.push(
      await recordSegment('01-shell', async page => {
        await login(page);
        await page.goto(`${BASE}/dashboard`, { waitUntil: 'domcontentloaded' });
        await showModuleCaption(page, 'Operations dashboard');
        await page.waitForTimeout(SHOT_DEFAULT);
        await expandSidebar(page);
        await scrollSidebarNav(page);
        await page.waitForTimeout(2200);
      }),
    );

    console.log('Recording: priority — tracking, alerts, payment wallet…');
    segments.push(
      await recordSegment('02-priority', async page => {
        await login(page);
        await expandSidebar(page);
        for (const mod of modulesForSegment('priority')) {
          try {
            await visitModule(page, mod);
          } catch (err) {
            console.warn('skip', mod.path, err.message);
          }
        }
      }),
    );

    console.log('Recording: fleet & operations…');
    segments.push(
      await recordSegment('03-operations', async page => {
        await login(page);
        await expandSidebar(page);
        for (const mod of modulesForSegment('operations')) {
          try {
            await visitModule(page, mod);
          } catch (err) {
            console.warn('skip', mod.path, err.message);
          }
        }
      }),
    );

    console.log('Recording: finance, reports, comms, AI…');
    segments.push(
      await recordSegment('04-finance', async page => {
        await login(page);
        await expandSidebar(page);
        for (const mod of modulesForSegment('finance')) {
          try {
            await visitModule(page, mod);
          } catch (err) {
            console.warn('skip', mod.path, err.message);
          }
        }
      }),
    );

    console.log('Recording: Book a Demo CTA…');
    segments.push(
      await recordSegment('05-book-demo', async page => {
        await page.goto(`${BASE}/request-demo`, { waitUntil: 'networkidle' });
        const acceptCookies = page.getByRole('button', { name: 'Accept All' });
        if (await acceptCookies.isVisible().catch(() => false)) {
          await acceptCookies.click();
          await page.waitForTimeout(400);
        }
        await dismissDevOverlays(page);
        await showModuleCaption(page, 'Book a demo');
        await page.waitForTimeout(5000);
      }),
    );
  }

  const segmentSlugs = ['01-shell', '02-priority', '03-operations', '04-finance', '05-book-demo'];
  const sectionTitles = [
    ['Operations shell', 'Dashboard · Sidebar navigation'],
    ['Priority modules', 'Live tracking · Alerts · Payment wallet'],
    ['Fleet & bookings', 'Geofences · Vehicles · Maintenance'],
    ['Finance & intelligence', 'Payments · Reports · WhatsApp · AI'],
    ['Book a Demo', 'Request your fleet walkthrough'],
  ];

  const mp4Parts = [];
  mp4Parts.push(join(SEG_DIR, 'title-open.mp4'));
  await makeTitleCard(
    mp4Parts[0],
    'One workspace for live GPS, fleet ops, and driver payouts',
    'SheikhGo Nexus ERP — logged-in tenant demo',
    3.2,
  );

  for (let i = 0; i < segments.length; i++) {
    const mp4 = join(SEG_DIR, `part-${segmentSlugs[i] ?? i + 1}.mp4`);
    webmToMp4(segments[i], mp4);
    mp4Parts.push(mp4);
    if (i === 1) {
      const faqGps = join(SEG_DIR, 'faq-gps-integration.mp4');
      await makeSceneCard(faqGps, {
        variant: 'faq',
        line1: 'How does Nexus connect to existing GPS systems?',
        line2:
          'Device feeds ingest into the GPS engine—Nexus surfaces live map, history, and alerts alongside your hardware.',
        durationSec: 4,
      });
      mp4Parts.push(faqGps);
    }
    if (i === 2) {
      const caseStudy = join(SEG_DIR, 'case-study-illustrative.mp4');
      await makeSceneCard(caseStudy, {
        variant: 'case-study',
        line1: 'Not a named customer — illustrative savings model',
        line2: '−18%',
        line3: '−12 hrs/week|+24%',
        durationSec: 4,
      });
      mp4Parts.push(caseStudy);
    }
    if (i === 3) {
      const faqSec = join(SEG_DIR, 'faq-data-security.mp4');
      await makeSceneCard(faqSec, {
        variant: 'faq',
        line1: 'How secure is the data?',
        line2:
          'Tenant-scoped roles, encrypted transport, and audit-friendly workflows—aligned to your compliance review.',
        durationSec: 4,
      });
      mp4Parts.push(faqSec);
      const driverCard = join(SEG_DIR, 'driver-app-mock.mp4');
      await makeSceneCard(driverCard, {
        variant: 'driver',
        line1: 'Driver app in the field',
        line2: 'Trips, wallet balance, and notifications—same tenant as Nexus ERP.',
        durationSec: 4,
      });
      mp4Parts.push(driverCard);
    }
    const [t1, t2] = sectionTitles[i] ?? ['SheikhGo NEXUS', ''];
    const titleMp4 = join(SEG_DIR, `title-${i + 1}.mp4`);
    await makeTitleCard(titleMp4, t1, t2, 2.8);
    mp4Parts.push(titleMp4);
  }

  mp4Parts.push(join(SEG_DIR, 'title-close.mp4'));
  await makeSceneCard(join(SEG_DIR, 'title-close.mp4'), {
    variant: 'cta',
    line1: 'Book a Demo',
    line2: 'sheikhgo.com · Start your fleet workflow review',
    durationSec: 3.5,
  });

  const listFile = join(SEG_DIR, 'concat.txt');
  const listBody = mp4Parts.map(p => `file '${p.replace(/'/g, "'\\''")}'`).join('\n');
  await writeFile(listFile, listBody);

  console.log('Stitching silent MP4…');
  concatMp4(listFile, FINAL_SILENT);

  if (process.env.SKIP_BGM === '1') {
    const { copyFile } = await import('node:fs/promises');
    await copyFile(FINAL_SILENT, FINAL_MP4);
  } else {
    if (!existsSync(BGM_PATH)) {
      throw new Error(`BGM not found at ${BGM_PATH}. Set BGM= or add ${DEFAULT_BGM}`);
    }
    console.log('Mixing background music…', BGM_PATH);
    mixBackgroundMusic(FINAL_SILENT, BGM_PATH, FINAL_MP4);
  }

  const durationSec = probeDurationSec(FINAL_MP4);

  const manifest = {
    generatedAt: new Date().toISOString(),
    baseUrl: BASE,
    output: FINAL_MP4.replace(ERP_ROOT + '/', ''),
    durationSec: Math.round(durationSec * 10) / 10,
    kind: 'erp-application',
    segments: segments.map(s => s.replace(ERP_ROOT + '/', '')),
    erpRoutes: ERP_MODULES.map(m => m.path),
    backgroundMusic: BGM_SOURCE,
    scriptDoc: 'docs/demo/SheikhGo-Nexus-ERP-Walkthrough-Script.md',
    notes: [
      'Marketing home/ai-solutions pages intentionally omitted.',
      'Case study metrics are illustrative placeholders.',
      'Driver scene uses product mock unless iOS capture is wired in.',
      'On-screen module captions, FAQ cards, and Mixkit BGM.',
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
