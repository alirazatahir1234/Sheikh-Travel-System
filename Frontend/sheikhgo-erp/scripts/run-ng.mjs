#!/usr/bin/env node
/**
 * npm run ng — ensure local Maps secrets, inject into env files, then forward to Angular CLI.
 * Bare `ng s` still works via environment.ts merging environment.secrets.local.ts.
 */
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const node = process.execPath;
const run = (script, args = []) => {
  const r = spawnSync(node, [path.join(root, 'scripts', script), ...args], {
    cwd: root,
    stdio: 'inherit',
    env: process.env,
  });
  if (r.status) process.exit(r.status ?? 1);
};

run('ensure-maps-secrets.mjs');
run('inject-maps-env.mjs', ['--dev']);

const require = createRequire(path.join(root, 'package.json'));
const ngCli = require.resolve('@angular/cli/bin/ng.js');
const result = spawnSync(node, [ngCli, ...process.argv.slice(2)], {
  cwd: root,
  stdio: 'inherit',
  env: process.env,
});
process.exit(result.status ?? 1);
