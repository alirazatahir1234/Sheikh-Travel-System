import { environment } from '../../../../environments/environment';

/** Static path — local dev + demo recording output (not shipped as public static in prod). */
export const NEXUS_ERP_WALKTHROUGH_DEMO_STATIC =
  '/website/demo/sheikhgo-nexus-erp-walkthrough.mp4';

/** Production stream (Vercel serverless); legacy MP4 URL rewrites here too. */
export const NEXUS_ERP_WALKTHROUGH_DEMO_STREAM = '/website-demo/erp-walkthrough';

/** Accounts that may use authenticated download in the demo modal (server also enforces). */
export const DEMO_VIDEO_DOWNLOAD_ALLOWLIST = ['info@sheikhgo.com'] as const;

/** Primary Watch Demo video — logged-in ERP application tour. */
export const NEXUS_ERP_WALKTHROUGH_DEMO_SRC = environment.production
  ? NEXUS_ERP_WALKTHROUGH_DEMO_STREAM
  : NEXUS_ERP_WALKTHROUGH_DEMO_STATIC;

export const NEXUS_ERP_WALKTHROUGH_DEMO_TITLE = 'SheikhGo Nexus ERP walkthrough';

/** Secondary marketing-site tour (home + product pages). */
export const NEXUS_WEBSITE_WALKTHROUGH_DEMO_SRC =
  '/website/demo/sheikhgo-nexus-walkthrough.mp4';

/** Public 120s fleet promo (static CDN asset; not the protected ERP stream). */
export const NEXUS_PROMO_120S_SRC = '/website/demo/sheikhgo-nexus-promo-120s.mp4';

/** English captions for the 120s promo (WebVTT; SRT also shipped beside the MP4). */
export const NEXUS_PROMO_120S_CAPTIONS_VTT = '/website/demo/sheikhgo-nexus-promo-120s.vtt';

export const NEXUS_PROMO_120S_CAPTIONS_SRT = '/website/demo/sheikhgo-nexus-promo-120s.srt';

export const NEXUS_PROMO_120S_TITLE = 'SheikhGo Nexus — Smarter Fleet Management';

export type NexusDemoModalMode = 'erp' | 'promo';

/** @deprecated Use {@link NEXUS_ERP_WALKTHROUGH_DEMO_SRC} for Watch Demo. */
export const NEXUS_WALKTHROUGH_DEMO_SRC = NEXUS_ERP_WALKTHROUGH_DEMO_SRC;

export const NEXUS_WALKTHROUGH_DEMO_TITLE = NEXUS_ERP_WALKTHROUGH_DEMO_TITLE;
