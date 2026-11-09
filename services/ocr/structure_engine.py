"""Local document layout and cell reconstruction, independent of banking rules."""
import math
import os
from functools import lru_cache
from html.parser import HTMLParser


@lru_cache(maxsize=1)
def structure_engine():
    from paddleocr import PPStructureV3
    size = os.getenv("OCR_MODEL_SIZE", "small")
    if size not in {"tiny", "small", "medium"}:
        raise ValueError("Invalid OCR_MODEL_SIZE")
    return PPStructureV3(
        device="cpu", enable_mkldnn=False,
        cpu_threads=max(1, min(8, int(os.getenv("OCR_CPU_THREADS", "2")))),
        layout_detection_model_name=os.getenv("OCR_LAYOUT_MODEL", "PP-DocLayout-M"),
        text_detection_model_name=f"PP-OCRv6_{size}_det",
        text_recognition_model_name=f"PP-OCRv6_{size}_rec",
        text_det_limit_type="max",
        text_det_limit_side_len=max(960, min(2560, int(os.getenv("OCR_DETECTION_MAX_SIDE", "1280")))),
        use_doc_orientation_classify=False, use_doc_unwarping=False,
        use_textline_orientation=False, use_region_detection=False,
        use_table_recognition=True, use_formula_recognition=False,
        use_seal_recognition=False, use_chart_recognition=False,
    )


class CellGrid(HTMLParser):
    """Expand spans into an explicit grid; a printed value occurs only once."""
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.grid, self.occupied = {}, set()
        self.row, self.column, self.cell = -1, 0, None

    def handle_starttag(self, tag, attrs):
        if tag == "tr":
            self.row += 1
            self.column = 0
        elif tag in {"td", "th"}:
            if self.row < 0 or self.cell is not None:
                raise ValueError("Malformed table")
            attributes = dict(attrs)
            span = tuple(int(attributes.get(key, "1")) for key in ("rowspan", "colspan"))
            if not all(1 <= n <= 64 for n in span):
                raise ValueError("Invalid cell span")
            self.cell = [[], span]
        elif tag == "br" and self.cell is not None:
            self.cell[0].append("\n")

    def handle_data(self, data):
        if self.cell is not None:
            self.cell[0].append(data)

    def handle_endtag(self, tag):
        if tag not in {"td", "th"} or self.cell is None:
            return
        while (self.row, self.column) in self.occupied:
            self.column += 1
        text, (height, width) = self.cell
        if self.column + width > 64 or len(self.occupied) + height * width > 200000:
            raise ValueError("Invalid table grid")
        for r in range(self.row, self.row + height):
            for c in range(self.column, self.column + width):
                if (r, c) in self.occupied:
                    raise ValueError("Overlapping cells")
                self.occupied.add((r, c))
                self.grid[r, c] = "".join(text).strip() if (r, c) == (self.row, self.column) else ""
        self.column += width
        self.cell = None

    def rows(self):
        if self.cell is not None:
            raise ValueError("Unclosed cell")
        if not self.grid:
            return []
        height = max(r for r, _ in self.grid) + 1
        width = max(c for _, c in self.grid) + 1
        rows = [[self.grid.get((r, c), "") for c in range(width)] for r in range(height)]
        # Empty span placeholders have no printed field or value; keep every nonempty column.
        columns = [c for c in range(width) if any(row[c].strip() for row in rows)]
        return [[row[c] for c in columns] for row in rows]


def read_layout(image):
    result = next(iter(structure_engine().predict(
        image[:, :, ::-1], use_table_orientation_classify=False,
        # The pinned PP-Structure pipeline has no nested OCR instance in table recognition.
        # Cell-wise re-recognition would dereference that missing instance on narrow tables.
        # Match the fresh page OCR to cells; ambiguous values are re-read by our crop stage.
        use_ocr_results_with_table_cells=False)))
    data = result.json
    return data.get("res", data)


def tables_from_layout(data, scale, width, height):
    tables = []
    rejected = 0
    for table in data.get("table_res_list", []):
        try:
            parsed = parse_table(table, scale, width, height)
            if parsed is not None:
                tables.append(parsed)
        except (ValueError, TypeError, KeyError):
            rejected += 1
    return tables, rejected


def parse_table(table, scale, width, height):
    parser = CellGrid()
    parser.feed(table.get("pred_html", ""))
    parser.close()
    rows = parser.rows()
    if not rows:
        return None
    cells = table.get("cell_box_list", [])
    xs, ys = [], []
    for cell in cells:
        # Models return either xyxy rectangles or quadrilateral coordinates.
        values = [float(n) for point in cell for n in point] if cell and isinstance(cell[0], list) else [float(n) for n in cell]
        if len(values) not in {4, 8} or not all(math.isfinite(n) for n in values):
            raise ValueError("Invalid cell coordinates")
        xs.extend(values[::2])
        ys.extend(values[1::2])
    bounds = None
    if xs and ys:
        bounds = {"left": max(0, min(xs) / scale), "top": max(0, min(ys) / scale),
                  "right": min(width, max(xs) / scale), "bottom": min(height, max(ys) / scale)}
        if bounds["left"] >= bounds["right"] or bounds["top"] >= bounds["bottom"]:
            raise ValueError("Empty table region")
    return {"rows": rows, "bounds": bounds}
