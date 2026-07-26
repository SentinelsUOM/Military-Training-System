# Module 2 — how defenders *should* behave (researched, cited)

**For:** Sentinels VR hostage-rescue trainer, Module 2 (enemy/defender NPC AI).
**Method:** deep multi-source research (95 agents, 15 sources fetched, 25 claims adversarially verified — **25 confirmed, 0 refuted**). Force model = **semi-trained** (between paramilitary drill and irregular chaos).
**Date:** 25 Jul 2026.

---

## The headline (your bug, confirmed)

> **"See the intruder, lose him, immediately walk back to your post" is doctrinally WRONG.**

Real forces treat a sighting as a trigger to **actively hunt**, not to withdraw. Withdrawal ("break contact") is a *separate, deliberate* decision a leader makes only after assessing the situation — it is never a free walk-off. So the behaviour you saw (both terrorists giving up and returning to their room after one confirmed you) contradicts how real defenders behave. The fix is grounded in doctrine, below.

---

## The verified findings → concrete Module 2 rules

### (a) The instant a teammate confirms the intruder → SHARED knowledge + CONVERGE
- **Finding (3-0):** a single member's sighting is *immediately converted into shared team knowledge* — the sighter calls "Contact!" with direction/limits and passes it up; every member acts on that one member's information. *(FM 3-21.71; CALL 96-3 Battle Drill 2; ADDRAC fire command.)*
- **Finding (3-0):** on contact the force **converges on the element in contact** — the leader physically moves up and brings others forward — rather than dispersing. *(CALL 96-3.)*
- **→ Rule:** the moment ANY defender confirms you, write your position to a **shared squad blackboard** (`LastConfirmedPosition`) that *every* defender reads and acts on. It is **not** personal to the one who saw you. Nearby defenders **converge toward** that spot to re-acquire. Add a small propagation delay (semi-trained: the "call" isn't instant).
- **This is the core fix for your bug.** Today "confirmed" only protects the individual who saw you; it must become a shared, persistent squad state.

### (b) How long / how hard they hunt a LOST contact before standing down
- **Finding (3-0):** return aimed fire + take nearest cover **within ~3 s** of contact — never withdraw as the reflex. *(CALL 96-3 STANDARDS; ATP 3-21.8.)*
- **Finding (3-0):** breaking contact is a **deliberate leader decision after a quick assessment** (enemy size/location), executed as a *covered bounding move under suppressing fire* — not a stroll back. *(FM 3-21.71.)*
- **→ Rule:** defenders **press a confirmed contact by default.** They only stand down when a *leader-level condition* trips (e.g. heavily outnumbered / mounting casualties / area genuinely cleared), and even then they fall back under cover, not walk off. **Raise the current give-up timer massively for a CONFIRMED contact**, and gate stand-down on "area searched" rather than a short clock.
- **Caveat:** doctrine gives no exact "give-up" number for a *lost* contact (flagged open question) — so the persist-duration is a tuning choice; make it long and area-based, not a few seconds.

### (c) Divide to search, then RE-CONVERGE on a fresh sighting
- **Finding (3-0):** after losing sight, search anchors on the **Point-Last-Seen (PLS)**; the re-acquisition area is a **circle whose radius = subject-speed × time-elapsed** — it **grows with time**. A fresh sighting **resets the anchor and collapses the radius**. *(Phillips et al., Wilderness & Env. Medicine 2014; sarmath IPP.)*
- **Finding (3-0):** a hunting team splits into **base-of-fire + maneuver**, isolates/cuts off escape, clears **room-by-room systematically, marking cleared rooms**, never leaving cleared areas/escape routes unwatched. *(FM 3-21.71 App E; FM 3-06.11.)*
- **Finding (3-0):** F.E.A.R. does exactly this in a game: **split into pairs that cover each other and search rooms**, flush with grenades; "flanking" was *emergent* (move to the only valid cover on the player's side), not scripted. *(Orkin GDC 2006; DiVA thesis.)*
- **→ Rule:** your fan-out is right in spirit — but drive it from the **PLS**: push to the last-seen point first, then expand the divide-search radius **as time passes**. **Any fresh gunshot/sighting resets the PLS and re-converges everyone** (you have this — make it authoritative and shared). Remember cleared rooms; keep overwatch rather than re-checking randomly.

### (d) Cover, suppression, fire discipline
- **Finding (3-0):** suppressive fire's **primary effect is psychological** — it makes the target feel unable to do anything but seek cover, and *that* is what enables friendly movement. *(Wikipedia/USMC; FM 3-21.71.)*
- **→ Rule:** one defender **holds a doorway/chokepoint and suppresses** (aimed bursts, fire discipline) to pin you while another **bounds/flanks** to close. Incoming fire should make a defender *seek cover* even without hits. (Ties to your still-open cover-system and suppressive-fire items.)

### (e) Morale / breaking point
- **Finding (3-0 qualitative, 2-1 on numbers):** units break against a **background of confusion, fatigue, mounting casualties and lost morale — not a fixed casualty count.** Major triggers: **loss of leadership**, loss of belief in the objective, surprise, loss of control. *(Dupuy Institute.)*
- **→ Rule:** drive a squad/per-defender **morale variable** from *leader-alive, relative numbers, casualties-over-time, surprise*; trigger flee/hide/surrender **probabilistically**, and **specifically spike it on leader death**. (You already re-elect a leader — pair it with a morale dip.)
- **Caveat:** the 3% suppressed / 10% neutralised / 30% destroyed figures are **fire-support targeting definitions, not morale thresholds** — use only as rough loss bands, not a hard break number.

### (f) The hostage guard, torn
- **No source directly covers a guard tethered to a fixed asset** (flagged open question). Derive from fixed-asset-defence + fire-discipline principles: he holds his position, watches the approach/chokepoint, and does not abandon his charge to reinforce.
- **→ Rule:** this validates the **pressured door↔hostage pacing** already built — keep it; it's a reasonable derivation, just not citable.

### Time constants (semi-trained calibration)
| Behaviour | Doctrine anchor | Use for semi-trained NPCs |
|---|---|---|
| Return fire + take cover after contact | **~3 s** (CALL 96-3) | **3–6 s** with hesitation variance |
| "Contact!" propagation to squad | short sequential chain | small delay (not instant) |
| Search radius growth after losing LOS | **speed × time** (SAR) | grow the hunt radius over time from PLS |
| Give up a confirmed *lost* contact | **not in doctrine** | long / area-based, NOT a short clock |
| Leader death → coordination hit | major break trigger | morale dip + re-elect (already have) |

---

## Architecture the research points to (F.E.A.R.'s model)
The most directly-implementable published model for exactly this is **F.E.A.R.'s two-layer GOAP squad AI** *(Orkin GDC 2006; DiVA thesis, both primary/verified)*:
- **Individual layer:** each defender runs its own behaviour (target, attack, dodge, take cover).
- **Squad coordination layer:** fills required participant slots, issues orders (suppress, search-pair, flank), and **monitors members until they fulfil the order or die.**
- Search = **split into pairs, cover each other, clear rooms, flush.** Flanking = emergent (move to valid cover on the player's side).
- **Your `Squad` class is already this layer** — the fix is to add the **shared confirmed-contact / PLS blackboard** and the **persistent-hunt** state on top of it, instead of per-NPC "confirmed" + short give-up timers.

## What is NOT grounded (be honest in the sim)
- **Hostage-guard-torn behaviour** — derived, not cited.
- **Exact give-up duration for a lost contact** — a tuning choice.
- **The 3-second figure** — an order-of-magnitude anchor (1996 night-fire scenario; core standard persists), widen for semi-trained.
- **Morale 3/10/30%** — targeting definitions repurposed; rough bands only.
- **Halo / Last of Us search-timeout specifics** — not verified this pass.

## Sources (verified, primary/secondary)
- CALL Handbook 96-3, Battle Drill 2 (React to Contact) — *primary*
- FM 3-21.71 App E (React to Contact, MOUT clearing, Break Contact) — *primary*
- FM 7-8 / ATP 3-21.8 Battle Drills; 550cord; infantrydrills — *secondary*
- Phillips et al., *Wilderness & Environmental Medicine* 2014 (PLS/LKP) — *primary*; sarmath IPP — *secondary*; NIST PLS glossary — *primary*
- Orkin, "Three States and a Plan: The AI of F.E.A.R." GDC 2006 — *primary*; Game Developer GOAP article — *secondary*; DiVA thesis (two-layer GOAP) — *primary*
- Wikipedia/USMC Suppressive fire — *secondary*; Dupuy Institute breakpoints — *secondary*
