"""Generate a Word (.docx) version of MODULE2_ABLATION_STUDY_REPORT.md for
presenting to supervisors/evaluators. Embeds the same 5 charts and headline
numbers table as the markdown report. Re-run after regenerating the charts or
editing the report text so the two stay in sync.
"""
import os
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_ALIGN_VERTICAL
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), 'MODULE2_ABLATION_STUDY_REPORT.docx')

INK = RGBColor(0x0b, 0x0b, 0x0b)
INK2 = RGBColor(0x52, 0x51, 0x4e)
MUTED = RGBColor(0x89, 0x87, 0x81)
BLUE = RGBColor(0x2a, 0x78, 0xd6)
HEADER_FILL = 'DCE6F5'


def set_cell_shading(cell, hex_color):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), hex_color)
    tcPr.append(shd)


def add_table(doc, headers, rows, bold_first_col=False):
    table = doc.add_table(rows=1, cols=len(headers))
    table.style = 'Table Grid'
    hdr = table.rows[0].cells
    for i, h in enumerate(headers):
        hdr[i].text = h
        set_cell_shading(hdr[i], HEADER_FILL)
        for p in hdr[i].paragraphs:
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            for r in p.runs:
                r.bold = True
                r.font.size = Pt(9.5)
    for row in rows:
        cells = table.add_row().cells
        for i, val in enumerate(row):
            cells[i].text = str(val)
            for p in cells[i].paragraphs:
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER if i > 0 else WD_ALIGN_PARAGRAPH.LEFT
                for r in p.runs:
                    r.font.size = Pt(9.5)
                    if bold_first_col and i == 0:
                        r.bold = True
    doc.add_paragraph()
    return table


def add_code_block(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Inches(0.3)
    run = p.add_run(text)
    run.font.name = 'Consolas'
    run.font.size = Pt(9)
    run.font.color.rgb = INK2
    return p


def add_image(doc, filename, caption, width=6.2):
    doc.add_picture(os.path.join(HERE, filename), width=Inches(width))
    doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap = doc.add_paragraph(caption)
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    for r in cap.runs:
        r.italic = True
        r.font.size = Pt(9)
        r.font.color.rgb = MUTED
    doc.add_paragraph()


doc = Document()

# ── Base style ────────────────────────────────────────────────────────────────
style = doc.styles['Normal']
style.font.name = 'Segoe UI'
style.font.size = Pt(10.5)
style.font.color.rgb = INK
sec = doc.sections[0]
sec.left_margin = sec.right_margin = Inches(0.9)

# ── Title ─────────────────────────────────────────────────────────────────────
title = doc.add_heading('Module 2 — NPC Selection Ablation Study', level=0)
title.runs[0].font.color.rgb = INK
sub = doc.add_paragraph('Full Results — live run, Unity 6000.3.1f1, 2026-08-01')
sub.runs[0].italic = True
sub.runs[0].font.color.rgb = INK2
sub.runs[0].font.size = Pt(12)
doc.add_paragraph()

# ── Intro ─────────────────────────────────────────────────────────────────────
doc.add_paragraph(
    'What this measures. EventManager routes every "targeted" scenario event '
    '(i.e. not a broadcast type) to exactly one NPC, chosen by '
    'NPCSelector.SelectBest (Assets/Scripts/Selection/NPCSelector.cs). The score '
    'is a weighted sum of four factors:'
)
add_code_block(doc,
    'score = (1/distance) x DistanceWeight\n'
    '      + roleBonus[role][eventType] x RoleWeight\n'
    '      + stateReadiness              x StateWeight\n'
    '      + losBonus                    x LosWeight'
)
doc.add_paragraph(
    'This study measures how much each of those four terms actually matters by '
    'running the real selector with one term zeroed out at a time ("ablated") '
    'and comparing the result to the full formula, on identical events.'
)

p = doc.add_paragraph()
r = p.add_run('What "matters" means here — and what it doesn\'t. ')
r.bold = True
p.add_run(
    'Every comparison in this study is the formula against itself (full formula '
    'vs. the same formula with one term removed) — there is no external ground '
    'truth involved anywhere. So when a result says "removing Role changes 78% '
    'of decisions," that is a real, NOT circular finding: it did not have to '
    'come out that way — it could just as easily have been 2% if the other '
    'three terms already agreed with Role most of the time, and it wasn\'t '
    'assumed by the test. What this proves is that the formula\'s output '
    'genuinely depends on that input — the term isn\'t dead weight. What it '
    'does NOT prove is that including the term makes the decision better, more '
    'realistic, or doctrinally correct — proving that would require comparing '
    'selections against an external judgement (e.g. a subject-matter expert\'s '
    'opinion of who should respond), which this study does not do. Read every '
    'finding below as "this term has real, measurable influence on the '
    'outcome," never as "this term produces the correct outcome."'
)

p = doc.add_paragraph()
r = p.add_run(
    'Status: this is a REAL run, not a simulation. It executed live inside the '
    'Unity Editor (6000.3.1f1) via MCP on 2026-08-01, using the project\'s own '
    'AblationExperimentRunner and NPCSelector compiled code — the same code path '
    'the shipping game uses.'
)
r.bold = True

# ── 1. Harness fixes ──────────────────────────────────────────────────────────
doc.add_heading('1. What changed in the test harness before running it', level=1)
doc.add_paragraph(
    'The existing AblationExperimentRunner only tested 3 of the 4 scoring terms '
    '— it had Full, NoLOS, NoDistance, NoRole modes, but never disabled '
    'StateWeight, even though state-readiness is one of the four documented '
    'factors in NPCSelector. It also spawned every synthetic terrorist with the '
    'same default currentState = Idle, so StateScore was a constant 1.0 for '
    'every candidate — with no variance across candidates, ablating the state '
    'term could never change a ranking, making a NoState mode pointless even if '
    'it existed.'
)
doc.add_paragraph('Two small, targeted fixes were made to close this gap (both diffs are in the repo):')
for txt in [
    'Added a fifth ablation mode, NoState (StateWeight = 0), alongside Full, NoDistance, NoRole, NoLOS.',
    'Spawned NPCs now round-robin through Idle / Suspicious / Alert / TakeCover / Retreat instead of all defaulting to Idle, so the state term has real variance to be measured against.',
    'Added a state_score column to the CSV export so the selected NPC\'s state-readiness is recorded per decision, not just its distance/LOS.',
]:
    doc.add_paragraph(txt, style='List Number')
doc.add_paragraph(
    'No change was made to NPCSelector itself — only to the test harness that '
    'exercises it, so the scoring logic under test is unmodified production code.'
)

# ── 2. Method ─────────────────────────────────────────────────────────────────
doc.add_heading('2. Method', level=1)
add_table(doc, ['Parameter', 'Value'], [
    ['NPCs', '8 terrorists, round-robin roles (Guard/Roamer/Leader) and round-robin states (Idle/Suspicious/Alert/TakeCover/Retreat)'],
    ['Events', '50 random GunshotHeard events, uniform in a 30m x 30m arena'],
    ['Occlusion', 'One centre wall (10m x 3m x 1m) placed at the origin, so ~10-30% of shots have their nearest responder LOS-blocked'],
    ['Seed', '42 (reproducible NPC placement + event positions)'],
    ['Modes per event', 'Full, NoDistance, NoRole, NoState, NoLOS — same 50 events replayed under each mode'],
    ['Total decisions', '50 events x 5 modes = 250 selection calls'],
    ['Engine', 'Unity 6000.3.1f1, run live via MCP in the project\'s own XRI Starter Kit scene'],
    ['Weights (Full)', 'DistanceWeight = 2.0, RoleWeight = 1.0, StateWeight = 1.0, LosWeight = 1.0 (all project defaults, unchanged)'],
], bold_first_col=True)
doc.add_paragraph(
    'Raw output: Report_Figures/ablation_data/AblationResults_20260801_120355.csv '
    '(all 250 rows — event position, ablation mode, selected NPC, its role, its '
    'distance to the shot, whether it had line-of-sight, and its state-readiness score).'
)

# ── 3. Headline numbers ───────────────────────────────────────────────────────
doc.add_heading('3. Headline numbers', level=1)
add_table(doc,
    ['Mode', 'Guard picks', 'Roamer picks', 'Leader picks', 'Mean dist. of pick',
     'Mean StateScore of pick', 'LOS hit-rate', 'Agreement with Full'],
    [
        ['Full', '2', '48', '0', '11.40 m', '0.71', '88%', '100% (baseline)'],
        ['NoDistance', '0', '50', '0', '12.30 m', '0.72', '84%', '94%'],
        ['NoRole', '20', '9', '21', '9.56 m', '0.93', '92%', '22%'],
        ['NoState', '2', '48', '0', '8.95 m', '0.40', '88%', '44%'],
        ['NoLOS', '0', '50', '0', '13.43 m', '0.78', '68%', '80%'],
    ], bold_first_col=True)
doc.add_paragraph(
    '"Agreement with Full" = the % of the 50 events where the ablated mode '
    'picked the exact same NPC as the full formula. Lower agreement = that term '
    'changes who responds more often = that term matters more.'
)

doc.add_heading('Fig 1 — who gets picked, by mode', level=2)
add_image(doc, 'fig_ablation_1_role_distribution.png', 'Figure 1. Role of the selected NPC, by ablation mode (n=50 per mode).')
doc.add_paragraph(
    'Under Full, NoDistance, NoState, and NoLOS, Roamers win almost every event '
    '(48-50 / 50) — because Roamer + GunshotHeard = +2.0 is the single largest '
    'additive term in the formula, roughly matching or beating the maximum '
    'plausible distance contribution (2.0 x 1/dist, which only rivals 2.0 when '
    'the NPC is within ~1m). Only when the role bonus itself is removed (NoRole) '
    'does the picture change completely: Guards and Leaders — who get zero role '
    'bonus for GunshotHeard — suddenly win 41/50 events between them, because '
    'without their competitor\'s flat +2.0 head start, proximity and '
    'state-readiness decide instead.'
)

doc.add_heading('Fig 2 — selection agreement vs. the full formula', level=2)
add_image(doc, 'fig_ablation_2_agreement_with_full.png', 'Figure 2. % of events where each ablated mode picked the same NPC as the full formula.')
doc.add_paragraph('This is the clearest ranking of "how much does each term matter, in THIS arena and event distribution":')
for txt in [
    'Role (22% agreement -> 78% of decisions flip) — by far the dominant term. Removing it changes the winner in more than 3 out of 4 events.',
    'State (44% agreement -> 56% flip) — a strong secondary factor. More than half the time, the NPC that\'s most "ready" to respond isn\'t the one distance or role alone would have picked.',
    'LOS (80% agreement -> 20% flip) — a real but smaller effect: one in five decisions changes when perception is ignored.',
    'Distance (94% agreement -> 6% flip) — the smallest effect of the four in this configuration. Distance still shapes scores, but rarely flips the winner, because the role bonus usually already dominates the ranking before distance is even added.',
]:
    doc.add_paragraph(txt, style='List Number')

doc.add_heading('Fig 3 — mean distance of the selected NPC', level=2)
add_image(doc, 'fig_ablation_3_mean_distance.png', 'Figure 3. Mean distance (m) between the selected NPC and the event origin, by mode.')
doc.add_paragraph(
    'Isolating the one comparison this metric is actually valid for — Full '
    '(11.40m) vs. NoDistance (12.30m) — removing the distance term sends a '
    'responder that is, on average, 0.9m farther away from the gunshot. That is '
    'a real but modest effect for this arena size (30m x 30m), consistent with '
    'distance being a tie-breaker rather than the deciding factor here. (The '
    'lower distances under NoRole and NoState are a side-effect of those terms '
    'changing who wins, not evidence about the distance term itself — '
    'cross-mode bars other than the Full/NoDistance pair aren\'t isolating the '
    'same variable.)'
)

doc.add_heading('Fig 4 — mean state-readiness of the selected NPC', level=2)
add_image(doc, 'fig_ablation_4_mean_state_score.png', 'Figure 4. Mean StateScore (0-1) of the selected NPC, by mode.')
doc.add_paragraph(
    'The valid pair here is Full (0.71) vs. NoState (0.40): removing the '
    'state-readiness term nearly halves the average readiness of who gets sent '
    '— the formula is genuinely pulling toward more "available" '
    '(Idle/Suspicious) NPCs over busier (TakeCover/Retreat) ones when the term '
    'is active, and loses that entirely without it.'
)

doc.add_heading('Fig 5 — line-of-sight hit rate of the selected NPC', level=2)
add_image(doc, 'fig_ablation_5_los_hit_rate.png', 'Figure 5. % of selections where the chosen NPC had clear line-of-sight to the event, by mode.')
doc.add_paragraph(
    'The valid pair is Full (88%) vs. NoLOS (68%): without the perception term, '
    'the selector sends an NPC that can\'t actually see the event origin 32% of '
    'the time, up from 12% — almost a 3x increase in "blind" dispatches. This is '
    'the term that most directly protects against picking a responder who is '
    'technically close but behind a wall.'
)

# ── 4. Interpretation ─────────────────────────────────────────────────────────
doc.add_heading('4. Interpretation for the report / slide', level=1)
for txt in [
    'The formula\'s four terms are not equally load-bearing in this test configuration. Ranked by how often each one changes the outcome: Role >> State > LOS > Distance. (Again: this ranks how much each term INFLUENCES the output, not how CORRECT the output is with vs. without it.)',
    'This does not mean distance or LOS are poorly designed — it means that, given the current role-bonus magnitudes (Guard/Roamer/Leader bonuses of 3.0/2.0/1.5) relative to a 30m arena, role dominates the ranking before the other terms get a chance to matter as often. If the design intent is for distance to matter more relative to role, the fix is tuning RoleWeight down or DistanceWeight up relative to each other — not evidence of a bug.',
    'The NoRole result is worth a specific callout for the report: 78% of decisions change when role is removed, and the responder pool flips from "almost always the Roamer" to "roughly even across all three roles." That\'s a strong, concrete number for the "context-aware multi-factor scoring" claim.',
    'The State result was previously untestable in this harness (see Section 1) — this run is the first time it\'s been measured, and it shows a real, substantial effect (56% flip rate), not a token term.',
]:
    doc.add_paragraph(txt, style='List Bullet')

# ── 5. Caveats ────────────────────────────────────────────────────────────────
doc.add_heading('5. Caveats / what this does NOT show', level=1)
for txt in [
    'One arena, one seed, one event type. All 250 decisions are GunshotHeard events in a single 30m x 30m arena with one occluding wall. The relative ranking of terms (role > state > LOS > distance) is specific to this arena scale and role-bonus table — a much larger arena would likely shift distance\'s relative importance upward, and a different event type (e.g. RoomBreached, which favors Guard) would shift the role-bonus story entirely.',
    'n = 50 events per mode. Adequate for the observed effect sizes here (a 56-78% flip rate is far outside noise), but not a large-sample statistical study — no confidence intervals or significance tests are computed.',
    'Synthetic NPCs, not live gameplay. NPCs are spawned standing still (idleMode = Static) purely to be scored; this isolates the selector\'s decision logic but doesn\'t reflect a full mission with moving squads, active engagements, or the cooldown/CanRespond filtering that runs before real candidates ever reach NPCSelector in-game.',
    'Distance/LOS conclusions are pairwise, not global. As noted under Figs 3-5, only the Full-vs-single-ablation comparison isolates one variable; comparing e.g. NoRole\'s distance to NoState\'s distance conflates two different changes and isn\'t a valid claim about either term in isolation.',
    '"Changes the outcome" measures influence, not correctness. Every number in this report comes from comparing the formula against itself (with vs. without a term) — never against an external judgement of which NPC SHOULD have responded. A high flip-rate (like Role\'s 78%) proves that term is doing real work in the formula; it does not prove that work makes the AI\'s decisions more realistic or doctrinally sound. That would need the SME comparison described in MODULE2_EVALUATION_METHODOLOGY.md (Part B5), which this study doesn\'t attempt.',
]:
    doc.add_paragraph(txt, style='List Bullet')

# ── 6. Reproducing ────────────────────────────────────────────────────────────
doc.add_heading('6. Reproducing this run', level=1)
for txt in [
    'Open the project in Unity 6000.3.1f1+ with MCP for Unity connected, enter Play mode in any scene.',
    'Add an empty GameObject, attach AblationExperimentRunner, set npcCount = 8, eventCount = 50, seed = 42, addCentreWall = true.',
    'Invoke its context menu action "EXP -> Run Ablation Experiment" (or call the private RunMenu() via reflection, as this run did).',
    'The CSV is written to Application.persistentDataPath/Telemetry/AblationResults_<timestamp>.csv.',
    'Regenerate the charts/summary with the command below (edit the DATA path at the top of the script to point at a new CSV if you re-run the experiment).',
]:
    doc.add_paragraph(txt, style='List Number')
add_code_block(doc, 'cd Report_Figures\npython generate_ablation_charts.py')

doc.save(OUT)
print('Word report written to', OUT)
