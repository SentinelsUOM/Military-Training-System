# -*- coding: utf-8 -*-
"""
Renders module2_complete_explainer_content.py's BLOCKS into three synchronized
outputs:
    MODULE2_COMPLETE_EXPLAINER.md
    MODULE2_COMPLETE_EXPLAINER.docx
    MODULE2_COMPLETE_EXPLAINER.pdf

Reuses the exact same render_md/render_docx/render_pdf renderers as
generate_briefing.py, so this document matches the look of the evaluation
briefing. To update: edit BLOCKS in module2_complete_explainer_content.py,
then re-run this script.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)

from module2_complete_explainer_content import BLOCKS, LAST_UPDATED
from generate_briefing import render_md, render_docx, render_pdf

OUT_MD   = os.path.join(ROOT, 'MODULE2_COMPLETE_EXPLAINER.md')
OUT_DOCX = os.path.join(ROOT, 'MODULE2_COMPLETE_EXPLAINER.docx')
OUT_PDF  = os.path.join(ROOT, 'MODULE2_COMPLETE_EXPLAINER.pdf')

if __name__ == '__main__':
    render_md(BLOCKS, OUT_MD)
    render_docx(BLOCKS, OUT_DOCX)
    render_pdf(BLOCKS, OUT_PDF)
    print(f'\nAll three outputs generated (content last updated {LAST_UPDATED}).')
