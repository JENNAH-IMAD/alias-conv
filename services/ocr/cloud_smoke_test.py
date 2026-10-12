"""Test the official hosted job API using a fictional repo fixture; never log credentials/results URLs."""
import json
import time
from pathlib import Path
import requests

root = Path(__file__).resolve().parents[2]
settings = json.loads((root / 'backend/BankStatementConverter.API/appsettings.Development.json').read_text(encoding='utf-8-sig'))
token = settings['Ocr']['CloudToken']
model = settings['Ocr'].get('CloudModel', 'PaddleOCR-VL-1.6')
options = {'useDocOrientationClassify': False, 'useDocUnwarping': False,
           ('useTextlineOrientation' if model == 'PP-OCRv6' else 'useChartRecognition'): False}
fixture = next(root.glob('relv*/releve_bmce_exemple_01.pdf'))
jobs = 'https://paddleocr.aistudio-app.com/api/v2/ocr/jobs'
headers = {'Authorization': f'Bearer {token}'}
with fixture.open('rb') as file:
    response = requests.post(jobs, headers=headers, files={'file': ('releve.pdf', file, 'application/pdf')},
                             data={'model': model, 'optionalPayload': json.dumps(options)}, timeout=60)
print(f'Soumission PaddleOCR : HTTP {response.status_code}', flush=True)
response.raise_for_status()
data = response.json()['data']
job_id = data['jobId']
end = time.monotonic() + 600
while time.monotonic() < end:
    response = requests.get(f'{jobs}/{job_id}', headers=headers, timeout=60)
    response.raise_for_status()
    data = response.json()['data']
    state = data['state']
    print(f"État : {state}, pages : {data.get('extractProgress', {}).get('extractedPages', '?')}", flush=True)
    if state == 'failed':
        raise RuntimeError('Le fournisseur a échoué à lire le PDF de test.')
    if state == 'done':
        result = requests.get(data['resultUrl']['jsonUrl'], timeout=60)
        result.raise_for_status()
        (root / 'output').mkdir(exist_ok=True)
        (root / 'output/paddle-vl-result.jsonl').write_text(result.text, encoding='utf-8')
        print('Résultat JSONL enregistré dans output/paddle-vl-result.jsonl.', flush=True)
        break
    if state not in ('pending', 'running'):
        raise RuntimeError('État API inattendu.')
    time.sleep(5)
else:
    raise TimeoutError('Lecture PaddleOCR supérieure à 600 secondes.')
