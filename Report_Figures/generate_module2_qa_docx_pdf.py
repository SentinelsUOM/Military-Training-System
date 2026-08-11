# -*- coding: utf-8 -*-
"""
Same generic markdown -> docx/pdf converter as generate_module1_qa_docx_pdf.py,
just pointed at MODULE2_EVALUATOR_QA_SCRIPT.md instead. Reuses that script's
parser/renderer functions directly rather than duplicating them.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)

from generate_module1_qa_docx_pdf import parse_markdown, render_docx, render_pdf

SRC = os.path.join(ROOT, 'MODULE2_EVALUATOR_QA_SCRIPT.md')
OUT_DOCX = os.path.join(ROOT, 'MODULE2_EVALUATOR_QA_SCRIPT.docx')
OUT_PDF = os.path.join(ROOT, 'MODULE2_EVALUATOR_QA_SCRIPT.pdf')

if __name__ == '__main__':
    with open(SRC, 'r', encoding='utf-8') as f:
        text = f.read()
    blocks = parse_markdown(text)
    render_docx(blocks, OUT_DOCX)
    render_pdf(blocks, OUT_PDF)
