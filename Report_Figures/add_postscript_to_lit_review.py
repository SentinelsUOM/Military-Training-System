"""One-off script: insert a Section 14 postscript into
Assets/Docs/Literature_Review_Module2.docx, grounding NPCSelector's four
responder-selection parameters (distance/role/state/LOS) in the wider
literature, since the original review (Sections 1-13) does not cover this
topic at all. Matches the document's existing "Heading 1" / "Normal" style
pattern. New references are appended as a clearly-labelled, separate group so
they are never conflated with the original list's adversarial-verification
process (see the document's own "Note on sources and method").

Run once. Idempotent-ish: checks for the heading text before inserting so a
second run doesn't duplicate the section.
"""
import docx

PATH = 'Assets/Docs/Literature_Review_Module2.docx'
SECTION_HEADING = '14. Postscript: Grounding the NPC Responder-Selection Formula (Added August 2026)'

doc = docx.Document(PATH)

# Guard against double-insertion if this script is re-run.
if any(p.text.strip() == SECTION_HEADING for p in doc.paragraphs):
    print('Postscript already present — no changes made.')
    raise SystemExit(0)

# Find "13. Conclusion" and the "References" heading + the trailing
# "Note on sources and method" paragraph, by text (robust to index drift).
references_heading = None
note_para = None
for p in doc.paragraphs:
    if p.text.strip() == 'References':
        references_heading = p
    if p.text.strip().startswith('Note on sources and method'):
        note_para = p

if references_heading is None or note_para is None:
    raise RuntimeError('Could not locate References heading / Note paragraph — aborting.')

# ── Insert Section 14, right before the "References" heading ────────────────
section_body = [
    ("14. Postscript: Grounding the NPC Responder-Selection Formula (Added August 2026)", 'Heading 1'),
    ("Separately from the enemy-NPC behavioural doctrine reviewed in Sections 1-13, "
     "Module 2 must also decide, for events with a single correct responder, which of "
     "several eligible NPCs reacts. The implementation (NPCSelector) scores each "
     "candidate on four factors — distance to the event, a role-based bonus specific "
     "to the event type, the candidate's current state-readiness, and whether it has "
     "direct line-of-sight to the event origin — and selects the highest-scoring "
     "candidate. This postscript reviews the wider literature for this specific "
     "mechanism, which fell outside the scope of the review above.", 'Normal'),
    ("The overall technique — normalising several factors, weighting and summing "
     "them, and acting on the highest-scoring candidate — is the standard "
     "utility-based decision-making approach in game AI (Mark, 2009), popularised as a "
     "mainstream architecture at GDC (Dill, 2010) and applied, notably, to autonomous "
     "virtual characters for military training simulators in two peer-reviewed I/ITSEC "
     "papers by the same author (Dill & Martin, 2011; Dill, 2012) — the same "
     "application domain as this project. A dedicated practitioner reference (Lewis, "
     "2015) lists distance and line-of-sight by name as standard scoring "
     "considerations, and a related source (Welsh, Game AI Pro) documents perception/"
     "visibility incorporated into target scoring in a shipped commercial engine.", 'Normal'),
    ("Each of the remaining two factors is independently supported outside game AI. "
     "Role/capability matching and state/availability are the second and third "
     "canonical factors, alongside distance, in the multi-robot task allocation (MRTA) "
     "literature (ACM Computing Surveys, 2024), and appear again in real-world "
     "Computer-Aided Dispatch (CAD) systems for emergency response, which select units "
     "by location, capability/certification, and current status. The military "
     "Weapon-Target Assignment (WTA) problem likewise combines distance and capability "
     "constraints in assigning responders to targets.", 'Normal'),
    ("No source located, in this review or in a subsequent search, specifies this "
     "exact four-factor combination, nor the specific weight values used "
     "(DistanceWeight=2.0, RoleWeight=1.0, StateWeight=1.0, LosWeight=1.0; role bonuses "
     "of 3.0/2.0/1.5). As with the hostage-guard behaviour (Section 8) and the "
     "lost-contact persistence duration (Section 12), this combination and these "
     "values are an engineering design, consistent with but not derived from the "
     "literature above, and are stated as such rather than mistaken for an evidenced "
     "fact.", 'Normal'),
    ("In the absence of a literature-derived value, the project validated the chosen "
     "weights empirically after implementation: an ablation study (each factor "
     "switched fully on/off across 250 real selection decisions) established that all "
     "four factors measurably affect the outcome, ranked Role > State > LOS > Distance "
     "in relative influence; a weight-sensitivity study (each factor's weight swept "
     "across a 0-4× range across 25,200 real decisions spanning three event types "
     "and three independent NPC layouts) established that three of the four factors "
     "are stable across a wide range around their defaults, while the fourth, "
     "line-of-sight, was identified as the most sensitive and flagged for future "
     "re-tuning. Full detail is reported separately (MODULE2_ABLATION_STUDY_REPORT; "
     "MODULE2_WEIGHT_SENSITIVITY_REPORT).", 'Normal'),
]

for text, style_name in section_body:
    new_p = references_heading.insert_paragraph_before(text, style=style_name)

# ── Append new, clearly-separated references before the trailing Note ───────
postscript_refs = [
    ("Postscript references (added 2026-08-02 via a live web search; not subjected to "
     "the multi-source adversarial-verification process described in the note below):",
     None),  # bold flag handled separately
    ('Dill, K. (2010). "Improving AI Decision Making Using Utility Theory." Game '
     "Developers Conference (GDC).", 'Normal'),
    ('Dill, K. & Martin, L. (2011). "A Game AI Approach to Autonomous Control of '
     'Virtual Characters." I/ITSEC (Interservice/Industry Training, Simulation and '
     'Education Conference). [Primary; utility-based AI for military-training virtual '
     'characters — the exact application domain of this project.]', 'Normal'),
    ('Dill, K. (2012). "Design Patterns for the Configuration of Utility-Based AI." '
     'I/ITSEC. [Primary.]', 'Normal'),
    ('Lewis, M. (2015). "Choosing Effective Utility-Based Considerations." Game AI Pro '
     '3, ch. 13. [Primary practitioner source; names distance and line-of-sight as '
     'standard utility-AI scoring considerations.]', 'Normal'),
    ("Mark, D. (2009). Behavioral Mathematics for Game AI. [Primary; the foundational "
     "utility-based multi-factor scoring text.]", 'Normal'),
    ('Welsh, R. "Crytek\'s Target Tracks Perception System." Game AI Pro. [Primary '
     'practitioner source; perception/visibility in target-response scoring.]', 'Normal'),
    ('ACM Computing Surveys (2024). "A Systematic Literature Review on Multi-Robot '
     'Task Allocation." [Peer-reviewed; distance + capability + availability as the '
     'canonical multi-robot task-allocation factor set.]', 'Normal'),
    ('Weapon-Target Assignment problem (operations research overview; see Wikipedia '
     'and its cited primary sources). [Secondary/tertiary; used for the well-established '
     'general principle of distance + capability constraints in military assignment.]',
     'Normal'),
    ('Computer-Aided Dispatch (CAD) systems for EMS/police — industry '
     'documentation (e.g. Resgrid blog). [Grey/tertiary — practitioner '
     'documentation, not peer-reviewed; used to illustrate real-world dispatch '
     'practice, not as academic evidence.]', 'Normal'),
]

first = True
for text, style_name in postscript_refs:
    if first:
        p = note_para.insert_paragraph_before('', style='Normal')
        run = p.add_run(text)
        run.italic = True
        first = False
    else:
        note_para.insert_paragraph_before(text, style=style_name)

doc.save(PATH)
print('Postscript section + references inserted into', PATH)
