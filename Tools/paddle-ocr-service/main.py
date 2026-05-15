# PaddleOCR HTTP sidecar for the .NET API (Ocr:PaddleOcrServiceUrl, e.g. http://127.0.0.1:8088).
# Install: pip install -r requirements.txt  (see https://www.paddlepaddle.org.cn for paddlepaddle wheel)
# Run:    uvicorn main:app --host 127.0.0.1 --port 8088
# Optional: PADDLEOCR_LANG=ur for Urdu-heavy documents (if supported by your PaddleOCR build).

from __future__ import annotations

import os
import tempfile
from pathlib import Path

from fastapi import FastAPI, File, UploadFile
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI(title="Paddle OCR sidecar")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

_ocr = None


def get_ocr():
    global _ocr
    if _ocr is None:
        from paddleocr import PaddleOCR

        lang = os.getenv("PADDLEOCR_LANG", "en")
        _ocr = PaddleOCR(use_angle_cls=True, lang=lang, show_log=False)
    return _ocr


@app.post("/ocr")
async def ocr_identity(file: UploadFile = File(...)):
    data = await file.read()
    if not data:
        return {"rawText": "", "confidence": 0}

    suffix = Path(file.filename or "upload.jpg").suffix or ".jpg"
    path: str | None = None
    try:
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
            tmp.write(data)
            path = tmp.name

        result = get_ocr().ocr(path, cls=True)
    finally:
        if path:
            try:
                os.unlink(path)
            except OSError:
                pass

    lines: list[str] = []
    scores: list[float] = []
    if result and result[0]:
        for block in result[0]:
            if not block or len(block) < 2:
                continue
            text_conf = block[1]
            if isinstance(text_conf, (list, tuple)) and len(text_conf) >= 2:
                lines.append(str(text_conf[0]))
                try:
                    scores.append(float(text_conf[1]))
                except (TypeError, ValueError):
                    pass

    raw_text = "\n".join(lines)
    conf = int(round(sum(scores) / len(scores) * 100)) if scores else 0
    return {"rawText": raw_text, "confidence": conf}
