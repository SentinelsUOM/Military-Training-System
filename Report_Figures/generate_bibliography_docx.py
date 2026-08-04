"""Generate a Word (.docx) version of MODULE2_MASTER_BIBLIOGRAPHY.md for
presenting to supervisors/evaluators. Real clickable hyperlinks (not just
plain text URLs). Re-run after editing the bibliography content.
"""
import os
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), 'MODULE2_MASTER_BIBLIOGRAPHY.docx')

INK = RGBColor(0x0b, 0x0b, 0x0b)
INK2 = RGBColor(0x52, 0x51, 0x4e)
MUTED = RGBColor(0x89, 0x87, 0x81)
LINK_BLUE = '2a78d6'
FLAG_ORANGE = RGBColor(0xeb, 0x68, 0x34)


def add_hyperlink(paragraph, text, url):
    part = paragraph.part
    r_id = part.relate_to(url, 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink', is_external=True)
    hyperlink = OxmlElement('w:hyperlink')
    hyperlink.set(qn('r:id'), r_id)

    new_run = OxmlElement('w:r')
    rPr = OxmlElement('w:rPr')
    color = OxmlElement('w:color')
    color.set(qn('w:val'), LINK_BLUE)
    rPr.append(color)
    u = OxmlElement('w:u')
    u.set(qn('w:val'), 'single')
    rPr.append(u)
    new_run.append(rPr)
    t = OxmlElement('w:t')
    t.text = text
    new_run.append(t)
    hyperlink.append(new_run)
    paragraph._p.append(hyperlink)
    return hyperlink


def entry(doc, num, citation, url_or_note, relevance, flag=None):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(2)
    run = p.add_run(f'{num}. ')
    run.bold = True
    run = p.add_run(citation)
    run.bold = True

    p2 = doc.add_paragraph()
    p2.paragraph_format.left_indent = Inches(0.25)
    p2.paragraph_format.space_after = Pt(2)
    if url_or_note.startswith('http'):
        add_hyperlink(p2, url_or_note, url_or_note)
    else:
        r = p2.add_run(url_or_note)
        r.italic = True
        r.font.color.rgb = MUTED

    p3 = doc.add_paragraph()
    p3.paragraph_format.left_indent = Inches(0.25)
    p3.paragraph_format.space_after = Pt(10)
    r = p3.add_run(relevance)
    r.font.color.rgb = INK2
    r.font.size = Pt(10)

    if flag:
        p4 = doc.add_paragraph()
        p4.paragraph_format.left_indent = Inches(0.25)
        p4.paragraph_format.space_after = Pt(10)
        r = p4.add_run('⚠ ' + flag)
        r.font.color.rgb = FLAG_ORANGE
        r.italic = True
        r.font.size = Pt(9.5)


doc = Document()
style = doc.styles['Normal']
style.font.name = 'Segoe UI'
style.font.size = Pt(10.5)
style.font.color.rgb = INK
sec = doc.sections[0]
sec.left_margin = sec.right_margin = Inches(0.9)

title = doc.add_heading('Module 2 — Master Research Bibliography', level=0)
title.runs[0].font.color.rgb = INK
sub = doc.add_paragraph('43 sources, consolidated across the Literature Review, Evaluation Methodology, Selection Justification, and survey instruments — links verified 2026-08-03')
sub.runs[0].italic = True
sub.runs[0].font.color.rgb = INK2
sub.runs[0].font.size = Pt(11.5)
doc.add_paragraph()

doc.add_paragraph(
    'Every research source that backs Module 2\'s design and evaluation, consolidated from '
    'Literature_Review_Module2.docx, MODULE2_EVALUATION_METHODOLOGY.md, '
    'MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md, and the survey instruments implemented in '
    'sentinels-aar/lib/. Links were verified via live web search, not guessed — where a real '
    'link could not be confirmed, that is stated explicitly rather than inventing one.'
)
p = doc.add_paragraph()
r = p.add_run('Two flagged discrepancies, kept visible rather than silently resolved:')
r.bold = True
doc.add_paragraph(
    '#12 (DiVA thesis): the repo\'s reference list describes it generically; the confirmed real '
    'title/authors differ slightly — still on-topic, worth updating the citation text.',
    style='List Bullet'
)
doc.add_paragraph(
    '#20 (Almeida et al. FPS AI review): the confirmed DOI/volume/issue/article-number match '
    'the citation, but the author list and exact title on record differ from what\'s actually '
    'on that DOI. Re-check against the original source before citing in front of an evaluator.',
    style='List Bullet'
)

# ── Group 1 ──────────────────────────────────────────────────────────────────
doc.add_heading('Group 1 — Squad-tactics doctrine & AI architecture', level=1)
doc.add_paragraph('(backs Literature_Review_Module2.docx — how the terrorist squad reacts, searches, and coordinates)').runs[0].italic = True

entry(doc, 1, 'Orkin, J. (2006). "Three States and a Plan: The AI of F.E.A.R." GDC.',
      'https://www.gamedevs.org/uploads/three-states-plan-ai-of-fear.pdf',
      "The single most load-bearing citation in Module 2's design: F.E.A.R.'s GOAP + two-layer "
      "squad coordination is the direct model for Squad.cs / TerroristController.cs.")
entry(doc, 2, 'U.S. Army (1996). CALL Handbook 96-3, "Battle Drill 2: React to Contact."',
      'https://www.globalsecurity.org/military/library/report/call/call_96-3_cpt2bd2.htm',
      'Source of the "return fire within 3 seconds, converge on contact" doctrine that drives '
      "the squad's react-to-contact behaviour.")
entry(doc, 3, 'FM 3-21.71 — Mechanized Infantry Platoon and Squad.',
      'https://www.globalsecurity.org/military/library/policy/army/fm/3-21-71/index.html',
      'Battle Drills (React to Contact, Break Contact, Clear a Building) — the doctrinal basis '
      'for "pursuit is default, disengagement is deliberate."')
entry(doc, 4, 'FM 3-06.11 — Combined Arms Operations in Urban Terrain.',
      'https://www.globalsecurity.org/military/library/policy/army/fm/3-06-11/ch9.htm',
      "Systematic building-clearing doctrine; informs the squad's room-clearing / search behaviour.")
entry(doc, 5, 'FM 7-8 — Infantry Rifle Platoon and Squad.',
      'https://www.globalsecurity.org/military/library/policy/army/fm/7-8/index.html',
      'Battle drills; secondary corroboration for the react-to-contact model.')
entry(doc, 6, 'ATP 3-21.8 — Infantry Rifle Platoon and Squad.',
      'No verified free authoritative link found (only unofficial mirrors — not independently confirmed).',
      'Source of the standard fire-command (ADDRAC) format the shared-contact reporting is modelled on.')
entry(doc, 7, 'NIST — "Last Known Position (LKP) / Point Last Seen (PLS)."',
      'https://www.nist.gov/glossary-term/26441',
      "Defines the search-anchor concept Squad.cs's persistent-hunt system (PointLastSeen) is built around.")
entry(doc, 8, 'Phillips, K. et al. (2014). "Wilderness Search Strategy and Tactics." Wilderness & Environmental Medicine, 25(2).',
      'https://doi.org/10.1016/j.wem.2014.02.006',
      'Peer-reviewed source for the expanding-search-radius model (radius = speed × elapsed time) '
      'used in Squad.NextSearchPoint.')
entry(doc, 9, 'SARMath — "Initial Planning Point (IPP)."',
      'http://www.sarmath.com/terms/initial-planning-point',
      'Secondary source for the same expanding-circle search math.')
entry(doc, 10, 'The Dupuy Institute (2018). "Breakpoints in U.S. Army Doctrine."',
      'https://dupuyinstitute.org/2018/04/18/breakpoints-in-u-s-army-doctrine/',
      'Operations-research basis for the morale-collapse model (leadership loss, casualties, '
      'surprise — not a simple casualty counter).')
entry(doc, 11, '"Suppressive fire." Wikipedia (citing USMC doctrine).',
      'https://en.wikipedia.org/wiki/Suppressive_fire',
      'Grey-literature source for "suppression is psychological, not lethal" — the principle '
      'behind cover/chokepoint tactics.')
entry(doc, 12, 'Sjöblom, M. & Svala, R. (2025). "Multi-Agent Performance Using Goal-Oriented Action Planning (GOAP) in a Procedural Game." DiVA thesis.',
      'https://www.diva-portal.org/smash/record.jsf?pid=diva2:1972169',
      "Corroborates Orkin's two-layer squad architecture from a second, independent source.",
      flag="The repo's reference list describes this generically as 'thesis on cooperative game AI'; "
           "confirmed real title/authors shown above — still on-topic, worth updating the citation text.")

# ── Group 2 ──────────────────────────────────────────────────────────────────
doc.add_heading('Group 2 — Believability / Turing-test evaluation methodology', level=1)
doc.add_paragraph('(backs MODULE2_EVALUATION_METHODOLOGY.md — how to prove the AI "seems intelligent")').runs[0].italic = True

entry(doc, 13, 'Hingston, P. (2009). "A Turing test for computer game bots." IEEE ToCIAIG, 1(3).',
      'https://ieeexplore.ieee.org/document/5247069/',
      'Founding paper of the "humanness ratio" Turing-test paradigm the believability study is built on.')
entry(doc, 14, 'Schrum, J. et al. "Optimising Humanness... UT2004." Springer 2017.',
      'https://link.springer.com/chapter/10.1007/978-3-319-59147-6_58',
      'The 2K BotPrize competition write-up — precedent for judge-based humanness scoring.')
entry(doc, 15, 'Gorman, B., Thurau, C., Bauckhage, C., Humphrys, M. (2006). "Believability Testing and Bayesian Imitation in Interactive Computer Games." SAB.',
      'https://link.springer.com/chapter/10.1007/11840541_54',
      'Source of the main believability instrument: the 5-point Human↔Artificial gradient + '
      'experience-weighted index.')
entry(doc, 16, 'Even, C., Bosser, A-G., Buche, C. (2021). Frontiers in Computer Science.',
      'https://www.frontiersin.org/journals/computer-science/articles/10.3389/fcomp.2021.774763/full',
      'The 7-characteristic protocol for designing a defensible believability study (judge count, '
      'expertise, clip duration, etc.).')
entry(doc, 17, 'Togelius, Yannakakis, Karakovskiy, Shaker — "Assessing believability," in Believable Bots (Springer, 2012).',
      'https://link.springer.com/chapter/10.1007/978-3-642-32323-2_9',
      'Mario AI Turing-test track — precedent for external-observer (spectator) judging over '
      'participatory judging.')
entry(doc, 18, 'Justesen, N. et al. "Human-like Bots for Tactical Shooters Using Compute-Efficient Sensors." arXiv:2501.00078.',
      'https://arxiv.org/abs/2501.00078',
      'Argues human-likeness, not win-rate, is the right target for tactical-shooter AI — directly '
      'justifies the "believability over difficulty" framing.')
entry(doc, 19, 'Durst, D. et al. "Learning to Move Like Professional Counter-Strike Players." arXiv:2408.13934.',
      'https://arxiv.org/abs/2408.13934',
      'TrueSkill-based human-likeness rating; behaviour-distribution / mistake / teamwork metrics '
      '— the objective-metric taxonomy the telemetry design borrows from.')
entry(doc, 20, 'Almeida, P., Carvalho, V., Simões, A. (2023). Algorithms, 16(7):323.',
      'https://www.mdpi.com/1999-4893/16/7/323',
      'Source of the game-based-metrics-over-RL-reward taxonomy and the reusable 20-participant '
      'survey design.',
      flag='Confirmed DOI/volume/issue/article-number match the citation, but the author list and '
           'exact title on record differ from what is actually on that DOI. Re-check before citing.')

# ── Group 3 ──────────────────────────────────────────────────────────────────
doc.add_heading('Group 3 — Player-experience, workload & VR questionnaires', level=1)
doc.add_paragraph('(the actual instruments the dashboard survey implements — lib/aiEval.js, lib/simTlx.js)').runs[0].italic = True

entry(doc, 21, 'Bartneck, C., Kulić, D., Croft, E., Zoghbi, S. (2009). "Measurement instruments for anthropomorphism, animacy, likeability, perceived intelligence, and perceived safety of robots." Int J Social Robotics, 1.',
      'https://doi.org/10.1007/s12369-008-0001-3',
      'Godspeed — the exact source of the "Perceived Intelligence" and "Animacy" survey items.')
entry(doc, 22, 'Schrepp, M., Hinderks, A., Thomaschewski, J. (2017). UEQ-S short form. IJIMAI, 4(6).',
      'https://doi.org/10.9781/ijimai.2017.09.001',
      'Source of the Pragmatic / Hedonic UEQ-S questions.')
entry(doc, 23, 'Laugwitz, B., Held, T., Schrepp, M. (2008). Original UEQ. USAB / LNCS 5298.',
      'https://doi.org/10.1007/978-3-540-89350-9_6',
      'The full-length instrument UEQ-S is a short form of.')
entry(doc, 24, 'Johnson, D., Gardner, J., Perry, R. (2018). IJHCS.',
      'https://doi.org/10.1016/j.ijhcs.2018.05.003',
      "Validates (and flags caveats for) PENS/GEQ — cited in the methodology doc's honesty note "
      'about instrument limitations.')
entry(doc, 25, 'Denisova, A., Nordin, A.I., Cairns, P. (2016). CHI PLAY.',
      'https://doi.org/10.1145/2967934.2968095',
      "Shows IEQ/GEQ/PENS converge strongly — the reason the survey battery doesn't stack all of them.")
entry(doc, 26, 'IJsselsteijn, W., de Kort, Y., Poels, K. — "The Game Experience Questionnaire" (FUGA project deliverable).',
      'https://pure.tue.nl/ws/files/21666952/Fuga_d3.3.pdf',
      "The GEQ itself (distinct from the other 'GEQ' — Brockmyer's Game Engagement Questionnaire — "
      'a naming collision the methodology doc explicitly flags).')
entry(doc, 27, 'Jennett, C. et al. (2008). "Measuring and defining the experience of immersion in games." IJHCS, 66(9).',
      'https://doi.org/10.1016/j.ijhcs.2008.04.004',
      'Source of the IEQ / IEQ-SF immersion questionnaire.')
entry(doc, 28, 'Carpinella, C.M., Wyman, A.B., Perez, M.A., Stroessner, S.J. (2017). "RoSAS." HRI 2017.',
      'https://doi.org/10.1145/2909824.3020208',
      'Robotic Social Attributes Scale — an alternative/supplementary instrument to Godspeed.')
entry(doc, 29, 'Kennedy, R.S., Lane, N.E., Berbaum, K.S., Lilienthal, M.G. (1993). "Simulator Sickness Questionnaire." Int J Aviation Psychology, 3(3).',
      'https://doi.org/10.1207/s15327108ijap0303_3',
      'The VR safety/comfort control instrument (SSQ).')
entry(doc, 30, 'Hart, S.G., Staveland, L.E. (1988). NASA-TLX chapter.',
      'https://doi.org/10.1016/S0166-4115(08)62386-9',
      'The cognitive-workload instrument the player-experience battery includes.')
entry(doc, 31, 'Harris, D.J., Wilson, M.R., Vine, S.J. (2020). "SIM-TLX." Virtual Reality, 24.',
      'https://doi.org/10.1007/s10055-019-00422-9',
      'The actual instrument the "Workload" tab (SimTlxTab.jsx) implements.')
entry(doc, 32, 'Harris, D.J., Bird, K.J., Smart, P.A., Wilson, M.R., Vine, S.J. (2020). Frontiers in Psychology, art. 605.',
      'https://pmc.ncbi.nlm.nih.gov/articles/PMC7136518/',
      'The four-fidelity-subtype (physical/psychological/affective/ergonomic) framework, and the '
      'key "face validity ≠ construct validity" argument the SME-review plan (Part B5) is built on.')

# ── Group 4 ──────────────────────────────────────────────────────────────────
doc.add_heading('Group 4 — NPC responder-selection technique grounding', level=1)
doc.add_paragraph('(backs MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md — the NPCSelector weighted-scoring defence)').runs[0].italic = True

entry(doc, 33, 'Mark, D. (2009). Behavioral Mathematics for Game AI.',
      'https://archive.org/details/behavioralmathem0000mark',
      "Foundational utility-AI text; the technique NPCSelector's scoring formula follows.")
entry(doc, 34, 'Dill, K. & Martin, L. (2011). "A Game AI Approach to Autonomous Control of Virtual Characters." I/ITSEC.',
      'https://course.ccs.neu.edu/cs5150f13/readings/dill_granny.pdf',
      'The single most on-domain citation: utility-based AI for military-training virtual '
      'characters specifically.')
entry(doc, 35, 'Dill, K. (2012). "Design Patterns for the Configuration of Utility-Based AI." I/ITSEC.',
      'https://course.ccs.neu.edu/cs5150f13/readings/dill_designpatterns.pdf',
      'Structuring/configuring the kind of scoring system NPCSelector implements.')
entry(doc, 36, 'Lewis, M. (2015). "Choosing Effective Utility-Based Considerations." Game AI Pro 3, ch. 13.',
      'Book chapter — no free URL found.',
      'Names distance and line-of-sight, by name, as standard scoring considerations.')
entry(doc, 37, 'Welsh, R. "Crytek\'s Target Tracks Perception System." Game AI Pro.',
      'Book chapter — no free URL found.',
      'Perception incorporated into target scoring in a shipped commercial engine.')
entry(doc, 38, 'ACM Computing Surveys (2024). "A Systematic Literature Review on Multi-Robot Task Allocation."',
      'Paywalled DOI — not separately re-verified this pass.',
      'Source of the distance + capability + availability three-factor pattern the Role/State '
      'factors mirror.')
entry(doc, 39, 'Weapon-Target Assignment problem — Wikipedia overview.',
      'https://en.wikipedia.org/wiki/Weapon-target_assignment_problem',
      'Military OR precedent for distance + capability-based assignment.')

# ── Group 5 ──────────────────────────────────────────────────────────────────
doc.add_heading('Group 5 — Statistics methodology', level=1)
doc.add_paragraph("(backs the Friedman/Wilcoxon/Kendall's W panel in sentinels-aar/lib/stats.js and Section 6.5-6.7 of MODULE2_FULL_REPORT.md)").runs[0].italic = True

entry(doc, 40, 'Friedman, M. (1937). JASA, 32(200).',
      'https://www.jstor.org/stable/2279372',
      'The Friedman test itself.')
entry(doc, 41, 'Wilcoxon, F. (1945). Biometrics Bulletin, 1(6).',
      'https://www.jstor.org/stable/3001968',
      'The Wilcoxon signed-rank test itself.')
entry(doc, 42, 'Kendall, M.G. & Babington Smith, B. (1939). Annals of Mathematical Statistics, 10(3).',
      'https://doi.org/10.1214/aoms/1177732186',
      "Source of Kendall's W — the effect-size measure for the Friedman omnibus test.")
entry(doc, 43, 'Dunn, O.J. (1961). JASA, 56(293).',
      'https://doi.org/10.1080/01621459.1961.10482090',
      'Source of the Bonferroni correction used on the 3 pairwise post-hoc comparisons.')

doc.save(OUT)
print('Word bibliography written to', OUT)
