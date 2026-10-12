"""Local OCR only: PDF pages -> text boxes. No banking or export logic here."""
import os
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
os.environ.setdefault("FLAGS_use_mkldnn", "0")
import math
import threading
from contextlib import asynccontextmanager
from functools import lru_cache

import numpy as np
import pymupdf
from fastapi import FastAPI, HTTPException, UploadFile
from starlette.concurrency import run_in_threadpool

MAX_BYTES = 20 * 1024 * 1024
MAX_PAGES = 200
_lock = threading.Lock()

@lru_cache(maxsize=1)
def engine():
    from paddleocr import PaddleOCR
    return PaddleOCR(ocr_version="PP-OCRv6", lang="fr", device="cpu",
                     use_doc_orientation_classify=False, use_doc_unwarping=False,
                     use_textline_orientation=False, enable_mkldnn=False,
                     cpu_threads=max(1, min(8, int(os.getenv("OCR_CPU_THREADS", "2")))))

@asynccontextmanager
async def lifespan(_app):
    await run_in_threadpool(engine)
    yield

app = FastAPI(title="Alias local OCR", lifespan=lifespan)

@app.get("/health")
def health():
    return {"status": "ok", "engine": "PaddleOCR", "version": "PP-OCRv6"}

def extract_pdf(content: bytes):
    try:
        pdf = pymupdf.open(stream=content, filetype="pdf")
    except Exception:
        raise HTTPException(422, "PDF illisible ou endommagé.") from None
    with pdf:
        if pdf.needs_pass:
            raise HTTPException(422, "Déverrouillez le PDF avant l’importation.")
        if not 0 < len(pdf) <= MAX_PAGES:
            raise HTTPException(422, "Le PDF doit contenir entre 1 et 200 pages.")
        pages = []
        with _lock:
            for index, page in enumerate(pdf):
                # Bound image size independently of the uploaded PDF's page dimensions.
                scale = min(3, 3200 / max(page.rect.width, page.rect.height))
                pix = page.get_pixmap(matrix=pymupdf.Matrix(scale, scale), colorspace=pymupdf.csRGB, alpha=False)
                image = np.frombuffer(pix.samples, dtype=np.uint8).reshape(pix.height, pix.width, 3)
                result = next(iter(engine().predict(image[:, :, ::-1])))
                data = result.json.get("res", result.json)
                boxes = []
                for text, score, poly in zip(data["rec_texts"], data["rec_scores"], data["rec_polys"], strict=True):
                    if not text.strip():
                        continue
                    coords = np.asarray(poly)
                    left, top = coords.min(axis=0).tolist()
                    right, bottom = coords.max(axis=0).tolist()
                    if not all(math.isfinite(v) for v in (left, top, right, bottom, float(score))):
                        continue
                    boxes.append({"text": text, "left": left / scale, "right": right / scale,
                                  "top": top / scale, "bottom": bottom / scale, "confidence": float(score)})
                pages.append({"number": index + 1, "width": page.rect.width, "height": page.rect.height, "boxes": boxes})
        return {"method": "PADDLE_OCR", "pages": pages}

@app.post("/extract")
async def extract(file: UploadFile):
    try:
        content = await file.read(MAX_BYTES + 1)
        if len(content) > MAX_BYTES:
            raise HTTPException(413, "PDF trop volumineux.")
        if not content.startswith(b"%PDF-"):
            raise HTTPException(400, "Signature PDF invalide.")
        return await run_in_threadpool(extract_pdf, content)
    except HTTPException:
        raise
    except Exception:
        # Never expose recognized banking text or runtime paths in errors/logs.
        raise HTTPException(503, "Le moteur OCR n’a pas pu terminer la lecture.") from None
    finally:
        await file.close()
