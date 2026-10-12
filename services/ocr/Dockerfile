FROM python:3.12-slim
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 libgl1 libglib2.0-0 && rm -rf /var/lib/apt/lists/*
COPY services/ocr/requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY services/ocr/app.py ./
ENV PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK=True
ENV WEB_CONCURRENCY=2 OCR_CPU_THREADS=2
EXPOSE 8091
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8091", "--no-access-log"]
