#!/usr/bin/env node
/**
 * Ensures gitignored environment.secrets.local.ts exists (copy from example).
 * NEVER overwrites an existing secrets file (preserves local keys).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const envDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'src', 'environments');
const target = path.join(envDir, 'environment.secrets.local.ts');
const example = path.join(envDir, 'environment.secrets.local.ts.example');

if (fs.existsSync(target)) {
  process.exit(0);
}
if (!fs.existsSync(example)) {
  console.error('[ensure-maps-secrets] missing example:', example);
  process.exit(1);
}
fs.copyFileSync(example, target);
console.log(
  '[ensure-maps-secrets] Created environment.secrets.local.ts from example (fill googleMapsApiKey).'
);
