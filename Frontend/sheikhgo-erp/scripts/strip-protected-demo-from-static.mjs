import { existsSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';

/** Remove ERP walkthrough from static output; playback is via /website-demo/erp-walkthrough API. */
const protectedRelative = join(
  'dist',
  'sheikhgo-erp',
  'browser',
  'website',
  'demo',
  'sheikhgo-nexus-erp-walkthrough.mp4'
);

const target = join(process.cwd(), protectedRelative);
if (existsSync(target)) {
  unlinkSync(target);
  console.log(`strip-protected-demo: removed ${protectedRelative}`);
} else {
  console.log(`strip-protected-demo: nothing to remove (${protectedRelative})`);
}
