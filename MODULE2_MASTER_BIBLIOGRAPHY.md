# Module 2 — Master Research Bibliography

Every research source that backs Module 2's design and evaluation, consolidated
from `Literature_Review_Module2.docx`, `MODULE2_EVALUATION_METHODOLOGY.md`,
`MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md`, and the survey instruments
implemented in `sentinels-aar/lib/`. Links were verified via live web search
on 2026-08-03, not guessed — where a real link could not be confirmed, that is
stated explicitly rather than inventing one.

**Two flagged discrepancies**, kept visible rather than silently resolved:
- **#12** (DiVA thesis): the repo's reference list describes it generically;
  the confirmed real title/authors differ slightly — still on-topic, worth
  updating the citation text.
- **#20** (Almeida et al. FPS AI review): the confirmed DOI/volume/issue/
  article-number match your citation, but the author list and exact title on
  record differ from what's actually on that DOI. Re-check against your
  original source before citing in front of an evaluator.

---

## Group 1 — Squad-tactics doctrine & AI architecture
*(backs `Literature_Review_Module2.docx` — how the terrorist squad reacts, searches, and coordinates)*

1. **Orkin, J. (2006). "Three States and a Plan: The AI of F.E.A.R." GDC.**
   [Link](https://www.gamedevs.org/uploads/three-states-plan-ai-of-fear.pdf)
   The single most load-bearing citation in Module 2's design: F.E.A.R.'s GOAP
   + two-layer squad coordination is the direct model for `Squad.cs` /
   `TerroristController.cs`.

2. **U.S. Army (1996). CALL Handbook 96-3, "Battle Drill 2: React to Contact."**
   [Link](https://www.globalsecurity.org/military/library/report/call/call_96-3_cpt2bd2.htm)
   Source of the "return fire within 3 seconds, converge on contact" doctrine
   that drives the squad's react-to-contact behaviour.

3. **FM 3-21.71 — Mechanized Infantry Platoon and Squad.**
   [Link](https://www.globalsecurity.org/military/library/policy/army/fm/3-21-71/index.html)
   Battle Drills (React to Contact, Break Contact, Clear a Building) — the
   doctrinal basis for "pursuit is default, disengagement is deliberate."

4. **FM 3-06.11 — Combined Arms Operations in Urban Terrain.**
   [Link](https://www.globalsecurity.org/military/library/policy/army/fm/3-06-11/ch9.htm)
   Systematic building-clearing doctrine; informs the squad's room-clearing /
   search behaviour.

5. **FM 7-8 — Infantry Rifle Platoon and Squad.**
   [Link](https://www.globalsecurity.org/military/library/policy/army/fm/7-8/index.html)
   Battle drills; secondary corroboration for the react-to-contact model.

6. **ATP 3-21.8 — Infantry Rifle Platoon and Squad.**
   *No verified free authoritative link found* (only unofficial mirrors —
   not independently confirmed). Source of the standard fire-command
   (ADDRAC) format the shared-contact reporting is modelled on.

7. **NIST — "Last Known Position (LKP) / Point Last Seen (PLS)."**
   [Link](https://www.nist.gov/glossary-term/26441)
   Defines the search-anchor concept `Squad.cs`'s persistent-hunt system
   (`PointLastSeen`) is built around.

8. **Phillips, K. et al. (2014). "Wilderness Search Strategy and Tactics."
   *Wilderness & Environmental Medicine*, 25(2).**
   [Link](https://doi.org/10.1016/j.wem.2014.02.006)
   Peer-reviewed source for the expanding-search-radius model (radius =
   speed × elapsed time) used in `Squad.NextSearchPoint`.

9. **SARMath — "Initial Planning Point (IPP)."**
   [Link](http://www.sarmath.com/terms/initial-planning-point)
   Secondary source for the same expanding-circle search math.

10. **The Dupuy Institute (2018). "Breakpoints in U.S. Army Doctrine."**
    [Link](https://dupuyinstitute.org/2018/04/18/breakpoints-in-u-s-army-doctrine/)
    Operations-research basis for the morale-collapse model (leadership loss,
    casualties, surprise — not a simple casualty counter).

11. **"Suppressive fire." Wikipedia (citing USMC doctrine).**
    [Link](https://en.wikipedia.org/wiki/Suppressive_fire)
    Grey-literature source for "suppression is psychological, not lethal" —
    the principle behind cover/chokepoint tactics.

12. **Sjöblom, M. & Svala, R. (2025). "Multi-Agent Performance Using
    Goal-Oriented Action Planning (GOAP) in a Procedural Game." DiVA thesis.**
    [Link](https://www.diva-portal.org/smash/record.jsf?pid=diva2:1972169)
    *(Flagged — see note above.)* Corroborates Orkin's two-layer squad
    architecture from a second, independent source.

## Group 2 — Believability / Turing-test evaluation methodology
*(backs `MODULE2_EVALUATION_METHODOLOGY.md` — how to prove the AI "seems intelligent")*

13. **Hingston, P. (2009). "A Turing test for computer game bots."
    *IEEE ToCIAIG*, 1(3).**
    [Link](https://ieeexplore.ieee.org/document/5247069/)
    Founding paper of the "humanness ratio" Turing-test paradigm the
    believability study is built on.

14. **Schrum, J. et al. "Optimising Humanness... UT2004." Springer 2017.**
    [Link](https://link.springer.com/chapter/10.1007/978-3-319-59147-6_58)
    The 2K BotPrize competition write-up — precedent for judge-based
    humanness scoring.

15. **Gorman, B., Thurau, C., Bauckhage, C., Humphrys, M. (2006).
    "Believability Testing and Bayesian Imitation in Interactive Computer
    Games." SAB.**
    [Link](https://link.springer.com/chapter/10.1007/11840541_54)
    Source of the main believability instrument: the 5-point
    Human↔Artificial gradient + experience-weighted index.

16. **Even, C., Bosser, A-G., Buche, C. (2021). Frontiers in Computer Science.**
    [Link](https://www.frontiersin.org/journals/computer-science/articles/10.3389/fcomp.2021.774763/full)
    The 7-characteristic protocol for designing a defensible believability
    study (judge count, expertise, clip duration, etc.).

17. **Togelius, Yannakakis, Karakovskiy, Shaker — "Assessing believability,"
    in *Believable Bots* (Springer, 2012).**
    [Link](https://link.springer.com/chapter/10.1007/978-3-642-32323-2_9)
    Mario AI Turing-test track — precedent for external-observer (spectator)
    judging over participatory judging.

18. **Justesen, N. et al. "Human-like Bots for Tactical Shooters Using
    Compute-Efficient Sensors." arXiv:2501.00078.**
    [Link](https://arxiv.org/abs/2501.00078)
    Argues human-likeness, not win-rate, is the right target for
    tactical-shooter AI — directly justifies the "believability over
    difficulty" framing.

19. **Durst, D. et al. "Learning to Move Like Professional Counter-Strike
    Players." arXiv:2408.13934.**
    [Link](https://arxiv.org/abs/2408.13934)
    TrueSkill-based human-likeness rating; behaviour-distribution / mistake /
    teamwork metrics — the objective-metric taxonomy the telemetry design
    borrows from.

20. **Almeida, P., Carvalho, V., Simões, A. (2023). *Algorithms*, 16(7):323.**
    [Link](https://www.mdpi.com/1999-4893/16/7/323)
    *(Flagged — see note above.)* Source of the game-based-metrics-over-RL-
    reward taxonomy and the reusable 20-participant survey design.

## Group 3 — Player-experience, workload & VR questionnaires
*(the actual instruments the dashboard survey implements — `lib/aiEval.js`, `lib/simTlx.js`)*

21. **Bartneck, C., Kulić, D., Croft, E., Zoghbi, S. (2009). "Measurement
    instruments for anthropomorphism, animacy, likeability, perceived
    intelligence, and perceived safety of robots." *Int J Social Robotics*, 1.**
    [Link](https://doi.org/10.1007/s12369-008-0001-3)
    **Godspeed** — the exact source of the "Perceived Intelligence" and
    "Animacy" survey items.

22. **Schrepp, M., Hinderks, A., Thomaschewski, J. (2017). UEQ-S short form.
    *IJIMAI*, 4(6).**
    [Link](https://doi.org/10.9781/ijimai.2017.09.001)
    Source of the Pragmatic / Hedonic UEQ-S questions.

23. **Laugwitz, B., Held, T., Schrepp, M. (2008). Original UEQ.
    USAB / LNCS 5298.**
    [Link](https://doi.org/10.1007/978-3-540-89350-9_6)
    The full-length instrument UEQ-S is a short form of.

24. **Johnson, D., Gardner, J., Perry, R. (2018). *IJHCS*.**
    [Link](https://doi.org/10.1016/j.ijhcs.2018.05.003)
    Validates (and flags caveats for) PENS/GEQ — cited in the methodology
    doc's honesty note about instrument limitations.

25. **Denisova, A., Nordin, A.I., Cairns, P. (2016). CHI PLAY.**
    [Link](https://doi.org/10.1145/2967934.2968095)
    Shows IEQ/GEQ/PENS converge strongly — the reason the survey battery
    doesn't stack all of them.

26. **IJsselsteijn, W., de Kort, Y., Poels, K. — "The Game Experience
    Questionnaire" (FUGA project deliverable).**
    [Link](https://pure.tue.nl/ws/files/21666952/Fuga_d3.3.pdf)
    The GEQ itself (distinct from the *other* "GEQ" — Brockmyer's Game
    Engagement Questionnaire — a naming collision the methodology doc
    explicitly flags).

27. **Jennett, C. et al. (2008). "Measuring and defining the experience of
    immersion in games." *IJHCS*, 66(9).**
    [Link](https://doi.org/10.1016/j.ijhcs.2008.04.004)
    Source of the IEQ / IEQ-SF immersion questionnaire.

28. **Carpinella, C.M., Wyman, A.B., Perez, M.A., Stroessner, S.J. (2017).
    "RoSAS." HRI 2017.**
    [Link](https://doi.org/10.1145/2909824.3020208)
    Robotic Social Attributes Scale — an alternative/supplementary
    instrument to Godspeed.

29. **Kennedy, R.S., Lane, N.E., Berbaum, K.S., Lilienthal, M.G. (1993).
    "Simulator Sickness Questionnaire." *Int J Aviation Psychology*, 3(3).**
    [Link](https://doi.org/10.1207/s15327108ijap0303_3)
    The VR safety/comfort control instrument (SSQ).

30. **Hart, S.G., Staveland, L.E. (1988). NASA-TLX chapter.**
    [Link](https://doi.org/10.1016/S0166-4115(08)62386-9)
    The cognitive-workload instrument the player-experience battery includes.

31. **Harris, D.J., Wilson, M.R., Vine, S.J. (2020). "SIM-TLX."
    *Virtual Reality*, 24.**
    [Link](https://doi.org/10.1007/s10055-019-00422-9)
    The actual instrument the "Workload" tab (`SimTlxTab.jsx`) implements.

32. **Harris, D.J., Bird, K.J., Smart, P.A., Wilson, M.R., Vine, S.J. (2020).
    *Frontiers in Psychology*, art. 605.**
    [Link](https://pmc.ncbi.nlm.nih.gov/articles/PMC7136518/)
    The four-fidelity-subtype (physical/psychological/affective/ergonomic)
    framework, and the key "face validity ≠ construct validity" argument the
    SME-review plan (Part B5) is built on.

## Group 4 — NPC responder-selection technique grounding
*(backs `MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md` — the `NPCSelector` weighted-scoring defence)*

33. **Mark, D. (2009). *Behavioral Mathematics for Game AI*.**
    [Link](https://archive.org/details/behavioralmathem0000mark)
    Foundational utility-AI text; the technique `NPCSelector`'s scoring
    formula follows.

34. **Dill, K. & Martin, L. (2011). "A Game AI Approach to Autonomous
    Control of Virtual Characters." I/ITSEC.**
    [Link](https://course.ccs.neu.edu/cs5150f13/readings/dill_granny.pdf)
    The single most on-domain citation: utility-based AI for
    *military-training* virtual characters specifically.

35. **Dill, K. (2012). "Design Patterns for the Configuration of
    Utility-Based AI." I/ITSEC.**
    [Link](https://course.ccs.neu.edu/cs5150f13/readings/dill_designpatterns.pdf)
    Structuring/configuring the kind of scoring system `NPCSelector`
    implements.

36. **Lewis, M. (2015). "Choosing Effective Utility-Based Considerations."
    *Game AI Pro 3*, ch. 13.**
    *(Book chapter — no free URL found.)*
    Names distance and line-of-sight, by name, as standard scoring
    considerations.

37. **Welsh, R. "Crytek's Target Tracks Perception System." *Game AI Pro*.**
    *(Book chapter — no free URL found.)*
    Perception incorporated into target scoring in a shipped commercial
    engine.

38. **ACM Computing Surveys (2024). "A Systematic Literature Review on
    Multi-Robot Task Allocation."**
    *(Paywalled DOI — not separately re-verified this pass.)*
    Source of the distance + capability + availability three-factor pattern
    the Role/State factors mirror.

39. **Weapon-Target Assignment problem — Wikipedia overview.**
    [Link](https://en.wikipedia.org/wiki/Weapon-target_assignment_problem)
    Military OR precedent for distance + capability-based assignment.

## Group 5 — Statistics methodology
*(backs the Friedman/Wilcoxon/Kendall's W panel in `sentinels-aar/lib/stats.js` and §6.5–6.7 of `MODULE2_FULL_REPORT.md`)*

40. **Friedman, M. (1937). *JASA*, 32(200).**
    [Link](https://www.jstor.org/stable/2279372)
    The Friedman test itself.

41. **Wilcoxon, F. (1945). *Biometrics Bulletin*, 1(6).**
    [Link](https://www.jstor.org/stable/3001968)
    The Wilcoxon signed-rank test itself.

42. **Kendall, M.G. & Babington Smith, B. (1939).
    *Annals of Mathematical Statistics*, 10(3).**
    [Link](https://doi.org/10.1214/aoms/1177732186)
    Source of Kendall's W — the effect-size measure for the Friedman
    omnibus test.

43. **Dunn, O.J. (1961). *JASA*, 56(293).**
    [Link](https://doi.org/10.1080/01621459.1961.10482090)
    Source of the Bonferroni correction used on the 3 pairwise post-hoc
    comparisons.
