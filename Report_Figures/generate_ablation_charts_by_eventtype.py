"""Generate print-quality charts for the Module 2 NPC-selection ablation study,
covering all THREE role-favouring event types (not just GunshotHeard).

WHY THIS SCRIPT EXISTS (separate from generate_ablation_charts.py):
The original ablation study (generate_ablation_charts.py, fig_ablation_1..5)
only ever tested GunshotHeard events. Since NPCSelector.GetRoleBonus only
awards a role bonus to Guard on RoomBreached and to Leader on AllyDownSeen,
a GunshotHeard-only run gives ZERO evidence for those two bonuses — only for
Roamer/GunshotHeard=2.0. This script processes a CSV produced by the UPDATED
AblationExperimentRunner (Assets/Scripts/Tests/AblationExperimentRunner.cs),
which now tests GunshotHeard, RoomBreached, AND AllyDownSeen against the SAME
50 event positions, so all three role bonuses get real evidence.

Outputs are NEW files (fig_ablation_<eventtype>_*.png) — this script does NOT
touch or regenerate fig_ablation_1..5, which belong to the original, already-
published GunshotHeard-only study and its report (MODULE2_ABLATION_STUDY_REPORT.md
section 3, Figs 1-5). Keep both studies' evidence separately citable.

Data source: set DATA below to the CSV path written by AblationExperimentRunner
after a REAL run (Application.persistentDataPath/Telemetry/AblationResults_*.csv,
copied into Report_Figures/ablation_data/). This script does not run unless that
file exists — there is no synthetic/placeholder data path.

Palette: same validated reference palette as generate_ablation_charts.py.
"""
import csv
import os
import sys
from collections import defaultdict

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

OUT = '.'
# ↓↓↓ Point this at the REAL CSV from a live AblationExperimentRunner run before
#     executing this script. There is no fallback / synthetic data.
DATA = 'ablation_data/AblationResults_MULTIEVENT_20260809_150537.csv'

BLUE, ORANGE = '#2a78d6', '#eb6834'
GREY, ACCENT = '#898781', '#2a78d6'
INK, INK2, MUTED = '#0b0b0b', '#52514e', '#898781'
GRID, BASE = '#e1e0d9', '#c3c2b7'
MODE_COLOR = {
    'Full': '#104281', 'NoDistance': '#2a78d6', 'NoRole': '#86b6ef',
    'NoState': '#eb6834', 'NoLOS': '#c3843f',
}
MODES = ['Full', 'NoDistance', 'NoRole', 'NoState', 'NoLOS']

# event_type → (favoured role, that role's bonus value, short slug for filenames)
EVENT_TYPES = {
    'GunshotHeard': {'role': 'Roamer', 'bonus': 2.0, 'slug': 'gunshotheard'},
    'RoomBreached': {'role': 'Guard',  'bonus': 3.0, 'slug': 'roombreached'},
    'AllyDownSeen': {'role': 'Leader', 'bonus': 1.5, 'slug': 'allydownseen'},
}

plt.rcParams.update({
    'font.family': 'sans-serif',
    'font.sans-serif': ['Segoe UI', 'DejaVu Sans'],
    'font.size': 9,
    'axes.edgecolor': BASE,
    'axes.labelcolor': INK2,
    'axes.titlecolor': INK,
    'text.color': INK,
    'xtick.color': MUTED,
    'ytick.color': MUTED,
    'xtick.labelcolor': INK2,
    'ytick.labelcolor': INK2,
    'figure.facecolor': 'white',
    'axes.facecolor': 'white',
    'savefig.facecolor': 'white',
    'axes.spines.top': False,
    'axes.spines.right': False,
    'grid.color': GRID,
    'grid.linewidth': 0.6,
})
DPI = 220
W = 5.6


def tidy(ax, ygrid=True):
    ax.grid(axis='y', visible=ygrid, zorder=0)
    ax.set_axisbelow(True)
    for s in ('left', 'bottom'):
        ax.spines[s].set_linewidth(0.8)
    ax.tick_params(length=3, width=0.7)


def load(path):
    if not os.path.exists(path):
        sys.exit(
            f"\n[generate_ablation_charts_by_eventtype] No data file at '{path}'.\n"
            "This script only runs against a REAL AblationExperimentRunner CSV — "
            "there is no synthetic fallback.\n\n"
            "1. Open the project in Unity, enter Play mode in any scene.\n"
            "2. Add an empty GameObject, attach AblationExperimentRunner\n"
            "   (npcCount=8, eventCount=50, seed=42, addCentreWall=true).\n"
            "3. Right-click its component -> 'EXP -> Run Ablation Experiment'.\n"
            "4. Copy the resulting CSV from\n"
            "   Application.persistentDataPath/Telemetry/AblationResults_<stamp>.csv\n"
            "   into Report_Figures/ablation_data/, then update DATA at the top of\n"
            "   this script to point at it.\n"
        )
    rows = []
    with open(path, newline='') as f:
        for r in csv.DictReader(f):
            if 'event_type' not in r:
                sys.exit(
                    "This CSV has no 'event_type' column — it's from the OLD "
                    "single-event-type runner. Re-run AblationExperimentRunner "
                    "(now updated to test all three event types) to get a "
                    "compatible file."
                )
            r['event_id'] = int(r['event_id'])
            r['distance_m'] = float(r['distance_m']) if r['distance_m'] != '-' else None
            r['had_los'] = int(r['had_los']) if r['had_los'] != '-' else None
            r['state_score'] = float(r['state_score']) if r['state_score'] != '-' else None
            rows.append(r)
    return rows


def compute_stats(rows, event_type):
    by_mode = defaultdict(list)
    for r in rows:
        if r['event_type'] == event_type:
            by_mode[r['ablation_mode']].append(r)

    n_events = max((r['event_id'] for r in by_mode['Full']), default=-1) + 1
    full_pick = {r['event_id']: r['selected_npc'] for r in by_mode['Full']}

    stats = {}
    for mode in MODES:
        recs = by_mode[mode]
        roles = defaultdict(int)
        for r in recs:
            roles[r['selected_role']] += 1
        agree = sum(1 for r in recs if r['selected_npc'] == full_pick.get(r['event_id']))
        stats[mode] = {
            'roles': dict(roles),
            'agree_with_full': agree / n_events if n_events else 0,
        }
    return stats, n_events


# ── Fig: role distribution per ablation mode, for ONE event type ────────────
def fig_role_distribution(stats, event_type_key, slug, favoured_role):
    fig, ax = plt.subplots(figsize=(W, 3.0), dpi=DPI)
    roles = ['Guard', 'Roamer', 'Leader']
    role_color = {'Guard': BLUE, 'Roamer': '#86b6ef', 'Leader': ORANGE}
    n = len(roles)
    bw = 0.24
    for i, role in enumerate(roles):
        xs = [j + (i - (n - 1) / 2) * (bw + 0.03) for j in range(len(MODES))]
        ys = [stats[m]['roles'].get(role, 0) for m in MODES]
        ax.bar(xs, ys, width=bw, color=role_color[role], linewidth=0, label=role, zorder=3)
    ax.set_xticks(range(len(MODES)))
    ax.set_xticklabels(MODES)
    ax.set_ylabel('Events selecting role (of N)')
    ax.set_title(f'Who gets picked for {event_type_key} (favours {favoured_role})',
                 loc='left', fontsize=10, fontweight='bold')
    ax.legend(frameon=False, ncol=3, loc='upper center', bbox_to_anchor=(0.5, -0.14))
    tidy(ax)
    fig.tight_layout()
    fname = f'fig_ablation_{slug}_1_role_distribution.png'
    fig.savefig(os.path.join(OUT, fname))
    plt.close(fig)
    return fname


# ── Fig: agreement with Full, for ONE event type ─────────────────────────────
def fig_agreement(stats, event_type_key, slug):
    modes = ['NoDistance', 'NoRole', 'NoState', 'NoLOS']
    fig, ax = plt.subplots(figsize=(W, 2.6), dpi=DPI)
    ys = [stats[m]['agree_with_full'] * 100 for m in modes]
    xs = range(len(modes))
    ax.bar(xs, ys, width=0.5, color=[MODE_COLOR[m] for m in modes], linewidth=0, zorder=3)
    for x, y in zip(xs, ys):
        ax.text(x, y + 1.5, f'{y:.0f}%', ha='center', fontsize=8, color=INK2)
    ax.set_xticks(xs)
    ax.set_xticklabels(modes)
    ax.set_ylim(0, 100)
    ax.set_ylabel('% of events — same NPC as Full')
    ax.set_title(f'Selection agreement vs. Full — {event_type_key}',
                 loc='left', fontsize=10, fontweight='bold')
    tidy(ax)
    fig.tight_layout()
    fname = f'fig_ablation_{slug}_2_agreement.png'
    fig.savefig(os.path.join(OUT, fname))
    plt.close(fig)
    return fname


# ── Fig: does bonus MAGNITUDE track measured DOMINANCE? ─────────────────────
# For each event type, under Full, what % of events did the intended/favoured
# role actually win? Plotted against that role's bonus value. A monotonic
# relationship is evidence the RELATIVE ordering of the three bonus constants
# (Guard 3.0 > Roamer 2.0 > Leader 1.5) produces the intended relative
# behaviour — even without a literature source for the absolute numbers.
def fig_bonus_vs_dominance(all_stats):
    fig, ax = plt.subplots(figsize=(W, 3.2), dpi=DPI)
    pts = []
    for et, meta in EVENT_TYPES.items():
        s = all_stats[et]['Full']
        n = sum(s['roles'].values())
        dominance = 100 * s['roles'].get(meta['role'], 0) / n if n else 0
        pts.append((meta['bonus'], dominance, meta['role'], et))
    pts.sort()
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    ax.plot(xs, ys, '-', color=GREY, linewidth=1.2, zorder=2)
    ax.scatter(xs, ys, s=70, color=ACCENT, zorder=3)
    for x, y, role, et in pts:
        ax.annotate(f'{role}\n({et})', (x, y), textcoords='offset points',
                    xytext=(8, 6), fontsize=8, color=INK2)
    ax.set_xlabel('Role bonus value (roleBonus table, NPCSelector.cs)')
    ax.set_ylabel("% of Full-mode events won by that role\n(on its own favoured event type)")
    ax.set_title('Does bonus magnitude track measured dominance?',
                 loc='left', fontsize=10, fontweight='bold')
    ax.set_ylim(0, 105)
    tidy(ax)
    fig.tight_layout()
    fname = 'fig_ablation_6_bonus_vs_dominance.png'
    fig.savefig(os.path.join(OUT, fname))
    plt.close(fig)
    return fname


if __name__ == '__main__':
    rows = load(DATA)

    all_stats = {}
    print('=' * 78)
    print('Multi-event-type ablation summary')
    print('=' * 78)
    for et, meta in EVENT_TYPES.items():
        stats, n = compute_stats(rows, et)
        all_stats[et] = stats
        print(f"\n{et}  (favours {meta['role']}, bonus={meta['bonus']}, n={n} events)")
        print(f"{'Mode':<12}{'Guard':>7}{'Roamer':>8}{'Leader':>8}{'AgreeFull%':>12}")
        for mode in MODES:
            s = stats[mode]
            g = s['roles'].get('Guard', 0)
            ro = s['roles'].get('Roamer', 0)
            l = s['roles'].get('Leader', 0)
            print(f"{mode:<12}{g:>7}{ro:>8}{l:>8}{s['agree_with_full']*100:>11.0f}%")

        f1 = fig_role_distribution(stats, et, meta['slug'], meta['role'])
        f2 = fig_agreement(stats, et, meta['slug'])
        print(f"  -> {f1}, {f2}")

    f3 = fig_bonus_vs_dominance(all_stats)
    print(f"\n-> {f3}")
    print('\nCharts written to', os.path.abspath(OUT))
