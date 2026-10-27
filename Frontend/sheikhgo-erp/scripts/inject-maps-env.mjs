#!/usr/bin/env node
/**
 * Patches environment.prod.ts googleMapsApiKey (+ optional googleMapsMapId, googleAnalyticsMeasurementId)
 * before `ng build --configuration=production`.
 *
 * Priority:
 *   1) GOOGLE_MAPS_API_KEY / GOOGLE_MAPS_MAP_ID / GOOGLE_ANALYTICS_MEASUREMENT_ID env (Vercel / ADO)
 *   2) src/environments/environment.secrets.local.ts (gitignored, local/prod builds)
 *
 * Never commits keys — only mutates the working copy for this build.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const envDir = path.join(__dirname, '..', 'src', 'environments');
const prodPath = path.join(envDir, 'environment.prod.ts');
const secretsLocalPath = path.join(envDir, 'environment.secrets.local.ts');

function readSecretsLocal() {
  if (!fs.existsSync(secretsLocalPath)) return { key: '', mapId: '', gaId: '' };
  const text = fs.readFileSync(secretsLocalPath, 'utf8');
  const key = (text.match(/googleMapsApiKey:\s*'([^']*)'/) || [])[1] || '';
  const mapId = (text.match(/googleMapsMapId:\s*'([^']*)'/) || [])[1] || '';
  const gaId = (text.match(/googleAnalyticsMeasurementId:\s*'([^']*)'/) || [])[1] || '';
  return { key: key.trim(), mapId: mapId.trim(), gaId: gaId.trim() };
}

const local = readSecretsLocal();
const key = (process.env.GOOGLE_MAPS_API_KEY || '').trim() || local.key;
const mapId = (process.env.GOOGLE_MAPS_MAP_ID || '').trim() || local.mapId;
const gaId = (process.env.GOOGLE_ANALYTICS_MEASUREMENT_ID || '').trim() || local.gaId;

if (!key) {
  console.error(
    '[inject-maps-env] GOOGLE_MAPS_API_KEY is empty and no environment.secrets.local.ts fallback.\n' +
      '  Set the env var in Vercel/ADO, or create environment.secrets.local.ts from the example.'
  );
  process.exit(1);
}

let prod = fs.readFileSync(prodPath, 'utf8');

if (!/googleMapsApiKey\s*:/.test(prod)) {
  console.error('[inject-maps-env] environment.prod.ts has no googleMapsApiKey field');
  process.exit(1);
}

prod = prod.replace(
  /googleMapsApiKey:\s*(?:'[^']*'|"[^"]*")/,
  `googleMapsApiKey: '${key.replace(/'/g, "\\'")}'`
);

if (mapId) {
  if (/googleMapsMapId\s*:/.test(prod)) {
    prod = prod.replace(
      /googleMapsMapId:\s*(?:'[^']*'|"[^"]*")/,
      `googleMapsMapId: '${mapId.replace(/'/g, "\\'")}'`
    );
  } else {
    prod = prod.replace(
      /(googleMapsApiKey:\s*'[^']*')/,
      `$1,\n  googleMapsMapId: '${mapId.replace(/'/g, "\\'")}'`
    );
  }
}

if (gaId) {
  if (/googleAnalyticsMeasurementId\s*:/.test(prod)) {
    prod = prod.replace(
      /googleAnalyticsMeasurementId:\s*(?:'[^']*'|"[^"]*")/,
      `googleAnalyticsMeasurementId: '${gaId.replace(/'/g, "\\'")}'`
    );
  } else {
    prod = prod.replace(/\n};\s*$/, `,\n  googleAnalyticsMeasurementId: '${gaId.replace(/'/g, "\\'")}'\n};\n`);
  }
}

fs.writeFileSync(prodPath, prod);
const source = process.env.GOOGLE_MAPS_API_KEY?.trim()
  ? 'env'
  : 'environment.secrets.local.ts';
console.log(
  `[inject-maps-env] Patched environment.prod.ts (key from ${source}, mapId=${mapId ? 'set' : 'omit'}, ga=${gaId ? 'set' : 'omit'})`
);
