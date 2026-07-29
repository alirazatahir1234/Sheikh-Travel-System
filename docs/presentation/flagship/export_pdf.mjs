#!/usr/bin/env node
/** Export flagship HTML companion to PDF via Playwright. */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUTPUT = path.join(__dirname, "output");
const htmlPath = path.join(OUTPUT, "SheikhGo-AI-Fleet-Operations-Platform.html");
const pdfPath = path.join(OUTPUT, "SheikhGo-AI-Fleet-Operations-Platform.pdf");

async function main() {
  if (!fs.existsSync(htmlPath)) {
    console.error("HTML not found. Run: npm run build");
    process.exit(1);
  }
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
  console.log(`Wrote ${pdfPath}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
