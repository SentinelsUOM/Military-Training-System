# -*- coding: utf-8 -*-
"""
Single source of truth for the Module 2 Evaluation Briefing document.

This is a LIVING DOCUMENT — edit the BLOCKS list below to add findings, fix
numbers, or add new sections. Then run generate_briefing.py, which renders
this SAME content into three synchronized outputs:
    MODULE2_EVALUATION_BRIEFING.md
    MODULE2_EVALUATION_BRIEFING.docx
    MODULE2_EVALUATION_BRIEFING.pdf

Block format — a list of tuples, first element is the block type:
    ('h1', text)
    ('h2', text)
    ('h3', text)
    ('p', text)                      -- body paragraph
    ('bp', text)                     -- bold/emphasis paragraph (callout)
    ('quote', text)                  -- indented italic blockquote
    ('bullet', [items])
    ('numbered', [items])
    ('table', [headers], [[row],...])
    ('image', filename, caption)     -- filename relative to Report_Figures/
    ('hr',)                          -- horizontal rule / section break
    ('pagebreak',)                   -- force a new page (PDF/DOCX only)
"""

LAST_UPDATED = "2026-08-04"

BLOCKS = [
    ('h1', 'Module 2 — Evaluation Briefing'),
    ('bp', f'A living reference document — how to explain Module 2 (NPC behaviour & '
           f'coordination) to your supervisor and evaluators, with the evidence behind '
           f'every claim. Last updated {LAST_UPDATED}.'),
    ('p', 'This document consolidates everything established across the ablation study, '
          'the weight-sensitivity study, the literature grounding, the real human '
          'evaluation results, and the recent behaviour fixes — organised in the order '
          'you would actually present it, not the order it was discovered. Update this '
          'file as new findings come in; it is designed to grow with the project rather '
          'than be rewritten each time.'),
    ('hr',),

    # ══════════════════════════════════════════════════════════════════════
    ('h1', '1. What Module 2 Is — the one-minute pitch'),
    ('bp', '"Module 2 is the enemy AI — a squad of terrorists that reacts, searches, and '
           'coordinates like a real (if not elite) hostile force, instead of standing '
           'still or shooting on sight."'),
    ('p', 'This framing matters because it tells an evaluator what standard to judge the '
          'work against: not "is it hard to beat," but "does it behave believably." '
          'Everything in this document is evidence for that specific claim.'),
    ('h2', 'The problem that motivated the redesign'),
    ('p', 'The starting point had a specific, concrete bug: a terrorist who saw the '
          'trainee and then lost sight of them would simply abandon the chase and return '
          'to its post — even if a squadmate was standing right there. Real react-to-'
          'contact doctrine says the opposite: a confirmed sighting should commit the '
          'whole squad to a coordinated pursuit, not a private memory that expires. That '
          'gap — and the U.S. Army doctrine that contradicts it — is the origin story for '
          'the whole squad-coordination architecture (see Section 3).'),

    ('h1', '2. How It Is Built — conceptual architecture (no code)'),
    ('bullet', [
        'Individually, each terrorist runs a small finite-state machine: Idle → '
        'Suspicious → Alert → Engage → TakeCover / Retreat → Down.',
        'Above the individual level, a squad layer holds SHARED knowledge — where the '
        'trainee was last seen, whether a hunt is currently active — so the group acts '
        'as a team instead of isolated agents with private memories.',
        'When an event needs exactly one responder (e.g. "who investigates this '
        'gunshot?"), a selection formula (NPCSelector) scores every eligible NPC on '
        'four factors — distance, role, how ready they currently are, and whether they '
        'can actually see the event — and picks the highest scorer.',
    ]),
    ('p', 'That is the whole architecture in three sentences. Depth beyond this belongs '
          'in Q&A answers, not the opening pitch — see Section 8.'),

    ('h1', '3. Why It Is Built This Way — literature grounding, summarised'),
    ('p', 'The two-layer structure (individual behaviour under squad coordination) mirrors '
          'F.E.A.R.\'s well-known enemy AI (Orkin, 2006). The react-to-contact and '
          'shared-sighting behaviour follows actual U.S. Army battle-drill doctrine '
          '(CALL Handbook 96-3; FM 3-21.71). The expanding-search-on-lost-contact model '
          'follows peer-reviewed wilderness search-and-rescue theory (Phillips et al., '
          '2014) — anchor on the point last seen, grow the search radius with elapsed '
          'time. The responder-selection technique is standard utility-based AI (Mark, '
          '2009; Dill, 2010/2011/2012), with two citations specifically about virtual '
          'characters for military training simulators — the same domain as this '
          'project. Full citations, verified working links, and what each source backs '
          'are in Section 9 (Bibliography).'),

    # ══════════════════════════════════════════════════════════════════════
    ('h1', '4. The Responder-Selection Formula — full justification'),
    ('h2', '4.1 What it is'),
    ('p', 'NPCSelector.SelectBest scores every eligible NPC on four weighted factors and '
          'picks the highest total:'),
    ('quote', 'score = (1/distance) x DistanceWeight\n'
              '      + roleBonus[role][eventType] x RoleWeight\n'
              '      + stateReadiness              x StateWeight\n'
              '      + losBonus                    x LosWeight\n\n'
              'Project defaults: DistanceWeight=2.0, RoleWeight=1.0, StateWeight=1.0, '
              'LosWeight=1.0\n'
              'Role bonuses: Guard+RoomBreached=3.0, Roamer+GunshotHeard=2.0, '
              'Leader+AllyDownSeen=1.5'),
    ('p', 'No published source specifies this exact four-factor combination or these '
          'exact numbers — that combination is this project\'s own design. What IS '
          'well-grounded is the technique itself (weighted multi-factor scoring) and each '
          'individual factor, matched independently across three literatures: game-AI '
          'utility theory, multi-robot task allocation, and military/emergency dispatch '
          'operations research (Section 9, Group 4).'),

    ('h2', '4.2 Ablation study — does each factor actually matter?'),
    ('p', 'Method: the real NPCSelector was run live inside Unity, switching each of the '
          'four factors fully OFF one at a time, across 250 real selection decisions '
          '(50 gunshot events x 5 modes: Full, NoDistance, NoRole, NoState, NoLOS), and '
          'comparing each ablated mode against the full formula on the same events.'),
    ('image', 'fig_ablation_1_role_distribution.png',
     'Figure 1. Which role gets picked, by ablation mode. Roamers win almost every event '
     'under Full/NoDistance/NoState/NoLOS; only removing Role itself changes the picture.'),
    ('image', 'fig_ablation_2_agreement_with_full.png',
     'Figure 2. % of events where each ablated mode picked the same NPC as the full '
     'formula. This is the headline ranking of how much each term matters.'),
    ('table',
     ['Factor', 'Agreement with Full', 'Decisions that flip when removed'],
     [
         ['Role', '22%', '78% — the dominant term'],
         ['State', '44%', '56% — a strong secondary factor'],
         ['LOS', '80%', '20% — real but smaller'],
         ['Distance', '94%', '6% — smallest effect in this configuration'],
     ]),
    ('image', 'fig_ablation_3_mean_distance.png',
     'Figure 3. Mean distance of the selected NPC, by mode. Full vs. NoDistance is the '
     'only valid pairwise comparison here (11.4m vs 12.3m) — removing distance sends a '
     'responder about 0.9m farther away on average.'),
    ('image', 'fig_ablation_4_mean_state_score.png',
     'Figure 4. Mean state-readiness of the selected NPC. Full (0.71) vs NoState (0.40): '
     'removing the state term nearly halves the average readiness of who gets sent.'),
    ('image', 'fig_ablation_5_los_hit_rate.png',
     'Figure 5. % of selections with clear line-of-sight to the event. Full (88%) vs '
     'NoLOS (68%): without the perception term, the selector sends a "blind" responder '
     'almost 3x more often.'),
    ('bp', 'What this proves: every one of the four factors has real, measurable '
           'influence on the outcome — none of them is dead weight in the formula. What '
           'this does NOT prove: that including a factor makes the decision "better" or '
           '"more correct" — only that the formula\'s output genuinely depends on it. '
           'Whether that dependency helps or hurts training realism would need an '
           'external ground truth (an expert\'s judgement of who SHOULD respond), which '
           'this study does not have.'),

    ('h2', '4.3 Weight-sensitivity study — are the chosen numbers fragile or robust?'),
    ('p', 'Method: each factor\'s weight was swept across seven values (0, 0.5, 1, 1.5, '
          '2, 3, 4) while holding the other three at their defaults, across 25,200 real '
          'selection decisions — 3 event types (the three rows of the role-bonus table) '
          'x 3 independent random NPC layouts x 4 weights x 7 values x 100 shared events '
          'per point.'),
    ('bp', 'Read this before the charts: the 100% point exactly at each default is '
           'guaranteed by construction (the sweep is compared against itself at that '
           'point), not a discovered result. It is NOT evidence the default is optimal. '
           'The only real evidence is the SHAPE around that point: a flat curve on both '
           'sides = robust (many nearby values behave the same); a steep drop = '
           'sensitive (the exact number matters more).'),
    ('image', 'fig_sensitivity_distance.png',
     'Figure 6. Distance — the most robust of the four. Agreement never drops below '
     '~90% across the entire 0-4x range. Even zeroing it out only flips ~9-10% of '
     'decisions.'),
    ('image', 'fig_sensitivity_role.png',
     'Figure 7. Role — a cliff only at zero (21-51% agreement), flat and high (91-99%) '
     'from half the default value all the way to 4x it. It just needs to exist; the '
     'exact magnitude barely matters once turned on.'),
    ('image', 'fig_sensitivity_state.png',
     'Figure 8. State — same zero-cliff as Role, but a real (if modest) decline when '
     'over-weighted (down to 81-92% at 4x). Less forgiving of a large deviation than '
     'Role or Distance.'),
    ('image', 'fig_sensitivity_los.png',
     'Figure 9. LOS — the most sensitive of the four. Steepest decline when '
     'over-weighted, and event-type specific: for AllyDownSeen, stability drops to ~75% '
     'by 3-4x the default. The one legitimate, evidence-based candidate for future '
     're-tuning.'),
    ('bp', 'What this proves: for three of the four factors (Distance always; Role and '
           'State once given any positive value), the current weights sit in a wide, '
           'forgiving zone — nearby values, even 2-4x larger or fully removed, produce '
           'nearly the same selections. The system does not depend on hitting these '
           'exact numbers. What this does NOT prove: that 2.0/1.0/1.0/1.0 are '
           'individually the "best" possible numbers — no experiment run here, or '
           'runnable without expert ground truth, can show that.'),

    ('h2', '4.4 The honest, calibrated defense'),
    ('quote', '"The specific combination of distance, role, state-readiness, and '
              'line-of-sight — and the exact weights used — is our own design; no '
              'single paper prescribes this formula. Each factor individually, and the '
              'overall technique of weighted multi-factor scoring, is well-established '
              'in three separate literatures. Since no source specifies the exact '
              'numbers for our specific combination, we validated them ourselves: an '
              'ablation study showing each factor genuinely changes the outcome (i.e. '
              'none is dead weight), and a sensitivity study showing the chosen weights '
              'are not fragile. Neither study proves the decisions are doctrinally '
              'correct — only that the formula behaves the way it is supposed to, '
              'structurally. Establishing correctness would need comparison against '
              'expert judgement, which we have not done and say so plainly."'),
    ('p', 'This is deliberately a narrower claim than "these are the best weights" — '
          'and that narrower, calibrated claim is what makes it defensible. Overclaiming '
          'optimality is the single easiest way to lose credibility with an evaluator who '
          'pushes back on this exact point.'),

    # ══════════════════════════════════════════════════════════════════════
    ('h1', '5. Human Evaluation Results'),
    ('h2', '5.1 What was tested'),
    ('p', 'The same trainees played the identical mission three times — once against '
          '"Basic" AI, once "Intermediate," once "Advanced" — and rated the enemy after '
          'each play. n = 20 Basic-tagged sessions, 14 Intermediate, 11 Advanced; n = 9 '
          'participants completed all three tiers (the set the significance tests run '
          'on). Source: evaluation results.pdf (dashboard export), reported in full in '
          'MODULE2_FULL_REPORT.md Section 6.7.'),

    ('h2', '5.2 What each survey measure actually asks'),
    ('table',
     ['Measure', 'What it asks', 'Instrument'],
     [
         ['Perceived Intelligence', 'Did the enemy seem smart? (Competent, Knowledgeable, '
          'Responsible, Intelligent, Sensible)', 'Godspeed (Bartneck et al. 2009)'],
         ['Animacy', 'Did it feel alive, not robotic? (Alive, Lively, Organic, Lifelike, '
          'Interactive, Responsive)', 'Godspeed (Bartneck et al. 2009)'],
         ['Tactical realism', 'Did they act like real hostiles would? (reacted to being '
          'shot at, searched, overall behaviour realistically)', 'Author-defined, 3 items'],
         ['Pragmatic (UEQ)', 'Was fighting them frustrating or smooth? (usability — '
          'Supportive, Easy, Efficient, Clear)', 'UEQ-S (Schrepp et al. 2017)'],
         ['Hedonic (UEQ)', 'Was fighting them fun/engaging? (Exciting, Interesting, '
          'Inventive, Leading edge)', 'UEQ-S (Schrepp et al. 2017)'],
     ]),
    ('p', 'Perceived Intelligence and Tactical Realism are the core "does this AI seem '
          'believable" measures — the actual Module 2 research question. Pragmatic and '
          'Hedonic are standard UX checks (is the experience usable and fun) — a '
          'different, complementary construct, deliberately kept separate.'),

    ('h2', '5.3 The statistics, explained plainly'),
    ('table',
     ['Term', 'Plain-English meaning'],
     [
         ['p-value', 'How likely the observed difference is just random luck. p=0.004 '
          'means a 0.4% chance it is luck — i.e. very likely a real effect.'],
         ['Friedman test', 'The first check: "is there ANY difference at all among the '
          '3 AI tiers?" (an omnibus test, not yet saying which pair differs).'],
         ["Kendall's W", 'How consistently participants agreed on the pattern, 0 to 1. '
          'W=0.60 means most people rated the tiers in the same increasing order.'],
         ['Wilcoxon signed-rank', 'The follow-up check: "which SPECIFIC pair (e.g. '
          'Basic vs Intermediate) actually differs?" — run once per pair.'],
         ['Bonferroni correction', 'Running 3 pairwise tests instead of 1 raises the '
          'odds of a false alarm, so the significance bar is tightened from p<0.05 to '
          'p<0.017 per pair to compensate.'],
         ['Effect size (r)', 'How BIG the difference is (0-1), independent of sample '
          'size. ~0.1 small, ~0.3 medium, ~0.5 large. This study\'s values (0.85-0.89) '
          'are very large.'],
     ]),
    ('p', 'The exact formulas, and a hand-worked example that reproduces the numbers in '
          "your own report's validation note exactly, are recorded separately if needed "
          'for a methods appendix — ask and this section can be expanded with them.'),

    ('h2', '5.4 The results'),
    ('table',
     ['Measure', 'Basic', 'Intermediate', 'Advanced', 'Friedman p', 'Effect size'],
     [
         ['Perceived Intelligence (/5)', '1.6', '3.7', '4.4', '0.004',
          'r=0.85-0.89 (all pairs significant)'],
         ['Animacy — life-like (/5)', '2.0', '3.8', '4.1', '<0.001',
          'r=0.89 (Basic pairs sig.; Int.-vs-Adv. NOT significant, p=0.188)'],
         ['Tactical realism (/5)', '1.7', '3.5', '4.4', '0.003',
          'r=0.85-0.89 (all pairs significant)'],
     ]),
    ('bp', 'Summary sentence: real trainees playing the identical mission rated the '
           'smarter AI tiers as significantly more intelligent and tactically realistic '
           'than the dumbest tier — not a small pilot trend, but a statistically '
           'significant, large effect. The one honest exception: Intermediate and '
           'Advanced were NOT reliably distinguished on "does it feel alive" '
           'specifically (p=0.188) — both felt about equally life-like to trainees, even '
           'though Advanced still scored higher on intelligence and tactical realism.'),
    ('h2', '5.5 How this connects back to the selection formula — and what it does NOT prove'),
    ('p', 'TerroristController.cs gates squad coordination behind AI tier: '
          'AllowTeam => aiLevel >= AILevel.Intermediate. This is the exact switch that '
          'determines whether NPCSelector\'s picks are ever acted on — at Basic tier, '
          'NPCSelector still runs and picks an investigator, but that NPC silently '
          'declines to act on it. So the Basic-vs-Intermediate jump above is real '
          'evidence that the coordinated-response package (of which NPCSelector\'s '
          'selection is one necessary ingredient) meaningfully improves perceived '
          'intelligence.'),
    ('bp', 'What this does NOT prove: AllowTeam gates the ENTIRE squad-coordination '
           'package at once — dispatch, persistent hunt, leader directives, converge-on-'
           'contact all switch on together. This evaluation cannot isolate '
           "NPCSelector's specific contribution from the rest of that package. It "
           "corroborates that the mechanism is worth having; it does not validate its "
           'specific weights — that remains the job of Section 4\'s ablation/sensitivity '
           'studies.'),

    # ══════════════════════════════════════════════════════════════════════
    ('h1', '6. Recent Behaviour Fixes (verified compiling; playtest still needed)'),
    ('p', 'Three concrete bugs were found and fixed while preparing for evaluation. '
          'Listed here because they are good evidence of iterative rigor if asked "what '
          'did you find and fix during testing?" — and as a reminder to actually '
          'playtest them before the evaluation.'),
    ('numbered', [
        'Advance-to-engage threshold: engaging terrorists previously only closed '
        'distance if the trainee was farther than 5.5m — in small indoor rooms this '
        'almost never triggered, so they would just plant and shoot from wherever first '
        'spotted. Fixed: they now advance to a ~3.5m firing ring the moment the trainee '
        'is beyond it, no dead zone.',
        'Squad-support routine hijacking a live sighting: a background search routine '
        'could still send an NPC off to search a different room during the ~0.5s window '
        'between "I see you" and "confirmed, engaging" — even while that NPC was '
        'actively looking straight at the trainee. Fixed: the routine now backs off '
        'completely while there is a live sighting.',
        'Fan-out search not prioritising the witness: when a trainee broke line of '
        'sight, the squad search assigned "lanes" (direct line vs. side vs. up to 180 '
        'degrees behind) by pure distance-sort — so the NPC who actually saw the '
        'trainee could be assigned a rearward lane while an uninvolved squadmate got '
        'the direct line. Fixed: the witness now always gets dispatched straight to the '
        'exact last-seen position first; other squad members still fan out around that '
        'point to cover the wider approach.',
    ]),
    ('bp', 'Status: all three changes compile cleanly (verified live in the Unity '
           'Editor via MCP). None have been confirmed with an actual VR playtest yet — '
           'do that before the evaluation, specifically: get spotted at close range and '
           'confirm the terrorist closes in; then break line of sight and confirm he '
           'goes to where you actually were, not somewhere else.'),

    ('h1', '7. Presentation Structure — how to actually run the demo'),
    ('numbered', [
        'One-sentence framing (Section 1) — set the standard you want to be judged '
        'against before showing anything.',
        'The problem, in 20 seconds — the origin-story bug and the doctrine that '
        'contradicts it (Section 1).',
        'Live demo — show 3 things, not everything: (a) individual reaction to a '
        'threat, (b) squad coordination surviving broken contact — your strongest '
        'moment, it is literally the bug that motivated the redesign, (c) the '
        'hostage-guard escalation ladder.',
        'How it is built — 30 seconds, conceptual only, no code (Section 2).',
        'Why it is built this way — 30 seconds, name 2-3 sources (Section 3).',
        'How you know it works — do not rush this, it is your strongest material: the '
        'ablation numbers, the sensitivity numbers, and the real significance results '
        '(Sections 4 and 5).',
        'State one limitation, unprompted — e.g. "the specific weights are not from a '
        'published source; we validated them ourselves" (Section 4.4). Volunteering it '
        'reads as rigor, not weakness.',
    ]),

    ('h1', '8. Anticipated Questions — quick answers'),
    ('table',
     ['If asked...', 'Say...'],
     [
         ['Why these specific weights?', 'Not claimed to be provably optimal — shown to '
          'be robust (small changes don\'t flip decisions) and that each factor has real '
          'influence, backed by 25,000+ test decisions.'],
         ['Isn\'t the ablation study circular — testing the formula with the formula?',
          'Not circular: the result was not guaranteed. Removing Distance barely changed '
          'anything (6%); removing Role changed 78% of decisions. That is a real, '
          'falsifiable finding, not an assumption.'],
         ['Where does the selection logic come from — is it published research?',
          'The technique (utility-based scoring) is standard and cited; the specific '
          'four-factor combination is original, validated empirically since no paper '
          'covers this exact combination.'],
         ['How do you know the AI is more believable, not just harder?', 'Perceived '
          'Intelligence and Tactical Realism, not difficulty, were what was measured — '
          'and Basic was reliably distinguished from both higher tiers with large '
          'effect sizes.'],
         ['Are these weights proven to be the best possible?', 'No — and no capstone '
          'project could prove that without a full expert-comparison study, which is '
          'out of scope here. This is stated plainly rather than hidden.'],
     ]),

    ('h1', '9. Bibliography — every cited source, with working links'),
    ('p', 'Grouped by what each cluster of sources backs. Links verified by direct '
          'fetch, not guessed; where a link is genuinely dead or access-restricted, that '
          'is stated rather than invented.'),

    ('h2', '9.1 Squad-tactics doctrine & AI architecture'),
    ('table',
     ['Source', 'Link', 'What it backs'],
     [
         ['Orkin, J. (2006). "Three States and a Plan: The AI of F.E.A.R." GDC.',
          'https://www.gamedevs.org/uploads/three-states-plan-ai-of-fear.pdf',
          "The two-layer squad architecture (individual FSM + squad coordination) that Squad.cs / TerroristController.cs directly follows."],
         ['U.S. Army (1996). CALL Handbook 96-3, "Battle Drill 2: React to Contact."',
          'https://www.globalsecurity.org/military/library/report/call/call_96-3_cpt2bd2.htm',
          'Source of the "return fire within 3s, converge on contact" doctrine.'],
         ['FM 3-21.71 — Mechanized Infantry Platoon and Squad.',
          'https://www.globalsecurity.org/military/library/policy/army/fm/3-21-71/index.html',
          'Battle drills — "pursuit is default, disengagement is deliberate."'],
         ['Phillips, K. et al. (2014). "Wilderness Search Strategy and Tactics." '
          'Wilderness & Environmental Medicine, 25(2).',
          'https://doi.org/10.1016/j.wem.2014.02.006',
          'Peer-reviewed basis for the expanding-search-radius model (radius = speed x elapsed time).'],
         ['The Dupuy Institute (2018). "Breakpoints in U.S. Army Doctrine."',
          'https://dupuyinstitute.org/2018/04/18/breakpoints-in-u-s-army-doctrine/',
          'Basis for the morale-collapse model (leadership loss, casualties, surprise).'],
     ]),

    ('h2', '9.2 Believability / Turing-test evaluation methodology'),
    ('table',
     ['Source', 'Link', 'What it backs'],
     [
         ['Hingston, P. (2009). "A Turing test for computer game bots." IEEE ToCIAIG, 1(3).',
          'https://ieeexplore.ieee.org/document/5247069/',
          'The "humanness ratio" Turing-test paradigm the believability study is built on.'],
         ['Gorman, B. et al. (2006). "Believability Testing and Bayesian Imitation." SAB.',
          'https://link.springer.com/chapter/10.1007/11840541_54',
          'Source of the main believability instrument (5-point gradient + experience-weighted index).'],
         ['Justesen, N. et al. "Human-like Bots for Tactical Shooters." arXiv:2501.00078.',
          'https://arxiv.org/abs/2501.00078',
          'Argues human-likeness, not win-rate, is the right target for tactical-shooter AI.'],
     ]),

    ('h2', '9.3 Player-experience & workload questionnaires (the actual survey instruments)'),
    ('table',
     ['Source', 'Link', 'What it backs'],
     [
         ['Bartneck, C. et al. (2009). "Godspeed" measurement instruments. Int J Social Robotics, 1.',
          'https://doi.org/10.1007/s12369-008-0001-3',
          'Exact source of the "Perceived Intelligence" and "Animacy" survey items.'],
         ['Schrepp, M. et al. (2017). UEQ-S short form. IJIMAI, 4(6).',
          'https://doi.org/10.9781/ijimai.2017.09.001',
          'Source of the Pragmatic / Hedonic UEQ-S questions.'],
         ['Harris, D.J. et al. (2020). "SIM-TLX." Virtual Reality, 24.',
          'https://doi.org/10.1007/s10055-019-00422-9',
          'The instrument the "Workload" tab (SimTlxTab.jsx) implements.'],
         ['Harris, D.J. et al. (2020). Frontiers in Psychology, art. 605.',
          'https://pmc.ncbi.nlm.nih.gov/articles/PMC7136518/',
          '"Face validity is not construct validity" — the argument the SME-review plan is built on.'],
     ]),

    ('h2', '9.4 NPC responder-selection technique grounding'),
    ('table',
     ['Source', 'Link', 'What it backs'],
     [
         ['Mark, D. (2009). Behavioral Mathematics for Game AI.',
          'archive.org (borrow-only — see note below)',
          'Foundational utility-AI text: the technique NPCSelector follows.'],
         ['Mark, D. & Dill, K. (2010). "Improving AI Decision Modeling Through Utility Theory." GDC.',
          'https://media.gdcvault.com/gdc10/slides/MarkDill_ImprovingAIUtilityTheory.pdf',
          'Confirmed working direct PDF. Pitched the technique to the wider industry as a shippable architecture.'],
         ['Dill, K. & Martin, L. (2011). "A Game AI Approach to Autonomous Control of Virtual Characters." I/ITSEC.',
          'https://web.archive.org/web/20240416015323/https://course.ccs.neu.edu/cs5150f13/readings/dill_granny.pdf',
          'Utility-based AI for MILITARY-TRAINING virtual characters specifically — the strongest domain-match citation.'],
         ['Dill, K. (2012). "Design Patterns for the Configuration of Utility-Based AI." I/ITSEC.',
          'https://web.archive.org/web/20250709085313/https://course.ccs.neu.edu/cs5150f13/readings/dill_designpatterns.pdf',
          'Structuring/configuring a utility-AI scoring system.'],
     ]),
    ('bp', 'Note on the Mark (2009) book: confirmed via direct check that archive.org '
           'lists it under Controlled Digital Lending ("Access-restricted-item: true") — '
           'it requires a free account and a temporary borrow, not a download. This is '
           'normal for a commercially published book (unlike the papers above) and is '
           'not a broken link.'),
    ('bp', 'Note on the two Dill I/ITSEC papers: the original course.ccs.neu.edu links '
           'are dead (404 — the hosting course page was taken down). The links above are '
           'Wayback Machine snapshots, confirmed live via the Wayback availability API. '
           'If either stops working, search "site:web.archive.org [paper title]" for a '
           'fresh snapshot.'),
    ('p', 'Full 43-source bibliography with every group (statistics methodology, '
          'multi-robot task allocation, weapon-target assignment, etc.) is in '
          'MODULE2_MASTER_BIBLIOGRAPHY.md / .docx — this section covers only the sources '
          'most likely to come up in a live Q&A.'),

    ('h1', '10. Open Items — track before the evaluation'),
    ('bullet', [
        'No SME (subject-matter expert) comparison has been run yet — this is the only '
        'thing that could establish ground-truth "correctness" for the selection '
        'formula, as opposed to robustness. Planned in MODULE2_EVALUATION_METHODOLOGY.md '
        'Part B5, not yet executed.',
        'The 45-world "full" weight-sensitivity design (varying arena scale, not just '
        'event type and seed) was scoped but not run — the current sensitivity study '
        'still only tests one arena size (30m x 30m).',
        'ATP 3-21.8 has no verified free authoritative link — only unofficial mirrors.',
        'Two citation discrepancies flagged in the master bibliography (#12 DiVA thesis '
        'title/authors, #20 Almeida FPS-AI review authors) — re-check against original '
        'sources before citing in front of an evaluator.',
        'The three behaviour fixes in Section 6 need an actual VR playtest confirmation '
        '— not yet done.',
    ]),
    ('p', 'Add new findings to this section (or a new numbered section above it) as they '
          'come up — that is the point of this being a living document.'),
]
