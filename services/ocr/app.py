"""Local OCR only: PDF pages -> text boxes. No banking or export logic here."""
import os
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
os.environ.setdefault("FLAGS_use_mkldnn", "0")
import math
import asyncio
import re
from datetime import datetime
import threading
import logging
from contextlib import asynccontextmanager, contextmanager, suppress
from functools import lru_cache

import numpy as np
import cv2
import pymupdf
from fastapi import FastAPI, Form, HTTPException, Request, UploadFile
from starlette.concurrency import run_in_threadpool
from structure_engine import structure_engine, read_layout, tables_from_layout

_lock = threading.Lock()
_layout_available = None
logger = logging.getLogger("alias.ocr")


def warmup():
    global _layout_available
    mode = os.getenv("OCR_PIPELINE", "structure")
    if mode not in {"structure", "text"}:
        raise ValueError("OCR_PIPELINE doit être structure ou text.")
    if mode == "structure":
        try:
            structure_engine()
            _layout_available = True
            return
        except Exception as error:
            _layout_available = False
            logger.warning("Layout model unavailable (%s); positional OCR remains active.", type(error).__name__)
    engine()


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


@lru_cache(maxsize=1)
def cell_recognizer():
    from paddleocr import TextRecognition
    return TextRecognition(model_name=f"PP-OCRv6_{os.getenv('OCR_MODEL_SIZE', 'small')}_rec",
                           device="cpu", enable_mkldnn=False,
                           cpu_threads=max(1, min(8, int(os.getenv("OCR_CPU_THREADS", "2")))))

@asynccontextmanager
async def lifespan(_app):
    await run_in_threadpool(warmup)
    yield

app = FastAPI(title="Alias local OCR", lifespan=lifespan)


@app.get("/health")
def health():
    return {"status": "ok", "engine": "PaddleOCR", "version": "PP-OCRv6", "model_size": os.getenv("OCR_MODEL_SIZE", "small"),
            "pipeline": os.getenv("OCR_PIPELINE", "structure"), "pipeline_version": "local-layout-v1",
            "layout_ready": _layout_available is True, "layout_model": os.getenv("OCR_LAYOUT_MODEL", "PP-DocLayout-M")}


def refine_ruled_cells(image, scale, boxes):
    """Re-read touching date columns and clipped decimals from their original pixels."""
    def read_crop(left, top, right, bottom):
        x, y = max(0, int(left * scale)), max(0, int(top * scale))
        crop = image[y:min(image.shape[0], int(bottom * scale) + 1),
                     x:min(image.shape[1], int(right * scale) + 1)].copy()
        if not crop.size:
            return []
        factor = max(1, 5 / scale)
        crop = cv2.resize(crop, None, fx=factor, fy=factor, interpolation=cv2.INTER_CUBIC)
        binary = cv2.threshold(cv2.cvtColor(crop, cv2.COLOR_RGB2GRAY), 180, 255, cv2.THRESH_BINARY_INV)[1]
        # Long vertical rules confuse recognition of adjacent 1s; remove only those rules.
        vertical = cv2.morphologyEx(binary, cv2.MORPH_OPEN,
                                    np.ones((max(35, crop.shape[0] // 3), 1), np.uint8))
        crop[vertical > 0] = 255
        prediction = next(iter(engine().predict(crop[:, :, ::-1]))).json
        data = prediction.get("res", prediction)
        found = []
        for text, score, poly in zip(data["rec_texts"], data["rec_scores"], data["rec_polys"], strict=True):
            coords = np.asarray(poly)
            a, b = coords.min(axis=0)
            c, d = coords.max(axis=0)
            found.append({"text": text, "left": (x + a / factor) / scale,
                          "right": (x + c / factor) / scale, "top": (y + b / factor) / scale,
                          "bottom": (y + d / factor) / scale, "confidence": float(score)})
        return found

    for heading in list(boxes):
        if not re.fullmatch(r"\s*OPER\s+VALEUR\s*", heading["text"], re.IGNORECASE):
            continue
        left, right = heading["left"] - 4, heading["right"] + 3
        dates = [b for b in boxes if b["top"] > heading["bottom"]
                 and b["left"] >= left and b["right"] <= right
                 and re.fullmatch(r"[\d/ILil|. -]+", b["text"]) and "/" in b["text"]]
        if not dates:
            continue
        for found in read_crop(left, min(b["top"] for b in dates) - 3,
                               right, max(b["bottom"] for b in dates) + 3):
            text = re.sub(r"\s+", "", found["text"]).translate(str.maketrans({"I": "1", "l": "1", "L": "1", "i": "1", "|": "1"}))
            match = re.fullmatch(r"(\d{2}/\d{2})(\d{2}/\d{2})", text)
            if not match:
                continue
            try:
                for value in match.groups():
                    datetime.strptime(value + "/2000", "%d/%m/%Y")
            except ValueError:
                continue
            center = (found["top"] + found["bottom"]) / 2
            previous = [b for b in dates if abs((b["top"] + b["bottom"]) / 2 - center) < 4]
            if len(previous) != 2:
                continue
            for old, value in zip(sorted(previous, key=lambda b: b["left"]), match.groups(), strict=True):
                old.update(text=value, confidence=found["confidence"])
    # Re-read ambiguous financial cells from pixels, including a missing decimal separator.
    # "66 88" must not simply be changed to "66.88" by a text parser.
    def financial_cell(box):
        return any(re.fullmatch(r"(?:d[ée]bits?|credits?|cr[ée]dits?|debit|credit)", heading["text"].strip(), re.IGNORECASE)
                   and heading["top"] < box["top"]
                   and heading["left"] - 30 <= box["left"] <= heading["right"] + 55
                   for heading in boxes)

    for box in list(boxes):
        text = box["text"].strip()
        clipped_decimal = re.fullmatch(r"[+-]?\d[\d \u00a0]*[,.]\d", text)
        missing_separator = re.fullmatch(r"[+-]?\d[\d \u00a0]*[ \u00a0]\d{2}", text) and financial_cell(box)
        if not clipped_decimal and not missing_separator:
            continue
        padding = 3 if missing_separator else 1
        x, y = max(0, int((box["left"] - (3 if missing_separator else 2)) * scale)), max(0, int((box["top"] - padding) * scale))
        crop = image[y:min(image.shape[0], int((box["bottom"] + padding) * scale) + 1),
                     x:min(image.shape[1], int((box["right"] + 3) * scale) + 1)]
        if not crop.size:
            continue
        factor = 1 if missing_separator else max(1, 5 / scale)
        crop = cv2.resize(crop, None, fx=factor, fy=factor, interpolation=cv2.INTER_CUBIC)
        crop = cv2.copyMakeBorder(crop, 2, 2, 6, 6, cv2.BORDER_CONSTANT, value=(255, 255, 255))
        # Recognition alone keeps the decimal and last digit in the same financial cell.
        result = next(iter(cell_recognizer().predict(crop[:, :, ::-1]))).json
        result = result.get("res", result)
        text, score = result["rec_text"].strip(), float(result["rec_score"])
        if score >= .9 and re.fullmatch(r"[+-]?\d[\d \u00a0]*[,.]\d{2}", text):
            box.update(text=text, confidence=score)
    return boxes

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
        warnings = []
        # Each request reads the original pixels again; only model weights are reused.
        with inference_lock(cancelled):
            for number in selected:
                if cancelled is not None and cancelled.is_set():
                    raise HTTPException(499, "Lecture annulée.")
                page = pdf[number - 1]
                # Bound image size independently of the uploaded PDF's page dimensions.
                scale = min(3, 3200 / max(page.rect.width, page.rect.height))
                pix = page.get_pixmap(matrix=pymupdf.Matrix(scale, scale), colorspace=pymupdf.csRGB, alpha=False)
                image = np.frombuffer(pix.samples, dtype=np.uint8).reshape(pix.height, pix.width, 3)
                layout = None
                if os.getenv("OCR_PIPELINE", "structure") == "structure" and _layout_available is not False:
                    try:
                        layout = read_layout(image)
                    except Exception as error:
                        logger.warning("Page layout failed (%s); reading original pixels with OCR.", type(error).__name__)
                if layout is None:
                    if os.getenv("OCR_PIPELINE", "structure") == "structure":
                        warnings.append({"code": "LOCAL_LAYOUT_UNAVAILABLE", "message": f"Page {number} : reconstruction des cellules indisponible ; lecture OCR des positions conservée."})
                    result = next(iter(engine().predict(image[:, :, ::-1])))
                    data = result.json.get("res", result.json)
                else:
                    data = layout["overall_ocr_res"]
                if cancelled is not None and cancelled.is_set():
                    raise HTTPException(499, "Lecture annulée.")
                boxes = []
                for text, score, poly in zip(data["rec_texts"], data["rec_scores"], data["rec_polys"], strict=True):
                    if not text.strip():
                        continue
                    coords = np.asarray(poly)
                    left, top = coords.min(axis=0).tolist()
                    right, bottom = coords.max(axis=0).tolist()
                    if not all(math.isfinite(v) for v in (left, top, right, bottom, float(score))):
                        continue
                    boxes.append({"text": text, "left": max(0, left / scale), "right": min(page.rect.width, right / scale),
                                  "top": max(0, top / scale), "bottom": min(page.rect.height, bottom / scale), "confidence": float(score)})
                before_refinement = [box["text"] for box in boxes]
                boxes = refine_ruled_cells(image, scale, boxes)
                tables = []
                if layout is not None:
                    try:
                        tables, rejected = tables_from_layout(layout, scale, page.rect.width, page.rect.height)
                        if rejected:
                            warnings.append({"code": "LOCAL_TABLE_GRID_INVALID", "message": f"Page {number} : {rejected} grille(s) de cellules écartée(s) ; les autres tableaux et les blocs OCR restent disponibles."})
                        # Propagate a crop-verified correction only when its source text is unique.
                        replacements = {old: box["text"] for old, box in zip(before_refinement, boxes, strict=True)
                                        if old != box["text"] and before_refinement.count(old) == 1}
                        for table in tables:
                            table["rows"] = [[replacements.get(cell.strip(), cell) for cell in row] for row in table["rows"]]
                    except (ValueError, TypeError, KeyError):
                        warnings.append({"code": "LOCAL_TABLE_GRID_INVALID", "message": f"Page {number} : structure du tableau écartée ; les textes et leurs positions restent disponibles."})
                    if not tables:
                        warnings.append({"code": "LOCAL_TABLE_GRID_NOT_FOUND", "message": f"Page {number} : aucun tableau exploitable détecté ; adaptation à partir des positions du texte."})
                recognized = {"number": number, "width": page.rect.width, "height": page.rect.height, "boxes": boxes,
                              "tables": tables or None,
                              "model": f"PP-OCRv6_{os.getenv('OCR_MODEL_SIZE', 'small')}" + (" + PP-StructureV3" if layout is not None else "")}
                pages.append(recognized)
        return {"method": "PADDLE_OCR", "pages": pages, "sourcePageCount": len(pdf), "warnings": warnings,
                "pipelineVersion": "local-layout-v1"}

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
