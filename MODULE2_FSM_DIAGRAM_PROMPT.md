# Module 2 — Terrorist FSM diagram: generation prompt

Source of truth: `Assets/Scripts/NPC/TerroristState.cs` (the enum) and
`Assets/Scripts/NPC/TerroristController.cs` (`HandleEvent`, the transition switch).
Tier gating matches Table 6.3 of the final report.

---

## The prompt (copy-paste)

> Draw a clean, print-quality **state-transition diagram** of a terrorist NPC's finite state
> machine for a VR military-training simulator. Flat vector style, white background, no
> shadows, no 3D, no icons, no clip-art. Rounded rectangles for states, thin arrows with
> small solid arrowheads, all text horizontal and fully legible. Sans-serif (Segoe UI or
> Inter). Landscape, roughly 3:2.
>
> **Seven states**, laid out left to right in this arrangement:
>
> - `Idle` — far left, vertically centred
> - `Suspicious` — right of Idle, upper row
> - `Alert` — right of Suspicious, vertically centred (this is the hub; most arrows meet here)
> - `Engage` — right of Alert, vertically centred
> - `TakeCover` — right of Engage, upper row
> - `Retreat` — right of Engage, lower row
> - `Down` — far right, vertically centred, drawn as the terminal state (grey fill, double
>   border or thicker outline)
>
> **Transitions**, each arrow labelled with the event that causes it:
>
> | From | To | Label on the arrow |
> |---|---|---|
> | Idle | Suspicious | `GunshotHeard` |
> | Suspicious | Alert | `GunshotHeard` / `PlayerSeen` |
> | Idle | Alert | `PlayerSeen` · `DoorOpened` · `RoomBreached` |
> | Idle / Suspicious / Alert | Alert | `AllyDownSeen` · `TerroristDown` |
> | Alert | Engage | `TargetConfirmed` (sustained line of sight) |
> | Suspicious | Engage | `TargetConfirmed` |
> | Engage | Alert | `PlayerLost` |
> | Engage | TakeCover | posture escalation (mounting pressure) |
> | TakeCover | Engage | cover reached, resume fire |
> | Engage | Retreat | health low — once per life |
> | any state | Down | `health = 0` |
>
> **Colour-code the states by the AI intelligence tier that unlocks them**, using a light-to-dark
> blue ramp so capability visibly accumulates, and **also put a small italic text tag under each
> gated state** so the meaning survives greyscale printing:
>
> - Lightest blue `#cde2fb` — available at **all tiers**: `Idle`, `Suspicious`, `Alert`, `Engage`
> - Mid blue `#9ec5f4` — **Intermediate and above**, tag *"Intermediate +"*: the investigate /
>   squad-hunt behaviour attached to `Alert`
> - Dark blue `#6da7ec` — **Advanced only**, tag *"Advanced only"*: `TakeCover`, `Retreat`
> - Grey `#e1e0d9` — terminal: `Down`
>
> Include a small **legend** along the bottom with the three tier swatches labelled
> "all tiers", "Intermediate and above", "Advanced only".
>
> Add one **annotation callout** pointing at the `Alert` state, in orange `#eb6834`, reading:
> *"Investigating a disturbance and joining the shared squad hunt are behaviours of the Alert
> state, gated at Intermediate and above — not separate states."*
>
> Title at the top left, bold: **"Terrorist finite-state machine — states gated by AI intelligence tier"**.
>
> Arrow-label text must never overlap a box or another label; route arrows around boxes if needed.

---

## Optional second panel (the squad hunt)

If you want the same two-panel figure as before, append this to the prompt:

> Below the state machine, separated by white space, add a second panel titled
> **"Squad layer — shared, persistent hunt lifecycle"**: four boxes in a left-to-right chain —
> `ReportConfirmedContact` → `BeginHunt (anchor: PointLastSeen)` → `Radius grows = speed x elapsed`
> → `EndHunt (budget approx. 45 s)`. Draw three curved orange `#eb6834` arrows returning from the
> later boxes back to `BeginHunt`, grouped under the italic orange heading *"re-anchor the hunt on:"*
> with the labels *fresh sighting*, *fresh gunshot*, *squad-mate down*. Underneath, spanning the
> full width, a dashed orange-bordered box on pale peach `#fdf0e8` reading
> **"Shared squad blackboard — PointLastSeen · hunt state · leader"**, connected up to each of the
> four boxes with dashed orange lines, with the italic caption
> *"read and written by every squad member, not private to the sighter"*.

---

## Notes before you generate

**1. What changed from the old figure.** The old `fig_5_4` showed a state called *Investigate* and
merged *Cover / Retreat* into one box, and it omitted *Suspicious* entirely. The code has seven
states — `Idle, Suspicious, Alert, Engage, TakeCover, Retreat, Down` — and investigating is an
action taken while in `Alert`, not a state. The prompt above is the code-accurate version. If you
would rather keep the diagram matching the simplified prose in Section 5.4 of the report, swap
`Suspicious` → `Investigate` and merge `TakeCover`/`Retreat` into one `Cover / Retreat` box; but
the seven-state version is the one you can defend if a panel member opens the source.

**2. Text-to-image models are poor at this.** Diagrams with this much small labelled text usually
come back with garbled words. Better options, in order:

- **Mermaid** (`stateDiagram-v2`) or **draw.io** — paste the transition table above and edit.
- **Regenerate the existing matplotlib figure at a higher resolution**, which is the real fix for
  "not clear": in [generate_diagrams.py:32-33](Report_Figures/generate_diagrams.py#L32-L33) change
  `DPI = 220` to `DPI = 600` and `W = 5.6` to `W = 9.0`, then re-run it. Same layout, roughly four
  times the pixels — it will stay sharp at the 6.5-inch size it occupies on the slide.

**3. Whatever you generate, keep it under `Report_Figures/`** with the same filename so the report
figure and the presentation slide both pick it up without further edits.
