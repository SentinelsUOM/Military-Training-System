"""Generate print-quality charts + summary stats for the Module 2 NPC-selection
WEIGHT SENSITIVITY sweep (the "lighter" generalization study, as distinct from
the binary on/off ablation study in generate_ablation_charts.py).

Data source: `ablation_data/WeightSensitivity_20260801_133111.csv`, produced by
a REAL run of `WeightSensitivityRunner` inside the Unity Editor (6000.3.1f1) via
MCP on 2026-08-01 — 3 event types (GunshotHeard/RoomBreached/AllyDownSeen, i.e.
the three rows of NPCSelector's role-bonus table) x 3 independent random seeds
(42/43/44) x 4 weights (Distance/Role/State/LOS) x 7 sweep values
(0/0.5/1/1.5/2/3/4) x 100 shared events per point = 25,200 selection calls.

For each (event_type, seed, weight, value) cell we measure % of the 100 events
where the selected NPC matches the all-defaults baseline for that same world —
i.e. how much the pick DRIFTS as that one weight is turned up or down, holding
the world (NPC layout, roles, states) and the other three weights fixed.

Palette: validated instance of the dataviz reference palette — categorical
slots 1/2/3 (blue/orange/aqua) for the three event types, matching
generate_charts.py / generate_ablation_charts.py.
"""
import csv
import os
import statistics
from collections import defaultdict

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

OUT = '.'
DATA = 'ablation_data/WeightSensitivity_20260801_133111.csv'

BLUE, ORANGE, AQUA = '#2a78d6', '#eb6834', '#1baf7a'
EVENT_COLOR = {'GunshotHeard': BLUE, 'RoomBreached': ORANGE, 'AllyDownSeen': AQUA}
EVENT_ORDER = ['GunshotHeard', 'RoomBreached', 'AllyDownSeen']

INK, INK2, MUTED = '#0b0b0b', '#52514e', '#898781'
GRID, BASE = '#e1e0d9', '#c3c2b7'

WEIGHTS = ['Distance', 'Role', 'State', 'LOS']
DEFAULT_VALUE = {'Distance': 2.0, 'Role': 1.0, 'State': 1.0, 'LOS': 1.0}
SWEEP_VALUES = [0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0]

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


def tidy(ax):
    ax.grid(axis='y', visible=True, zorder=0)
    ax.set_axisbelow(True)
    for s in ('left', 'bottom'):
        ax.spines[s].set_linewidth(0.8)
    ax.tick_params(length=3, width=0.7)


# ── Load ──────────────────────────────────────────────────────────────────────
rows = []
with open(DATA, newline='') as f:
    for r in csv.DictReader(f):
        r['weight_value'] = float(r['weight_value'])
        r['agree_with_baseline'] = int(r['agree_with_baseline'])
        rows.append(r)

# key: (event_type, weight, value, seed) -> list of agree flags
cells = defaultdict(list)
for r in rows:
    key = (r['event_type'], r['swept_weight'], r['weight_value'], r['seed'])
    cells[key].append(r['agree_with_baseline'])

# agreement %  per (event_type, weight, value, seed)
agree_pct = {k: 100.0 * sum(v) / len(v) for k, v in cells.items()}

seeds = sorted({r['seed'] for r in rows})

# aggregate across seeds -> mean, stdev  per (event_type, weight, value)
agg = defaultdict(list)
for (ev, w, val, seed), pct in agree_pct.items():
    agg[(ev, w, val)].append(pct)

summary = {}
for (ev, w, val), pcts in agg.items():
    summary[(ev, w, val)] = (statistics.mean(pcts),
                              statistics.stdev(pcts) if len(pcts) > 1 else 0.0)

print('=' * 78)
print(f'Weight sensitivity sweep  ({len(seeds)} seeds x {len(EVENT_ORDER)} event types x '
      f'{len(SWEEP_VALUES)} values per weight)')
print('=' * 78)
for w in WEIGHTS:
    print(f'\n{w}Weight (default = {DEFAULT_VALUE[w]}):')
    label = 'value'
    header = f'{label:>7}' + ''.join(f'{ev:>16}' for ev in EVENT_ORDER)
    print(header)
    for val in SWEEP_VALUES:
        line = f"{val:>7.1f}"
        for ev in EVENT_ORDER:
            mean, sd = summary[(ev, w, val)]
            line += f"{mean:>11.1f}%±{sd:>3.1f}"
        print(line)
print('=' * 78)


# ── One chart per weight: agreement% (y) vs weight value (x), one line per event type ──
def fig_weight(weight_name):
    fig, ax = plt.subplots(figsize=(W, 3.2), dpi=DPI)
    for ev in EVENT_ORDER:
        means = [summary[(ev, weight_name, v)][0] for v in SWEEP_VALUES]
        sds   = [summary[(ev, weight_name, v)][1] for v in SWEEP_VALUES]
        color = EVENT_COLOR[ev]
        ax.plot(SWEEP_VALUES, means, color=color, linewidth=2, marker='o',
                 markersize=4, label=ev, zorder=3)
        lo = [m - s for m, s in zip(means, sds)]
        hi = [m + s for m, s in zip(means, sds)]
        ax.fill_between(SWEEP_VALUES, lo, hi, color=color, alpha=0.15, linewidth=0, zorder=2)

    default_v = DEFAULT_VALUE[weight_name]
    ax.axvline(default_v, color=MUTED, linestyle='--', linewidth=1, zorder=1)
    ax.text(default_v, 4, f'  default = {default_v}', color=INK2, fontsize=8, va='bottom')

    ax.set_ylim(0, 105)
    ax.set_xlabel(f'{weight_name}Weight value')
    ax.set_ylabel('% selections matching all-defaults baseline')
    ax.set_title(f'{weight_name} term — selection stability across the weight range',
                 loc='left', fontsize=10, fontweight='bold')
    ax.legend(frameon=False, ncol=1, loc='lower left', fontsize=8)
    tidy(ax)
    fig.tight_layout()
    fname = f'fig_sensitivity_{weight_name.lower()}.png'
    fig.savefig(os.path.join(OUT, fname))
    plt.close(fig)
    return fname


if __name__ == '__main__':
    for w in WEIGHTS:
        print('wrote', fig_weight(w))
