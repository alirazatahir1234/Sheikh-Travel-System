#!/usr/bin/env python3
"""Build MODULE-04-SMART-TRIP-ASSIGNMENT.pptx from master content."""

from pathlib import Path

from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import MSO_ANCHOR, PP_ALIGN
from pptx.oxml.ns import nsmap
from pptx.oxml.xmlchemy import OxmlElement
from pptx.util import Emu, Inches, Pt

OUT = Path(__file__).resolve().parent / "MODULE-04-SMART-TRIP-ASSIGNMENT.pptx"
ASSETS = Path(__file__).resolve().parent / "assets"
OVERVIEW_IMG = ASSETS / "module-04-ai-trip-assignment-overview.jpg"
DRIVER_PERF_IMG = ASSETS / "module-04-driver-performance-screen.jpg"

NAVY = RGBColor(0x07, 0x1D, 0x2E)
TEAL = RGBColor(0x07, 0x8B, 0x7B)
TEAL_LIGHT = RGBColor(0x0A, 0xA8, 0x96)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
OFF_WHITE = RGBColor(0xF4, 0xF8, 0xF9)
MUTED = RGBColor(0x5A, 0x6B, 0x75)
DARK = RGBColor(0x1A, 0x2A, 0x35)
CARD_BG = RGBColor(0xE8, 0xF4, 0xF3)
WARN = RGBColor(0xB8, 0x5C, 0x00)
AMBER_BG = RGBColor(0xFF, 0xF6, 0xE8)

SLIDE_W = Inches(13.333)
SLIDE_H = Inches(7.5)


def set_run(run, text, size=14, bold=False, color=DARK, font="Calibri"):
    run.text = text
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.color.rgb = color
    run.font.name = font


def add_textbox(slide, left, top, width, height, text, size=14, bold=False, color=DARK,
                align=PP_ALIGN.LEFT, font="Calibri", anchor=MSO_ANCHOR.TOP):
    box = slide.shapes.add_textbox(left, top, width, height)
    tf = box.text_frame
    tf.word_wrap = True
    tf.auto_size = None
    try:
        tf._txBody.bodyPr.set("anchor", {MSO_ANCHOR.TOP: "t", MSO_ANCHOR.MIDDLE: "ctr", MSO_ANCHOR.BOTTOM: "b"}[anchor])
    except Exception:
        pass
    p = tf.paragraphs[0]
    p.alignment = align
    set_run(p.add_run() if p.runs else _ensure_run(p), text, size=size, bold=bold, color=color, font=font)
    if not p.runs:
        set_run(p.add_run(), text, size=size, bold=bold, color=color, font=font)
    else:
        # first run may be empty from add_run path — normalize
        p.runs[0].text = text
        set_run(p.runs[0], text, size=size, bold=bold, color=color, font=font)
    return box


def _ensure_run(p):
    if p.runs:
        return p.runs[0]
    return p.add_run()


def set_paragraph(p, text, size=14, bold=False, color=DARK, align=PP_ALIGN.LEFT,
                  font="Calibri", space_after=6, space_before=0):
    p.alignment = align
    p.clear()
    p.space_after = Pt(space_after)
    p.space_before = Pt(space_before)
    run = p.add_run()
    set_run(run, text, size=size, bold=bold, color=color, font=font)
    return p


def fill_shape(shape, color):
    shape.fill.solid()
    shape.fill.fore_color.rgb = color
    shape.line.fill.background()


def add_rect(slide, left, top, width, height, color):
    shape = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, left, top, width, height)
    fill_shape(shape, color)
    return shape


def add_round_rect(slide, left, top, width, height, color):
    shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, left, top, width, height)
    fill_shape(shape, color)
    # soften corners
    try:
        shape.adjustments[0] = 0.1
    except Exception:
        pass
    return shape


def content_bg(slide):
    add_rect(slide, 0, 0, SLIDE_W, SLIDE_H, OFF_WHITE)


def dark_bg(slide):
    add_rect(slide, 0, 0, SLIDE_W, SLIDE_H, NAVY)


def slide_header(slide, title, subtitle=None):
    """Light content slide with teal header band (not a thin accent stripe — full band)."""
    content_bg(slide)
    add_rect(slide, 0, 0, SLIDE_W, Inches(1.05), NAVY)
    add_rect(slide, 0, Inches(1.05), SLIDE_W, Inches(0.06), TEAL)
    box = slide.shapes.add_textbox(Inches(0.55), Inches(0.22), Inches(12.2), Inches(0.55))
    tf = box.text_frame
    tf.word_wrap = True
    set_paragraph(tf.paragraphs[0], title, size=28, bold=True, color=WHITE, font="Calibri")
    if subtitle:
        sub = slide.shapes.add_textbox(Inches(0.55), Inches(0.68), Inches(12.2), Inches(0.32))
        set_paragraph(sub.text_frame.paragraphs[0], subtitle, size=12, color=TEAL_LIGHT, font="Calibri")
    # footer
    foot = slide.shapes.add_textbox(Inches(0.55), Inches(7.15), Inches(10), Inches(0.25))
    set_paragraph(foot.text_frame.paragraphs[0], "SheikhGo Nexus  ·  Fleet Intelligence  ·  Module 4",
                  size=10, color=MUTED, font="Calibri")


def add_bullets(slide, left, top, width, height, items, size=14, color=DARK, bullet_color=TEAL):
    box = slide.shapes.add_textbox(left, top, width, height)
    tf = box.text_frame
    tf.word_wrap = True
    for i, item in enumerate(items):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = PP_ALIGN.LEFT
        p.space_after = Pt(8)
        p.level = 0
        # Use a teal bullet character via run prefix to avoid layout issues
        run = p.add_run()
        set_run(run, "●  ", size=size - 2, bold=False, color=bullet_color, font="Calibri")
        run2 = p.add_run()
        set_run(run2, item, size=size, bold=False, color=color, font="Calibri")
    return box


def card(slide, left, top, width, height, title, body_lines, title_color=TEAL):
    add_round_rect(slide, left, top, width, height, WHITE)
    # subtle inner tint top
    add_round_rect(slide, left, top, width, Inches(0.42), CARD_BG)
    t = slide.shapes.add_textbox(left + Inches(0.18), top + Inches(0.08), width - Inches(0.36), Inches(0.32))
    set_paragraph(t.text_frame.paragraphs[0], title, size=13, bold=True, color=title_color, font="Calibri")
    b = slide.shapes.add_textbox(left + Inches(0.18), top + Inches(0.5), width - Inches(0.36), height - Inches(0.6))
    tf = b.text_frame
    tf.word_wrap = True
    for i, line in enumerate(body_lines):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.space_after = Pt(4)
        set_run(p.add_run(), line, size=12, color=DARK, font="Calibri")


def build():
    prs = Presentation()
    prs.slide_width = SLIDE_W
    prs.slide_height = SLIDE_H
    blank = prs.slide_layouts[6]  # blank

    # ── 1. Title ──────────────────────────────────────────────
    s = prs.slides.add_slide(blank)
    dark_bg(s)
    # teal geometric accent panels (large blocks, not thin bars)
    add_rect(s, Inches(0), Inches(0), Inches(0.35), SLIDE_H, TEAL)
    add_rect(s, Inches(9.8), Inches(0), Inches(3.533), SLIDE_H, RGBColor(0x0A, 0x2A, 0x3C))
    # decorative circle motif
    circ = s.shapes.add_shape(MSO_SHAPE.OVAL, Inches(10.4), Inches(2.2), Inches(2.2), Inches(2.2))
    fill_shape(circ, TEAL)
    circ2 = s.shapes.add_shape(MSO_SHAPE.OVAL, Inches(11.1), Inches(4.6), Inches(1.4), Inches(1.4))
    fill_shape(circ2, RGBColor(0x0A, 0x5C, 0x55))

    eyebrow = s.shapes.add_textbox(Inches(0.9), Inches(1.8), Inches(8), Inches(0.4))
    set_paragraph(eyebrow.text_frame.paragraphs[0], "FLEET INTELLIGENCE  ·  MODULE 4",
                  size=14, bold=True, color=TEAL_LIGHT, font="Calibri")

    title = s.shapes.add_textbox(Inches(0.9), Inches(2.3), Inches(8.5), Inches(1.4))
    tf = title.text_frame
    tf.word_wrap = True
    set_paragraph(tf.paragraphs[0], "Smart Trip Assignment", size=40, bold=True, color=WHITE, font="Calibri")

    sub = s.shapes.add_textbox(Inches(0.9), Inches(3.9), Inches(8.2), Inches(0.9))
    tf = sub.text_frame
    tf.word_wrap = True
    set_paragraph(tf.paragraphs[0],
                  "Constraint-based scoring that recommends the best driver + vehicle —\n"
                  "with authorized staff confirm before assignment goes live.",
                  size=16, color=RGBColor(0xC5, 0xD5, 0xDE), font="Calibri")

    brand = s.shapes.add_textbox(Inches(0.9), Inches(6.4), Inches(8), Inches(0.4))
    set_paragraph(brand.text_frame.paragraphs[0], "SheikhGo Nexus", size=18, bold=True, color=WHITE, font="Calibri")
    meta = s.shapes.add_textbox(Inches(0.9), Inches(6.8), Inches(8), Inches(0.3))
    set_paragraph(meta.text_frame.paragraphs[0], "Presentation-ready  ·  28 Oct 2026",
                  size=12, color=MUTED, font="Calibri")

    # ── 2. Overview / business objective ──────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Overview & business objective",
                 "Recommend the most suitable available driver + vehicle — transparent scoring, staff confirm")

    card(s, Inches(0.5), Inches(1.35), Inches(4.0), Inches(2.55), "Objective", [
        "Mandatory hard filters first,",
        "then weighted rank + explainable",
        "factors. Authorized staff confirm",
        "before assignment becomes live.",
    ])
    card(s, Inches(4.7), Inches(1.35), Inches(4.0), Inches(2.55), "Business problem", [
        "Dispatchers choose under time",
        "pressure — fatigue, fuel/range,",
        "proximity, compliance, capacity,",
        "and fairness get missed.",
    ])
    card(s, Inches(8.9), Inches(1.35), Inches(3.9), Inches(2.55), "Positioning", [
        "Use: Smart Trip Assignment",
        "Not: ML/LLM auto-dispatch",
        "Constraint-based scoring,",
        "never a black-box “AI score”.",
    ])

    # Value outcomes row
    outcomes = [
        ("Faster dispatch", "Ranked shortlist"),
        ("Consistent policy", "Same hard rules"),
        ("Auditable why", "Factor + decision log"),
        ("Compliant offers", "Filters beat score"),
        ("Measurable trust", "Override / shadow KPIs"),
    ]
    x0 = Inches(0.5)
    for i, (h, b) in enumerate(outcomes):
        left = x0 + Inches(i * 2.5)
        add_round_rect(s, left, Inches(4.15), Inches(2.35), Inches(2.0), WHITE)
        num = s.shapes.add_textbox(left + Inches(0.15), Inches(4.3), Inches(2.0), Inches(0.45))
        set_paragraph(num.text_frame.paragraphs[0], f"0{i+1}", size=22, bold=True, color=TEAL, font="Calibri")
        ht = s.shapes.add_textbox(left + Inches(0.15), Inches(4.85), Inches(2.05), Inches(0.4))
        set_paragraph(ht.text_frame.paragraphs[0], h, size=13, bold=True, color=NAVY, font="Calibri")
        bt = s.shapes.add_textbox(left + Inches(0.15), Inches(5.3), Inches(2.05), Inches(0.5))
        set_paragraph(bt.text_frame.paragraphs[0], b, size=12, color=MUTED, font="Calibri")

    # ── 3. Architecture ───────────────────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Architecture", "Verified stack — Angular ERP · ASP.NET Core net10.0 · scoring engine · SQL")

    layers = [
        ("Angular ERP", "Smart Assignment board\n/trips/smart-assignment"),
        ("API  net10.0", "ASP.NET Core\nsuggestions · confirm · override"),
        ("Assignment\nengine", "Hard filters +\nweighted scoring"),
        ("SQL Server", "Dapper + audit\nTripSmartAssignmentDecisions"),
    ]
    for i, (title_t, body) in enumerate(layers):
        left = Inches(0.5) + Inches(i * 3.15)
        add_round_rect(s, left, Inches(1.5), Inches(2.9), Inches(2.35), WHITE if i % 2 == 0 else CARD_BG)
        tbox = s.shapes.add_textbox(left + Inches(0.15), Inches(1.7), Inches(2.6), Inches(0.85))
        tf = tbox.text_frame
        tf.word_wrap = True
        set_paragraph(tf.paragraphs[0], title_t.replace("\n", " "), size=16, bold=True, color=NAVY, font="Calibri")
        bbox = s.shapes.add_textbox(left + Inches(0.15), Inches(2.55), Inches(2.6), Inches(1.0))
        tf = bbox.text_frame
        tf.word_wrap = True
        for j, line in enumerate(body.split("\n")):
            p = tf.paragraphs[0] if j == 0 else tf.add_paragraph()
            p.space_after = Pt(2)
            set_run(p.add_run(), line, size=12, color=MUTED, font="Calibri")
        if i < 3:
            arrow = s.shapes.add_textbox(left + Inches(2.75), Inches(2.3), Inches(0.45), Inches(0.4))
            set_paragraph(arrow.text_frame.paragraphs[0], "→", size=22, bold=True, color=TEAL, align=PP_ALIGN.CENTER)

    # integrations
    ints = [
        ("Traccar / GPS", "Live proximity & ETA inputs"),
        ("Google Routes", "Distance, traffic, ETA"),
        ("Notifications", "Offer / reject / re-offer"),
        ("Audit logs", "Propose · confirm · override"),
    ]
    for i, (h, b) in enumerate(ints):
        left = Inches(0.5) + Inches(i * 3.15)
        add_round_rect(s, left, Inches(4.2), Inches(2.9), Inches(1.7), NAVY)
        ht = s.shapes.add_textbox(left + Inches(0.18), Inches(4.4), Inches(2.55), Inches(0.4))
        set_paragraph(ht.text_frame.paragraphs[0], h, size=14, bold=True, color=TEAL_LIGHT, font="Calibri")
        bt = s.shapes.add_textbox(left + Inches(0.18), Inches(4.9), Inches(2.55), Inches(0.7))
        set_paragraph(bt.text_frame.paragraphs[0], b, size=12, color=WHITE, font="Calibri")

    note = s.shapes.add_textbox(Inches(0.5), Inches(6.15), Inches(12.3), Inches(0.7))
    tf = note.text_frame
    tf.word_wrap = True
    set_paragraph(tf.paragraphs[0],
                  "Note: Ali’s overview mock may label API as “.NET 8” — product truth is net10.0. "
                  "DeepInfra chat is separate and is not the assignment scorer.",
                  size=12, color=MUTED, font="Calibri")

    # ── 4. Scoring breakdown ──────────────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Scoring breakdown", "Soft preferences — slide target vs MVP weights (must normalize to 1.0)")

    # Warning callout
    add_round_rect(s, Inches(0.5), Inches(1.3), Inches(12.3), Inches(0.7), AMBER_BG)
    warn = s.shapes.add_textbox(Inches(0.7), Inches(1.42), Inches(12.0), Inches(0.5))
    set_paragraph(warn.text_frame.paragraphs[0],
                  "⚠  Slide target weights sum to 110% — anomaly. Implementation must normalize / rebalance to 1.0 after Ali sign-off.",
                  size=13, bold=True, color=WARN, font="Calibri")

    # Left: slide target
    add_round_rect(s, Inches(0.5), Inches(2.2), Inches(6.0), Inches(4.5), WHITE)
    lt = s.shapes.add_textbox(Inches(0.7), Inches(2.35), Inches(5.6), Inches(0.35))
    set_paragraph(lt.text_frame.paragraphs[0], "Slide target (Ali overview mock)", size=14, bold=True, color=NAVY)

    slide_factors = [
        ("Proximity to pickup", "20%"),
        ("ETA & traffic", "20%"),
        ("Driver performance", "15%"),
        ("Vehicle suitability", "15%"),
        ("Cost optimization", "10%"),
        ("Fuel / energy efficiency", "10%"),
        ("Utilization / workload", "10%"),
        ("Historical reliability", "10%"),
        ("Sum on slide", "110%"),
    ]
    for i, (f, w) in enumerate(slide_factors):
        y = Inches(2.8) + Inches(i * 0.38)
        is_sum = i == len(slide_factors) - 1
        fb = s.shapes.add_textbox(Inches(0.75), y, Inches(4.2), Inches(0.35))
        set_paragraph(fb.text_frame.paragraphs[0], f, size=12,
                      bold=is_sum, color=WARN if is_sum else DARK, font="Calibri")
        wb = s.shapes.add_textbox(Inches(5.1), y, Inches(1.1), Inches(0.35))
        set_paragraph(wb.text_frame.paragraphs[0], w, size=12,
                      bold=True, color=WARN if is_sum else TEAL, align=PP_ALIGN.RIGHT, font="Calibri")

    # Right: MVP
    add_round_rect(s, Inches(6.8), Inches(2.2), Inches(6.0), Inches(4.5), WHITE)
    rt = s.shapes.add_textbox(Inches(7.0), Inches(2.35), Inches(5.6), Inches(0.35))
    set_paragraph(rt.text_frame.paragraphs[0], "Current MVP (TripAssignmentScorer)", size=14, bold=True, color=NAVY)

    mvp = [
        ("Driver rating", "0.22"),
        ("Proximity to pickup", "0.18"),
        ("Fatigue (hours today)", "0.15"),
        ("Fuel / EV battery %", "0.15"),
        ("On-time pickup", "0.15"),
        ("Route / traffic", "0.15"),
        ("Sum (required)", "1.00"),
    ]
    for i, (f, w) in enumerate(mvp):
        y = Inches(2.8) + Inches(i * 0.42)
        is_sum = i == len(mvp) - 1
        fb = s.shapes.add_textbox(Inches(7.05), y, Inches(4.2), Inches(0.35))
        set_paragraph(fb.text_frame.paragraphs[0], f, size=12,
                      bold=is_sum, color=TEAL if is_sum else DARK, font="Calibri")
        wb = s.shapes.add_textbox(Inches(11.3), y, Inches(1.2), Inches(0.35))
        set_paragraph(wb.text_frame.paragraphs[0], w, size=12,
                      bold=True, color=TEAL, align=PP_ALIGN.RIGHT, font="Calibri")

    note2 = s.shapes.add_textbox(Inches(7.0), Inches(5.95), Inches(5.5), Inches(0.55))
    tf = note2.text_frame
    tf.word_wrap = True
    set_paragraph(tf.paragraphs[0],
                  "Target additions in scope now: cost + utilization. UI always shows named factors — never an opaque AI score.",
                  size=11, color=MUTED, font="Calibri")

    # ── 5. Hard filters ───────────────────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Hard filters", "Fail → exclude. A high soft score never overrides a hard-filter failure.")

    filters = [
        ("Driver availability", "Available for trip window; Suspended/OnLeave blocked; OnTrip excluded (interim)"),
        ("Valid documents", "License / docs current — guard today; Document Tracker later"),
        ("Driving hours / shift", "Desired on slide; soft fatigue today — hard HOS still a gap"),
        ("Vehicle availability", "Vehicle free / assignable; no linked vehicle → exclude"),
        ("Type & capacity", "Fits trip type & passengers — enforce when data present"),
        ("Maintenance status", "Critical WO / overdue → hard block (interim)"),
        ("EV range (if EV)", "Range ≥ trip × ~15% buffer — hard skip"),
        ("Geo & compliance", "Desired on slide — policy rules still evolving"),
    ]
    for i, (h, b) in enumerate(filters):
        col = i % 2
        row = i // 2
        left = Inches(0.5) + Inches(col * 6.35)
        top = Inches(1.35) + Inches(row * 1.25)
        add_round_rect(s, left, top, Inches(6.1), Inches(1.1), WHITE)
        # teal number circle
        circ = s.shapes.add_shape(MSO_SHAPE.OVAL, left + Inches(0.18), top + Inches(0.28), Inches(0.45), Inches(0.45))
        fill_shape(circ, TEAL)
        nb = s.shapes.add_textbox(left + Inches(0.18), top + Inches(0.34), Inches(0.45), Inches(0.35))
        set_paragraph(nb.text_frame.paragraphs[0], str(i + 1), size=12, bold=True, color=WHITE,
                      align=PP_ALIGN.CENTER, font="Calibri")
        ht = s.shapes.add_textbox(left + Inches(0.8), top + Inches(0.18), Inches(5.0), Inches(0.35))
        set_paragraph(ht.text_frame.paragraphs[0], h, size=14, bold=True, color=NAVY, font="Calibri")
        bt = s.shapes.add_textbox(left + Inches(0.8), top + Inches(0.52), Inches(5.0), Inches(0.45))
        tf = bt.text_frame
        tf.word_wrap = True
        set_paragraph(tf.paragraphs[0], b, size=11, color=MUTED, font="Calibri")

    # ── 6. Flow ───────────────────────────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Assignment flow", "Needs → pairs → hard filters → score → explain → staff confirm → audit")

    steps = [
        ("1", "Needs", "Trip details,\ncargo, window"),
        ("2", "Pairs", "Drivers ×\nvehicles"),
        ("3", "Hard\nfilters", "Fail =\nexclude"),
        ("4", "Score", "Weighted\nrank"),
        ("5", "Explain", "Factors +\nweights"),
        ("6", "Confirm", "Staff\nauthorize"),
        ("7", "Audit", "Decision\nlog"),
    ]
    for i, (n, h, b) in enumerate(steps):
        left = Inches(0.4) + Inches(i * 1.85)
        add_round_rect(s, left, Inches(1.5), Inches(1.7), Inches(2.6), NAVY if i in (2, 5) else WHITE)
        tc = WHITE if i in (2, 5) else NAVY
        bc = TEAL_LIGHT if i in (2, 5) else MUTED
        nc = TEAL_LIGHT if i in (2, 5) else TEAL
        nb = s.shapes.add_textbox(left + Inches(0.1), Inches(1.65), Inches(1.5), Inches(0.4))
        set_paragraph(nb.text_frame.paragraphs[0], n, size=20, bold=True, color=nc, align=PP_ALIGN.CENTER)
        ht = s.shapes.add_textbox(left + Inches(0.1), Inches(2.15), Inches(1.5), Inches(0.7))
        tf = ht.text_frame
        tf.word_wrap = True
        set_paragraph(tf.paragraphs[0], h.replace("\n", " "), size=13, bold=True, color=tc, align=PP_ALIGN.CENTER)
        bt = s.shapes.add_textbox(left + Inches(0.1), Inches(2.9), Inches(1.5), Inches(0.9))
        tf = bt.text_frame
        tf.word_wrap = True
        for j, line in enumerate(b.split("\n")):
            p = tf.paragraphs[0] if j == 0 else tf.add_paragraph()
            p.alignment = PP_ALIGN.CENTER
            p.space_after = Pt(0)
            set_run(p.add_run(), line, size=11, color=bc, font="Calibri")

    # Modes
    add_round_rect(s, Inches(0.5), Inches(4.4), Inches(12.3), Inches(2.2), WHITE)
    mt = s.shapes.add_textbox(Inches(0.75), Inches(4.55), Inches(11.8), Inches(0.35))
    set_paragraph(mt.text_frame.paragraphs[0], "Operating modes (target)", size=14, bold=True, color=NAVY)

    modes = [
        ("SuggestOnly", "Default — ranked shortlist; dispatcher confirms before write"),
        ("Shadow", "Log “would assign” only — no live assignment write (~2 weeks before AutoApply)"),
        ("AutoApply", "Tenant flag only after shadow trust; create-time auto-assign stays off"),
    ]
    for i, (h, b) in enumerate(modes):
        y = Inches(5.05) + Inches(i * 0.45)
        hb = s.shapes.add_textbox(Inches(0.75), y, Inches(2.2), Inches(0.4))
        set_paragraph(hb.text_frame.paragraphs[0], h, size=13, bold=True, color=TEAL, font="Calibri")
        bb = s.shapes.add_textbox(Inches(3.1), y, Inches(9.3), Inches(0.4))
        set_paragraph(bb.text_frame.paragraphs[0], b, size=13, color=DARK, font="Calibri")

    # ── 7. Open decisions ─────────────────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Twelve open decisions + interim defaults",
                 "Numbered to match Module 4 planning doc §14 — confirm or revise with Ali")

    decisions = [
        ("1", "Create-time auto-assign", "No — never confirm without auth"),
        ("2", "Default mode", "Suggest-only"),
        ("3", "OnTrip drivers", "Exclude OnTrip"),
        ("4", "No linked vehicle", "Exclude"),
        ("5", "Capacity / type", "Still open — enforce when data present"),
        ("6", "Maintenance", "Hard block"),
        ("7", "Soft weights", "Still open — fix 110% then normalize"),
        ("8", "EV range buffer", "Keep 15%"),
        ("9", "Product name", "Smart Trip Assignment"),
        ("10", "LLM explain", "No (for now)"),
        ("11", "Cost / utilization", "Include now"),
        ("12", "Shadow before AutoApply", "~2 weeks shadow"),
    ]
    for i, (n, h, d) in enumerate(decisions):
        col = i % 3
        row = i // 3
        left = Inches(0.45) + Inches(col * 4.25)
        top = Inches(1.3) + Inches(row * 1.35)
        open_item = "Still open" in d
        bg = AMBER_BG if open_item else WHITE
        add_round_rect(s, left, top, Inches(4.05), Inches(1.2), bg)
        circ = s.shapes.add_shape(MSO_SHAPE.OVAL, left + Inches(0.15), top + Inches(0.35), Inches(0.42), Inches(0.42))
        fill_shape(circ, WARN if open_item else TEAL)
        nb = s.shapes.add_textbox(left + Inches(0.15), top + Inches(0.4), Inches(0.42), Inches(0.32))
        set_paragraph(nb.text_frame.paragraphs[0], n, size=11, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
        ht = s.shapes.add_textbox(left + Inches(0.7), top + Inches(0.2), Inches(3.15), Inches(0.35))
        set_paragraph(ht.text_frame.paragraphs[0], h, size=12, bold=True, color=NAVY, font="Calibri")
        dt = s.shapes.add_textbox(left + Inches(0.7), top + Inches(0.55), Inches(3.15), Inches(0.5))
        tf = dt.text_frame
        tf.word_wrap = True
        set_paragraph(tf.paragraphs[0], d, size=11, color=WARN if open_item else MUTED, font="Calibri")

    def fit_picture(slide, path, max_w, max_h, top):
        """Place picture centered, capped to max_w × max_h, starting at top."""
        pic = slide.shapes.add_picture(str(path), Inches(0.7), top, width=max_w)
        if pic.height > max_h:
            ratio = max_h / pic.height
            pic.width = int(pic.width * ratio)
            pic.height = max_h
        pic.left = int((SLIDE_W - pic.width) / 2)
        return pic

    # ── 8. Overview screenshot ────────────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Module overview", "Data Inputs → Engine (filters + scoring) → Ranked results · Assign Selected")
    if OVERVIEW_IMG.exists():
        fit_picture(s, OVERVIEW_IMG, Inches(11.9), Inches(5.0), Inches(1.3))
    else:
        add_round_rect(s, Inches(0.7), Inches(1.5), Inches(11.9), Inches(5.0), CARD_BG)
        miss = s.shapes.add_textbox(Inches(1), Inches(3.5), Inches(11), Inches(0.5))
        set_paragraph(miss.text_frame.paragraphs[0], "Overview asset not found",
                      size=16, color=MUTED, align=PP_ALIGN.CENTER)

    # Caption sits above footer (7.15); image bottom ~6.3
    cap = s.shapes.add_textbox(Inches(0.7), Inches(6.45), Inches(12), Inches(0.4))
    set_paragraph(cap.text_frame.paragraphs[0],
                  "Architecture strip on mock may say .NET 8 — product truth is net10.0. Soft-weight ring sums to 110%.",
                  size=11, color=MUTED, font="Calibri")

    # ── 9. Driver Performance (optional) ──────────────────────
    if DRIVER_PERF_IMG.exists():
        s = prs.slides.add_slide(blank)
        slide_header(s, "Driver Performance (scoring input)",
                     "Feeds Performance / reliability soft factors — weights on this screen are for performance scoring (100%)")
        fit_picture(s, DRIVER_PERF_IMG, Inches(11.9), Inches(5.0), Inches(1.3))
        cap = s.shapes.add_textbox(Inches(0.7), Inches(6.45), Inches(12), Inches(0.4))
        set_paragraph(cap.text_frame.paragraphs[0],
                      "Analytics → Driver Performance. Distinct from the Module 4 assignment soft-weight table.",
                      size=11, color=MUTED, font="Calibri")

    # ── 10. Shadow mode + next steps ──────────────────────────
    s = prs.slides.add_slide(blank)
    slide_header(s, "Shadow mode & next steps",
                 "Prove ranking quality without writing live assignments")

    # Shadow cards
    shadow_items = [
        ("Purpose", "Prove ranking quality without writing trip driver assignments"),
        ("Behavior", "Log “would assign X” — no live assignment write"),
        ("Duration", "~2 weeks shadow before any tenant enables AutoApply"),
        ("Exit criteria", "Acceptable override rate · trusted factor panel · hard-filter FP review OK"),
        ("Rollback", "Disable AutoApply; keep SuggestOnly + manual assign; retain decision rows"),
    ]
    for i, (h, b) in enumerate(shadow_items):
        left = Inches(0.5) + Inches((i % 5) * 2.5)
        add_round_rect(s, left, Inches(1.35), Inches(2.35), Inches(2.15), WHITE)
        ht = s.shapes.add_textbox(left + Inches(0.12), Inches(1.5), Inches(2.1), Inches(0.4))
        set_paragraph(ht.text_frame.paragraphs[0], h, size=13, bold=True, color=TEAL, font="Calibri")
        bt = s.shapes.add_textbox(left + Inches(0.12), Inches(2.0), Inches(2.1), Inches(1.3))
        tf = bt.text_frame
        tf.word_wrap = True
        set_paragraph(tf.paragraphs[0], b, size=11, color=DARK, font="Calibri")

    # Next steps
    add_round_rect(s, Inches(0.5), Inches(3.75), Inches(12.3), Inches(2.9), NAVY)
    nt = s.shapes.add_textbox(Inches(0.75), Inches(3.95), Inches(11.8), Inches(0.4))
    set_paragraph(nt.text_frame.paragraphs[0], "Next steps", size=16, bold=True, color=TEAL_LIGHT, font="Calibri")

    nexts = [
        "Sign off soft-weight table — fix 110% anomaly and normalize to 1.0",
        "Finalize capacity / vehicle-type hard-filter wording when data present",
        "Complete remaining screenshot slots (suggest board, factor panel, confirm, excluded, audit)",
        "Run ~2 weeks shadow metrics before any AutoApply tenant flag",
        "Keep create-time auto-assign off; ranking never requires an LLM call",
    ]
    for i, line in enumerate(nexts):
        y = Inches(4.45) + Inches(i * 0.38)
        nb = s.shapes.add_textbox(Inches(0.75), y, Inches(0.4), Inches(0.35))
        set_paragraph(nb.text_frame.paragraphs[0], f"{i+1}.", size=13, bold=True, color=TEAL, font="Calibri")
        lb = s.shapes.add_textbox(Inches(1.2), y, Inches(11.2), Inches(0.35))
        set_paragraph(lb.text_frame.paragraphs[0], line, size=13, color=WHITE, font="Calibri")

    prs.save(str(OUT))
    return len(prs.slides), str(OUT)


if __name__ == "__main__":
    count, path = build()
    print(f"Wrote {count} slides → {path}")
