#!/usr/bin/env node
/**
 * Runs ng build with Maps env inject/restore around it.
 * Forwards CLI args (e.g. --configuration=production) to `ng build`.
 * Used so Vercel/project overrides of `npm run build -- …` still bake GOOGLE_MAPS_*.
 */
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.join(__dirname, '..');
const inject = path.join(__dirname, 'inject-maps-env.mjs');
const ngArgs = process.argv.slice(2);

function run(cmd, args) {
  const r = spawnSync(cmd, args, { cwd: appRoot, stdio: 'inherit', shell: false });
  if (r.error) throw r.error;
  return r.status ?? 1;
}

let code = run(process.execPath, [inject]);
if (code !== 0) process.exit(code);

code = run(path.join(appRoot, 'node_modules', '.bin', 'ng'), ['build', ...ngArgs]);

const restoreCode = run(process.execPath, [inject, '--restore']);
if (code !== 0) process.exit(code);
if (restoreCode !== 0) process.exit(restoreCode);
process.exit(0);
