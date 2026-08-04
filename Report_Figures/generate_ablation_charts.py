"""Generate print-quality charts + summary stats for the Module 2 NPC-selection
ablation study.

Data source: `ablation_data/AblationResults_20260801_120355.csv`, produced by a
REAL run of `AblationExperimentRunner` inside the Unity Editor (6000.3.1f1) via
MCP on 2026-08-01 — 8 terrorist NPCs (round-robin Guard/Roamer/Leader roles and
Idle/Suspicious/Alert/TakeCover/Retreat states), 50 random GunshotHeard events in
a 30x30 arena with a centre wall for LOS occlusion, seed=42. Five ablation modes
per event: Full, NoDistance, NoRole, NoState, NoLOS (see NPCSelector.SetAblation).

Palette: validated instance of the dataviz reference palette (same as
generate_charts.py) — categorical blue/orange, ordinal blue ramp, ink/grid greys.
"""
import csv
import os
from collections import defaultdict

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

OUT = '.'
DATA = 'ablation_data/AblationResults_20260801_120355.csv'

BLUE, ORANGE = '#2a78d6', '#eb6834'
GREY, ACCENT = '#898781', '#2a78d6'
INK, INK2, MUTED = '#0b0b0b', '#52514e', '#898781'
GRID, BASE = '#e1e0d9', '#c3c2b7'
MODE_COLOR = {
    'Full': '#104281', 'NoDistance': '#2a78d6', 'NoRole': '#86b6ef',
    'NoState': '#eb6834', 'NoLOS': '#c3843f',
}
MODES = ['Full', 'NoDistance', 'NoRole', 'NoState', 'NoLOS']

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


# ── Load ──────────────────────────────────────────────────────────────────────
rows = []
with open(DATA, newline='') as f:
    for r in csv.DictReader(f):
        r['event_id'] = int(r['event_id'])
        r['distance_m'] = float(r['distance_m']) if r['distance_m'] != '-' else None
        r['had_los'] = int(r['had_los']) if r['had_los'] != '-' else None
        r['state_score'] = float(r['state_score']) if r['state_score'] != '-' else None
        rows.append(r)

n_events = max(r['event_id'] for r in rows) + 1
by_mode = defaultdict(list)
for r in rows:
    by_mode[r['ablation_mode']].append(r)

full_pick = {r['event_id']: r['selected_npc'] for r in by_mode['Full']}

# ── Stats ─────────────────────────────────────────────────────────────────────
stats = {}
for mode in MODES:
    recs = by_mode[mode]
    roles = defaultdict(int)
    for r in recs:
        roles[r['selected_role']] += 1
    dists = [r['distance_m'] for r in recs if r['distance_m'] is not None]
    states = [r['state_score'] for r in recs if r['state_score'] is not None]
    los_hits = [r['had_los'] for r in recs if r['had_los'] is not None]
    agree = sum(1 for r in recs if r['selected_npc'] == full_pick[r['event_id']])
    stats[mode] = {
        'roles': dict(roles),
        'mean_dist': sum(dists) / len(dists) if dists else 0,
        'mean_state': sum(states) / len(states) if states else 0,
        'los_rate': sum(los_hits) / len(los_hits) if los_hits else 0,
        'agree_with_full': agree / n_events,
    }

print('=' * 70)
print(f'Ablation study summary  (n = {n_events} events, {len(MODES)} modes)')
print('=' * 70)
print(f"{'Mode':<12}{'Guard':>7}{'Roamer':>8}{'Leader':>8}{'MeanDist':>10}{'MeanState':>11}{'LOS%':>7}{'AgreeFull%':>12}")
for mode in MODES:
    s = stats[mode]
    g, ro, l = s['roles'].get('Guard', 0), s['roles'].get('Roamer', 0), s['roles'].get('Leader', 0)
    print(f"{mode:<12}{g:>7}{ro:>8}{l:>8}{s['mean_dist']:>10.2f}{s['mean_state']:>11.2f}"
          f"{s['los_rate']*100:>6.0f}%{s['agree_with_full']*100:>11.0f}%")
print('=' * 70)


# ── Fig A: role distribution per ablation mode (grouped bars) ────────────────
def fig_role_distribution():
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
    ax.set_ylabel('Events selecting role (of 50)')
    ax.set_title('Who gets picked, by ablation mode', loc='left', fontsize=10, fontweight='bold')
    ax.legend(frameon=False, ncol=3, loc='upper center', bbox_to_anchor=(0.5, -0.14))
    tidy(ax)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, 'fig_ablation_1_role_distribution.png'))
    plt.close(fig)


# ── Fig B: agreement with Full baseline ──────────────────────────────────────
def fig_agreement():
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
    ax.set_title('Selection agreement vs. the full (all-4-terms) formula',
                 loc='left', fontsize=10, fontweight='bold')
    tidy(ax)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, 'fig_ablation_2_agreement_with_full.png'))
    plt.close(fig)


# ── Fig C: mean distance of the selected responder, per mode ────────────────
def fig_mean_distance():
    fig, ax = plt.subplots(figsize=(W, 2.6), dpi=DPI)
    ys = [stats[m]['mean_dist'] for m in MODES]
    xs = range(len(MODES))
    ax.bar(xs, ys, width=0.5, color=[MODE_COLOR[m] for m in MODES], linewidth=0, zorder=3)
    for x, y in zip(xs, ys):
        ax.text(x, y + 0.3, f'{y:.1f}m', ha='center', fontsize=8, color=INK2)
    ax.set_xticks(xs)
    ax.set_xticklabels(MODES)
    ax.set_ylabel('Mean distance of selected NPC (m)')
    ax.set_title('Mean distance of the selected NPC, by mode',
                 loc='left', fontsize=10, fontweight='bold')
    tidy(ax)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, 'fig_ablation_3_mean_distance.png'))
    plt.close(fig)


# ── Fig D: mean state-readiness of the selected responder, per mode ─────────
def fig_mean_state():
    fig, ax = plt.subplots(figsize=(W, 2.6), dpi=DPI)
    ys = [stats[m]['mean_state'] for m in MODES]
    xs = range(len(MODES))
    ax.bar(xs, ys, width=0.5, color=[MODE_COLOR[m] for m in MODES], linewidth=0, zorder=3)
    for x, y in zip(xs, ys):
        ax.text(x, y + 0.02, f'{y:.2f}', ha='center', fontsize=8, color=INK2)
    ax.set_xticks(xs)
    ax.set_xticklabels(MODES)
    ax.set_ylim(0, 1.05)
    ax.set_ylabel('Mean StateScore of selected NPC (0-1)')
    ax.set_title('Mean state-readiness of the selected NPC, by mode',
                 loc='left', fontsize=10, fontweight='bold')
    tidy(ax)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, 'fig_ablation_4_mean_state_score.png'))
    plt.close(fig)


# ── Fig E: line-of-sight hit rate of the selected responder, per mode ───────
def fig_los_rate():
    fig, ax = plt.subplots(figsize=(W, 2.6), dpi=DPI)
    ys = [stats[m]['los_rate'] * 100 for m in MODES]
    xs = range(len(MODES))
    ax.bar(xs, ys, width=0.5, color=[MODE_COLOR[m] for m in MODES], linewidth=0, zorder=3)
    for x, y in zip(xs, ys):
        ax.text(x, y + 1.5, f'{y:.0f}%', ha='center', fontsize=8, color=INK2)
    ax.set_xticks(xs)
    ax.set_xticklabels(MODES)
    ax.set_ylim(0, 100)
    ax.set_ylabel('% selected NPC had clear LOS to event')
    ax.set_title('Selected-NPC line-of-sight hit rate, by mode',
                 loc='left', fontsize=10, fontweight='bold')
    tidy(ax)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, 'fig_ablation_5_los_hit_rate.png'))
    plt.close(fig)


if __name__ == '__main__':
    fig_role_distribution()
    fig_agreement()
    fig_mean_distance()
    fig_mean_state()
    fig_los_rate()
    print('Charts written to', os.path.abspath(OUT))
