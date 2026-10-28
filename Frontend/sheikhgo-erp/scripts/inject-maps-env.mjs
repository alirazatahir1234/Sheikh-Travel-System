#!/usr/bin/env node
/**
 * Injects Google Maps / GA4 / Plausible values into Angular environment files.
 *
 * Priority for values:
 *   1) process.env GOOGLE_MAPS_API_KEY / GOOGLE_MAPS_MAP_ID /
 *      GOOGLE_ANALYTICS_MEASUREMENT_ID / PLAUSIBLE_DOMAIN / PLAUSIBLE_SCRIPT_SRC
 *   2) Frontend/sheikhgo-erp/.env.local or .env (gitignored)
 *   3) repo-root secrets/local.env (gitignored — house local secrets)
 *   4) src/environments/environment.secrets.local.ts (gitignored)
 *
 * Usage:
 *   node scripts/inject-maps-env.mjs              # patch environment.prod.ts (CI / build:prod)
 *   node scripts/inject-maps-env.mjs --dev        # also patch environment.ts for ng serve
 *   node scripts/inject-maps-env.mjs --restore    # clear placeholders (post-build)
 *
 * Never logs secret values — only source label and key length.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.join(__dirname, '..');
const repoRoot = path.join(appRoot, '..', '..');
const envDir = path.join(appRoot, 'src', 'environments');
const prodPath = path.join(envDir, 'environment.prod.ts');
const devPath = path.join(envDir, 'environment.ts');
const secretsLocalPath = path.join(envDir, 'environment.secrets.local.ts');
const repoSecretsEnvPath = path.join(repoRoot, 'secrets', 'local.env');

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
  if (!fs.existsSync(secretsLocalPath)) {
    return { key: '', mapId: '', gaId: '', plausibleDomain: '', plausibleScriptSrc: '' };
  }
  const text = fs.readFileSync(secretsLocalPath, 'utf8');
  const key = (text.match(/googleMapsApiKey:\s*'([^']*)'/) || [])[1] || '';
  const mapId = (text.match(/googleMapsMapId:\s*'([^']*)'/) || [])[1] || '';
  const gaId = (text.match(/googleAnalyticsMeasurementId:\s*'([^']*)'/) || [])[1] || '';
  const plausibleDomain = (text.match(/plausibleDomain:\s*'([^']*)'/) || [])[1] || '';
  const plausibleScriptSrc = (text.match(/plausibleScriptSrc:\s*'([^']*)'/) || [])[1] || '';
  return {
    key: key.trim(),
    mapId: mapId.trim(),
    gaId: gaId.trim(),
    plausibleDomain: plausibleDomain.trim(),
    plausibleScriptSrc: plausibleScriptSrc.trim(),
  };
}

function resolveValues() {
  const fileEnv = {
    ...loadDotEnvFile(path.join(appRoot, '.env')),
    ...loadDotEnvFile(path.join(appRoot, '.env.local')),
  };
  const repoSecrets = loadDotEnvFile(repoSecretsEnvPath);
  const local = readSecretsLocal();

  const key =
    (process.env.GOOGLE_MAPS_API_KEY || '').trim() ||
    (fileEnv.GOOGLE_MAPS_API_KEY || '').trim() ||
    (repoSecrets.GOOGLE_MAPS_API_KEY || repoSecrets.GOOGLE_MAPS_API_KEY_WEB || '').trim() ||
    local.key;
  const mapId =
    (process.env.GOOGLE_MAPS_MAP_ID || '').trim() ||
    (fileEnv.GOOGLE_MAPS_MAP_ID || '').trim() ||
    (repoSecrets.GOOGLE_MAPS_MAP_ID || '').trim() ||
    local.mapId;
  const gaId =
    (process.env.GOOGLE_ANALYTICS_MEASUREMENT_ID || '').trim() ||
    (fileEnv.GOOGLE_ANALYTICS_MEASUREMENT_ID || '').trim() ||
    (repoSecrets.GOOGLE_ANALYTICS_MEASUREMENT_ID || '').trim() ||
    local.gaId;
  const plausibleDomain =
    (process.env.PLAUSIBLE_DOMAIN || '').trim() ||
    (fileEnv.PLAUSIBLE_DOMAIN || '').trim() ||
    (repoSecrets.PLAUSIBLE_DOMAIN || '').trim() ||
    local.plausibleDomain;
  const plausibleScriptSrc =
    (process.env.PLAUSIBLE_SCRIPT_SRC || '').trim() ||
    (fileEnv.PLAUSIBLE_SCRIPT_SRC || '').trim() ||
    (repoSecrets.PLAUSIBLE_SCRIPT_SRC || '').trim() ||
    local.plausibleScriptSrc;

  let source = 'environment.secrets.local.ts';
  if ((process.env.GOOGLE_MAPS_API_KEY || '').trim()) source = 'env';
  else if ((fileEnv.GOOGLE_MAPS_API_KEY || '').trim()) source = '.env/.env.local';
  else if ((repoSecrets.GOOGLE_MAPS_API_KEY || repoSecrets.GOOGLE_MAPS_API_KEY_WEB || '').trim()) {
    source = 'secrets/local.env';
  }

  return { key, mapId, gaId, plausibleDomain, plausibleScriptSrc, source };
}

function replaceQuotedField(text, fieldName, value) {
  const esc = v => v.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
  const re = new RegExp(`${fieldName}:\\s*(?:'[^']*'|"[^"]*")`);
  if (re.test(text)) {
    return text.replace(re, `${fieldName}: '${esc(value)}'`);
  }
  if (value) {
    return text.replace(/\n};\s*$/, `,\n  ${fieldName}: '${esc(value)}'\n};\n`);
  }
  return text;
}

function patchFile(filePath, values, label) {
  if (!fs.existsSync(filePath)) {
    console.error(`[inject-maps-env] missing ${label}: ${filePath}`);
    process.exit(1);
  }

  let text = fs.readFileSync(filePath, 'utf8');
  if (!/googleMapsApiKey\s*:/.test(text)) {
    console.error(`[inject-maps-env] ${label} has no googleMapsApiKey field`);
    process.exit(1);
  }

  text = replaceQuotedField(text, 'googleMapsApiKey', values.key ?? '');
  text = replaceQuotedField(text, 'googleMapsMapId', values.mapId ?? '');
  text = replaceQuotedField(text, 'googleAnalyticsMeasurementId', values.gaId ?? '');
  text = replaceQuotedField(text, 'plausibleDomain', values.plausibleDomain ?? '');
  text = replaceQuotedField(text, 'plausibleScriptSrc', values.plausibleScriptSrc ?? '');

  fs.writeFileSync(filePath, text);
}

const emptyValues = { key: '', mapId: '', gaId: '', plausibleDomain: '', plausibleScriptSrc: '' };

if (restore) {
  patchFile(prodPath, emptyValues, 'environment.prod.ts');
  patchFile(devPath, emptyValues, 'environment.ts');
  console.log('[inject-maps-env] Restored empty Maps/GA/Plausible placeholders in environment*.ts');
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
    `(key from ${values.source}, keyLen=${values.key.length}, mapId=${values.mapId ? 'set' : 'omit'}, ga=${values.gaId ? 'set' : 'omit'}, plausible=${values.plausibleDomain ? 'set' : 'omit'})`
);
