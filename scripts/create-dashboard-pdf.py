from pathlib import Path
from decimal import Decimal
from reportlab.pdfgen import canvas
from reportlab.platypus import SimpleDocTemplate, Paragraph, Table, TableStyle, Spacer, PageBreak
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import fitz
from pypdf import PdfReader

root = Path(__file__).resolve().parents[1]
target = root / 'frontend/public/resources/modele-releve-alias.pdf'
pdfmetrics.registerFont(TTFont('Alias', 'C:/Windows/Fonts/arial.ttf'))
pdfmetrics.registerFont(TTFont('AliasBold', 'C:/Windows/Fonts/arialbd.ttf'))
style = ParagraphStyle('body', fontName='Alias', fontSize=9, leading=12, textColor=colors.HexColor('#142c45'))
title = ParagraphStyle('title', parent=style, fontName='AliasBold', fontSize=16, leading=21)
small = ParagraphStyle('small', parent=style, fontSize=8, leading=11)
def p(text, s=style): return Paragraph(text, s)
def money(value): return f'{value:,.2f}'.replace(',', ' ').replace('.', ',')
rows = []
for index in range(1, 25):
    credit = index % 2 == 0
    label = 'Virement reçu — exemple fictif' if credit else 'Paiement facture — exemple fictif'
    if index == 5: label += ' : libellé complet conservé, y compris une référence intégrée au texte REF-EXEMPLE'
    rows.append([f'{index:02}/01/2026', f'{index:02}/01/2026', p(label), '' if credit else '12,50', '100,00' if credit else ''])
head = ['Date opération', 'Date valeur', 'Libellé', 'Débit', 'Crédit']
def table(part):
    value = Table([head] + part, colWidths=[97, 97, 366, 109, 109], repeatRows=1, hAlign='LEFT')
    value.setStyle(TableStyle([
        ('FONTNAME', (0, 0), (-1, -1), 'Alias'), ('FONTNAME', (0, 0), (-1, 0), 'AliasBold'),
        ('FONTSIZE', (0, 0), (-1, -1), 9), ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#183e61')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.white), ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, colors.HexColor('#f1f5fa')]),
        ('LINEBELOW', (0, 0), (-1, -1), .4, colors.HexColor('#d4dee9')),
        ('TOPPADDING', (0, 0), (-1, -1), 7), ('BOTTOMPADDING', (0, 0), (-1, -1), 7),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'), ('ALIGN', (3, 1), (-1, -1), 'RIGHT'),
    ]))
    return value
story = [p('RELEVÉ BANCAIRE — MODÈLE ALIAS', title), p('EXEMPLE FICTIF — SANS VALEUR BANCAIRE', small), Spacer(1, 10),
         p('Banque : Banque exemple | Titulaire : Client exemple | Compte : 000012345678'),
         p('Relevé : EXEMPLE-2026-01 | Période : 01/01/2026 — 31/01/2026 | Devise : MAD'), Spacer(1, 12),
         p('Solde de départ du relevé : 1 000,00 MAD'), Spacer(1, 10), table(rows[:12]), PageBreak(),
         p('RELEVÉ BANCAIRE — MODÈLE ALIAS', title),
         p('Compte : 000012345678 | Période : 01/01/2026 — 31/01/2026 | MAD · Exemple fictif', small),
         Spacer(1, 14), table(rows[12:]), Spacer(1, 12),
         p('Total débits : 150,00 MAD | Total crédits : 1 200,00 MAD'),
         Spacer(1, 8), p('Solde final du relevé : 2 050,00 MAD'),
         Spacer(1, 8), p('Contrôle de cet exemple : 1 000,00 + 1 200,00 − 150,00 = 2 050,00 MAD.', small)]
def footer(c, doc):
    c.setFont('Alias', 8)
    c.setFillColor(colors.HexColor('#68798b'))
    c.drawString(32, 22, 'Copie structurée du document source · Modèle fictif de présentation')
    c.drawRightString(landscape(A4)[0] - 32, 22, f'Page {doc.page} / 2')
doc = SimpleDocTemplate(str(target), pagesize=landscape(A4), leftMargin=32, rightMargin=32, topMargin=28, bottomMargin=40)
doc.build(story, onFirstPage=footer, onLaterPages=footer)
reader = PdfReader(target)
assert len(reader.pages) == 2
text = '\n'.join(page.extract_text() for page in reader.pages)
assert text.count('Solde de départ du relevé') == 1
assert text.count('Solde final du relevé') == 1
preview = root / 'output/dashboard-pdf-preview'
preview.mkdir(parents=True, exist_ok=True)
with fitz.open(target) as pdf:
    for index, page in enumerate(pdf):
        page.get_pixmap(matrix=fitz.Matrix(1.3, 1.3)).save(preview / f'page-{index+1}.png')
print('Modèle PDF créé : 2 pages, 24 opérations fictives, 5 colonnes, 2 soldes.')
