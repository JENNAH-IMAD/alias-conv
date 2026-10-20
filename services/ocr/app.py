"""Local OCR only: PDF pages -> text boxes. No banking or export logic here."""
import os
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
os.environ.setdefault("FLAGS_use_mkldnn", "0")
import math
import asyncio
import hashlib
import json
import threading
import time
from collections import OrderedDict
from contextlib import asynccontextmanager, contextmanager, suppress
from functools import lru_cache

import numpy as np
import pymupdf
from fastapi import FastAPI, Form, HTTPException, Request, UploadFile
from starlette.concurrency import run_in_threadpool

_lock = threading.Lock()
_page_cache = OrderedDict()
_cache_bytes = 0
_cache_limit = 32 * 1024 * 1024


def cached_page(key):
    entry = _page_cache.get(key)
    if entry is None:
        return None
    expires, encoded = entry
    if expires <= time.monotonic():
        global _cache_bytes
        _cache_bytes -= len(_page_cache.pop(key)[1])
        return None
    _page_cache.move_to_end(key)
    return json.loads(encoded)


def remember_page(key, page):
    global _cache_bytes
    encoded = json.dumps(page, ensure_ascii=False, allow_nan=False).encode("utf-8")
    if len(encoded) > _cache_limit:
        return
    old = _page_cache.pop(key, None)
    if old is not None:
        _cache_bytes -= len(old[1])
    while _page_cache and _cache_bytes + len(encoded) > _cache_limit:
        _cache_bytes -= len(_page_cache.popitem(last=False)[1][1])
    _page_cache[key] = (time.monotonic() + 600, encoded)
    _cache_bytes += len(encoded)


@contextmanager
def inference_lock(cancelled):
    while not _lock.acquire(timeout=0.2):
        if cancelled is not None and cancelled.is_set():
            raise HTTPException(499, "Lecture annulée.")
    try:
        yield
    finally:
        _lock.release()

@lru_cache(maxsize=1)
def engine():
    from paddleocr import PaddleOCR
    size = os.getenv("OCR_MODEL_SIZE", "small")
    if size not in {"tiny", "small", "medium"}:
        raise ValueError("OCR_MODEL_SIZE doit être tiny, small ou medium.")
    return PaddleOCR(device="cpu",
                     text_detection_model_name=f"PP-OCRv6_{size}_det",
                     text_recognition_model_name=f"PP-OCRv6_{size}_rec",
                     use_doc_orientation_classify=False, use_doc_unwarping=False,
                     use_textline_orientation=False, enable_mkldnn=False,
                     # Bound detector work on CPU; recognition still uses the full-resolution crops.
                     text_det_limit_type="max",
                     text_det_limit_side_len=max(960, min(2560, int(os.getenv("OCR_DETECTION_MAX_SIDE", "1280")))),
                     cpu_threads=max(1, min(8, int(os.getenv("OCR_CPU_THREADS", "2")))))

@asynccontextmanager
async def lifespan(_app):
    await run_in_threadpool(engine)
    yield

app = FastAPI(title="Alias local OCR", lifespan=lifespan)

@app.get("/health")
def health():
    return {"status": "ok", "engine": "PaddleOCR", "version": "PP-OCRv6", "model_size": os.getenv("OCR_MODEL_SIZE", "small")}

def extract_pdf(content: bytes, selected_pages: tuple[int, ...] | None = None, cancelled: threading.Event | None = None):
    try:
        pdf = pymupdf.open(stream=content, filetype="pdf")
    except Exception:
        raise HTTPException(422, "PDF illisible ou endommagé.") from None
    with pdf:
        if pdf.needs_pass:
            raise HTTPException(422, "Déverrouillez le PDF avant l’importation.")
        if len(pdf) == 0:
            raise HTTPException(422, "Le PDF doit contenir au moins une page.")
        selected = selected_pages if selected_pages is not None else tuple(range(1, len(pdf) + 1))
        if not selected or len(set(selected)) != len(selected) or any(number < 1 or number > len(pdf) for number in selected):
            raise HTTPException(400, "Sélection de pages invalide.")
        pages = []
        digest = hashlib.sha256(content).hexdigest()
        settings = (os.getenv("OCR_MODEL_SIZE", "small"), os.getenv("OCR_DETECTION_MAX_SIDE", "1280"))
        # Cache only this exact PDF and model configuration, never templates or banking values
        # borrowed from another document. Access is serialized by the inference lock below.
        with inference_lock(cancelled):
            for number in selected:
                if cancelled is not None and cancelled.is_set():
                    raise HTTPException(499, "Lecture annulée.")
                key = ("ocr-page-v1", digest, settings, number)
                cached = cached_page(key)
                if cached is not None:
                    pages.append(cached)
                    continue
                page = pdf[number - 1]
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
                recognized = {"number": number, "width": page.rect.width, "height": page.rect.height, "boxes": boxes}
                remember_page(key, recognized)
                pages.append(recognized)
        return {"method": "PADDLE_OCR", "pages": pages}

@app.post("/extract")
async def extract(request: Request, file: UploadFile, pages: str | None = Form(default=None)):
    cancelled = threading.Event()
    watcher = None

    async def watch_connection():
        while not cancelled.is_set():
            if await request.is_disconnected():
                cancelled.set()
                return
            await asyncio.sleep(0.2)

    try:
        content = await file.read()
        if not content.startswith(b"%PDF-"):
            raise HTTPException(400, "Signature PDF invalide.")
        selected = None
        if pages is not None:
            if not pages or any(not value.isascii() or not value.isdecimal() for value in pages.split(',')):
                raise HTTPException(400, "Sélection de pages invalide.")
            selected = tuple(int(value) for value in pages.split(','))
        watcher = asyncio.create_task(watch_connection())
        return await run_in_threadpool(extract_pdf, content, selected, cancelled)
    except HTTPException:
        raise
    except Exception:
        # Never expose recognized banking text or runtime paths in errors/logs.
        raise HTTPException(503, "Le moteur OCR n’a pas pu terminer la lecture.") from None
    finally:
        cancelled.set()
        if watcher is not None:
            watcher.cancel()
            with suppress(asyncio.CancelledError):
                await watcher
        await file.close()
