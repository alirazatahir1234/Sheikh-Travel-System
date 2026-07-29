#!/usr/bin/env node
/**
 * Generate landscape PDF from slides.yaml (no browser required).
 * PPTX remains the editable primary; this PDF is presentation-ready for send-outs.
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import yaml from "js-yaml";
import PDFDocument from "pdfkit";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONTENT = path.join(__dirname, "content", "slides.yaml");
const OUTPUT = path.join(__dirname, "output");
const PDF_PATH = path.join(OUTPUT, "SheikhGo-AI-Fleet-Operations-Platform.pdf");
const LOGO = path.join(__dirname, "assets", "sheikhgo-logo.png");
const LOGO_WHITE = path.join(__dirname, "assets", "sheikhgo-logo-white.png");

const W = 842; // A4 landscape points
const H = 595;
const M = 36;

const C = {
  emerald: "#0B6B50",
  teal: "#0F766E",
  navy: "#0F172A",
  slate: "#475569",
  muted: "#64748B",
  line: "#E2E8F0",
  white: "#FFFFFF",
  soft: "#F8FAFC",
  vision: "#1E3A5F",
  amber: "#B45309",
  lightTeal: "#CCFBF1",
};

function badgeColor(badge) {
  if (badge === "Vision") return C.vision;
  if (badge === "Indicative") return C.amber;
  if (badge === "Available") return C.emerald;
  return C.teal;
}

function drawAccent(doc, badge) {
  const color = badge === "Vision" ? C.vision : C.emerald;
  doc.rect(0, 0, 8, H).fill(color);
}

function footer(doc, meta, i, total) {
  doc.fillColor("#94A3B8").fontSize(9).font("Helvetica");
  doc.text(`${meta.product || "SheikhGo"}  ·  ${meta.company || ""}`, M, H - 22, { width: 500, lineBreak: false });
  doc.text(`${i} / ${total}`, W - M - 60, H - 22, { width: 60, align: "right", lineBreak: false });
}

function drawBadge(doc, badge) {
  if (!badge) return;
  const labels = {
    Available: "Available Today",
    Vision: "Vision / Next-Gen",
    Indicative: "Indicative",
    Partial: "Partial",
  };
  const label = labels[badge] || badge;
  const color = badgeColor(badge);
  const tw = doc.widthOfString(label) + 16;
  const x = W - M - tw;
  doc.roundedRect(x, 18, tw, 18, 9).fill(color);
  doc.fillColor(C.white).fontSize(8).font("Helvetica-Bold");
  doc.text(label, x, 23, { width: tw, align: "center", lineBreak: false });
}

function title(doc, text) {
  doc.fillColor(C.navy).fontSize(22).font("Helvetica-Bold");
  doc.text(text, M + 8, 28, { width: W - M * 2 - 140 });
}

function bullets(doc, items, x, y, width, maxH = 400) {
  doc.fillColor(C.slate).fontSize(11).font("Helvetica");
  let cy = y;
  for (const item of items || []) {
    const h = doc.heightOfString(`•  ${item}`, { width: width - 8 });
    if (cy + h > y + maxH) break;
    doc.text(`•  ${item}`, x, cy, { width: width - 8 });
    cy += h + 6;
  }
  return cy;
}

function kpiRow(doc, kpis, y) {
  const list = (kpis || []).slice(0, 4);
  if (!list.length) return y;
  const gap = 10;
  const cardW = (W - M * 2 - 8 - gap * (list.length - 1)) / list.length;
  let x = M + 8;
  for (const kpi of list) {
    doc.roundedRect(x, y, cardW, 62, 6).fill(C.white).strokeColor(C.line).lineWidth(1).stroke();
    doc.rect(x, y, cardW, 4).fill(C.emerald);
    doc.fillColor(C.navy).fontSize(14).font("Helvetica-Bold");
    doc.text(String(kpi.value || ""), x + 8, y + 12, { width: cardW - 16, lineBreak: false });
    doc.fillColor(C.muted).fontSize(9).font("Helvetica");
    doc.text(String(kpi.label || ""), x + 8, y + 32, { width: cardW - 16 });
    if (kpi.hint) doc.text(String(kpi.hint), x + 8, y + 46, { width: cardW - 16 });
    x += cardW + gap;
  }
  return y + 74;
}

function card(doc, x, y, w, h, headerColor) {
  doc.roundedRect(x, y, w, h, 6).fill(C.white).strokeColor(C.line).lineWidth(1).stroke();
  doc.rect(x, y, w, 5).fill(headerColor);
}

function coverPage(doc, meta, s) {
  doc.rect(0, 0, W, H).fill(C.navy);
  doc.rect(0, 0, 8, H).fill(C.emerald);
  doc.rect(0, H - 56, W, 56).fill(C.emerald);
  if (fs.existsSync(LOGO_WHITE)) {
    try { doc.image(LOGO_WHITE, M + 8, 28, { width: 110 }); } catch { /* ignore */ }
  }
  doc.fillColor(C.white).fontSize(28).font("Helvetica-Bold");
  doc.text(s.title || meta.title, M + 8, 180, { width: W - M * 2 });
  doc.fillColor(C.lightTeal).fontSize(14).font("Helvetica");
  doc.text(s.subtitle || meta.subtitle || "", M + 8, 250, { width: W - M * 2 });
  if (s.bullets?.[0]) {
    doc.fillColor("#CBD5E1").fontSize(11);
    doc.text(s.bullets[0], M + 8, 290, { width: W - M * 2 });
  }
  doc.fillColor(C.white).fontSize(11).font("Helvetica-Bold");
  doc.text(meta.company || "", M + 8, H - 36, { lineBreak: false });
  doc.text(String(meta.year || ""), W - M - 50, H - 36, { width: 50, align: "right", lineBreak: false });
}

function closingPage(doc, meta, s, i, total) {
  doc.rect(0, 0, W, H).fill(C.navy);
  doc.rect(0, 0, 8, H).fill(C.emerald);
  if (fs.existsSync(LOGO_WHITE)) {
    try { doc.image(LOGO_WHITE, M + 8, 28, { width: 110 }); } catch { /* ignore */ }
  }
  doc.fillColor(C.white).fontSize(32).font("Helvetica-Bold");
  doc.text(s.title || "Thank You", M + 8, 180, { width: W - M * 2 });
  doc.fillColor(C.lightTeal).fontSize(14).font("Helvetica");
  doc.text(s.subtitle || "", M + 8, 235, { width: W - M * 2 });
  let y = 280;
  doc.fillColor("#CBD5E1").fontSize(12);
  for (const b of s.bullets || []) {
    doc.text(b, M + 8, y, { width: W - M * 2 });
    y += 22;
  }
  footer(doc, meta, i, total);
}

function contentPage(doc, meta, s, i, total) {
  doc.rect(0, 0, W, H).fill(C.soft);
  drawAccent(doc, s.badge);
  drawBadge(doc, s.badge);
  title(doc, s.title || "");
  let y = 70;

  if (s.kpis?.length) y = kpiRow(doc, s.kpis, y);

  if (["two_column", "comparison"].includes(s.layout)) {
    const colW = (W - M * 2 - 8 - 16) / 2;
    const leftX = M + 8;
    const rightX = leftX + colW + 16;
    card(doc, leftX, y, colW, H - y - 40, C.emerald);
    card(doc, rightX, y, colW, H - y - 40, C.teal);
    doc.fillColor(C.emerald).fontSize(12).font("Helvetica-Bold");
    doc.text(s.left_title || "", leftX + 12, y + 16, { width: colW - 24 });
    doc.fillColor(C.teal).fontSize(12).font("Helvetica-Bold");
    doc.text(s.right_title || "", rightX + 12, y + 16, { width: colW - 24 });
    bullets(doc, s.left, leftX + 12, y + 40, colW - 24, H - y - 90);
    bullets(doc, s.right, rightX + 12, y + 40, colW - 24, H - y - 90);
  } else if (s.phases?.length) {
    const n = s.phases.length;
    const gap = 12;
    const cardW = (W - M * 2 - 8 - gap * (n - 1)) / n;
    const colors = [C.emerald, C.teal, C.vision];
    s.phases.forEach((ph, idx) => {
      const x = M + 8 + idx * (cardW + gap);
      card(doc, x, y, cardW, H - y - 40, colors[idx % colors.length]);
      doc.rect(x, y, cardW, 28).fill(colors[idx % colors.length]);
      doc.fillColor(C.white).fontSize(12).font("Helvetica-Bold");
      doc.text(String(ph.name || ""), x, y + 8, { width: cardW, align: "center" });
      bullets(doc, ph.items, x + 10, y + 40, cardW - 20, H - y - 100);
    });
  } else {
    if (s.subtitle) {
      doc.fillColor(C.muted).fontSize(11).font("Helvetica-Oblique");
      doc.text(s.subtitle, M + 8, y, { width: W - M * 2 - 8 });
      y += 24;
    }
    bullets(doc, s.bullets, M + 8, y, W - M * 2 - 8, H - y - 50);
  }

  if (s.footnote) {
    doc.fillColor("#94A3B8").fontSize(8).font("Helvetica");
    doc.text(s.footnote, M + 8, H - 40, { width: W - M * 2 - 8 });
  }
  footer(doc, meta, i, total);
}

async function main() {
  fs.mkdirSync(OUTPUT, { recursive: true });
  const data = yaml.load(fs.readFileSync(CONTENT, "utf8"));
  const meta = data.meta || {};
  const slides = data.slides || [];

  const doc = new PDFDocument({
    size: [W, H],
    margin: 0,
    info: {
      Title: meta.title || "SheikhGo AI Fleet Operations Platform",
      Author: meta.company || "Sheikh Travel Group",
      Subject: meta.subtitle || "",
    },
  });
  const stream = fs.createWriteStream(PDF_PATH);
  doc.pipe(stream);

  slides.forEach((s, idx) => {
    if (idx > 0) doc.addPage({ size: [W, H], margin: 0 });
    const i = idx + 1;
    if (s.layout === "cover") coverPage(doc, meta, s);
    else if (s.layout === "closing") closingPage(doc, meta, s, i, slides.length);
    else contentPage(doc, meta, s, i, slides.length);
  });

  doc.end();
  await new Promise((resolve, reject) => {
    stream.on("finish", resolve);
    stream.on("error", reject);
  });
  console.log(`Wrote ${PDF_PATH} (${slides.length} pages)`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
