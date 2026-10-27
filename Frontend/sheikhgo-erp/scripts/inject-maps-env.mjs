#!/usr/bin/env node
/**
 * Injects Google Maps (and optional GA) values into Angular environment files.
 *
 * Priority for values:
 *   1) process.env GOOGLE_MAPS_API_KEY / GOOGLE_MAPS_MAP_ID / GOOGLE_ANALYTICS_MEASUREMENT_ID
 *   2) Frontend/sheikhgo-erp/.env.local or .env (gitignored) — names only loaded locally
 *   3) src/environments/environment.secrets.local.ts (gitignored)
 *
 * Usage:
 *   node scripts/inject-maps-env.mjs              # patch environment.prod.ts (CI / build:prod)
 *   node scripts/inject-maps-env.mjs --dev        # also patch environment.ts for ng serve
 *   node scripts/inject-maps-env.mjs --restore    # clear API key placeholders (post-build)
 *
 * Never logs secret values — only source label and key length.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.join(__dirname, '..');
const envDir = path.join(appRoot, 'src', 'environments');
const prodPath = path.join(envDir, 'environment.prod.ts');
const devPath = path.join(envDir, 'environment.ts');
const secretsLocalPath = path.join(envDir, 'environment.secrets.local.ts');

const args = new Set(process.argv.slice(2));
const restore = args.has('--restore');
const includeDev = args.has('--dev') || args.has('--all');

function loadDotEnvFile(filePath) {
  if (!fs.existsSync(filePath)) return {};
  const out = {};
  for (const raw of fs.readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq <= 0) continue;
    const name = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    out[name] = value;
  }
  return out;
}

function readSecretsLocal() {
  if (!fs.existsSync(secretsLocalPath)) return { key: '', mapId: '', gaId: '' };
  const text = fs.readFileSync(secretsLocalPath, 'utf8');
  const key = (text.match(/googleMapsApiKey:\s*'([^']*)'/) || [])[1] || '';
  const mapId = (text.match(/googleMapsMapId:\s*'([^']*)'/) || [])[1] || '';
  const gaId = (text.match(/googleAnalyticsMeasurementId:\s*'([^']*)'/) || [])[1] || '';
  return { key: key.trim(), mapId: mapId.trim(), gaId: gaId.trim() };
}

function resolveValues() {
  const fileEnv = {
    ...loadDotEnvFile(path.join(appRoot, '.env')),
    ...loadDotEnvFile(path.join(appRoot, '.env.local')),
  };
  const local = readSecretsLocal();

  const key =
    (process.env.GOOGLE_MAPS_API_KEY || '').trim() ||
    (fileEnv.GOOGLE_MAPS_API_KEY || '').trim() ||
    local.key;
  const mapId =
    (process.env.GOOGLE_MAPS_MAP_ID || '').trim() ||
    (fileEnv.GOOGLE_MAPS_MAP_ID || '').trim() ||
    local.mapId;
  const gaId =
    (process.env.GOOGLE_ANALYTICS_MEASUREMENT_ID || '').trim() ||
    (fileEnv.GOOGLE_ANALYTICS_MEASUREMENT_ID || '').trim() ||
    local.gaId;

  let source = 'environment.secrets.local.ts';
  if ((process.env.GOOGLE_MAPS_API_KEY || '').trim()) source = 'env';
  else if ((fileEnv.GOOGLE_MAPS_API_KEY || '').trim()) source = '.env/.env.local';

  return { key, mapId, gaId, source };
}

function patchFile(filePath, { key, mapId, gaId }, label) {
  if (!fs.existsSync(filePath)) {
    console.error(`[inject-maps-env] missing ${label}: ${filePath}`);
    process.exit(1);
  }

  let text = fs.readFileSync(filePath, 'utf8');
  if (!/googleMapsApiKey\s*:/.test(text)) {
    console.error(`[inject-maps-env] ${label} has no googleMapsApiKey field`);
    process.exit(1);
  }

  const esc = v => v.replace(/\\/g, '\\\\').replace(/'/g, "\\'");

  text = text.replace(
    /googleMapsApiKey:\s*(?:'[^']*'|"[^"]*")/,
    `googleMapsApiKey: '${esc(key)}'`
  );

  if (mapId !== undefined && mapId !== null && /googleMapsMapId\s*:/.test(text)) {
    text = text.replace(
      /googleMapsMapId:\s*(?:'[^']*'|"[^"]*")/,
      `googleMapsMapId: '${esc(mapId)}'`
    );
  } else if (mapId) {
    text = text.replace(
      /(googleMapsApiKey:\s*'[^']*')/,
      `$1,\n  googleMapsMapId: '${esc(mapId)}'`
    );
  }

  if (gaId && /googleAnalyticsMeasurementId\s*:/.test(text)) {
    text = text.replace(
      /googleAnalyticsMeasurementId:\s*(?:'[^']*'|"[^"]*")/,
      `googleAnalyticsMeasurementId: '${esc(gaId)}'`
    );
  } else if (gaId) {
    text = text.replace(/\n};\s*$/, `,\n  googleAnalyticsMeasurementId: '${esc(gaId)}'\n};\n`);
  }

  fs.writeFileSync(filePath, text);
}

if (restore) {
  patchFile(prodPath, { key: '', mapId: '', gaId: '' }, 'environment.prod.ts');
  if (includeDev || args.has('--dev')) {
    patchFile(devPath, { key: '', mapId: '', gaId: '' }, 'environment.ts');
  } else {
    // Always clear committed prod key after build; clear dev key if it was injected.
    patchFile(devPath, { key: '', mapId: '', gaId: '' }, 'environment.ts');
  }
  console.log('[inject-maps-env] Restored empty googleMapsApiKey placeholders in environment*.ts');
  process.exit(0);
}

const values = resolveValues();
if (!values.key) {
  console.error(
    '[inject-maps-env] GOOGLE_MAPS_API_KEY is empty and no local fallback found.\n' +
      '  Set the env var (Vercel/ADO), or copy:\n' +
      '    src/environments/environment.secrets.local.ts.example\n' +
      '      → environment.secrets.local.ts\n' +
      '    or Frontend/sheikhgo-erp/.env.example → .env.local'
  );
  process.exit(1);
}

patchFile(prodPath, values, 'environment.prod.ts');
if (includeDev) {
  patchFile(devPath, values, 'environment.ts');
}

console.log(
  `[inject-maps-env] Patched ${includeDev ? 'environment.ts + environment.prod.ts' : 'environment.prod.ts'} ` +
    `(key from ${values.source}, keyLen=${values.key.length}, mapId=${values.mapId ? 'set' : 'omit'}, ga=${values.gaId ? 'set' : 'omit'})`
);
