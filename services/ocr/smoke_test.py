"""Exercise actual PaddleOCR on a PDF with image-only pages. Uses synthetic bank fixtures."""
import json
import sys
from pathlib import Path
import httpx
import pymupdf

root = Path(__file__).resolve().parents[2]
fixture = next(root.glob("relv*/releve_credit_agricole_exemple_02.pdf"))
with pymupdf.open(fixture) as original, pymupdf.open() as scan:
    for page in original:
        image = page.get_pixmap(matrix=pymupdf.Matrix(2, 2), alpha=False)
        target = scan.new_page(width=page.rect.width, height=page.rect.height)
        target.insert_image(target.rect, stream=image.tobytes("png"))
    scan_bytes = scan.tobytes(deflate=True)
with httpx.Client(timeout=600) as client:
    response = client.post("http://127.0.0.1:8091/extract", files={"file": ("scan-synthetique.pdf", scan_bytes, "application/pdf")})
    response.raise_for_status()
    output = root / "output/ocr-scan-result.json"
    output.write_text(json.dumps(response.json(), ensure_ascii=False), encoding="utf-8")
    print(f"PaddleOCR : {len(response.json()['pages'])} page(s) lues, résultat dans output/ocr-scan-result.json")
