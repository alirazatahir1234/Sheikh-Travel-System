#!/usr/bin/env node
/**
 * Build SheikhGo flagship executive PPTX (+ printable HTML) from content/slides.yaml
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import yaml from "js-yaml";
import PptxGenJS from "pptxgenjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = __dirname;
const CONTENT = path.join(ROOT, "content", "slides.yaml");
const ASSETS = path.join(ROOT, "assets");
const OUTPUT = path.join(ROOT, "output");
const PPTX_NAME = "SheikhGo-AI-Fleet-Operations-Platform.pptx";

const C = {
  emerald: "0B6B50",
  teal: "0F766E",
  navy: "0F172A",
  slate: "475569",
  muted: "64748B",
  line: "E2E8F0",
  white: "FFFFFF",
  soft: "F8FAFC",
  vision: "1E3A5F",
  amber: "B45309",
  lightTeal: "CCFBF1",
  lightSlate: "CBD5E1",
};

const badgeMeta = {
  Available: { label: "Available Today", color: C.emerald },
  Vision: { label: "Vision / Next-Gen", color: C.vision },
  Indicative: { label: "Indicative", color: C.amber },
  Partial: { label: "Partial", color: C.teal },
};

function ensureOutput() {
  fs.mkdirSync(OUTPUT, { recursive: true });
}

function logoPath(white = false) {
  const p = path.join(ASSETS, white ? "sheikhgo-logo-white.png" : "sheikhgo-logo.png");
  return fs.existsSync(p) ? p : null;
}

function addChrome(slide, meta, s, index, total, { dark = false } = {}) {
  const accent = s.badge === "Vision" ? C.vision : C.emerald;
  slide.addShape(pptx.ShapeType.rect, {
    x: 0, y: 0, w: 0.12, h: 7.5,
    fill: { color: accent }, line: { color: accent },
  });
  const foot = dark ? "94A3B8" : "94A3B8";
  slide.addText(`${meta.product || "SheikhGo"}  ·  ${meta.company || ""}`, {
    x: 0.45, y: 7.1, w: 9, h: 0.28,
    fontSize: 10, color: foot, fontFace: "Calibri",
  });
  slide.addText(`${index} / ${total}`, {
    x: 11.2, y: 7.1, w: 1.7, h: 0.28,
    fontSize: 10, color: foot, fontFace: "Calibri", align: "right",
  });
}

function addBadge(slide, badge) {
  if (!badge || !badgeMeta[badge]) return;
  const b = badgeMeta[badge];
  slide.addShape(pptx.ShapeType.roundRect, {
    x: 10.4, y: 0.28, w: 2.5, h: 0.36,
    fill: { color: b.color }, line: { color: b.color }, rectRadius: 0.1,
  });
  slide.addText(b.label, {
    x: 10.4, y: 0.28, w: 2.5, h: 0.36,
    fontSize: 11, bold: true, color: C.white, fontFace: "Calibri",
    align: "center", valign: "middle",
  });
}

function addTitle(slide, title) {
  slide.addText(title, {
    x: 0.5, y: 0.32, w: 9.5, h: 0.55,
    fontSize: 26, bold: true, color: C.navy, fontFace: "Calibri",
  });
}

function bullets(items, opts = {}) {
  return (items || []).map((t, i) => ({
    text: t,
    options: {
      bullet: true,
      breakLine: i < items.length - 1,
      fontSize: opts.fontSize || 15,
      color: opts.color || C.slate,
      fontFace: "Calibri",
      paraSpaceAfter: 8,
    },
  }));
}

function addKpiRow(slide, kpis, y = 1.15) {
  const list = (kpis || []).slice(0, 4);
  if (!list.length) return y;
  const n = list.length;
  const gap = 0.25;
  const totalW = 12.3;
  const cardW = (totalW - gap * (n - 1)) / n;
  let x = 0.5;
  for (const kpi of list) {
    slide.addShape(pptx.ShapeType.roundRect, {
      x, y, w: cardW, h: 1.2,
      fill: { color: C.white }, line: { color: C.line }, rectRadius: 0.08,
    });
    slide.addShape(pptx.ShapeType.rect, {
      x, y, w: cardW, h: 0.08,
      fill: { color: C.emerald }, line: { color: C.emerald },
    });
    slide.addText(String(kpi.value || ""), {
      x: x + 0.15, y: y + 0.22, w: cardW - 0.3, h: 0.4,
      fontSize: 18, bold: true, color: C.navy, fontFace: "Calibri",
    });
    slide.addText(String(kpi.label || ""), {
      x: x + 0.15, y: y + 0.65, w: cardW - 0.3, h: 0.25,
      fontSize: 11, color: C.muted, fontFace: "Calibri",
    });
    if (kpi.hint) {
      slide.addText(String(kpi.hint), {
        x: x + 0.15, y: y + 0.9, w: cardW - 0.3, h: 0.22,
        fontSize: 9, color: C.muted, fontFace: "Calibri",
      });
    }
    x += cardW + gap;
  }
  return y + 1.45;
}

function nodeBox(slide, x, y, w, h, text, color) {
  slide.addShape(pptx.ShapeType.roundRect, {
    x, y, w, h,
    fill: { color }, line: { color }, rectRadius: 0.08,
  });
  slide.addText(text, {
    x, y, w, h,
    fontSize: 12, bold: true, color: C.white, fontFace: "Calibri",
    align: "center", valign: "middle",
  });
}

/** @type {import('pptxgenjs').default} */
let pptx;

function layoutCover(meta, s) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.navy }, line: { color: C.navy } });
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 0.18, h: 7.5, fill: { color: C.emerald }, line: { color: C.emerald } });
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 6.6, w: 13.333, h: 0.9, fill: { color: C.emerald }, line: { color: C.emerald } });
  const logo = logoPath(true);
  if (logo) slide.addImage({ path: logo, x: 0.7, y: 0.55, w: 2.0, h: 0.7 });
  slide.addText(s.title, {
    x: 0.7, y: 2.3, w: 11.5, h: 1.1,
    fontSize: 34, bold: true, color: C.white, fontFace: "Calibri",
  });
  slide.addText(s.subtitle || meta.subtitle || "", {
    x: 0.7, y: 3.5, w: 11, h: 0.45,
    fontSize: 18, color: C.lightTeal, fontFace: "Calibri",
  });
  if (s.bullets?.[0]) {
    slide.addText(s.bullets[0], {
      x: 0.7, y: 4.2, w: 11, h: 0.4,
      fontSize: 14, color: C.lightSlate, fontFace: "Calibri",
    });
  }
  slide.addText(meta.company || "", {
    x: 0.7, y: 6.8, w: 8, h: 0.35,
    fontSize: 14, bold: true, color: C.white, fontFace: "Calibri",
  });
  slide.addText(String(meta.year || ""), {
    x: 10, y: 6.8, w: 2.8, h: 0.35,
    fontSize: 14, color: C.white, fontFace: "Calibri", align: "right",
  });
  if (s.notes) slide.addNotes(s.notes);
}

function layoutClosing(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.navy }, line: { color: C.navy } });
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 0.18, h: 7.5, fill: { color: C.emerald }, line: { color: C.emerald } });
  const logo = logoPath(true);
  if (logo) slide.addImage({ path: logo, x: 0.7, y: 0.55, w: 2.0, h: 0.7 });
  slide.addText(s.title, {
    x: 0.7, y: 2.4, w: 11.5, h: 0.8,
    fontSize: 40, bold: true, color: C.white, fontFace: "Calibri",
  });
  slide.addText(s.subtitle || "", {
    x: 0.7, y: 3.3, w: 11, h: 0.4,
    fontSize: 18, color: C.lightTeal, fontFace: "Calibri",
  });
  let y = 4.1;
  for (const b of s.bullets || []) {
    slide.addText(b, {
      x: 0.7, y, w: 11, h: 0.35,
      fontSize: 15, color: C.lightSlate, fontFace: "Calibri",
    });
    y += 0.4;
  }
  addChrome(slide, meta, s, index, total, { dark: true });
  if (s.notes) slide.addNotes(s.notes);
}

function layoutBullets(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.soft }, line: { color: C.soft } });
  addChrome(slide, meta, s, index, total);
  addBadge(slide, s.badge);
  addTitle(slide, s.title);
  let y = 1.15;
  if (s.kpis?.length) y = addKpiRow(slide, s.kpis, 1.1);
  slide.addText(bullets(s.bullets, { fontSize: 16 }), {
    x: 0.55, y, w: 12.2, h: 4.5,
  });
  if (s.footnote) {
    slide.addText(s.footnote, {
      x: 0.55, y: 6.7, w: 12, h: 0.28,
      fontSize: 9, color: C.muted, fontFace: "Calibri",
    });
  }
  if (s.notes) slide.addNotes(s.notes);
}

function layoutTwoColumn(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.soft }, line: { color: C.soft } });
  addChrome(slide, meta, s, index, total);
  addBadge(slide, s.badge);
  addTitle(slide, s.title);

  slide.addShape(pptx.ShapeType.roundRect, {
    x: 0.5, y: 1.2, w: 5.9, h: 5.4,
    fill: { color: C.white }, line: { color: C.line }, rectRadius: 0.08,
  });
  slide.addShape(pptx.ShapeType.roundRect, {
    x: 6.85, y: 1.2, w: 5.9, h: 5.4,
    fill: { color: C.white }, line: { color: C.line }, rectRadius: 0.08,
  });
  slide.addShape(pptx.ShapeType.rect, { x: 0.5, y: 1.2, w: 5.9, h: 0.12, fill: { color: C.emerald }, line: { color: C.emerald } });
  slide.addShape(pptx.ShapeType.rect, { x: 6.85, y: 1.2, w: 5.9, h: 0.12, fill: { color: C.teal }, line: { color: C.teal } });

  slide.addText(s.left_title || "Left", {
    x: 0.75, y: 1.5, w: 5.4, h: 0.35,
    fontSize: 15, bold: true, color: C.emerald, fontFace: "Calibri",
  });
  slide.addText(s.right_title || "Right", {
    x: 7.1, y: 1.5, w: 5.4, h: 0.35,
    fontSize: 15, bold: true, color: C.teal, fontFace: "Calibri",
  });
  slide.addText(bullets(s.left, { fontSize: 13 }), { x: 0.75, y: 2.0, w: 5.4, h: 4.2 });
  slide.addText(bullets(s.right, { fontSize: 13 }), { x: 7.1, y: 2.0, w: 5.4, h: 4.2 });
  if (s.footnote) {
    slide.addText(s.footnote, {
      x: 0.55, y: 6.7, w: 12, h: 0.28,
      fontSize: 9, color: C.muted, fontFace: "Calibri",
    });
  }
  if (s.notes) slide.addNotes(s.notes);
}

function layoutKpiCards(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.soft }, line: { color: C.soft } });
  addChrome(slide, meta, s, index, total);
  addBadge(slide, s.badge);
  addTitle(slide, s.title);
  addKpiRow(slide, s.kpis, 1.2);
  if (s.bullets?.length) {
    slide.addText(bullets(s.bullets, { fontSize: 15 }), { x: 0.55, y: 2.8, w: 12.2, h: 3.5 });
  }
  if (s.footnote) {
    slide.addText(s.footnote, {
      x: 0.55, y: 6.7, w: 12, h: 0.28,
      fontSize: 9, color: C.muted, fontFace: "Calibri",
    });
  }
  if (s.notes) slide.addNotes(s.notes);
}

function layoutRoadmap(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.soft }, line: { color: C.soft } });
  addChrome(slide, meta, s, index, total);
  addBadge(slide, s.badge);
  addTitle(slide, s.title);
  const phases = s.phases || [];
  const n = phases.length || 1;
  const gap = 0.3;
  const cardW = (12.3 - gap * (n - 1)) / n;
  const colors = [C.emerald, C.teal, C.vision];
  let x = 0.5;
  phases.forEach((phase, i) => {
    const color = colors[i % colors.length];
    slide.addShape(pptx.ShapeType.roundRect, {
      x, y: 1.3, w: cardW, h: 5.3,
      fill: { color: C.white }, line: { color: C.line }, rectRadius: 0.08,
    });
    slide.addShape(pptx.ShapeType.rect, {
      x, y: 1.3, w: cardW, h: 0.7,
      fill: { color }, line: { color },
    });
    slide.addText(String(phase.name || ""), {
      x, y: 1.3, w: cardW, h: 0.7,
      fontSize: 18, bold: true, color: C.white, fontFace: "Calibri",
      align: "center", valign: "middle",
    });
    slide.addText(bullets(phase.items || [], { fontSize: 13 }), {
      x: x + 0.25, y: 2.25, w: cardW - 0.4, h: 4.0,
    });
    x += cardW + gap;
  });
  if (s.notes) slide.addNotes(s.notes);
}

const diagrams = {
  platform_overview(slide) {
    nodeBox(slide, 0.6, 1.4, 3.5, 1.1, "ERP Web\nAngular Command Center", C.emerald);
    nodeBox(slide, 4.9, 1.4, 3.5, 1.1, "HTTPS API\nASP.NET Core · MediatR", C.teal);
    nodeBox(slide, 9.2, 1.4, 3.5, 1.1, "Mobile Apps\nFlutter Driver · Fleet", C.vision);
    nodeBox(slide, 1.2, 3.3, 2.4, 0.9, "SQL Server", C.navy);
    nodeBox(slide, 3.9, 3.3, 2.4, 0.9, "Redis*", C.slate);
    nodeBox(slide, 6.6, 3.3, 2.4, 0.9, "Traccar GPS", C.emerald);
    nodeBox(slide, 9.3, 3.3, 2.4, 0.9, "SignalR / SMTP", C.teal);
    slide.addText("* Redis optional — fail-open cache", {
      x: 0.6, y: 4.4, w: 12, h: 0.3, fontSize: 10, color: C.muted, fontFace: "Calibri",
    });
  },
  system_architecture(slide) { diagrams.platform_overview(slide); },
  clean_architecture(slide) {
    const layers = [
      ["Presentation — ERP / Mobile / API Controllers", C.emerald],
      ["Application — MediatR Handlers · Use Cases", C.teal],
      ["Domain — Fleet · GPS · Notifications · Tenancy", C.vision],
      ["Infrastructure — SQL · Dapper · Traccar · Redis · SMTP", C.navy],
    ];
    let y = 1.4;
    for (const [label, color] of layers) {
      nodeBox(slide, 1.5, y, 10.3, 0.85, label, color);
      y += 1.0;
    }
  },
  azure(slide) {
    nodeBox(slide, 0.7, 1.5, 3.8, 3.5, "Azure Edge\n\nApp Service / Containers\nAPI Gateway\nKey Vault", C.emerald);
    nodeBox(slide, 4.8, 1.5, 3.8, 3.5, "Data Plane\n\nAzure SQL\nRedis Cache\nBlob Storage", C.teal);
    nodeBox(slide, 8.9, 1.5, 3.8, 3.5, "Integrations\n\nTraccar\nSMTP / Push\nMonitoring", C.vision);
  },
  multi_tenant(slide) {
    nodeBox(slide, 4.5, 1.35, 4.3, 0.8, "Platform Admin", C.navy);
    ["Tenant A", "Tenant B", "Tenant C"].forEach((name, i) => {
      const colors = [C.emerald, C.teal, C.vision];
      nodeBox(slide, 0.8 + i * 4.1, 2.5, 3.7, 2.5, `${name}\n\nUsers · Roles · Fleet\nGPS · Notifications\nTenantId isolation`, colors[i]);
    });
  },
  notification_arch(slide) {
    const steps = ["Events", "Compose", "Preferences", "Dispatch", "Channels", "Inbox"];
    steps.forEach((step, i) => {
      nodeBox(slide, 0.5 + i * 2.1, 2.2, 1.8, 1.2, step, i % 2 === 0 ? C.emerald : C.teal);
      if (i < steps.length - 1) {
        slide.addText("→", {
          x: 2.2 + i * 2.1, y: 2.55, w: 0.35, h: 0.4,
          fontSize: 18, bold: true, color: C.navy, fontFace: "Calibri", align: "center",
        });
      }
    });
    nodeBox(slide, 2.5, 3.9, 8.3, 1.0, "SignalR realtime  ·  Email SMTP  ·  Retention / Archive jobs", C.vision);
  },
  mobile_arch(slide) {
    nodeBox(slide, 0.7, 1.5, 3.8, 3.5, "Flutter Apps\n\nDriver · Fleet\nRBAC shells\nOffline outbox", C.emerald);
    nodeBox(slide, 4.8, 1.5, 3.8, 3.5, "Shared API\n\nJWT refresh\nPermissions\nSignalR / FCM", C.teal);
    nodeBox(slide, 8.9, 1.5, 3.8, 3.5, "Device Layer\n\nGPS · Biometrics\nHive · EN/AR\nStore builds", C.vision);
  },
  ai_notification_flow(slide) {
    const steps = [
      ["Ingest", "GPS · Maint · Trips"],
      ["Score", "Severity · Context"],
      ["Filter", "Suppress · Dedupe"],
      ["Route", "Role · Channel"],
      ["Act", "Escalate · Inbox"],
    ];
    steps.forEach(([title, sub], i) => {
      const color = i === 4 ? C.vision : i % 2 === 0 ? C.emerald : C.teal;
      nodeBox(slide, 0.45 + i * 2.55, 1.8, 2.3, 2.2, `${title}\n\n${sub}`, color);
    });
  },
  ai_decision_engine(slide) {
    nodeBox(slide, 0.6, 1.6, 3.5, 3.2, "Signals\n\nFleet · Driver\nGPS · Maint\nNotifications", C.emerald);
    nodeBox(slide, 4.9, 1.6, 3.5, 3.2, "Decision Engine\n\nCorrelate\nRecommend\nExplain", C.vision);
    nodeBox(slide, 9.2, 1.6, 3.5, 3.2, "Actions\n\nDispatch\nWO · Coach\nHuman approve", C.teal);
  },
  predictive_maintenance(slide) {
    ["History", "Telemetry", "Risk Model", "Work Order", "Parts / Shop"].forEach((step, i) => {
      const color = i < 2 ? C.emerald : i === 2 ? C.vision : C.teal;
      nodeBox(slide, 0.5 + i * 2.5, 2.0, 2.2, 1.5, step, color);
    });
  },
  fleet_health(slide) {
    nodeBox(slide, 4.7, 1.5, 3.8, 1.6, "Fleet Health\n82 / 100", C.emerald);
    [["Compliance", C.emerald], ["Maintenance", C.teal], ["GPS Health", C.vision], ["Utilization", C.navy]].forEach(([name, color], i) => {
      nodeBox(slide, 0.8 + i * 3.1, 3.5, 2.8, 1.2, name, color);
    });
  },
  ai_learning(slide) {
    nodeBox(slide, 0.7, 2.0, 3.5, 2.2, "Recommendations", C.emerald);
    nodeBox(slide, 4.9, 2.0, 3.5, 2.2, "Accept / Reject\nFeedback", C.teal);
    nodeBox(slide, 9.2, 2.0, 3.5, 2.2, "Model Improve\nPer Tenant", C.vision);
  },
  ai_ecosystem(slide) {
    nodeBox(slide, 4.5, 1.4, 4.3, 0.9, "AI Control Plane", C.vision);
    nodeBox(slide, 0.6, 2.7, 3.8, 2.0, "Event Fabric\nGPS · Ops · Mobile", C.emerald);
    nodeBox(slide, 4.75, 2.7, 3.8, 2.0, "Intelligence\nDecision · Notify · Learn", C.teal);
    nodeBox(slide, 8.9, 2.7, 3.8, 2.0, "Experiences\nCopilot · Dashboards", C.navy);
  },
};

function layoutDiagram(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.soft }, line: { color: C.soft } });
  addChrome(slide, meta, s, index, total);
  addBadge(slide, s.badge);
  addTitle(slide, s.title);
  const fn = diagrams[s.diagram] || diagrams.platform_overview;
  fn(slide);
  if (s.bullets?.length) {
    slide.addText(bullets(s.bullets, { fontSize: 12 }), { x: 0.5, y: 5.55, w: 12.3, h: 1.3 });
  }
  if (s.notes) slide.addNotes(s.notes);
}

function layoutMockup(meta, s, index, total) {
  const slide = pptx.addSlide();
  slide.addShape(pptx.ShapeType.rect, { x: 0, y: 0, w: 13.333, h: 7.5, fill: { color: C.soft }, line: { color: C.soft } });
  addChrome(slide, meta, s, index, total);
  addBadge(slide, s.badge);
  addTitle(slide, s.title);

  slide.addShape(pptx.ShapeType.roundRect, {
    x: 0.5, y: 1.15, w: 7.4, h: 5.4,
    fill: { color: C.white }, line: { color: C.line }, rectRadius: 0.08,
  });
  slide.addShape(pptx.ShapeType.rect, {
    x: 0.5, y: 1.15, w: 7.4, h: 0.55,
    fill: { color: C.navy }, line: { color: C.navy },
  });
  const mockLabel = String(s.mockup || "dashboard").replace(/_/g, " ");
  slide.addText(`SheikhGo · ${mockLabel.replace(/\b\w/g, (c) => c.toUpperCase())}`, {
    x: 0.7, y: 1.25, w: 6.5, h: 0.35,
    fontSize: 12, bold: true, color: C.white, fontFace: "Calibri",
  });

  const kind = s.mockup || "kpi";
  if (kind === "notifications") {
    const rows = [
      ["Critical", "Speeding · Vehicle 42", C.emerald],
      ["Warning", "Geofence exit · Depot B", C.amber],
      ["Info", "WO #118 scheduled", C.teal],
      ["System", "Device offline recovered", C.slate],
    ];
    let y = 1.95;
    for (const [title, body, color] of rows) {
      slide.addShape(pptx.ShapeType.roundRect, {
        x: 0.75, y, w: 6.9, h: 0.85,
        fill: { color: C.soft }, line: { color: C.line }, rectRadius: 0.06,
      });
      slide.addShape(pptx.ShapeType.rect, {
        x: 0.75, y, w: 0.12, h: 0.85,
        fill: { color }, line: { color },
      });
      slide.addText(title, { x: 1.1, y: y + 0.12, w: 6, h: 0.28, fontSize: 12, bold: true, color: C.navy, fontFace: "Calibri" });
      slide.addText(body, { x: 1.1, y: y + 0.42, w: 6, h: 0.28, fontSize: 11, color: C.muted, fontFace: "Calibri" });
      y += 1.0;
    }
  } else if (kind === "copilot") {
    slide.addShape(pptx.ShapeType.roundRect, {
      x: 0.85, y: 2.0, w: 6.7, h: 1.1,
      fill: { color: C.soft }, line: { color: C.line }, rectRadius: 0.06,
    });
    slide.addText("Which vehicles need maintenance this week?", {
      x: 1.05, y: 2.25, w: 6.3, h: 0.6, fontSize: 13, color: C.slate, fontFace: "Calibri",
    });
    slide.addShape(pptx.ShapeType.roundRect, {
      x: 0.85, y: 3.4, w: 6.7, h: 2.2,
      fill: { color: C.lightTeal }, line: { color: C.line }, rectRadius: 0.06,
    });
    slide.addText("Copilot: 7 vehicles show elevated risk — 3 overdue service, 2 high idle + fault history, 2 license/compliance flags. Open Work Orders?", {
      x: 1.05, y: 3.6, w: 6.3, h: 1.8, fontSize: 13, color: C.navy, fontFace: "Calibri",
    });
  } else if (kind === "ai_center") {
    const tiles = [["Providers", "Configured"], ["Capabilities", "8 toggles"], ["Guardrails", "On"], ["Acceptance", "64%"]];
    const positions = [[0.75, 1.95], [4.2, 1.95], [0.75, 3.7], [4.2, 3.7]];
    tiles.forEach(([t, v], i) => {
      const [x, y] = positions[i];
      slide.addShape(pptx.ShapeType.roundRect, {
        x, y, w: 3.2, h: 1.4,
        fill: { color: C.soft }, line: { color: C.line }, rectRadius: 0.06,
      });
      slide.addText(t, { x: x + 0.2, y: y + 0.3, w: 2.8, h: 0.3, fontSize: 12, color: C.muted, fontFace: "Calibri" });
      slide.addText(v, { x: x + 0.2, y: y + 0.7, w: 2.8, h: 0.4, fontSize: 18, bold: true, color: C.navy, fontFace: "Calibri" });
    });
  } else {
    const metrics = [["Active", "128"], ["Trips", "64"], ["Alerts", "12"], ["WO Open", "9"]];
    metrics.forEach(([label, val], i) => {
      const x = 0.75 + i * 1.75;
      slide.addShape(pptx.ShapeType.roundRect, {
        x, y: 1.95, w: 1.6, h: 1.1,
        fill: { color: C.soft }, line: { color: C.line }, rectRadius: 0.06,
      });
      slide.addText(val, { x, y: 2.1, w: 1.6, h: 0.4, fontSize: 18, bold: true, color: C.emerald, fontFace: "Calibri", align: "center" });
      slide.addText(label, { x, y: 2.55, w: 1.6, h: 0.3, fontSize: 11, color: C.muted, fontFace: "Calibri", align: "center" });
    });
    slide.addShape(pptx.ShapeType.roundRect, {
      x: 0.75, y: 3.4, w: 6.9, h: 2.7,
      fill: { color: C.soft }, line: { color: C.line }, rectRadius: 0.06,
    });
    slide.addText("Stylized analytics canvas — live charts in product", {
      x: 1.0, y: 4.4, w: 6.4, h: 0.5,
      fontSize: 13, color: C.muted, fontFace: "Calibri", align: "center",
    });
  }

  slide.addShape(pptx.ShapeType.roundRect, {
    x: 8.2, y: 1.15, w: 4.6, h: 5.4,
    fill: { color: C.white }, line: { color: C.line }, rectRadius: 0.08,
  });
  slide.addShape(pptx.ShapeType.rect, {
    x: 8.2, y: 1.15, w: 4.6, h: 0.12,
    fill: { color: C.emerald }, line: { color: C.emerald },
  });
  slide.addText("Highlights", {
    x: 8.45, y: 1.45, w: 4.1, h: 0.35,
    fontSize: 14, bold: true, color: C.emerald, fontFace: "Calibri",
  });
  slide.addText(bullets(s.bullets, { fontSize: 13 }), { x: 8.45, y: 1.95, w: 4.1, h: 4.3 });
  if (s.notes) slide.addNotes(s.notes);
}

const LAYOUTS = {
  cover: layoutCover,
  closing: layoutClosing,
  bullets: layoutBullets,
  two_column: layoutTwoColumn,
  comparison: layoutTwoColumn,
  kpi_cards: layoutKpiCards,
  roadmap: layoutRoadmap,
  diagram: layoutDiagram,
  mockup: layoutMockup,
  section: layoutBullets,
};

function writeHtml(meta, slides) {
  const total = slides.length;
  const parts = [
    "<!DOCTYPE html><html><head><meta charset='utf-8'/>",
    `<title>${meta.title || "SheikhGo"}</title>`,
    "<style>",
    "@page{size:landscape;margin:12mm}",
    "body{font-family:Calibri,Segoe UI,sans-serif;background:#0f172a;margin:0}",
    ".slide{background:#f8fafc;width:1100px;min-height:620px;margin:24px auto;padding:40px 48px;page-break-after:always;position:relative;border-left:8px solid #0B6B50}",
    ".slide.vision{border-left-color:#1e3a5f}.slide.cover{background:#0f172a;color:#fff}",
    ".badge{display:inline-block;background:#0B6B50;color:#fff;padding:4px 10px;border-radius:999px;font-size:12px;font-weight:700}",
    ".badge.Vision{background:#1e3a5f}.badge.Indicative{background:#b45309}",
    "h1{font-size:28px;margin:12px 0 16px}.cover h1{color:#fff;font-size:36px}",
    "ul{line-height:1.55;color:#475569}.cover ul,.cover p{color:#cbd5e1}",
    ".kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:16px 0}",
    ".kpi{background:#fff;border:1px solid #e2e8f0;padding:14px;border-top:4px solid #0B6B50}",
    ".kpi b{display:block;font-size:20px;color:#0f172a}",
    ".cols{display:grid;grid-template-columns:1fr 1fr;gap:16px}",
    ".card{background:#fff;border:1px solid #e2e8f0;padding:16px}",
    ".phases{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}",
    ".foot{position:absolute;bottom:16px;left:48px;right:48px;display:flex;justify-content:space-between;color:#94a3b8;font-size:12px}",
    "@media print{body{background:#fff}.slide{margin:0;width:auto}}",
    "</style></head><body>",
  ];
  slides.forEach((s, idx) => {
    const i = idx + 1;
    const badge = s.badge || "";
    const cls = ["cover", "closing"].includes(s.layout) ? "slide cover" : badge === "Vision" ? "slide vision" : "slide";
    parts.push(`<div class='${cls}'>`);
    if (badge && !["cover", "closing"].includes(s.layout)) parts.push(`<span class='badge ${badge}'>${badgeMeta[badge]?.label || badge}</span>`);
    parts.push(`<h1>${s.title || ""}</h1>`);
    if (s.subtitle) parts.push(`<p><em>${s.subtitle}</em></p>`);
    if (s.kpis?.length) {
      parts.push("<div class='kpis'>");
      for (const k of s.kpis) {
        parts.push(`<div class='kpi'><b>${k.value || ""}</b>${k.label || ""}<div style='font-size:11px;color:#94a3b8'>${k.hint || ""}</div></div>`);
      }
      parts.push("</div>");
    }
    if (["two_column", "comparison"].includes(s.layout)) {
      parts.push("<div class='cols'>");
      parts.push(`<div class='card'><h3>${s.left_title || ""}</h3><ul>${(s.left || []).map((b) => `<li>${b}</li>`).join("")}</ul></div>`);
      parts.push(`<div class='card'><h3>${s.right_title || ""}</h3><ul>${(s.right || []).map((b) => `<li>${b}</li>`).join("")}</ul></div>`);
      parts.push("</div>");
    }
    if (s.phases?.length) {
      parts.push("<div class='phases'>");
      for (const ph of s.phases) {
        parts.push(`<div class='card'><h3>${ph.name || ""}</h3><ul>${(ph.items || []).map((b) => `<li>${b}</li>`).join("")}</ul></div>`);
      }
      parts.push("</div>");
    }
    if (s.bullets?.length) {
      parts.push(`<ul>${s.bullets.map((b) => `<li>${b}</li>`).join("")}</ul>`);
    }
    if (s.footnote) parts.push(`<p style='font-size:11px;color:#94a3b8'>${s.footnote}</p>`);
    parts.push(`<div class='foot'><span>${meta.product || ""} · ${meta.company || ""}</span><span>${i} / ${total}</span></div>`);
    parts.push("</div>");
  });
  parts.push("</body></html>");
  const htmlPath = path.join(OUTPUT, "SheikhGo-AI-Fleet-Operations-Platform.html");
  fs.writeFileSync(htmlPath, parts.join("\n"), "utf8");
  return htmlPath;
}

async function tryPdfFromHtml(htmlPath) {
  const pdfPath = path.join(OUTPUT, "SheikhGo-AI-Fleet-Operations-Platform.pdf");
  // Try Playwright if installed globally/local
  try {
    const { chromium } = await import("playwright");
    const browser = await chromium.launch();
    const page = await browser.newPage();
    await page.goto(`file://${htmlPath}`, { waitUntil: "networkidle" });
    await page.pdf({
      path: pdfPath,
      landscape: true,
      printBackground: true,
      format: "A4",
      margin: { top: "10mm", bottom: "10mm", left: "10mm", right: "10mm" },
    });
    await browser.close();
    return pdfPath;
  } catch {
    /* continue */
  }
  try {
    const puppeteer = await import("puppeteer");
    const browser = await puppeteer.default.launch({ headless: true });
    const page = await browser.newPage();
    await page.goto(`file://${htmlPath}`, { waitUntil: "networkidle0" });
    await page.pdf({
      path: pdfPath,
      landscape: true,
      printBackground: true,
      format: "A4",
      margin: { top: "10mm", bottom: "10mm", left: "10mm", right: "10mm" },
    });
    await browser.close();
    return pdfPath;
  } catch {
    /* continue */
  }
  // LibreOffice on pptx
  const pptxPath = path.join(OUTPUT, PPTX_NAME);
  for (const bin of ["soffice", "libreoffice"]) {
    try {
      const { spawnSync } = await import("child_process");
      const r = spawnSync(bin, ["--headless", "--convert-to", "pdf", "--outdir", OUTPUT, pptxPath], { encoding: "utf8" });
      if (r.status === 0 && fs.existsSync(pdfPath)) return pdfPath;
    } catch {
      /* continue */
    }
  }
  return null;
}

async function main() {
  ensureOutput();
  const data = yaml.load(fs.readFileSync(CONTENT, "utf8"));
  const meta = data.meta || {};
  const slides = data.slides || [];
  if (slides.length !== 60) {
    console.warn(`WARNING: expected 60 slides, found ${slides.length}`);
  }

  pptx = new PptxGenJS();
  pptx.defineLayout({ name: "WIDE", width: 13.333, height: 7.5 });
  pptx.layout = "WIDE";
  pptx.author = meta.company || "Sheikh Travel Group";
  pptx.title = meta.title || "SheikhGo AI Fleet Operations Platform";
  pptx.subject = meta.subtitle || "";

  const total = slides.length;
  slides.forEach((s, idx) => {
    const layout = s.layout || "bullets";
    const fn = LAYOUTS[layout] || layoutBullets;
    if (layout === "cover") fn(meta, s);
    else fn(meta, s, idx + 1, total);
  });

  const out = path.join(OUTPUT, PPTX_NAME);
  await pptx.writeFile({ fileName: out });
  console.log(`Wrote ${out} (${total} slides)`);

  const htmlPath = writeHtml(meta, slides);
  console.log(`Wrote ${htmlPath}`);

  const pdf = await tryPdfFromHtml(htmlPath);
  if (pdf) {
    console.log(`Wrote ${pdf}`);
  } else {
    const note = path.join(OUTPUT, "PDF-EXPORT.txt");
    fs.writeFileSync(
      note,
      [
        "PDF converter not detected (LibreOffice / Playwright / Puppeteer).",
        "",
        "Quick options:",
        "1) Open SheikhGo-AI-Fleet-Operations-Platform.html in Chrome → Print → Save as PDF (landscape)",
        "2) Open the .pptx in PowerPoint / Keynote → Export → PDF",
        "3) npm install -D playwright && npx playwright install chromium && npm run pdf",
        "4) soffice --headless --convert-to pdf --outdir output output/*.pptx",
        "",
      ].join("\n"),
      "utf8",
    );
    console.log(`PDF pending — see ${note}`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
