"""Generate print-quality charts for the Sentinels final report.

Every value here is transcribed from `evaluation results.pdf` (dashboard export,
30 Jul 2026) and cross-checked for internal consistency.

Palette: validated instance of the dataviz reference palette.
  ordinal blue ramp (AI tiers)  #86b6ef / #2a78d6 / #104281   [--ordinal PASS]
  categorical facets            #2a78d6 (blue) / #eb6834 (orange)  [PASS]
  emphasis pair                 #898781 grey + #2a78d6 accent
  chrome  ink #0b0b0b · secondary #52514e · muted #898781
          gridline #e1e0d9 · baseline #c3c2b7 · surface #ffffff
"""
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import MultipleLocator

OUT = 'charts'
import os
os.makedirs(OUT, exist_ok=True)

TIER = ['#86b6ef', '#2a78d6', '#104281']
BLUE, ORANGE = '#2a78d6', '#eb6834'
GREY, ACCENT = '#898781', '#2a78d6'
INK, INK2, MUTED = '#0b0b0b', '#52514e', '#898781'
GRID, BASE = '#e1e0d9', '#c3c2b7'

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
W = 5.6  # inches — fits the 5.77in text column


def tidy(ax, xgrid=False, ygrid=False):
    ax.grid(axis='x' if xgrid else 'y', visible=xgrid or ygrid, zorder=0)
    ax.set_axisbelow(True)
    for s in ('left', 'bottom'):
        ax.spines[s].set_linewidth(0.8)
    ax.tick_params(length=3, width=0.7)


# ── Figure 7.11 — believability by AI tier (grouped bars, small multiples) ─────
def fig_7_11():
    tiers = ['Basic', 'Intermediate', 'Advanced']
    g1 = {'Perceived\nIntelligence': [1.6, 3.7, 4.4],
          'Animacy\n(life-like)': [2.0, 3.8, 4.1],
          'Tactical\nrealism': [1.7, 3.5, 4.4]}
    g2 = {'UEQ\nPragmatic': [4.6, 5.3, 5.7],
          'UEQ\nHedonic': [4.7, 5.4, 5.8]}

    fig, (axA, axB) = plt.subplots(
        1, 2, figsize=(W, 2.9), dpi=DPI,
        gridspec_kw={'width_ratios': [3, 2], 'wspace': 0.34})

    def panel(ax, data, ymax, ylab, ticks):
        n = len(tiers)
        bw = 0.24
        for i, tier in enumerate(tiers):
            xs = [j + (i - (n - 1) / 2) * (bw + 0.035) for j in range(len(data))]
            ys = [v[i] for v in data.values()]
            ax.bar(xs, ys, width=bw, color=TIER[i], linewidth=0,
                   label=tier if ax is axA else None, zorder=3)
            for x, y in zip(xs, ys):
                ax.text(x, y + ymax * 0.022, f'{y:.1f}', ha='center', va='bottom',
                        fontsize=6.6, color=INK2, zorder=4)
        ax.set_xticks(range(len(data)))
        ax.set_xticklabels(list(data), fontsize=7.2)
        ax.set_ylim(0, ymax)
        ax.yaxis.set_major_locator(MultipleLocator(ticks))
        ax.set_ylabel(ylab, fontsize=7.6)
        tidy(ax, ygrid=True)
        ax.grid(axis='y', visible=True, zorder=0)

    panel(axA, g1, 5.4, 'mean rating (out of 5)', 1)
    panel(axB, g2, 7.4, 'mean rating (out of 7)', 1)
    axA.legend(frameon=False, fontsize=7.2, loc='upper left',
               bbox_to_anchor=(0, 1.16), ncol=3, handlelength=0.9,
               handleheight=0.9, columnspacing=1.1, labelcolor=INK2)
    fig.savefig(f'{OUT}/fig_7_11_believability_by_tier.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


# ── Figure 7.14 — reaction time per participant vs published references ───────
def fig_7_14():
    rows = [('P0001', 0.50), ('P0002', 1.20), ('P0003', 0.80), ('P0004', 0.90),
            ('P0005', 0.70), ('P0006', 0.60), ('P0007', 0.30), ('P0008', 0.70),
            ('P0009', 0.60), ('P0010', 0.70), ('P0011', 0.60)]
    rows.sort(key=lambda r: r[1])
    labels = [r[0] for r in rows][::-1]
    vals = [r[1] for r in rows][::-1]

    fig, ax = plt.subplots(figsize=(W, 3.1), dpi=DPI)
    ys = range(len(vals))
    ax.barh(ys, vals, height=0.6, color=BLUE, linewidth=0, zorder=3)
    for y, v in zip(ys, vals):
        ax.text(v + 0.018, y, f'{v:.2f} s', va='center', ha='left',
                fontsize=7, color=INK2, zorder=4)
    ax.set_yticks(list(ys))
    ax.set_yticklabels(labels, fontsize=7.4)
    ax.set_xlim(0, 1.42)
    ax.set_xlabel('mean reaction time (s) — sessions pooled across AI tiers', fontsize=7.6)
    ax.xaxis.set_major_locator(MultipleLocator(0.2))
    tidy(ax, xgrid=True)
    ax.grid(axis='x', visible=True, zorder=0)

    for x, col, lab, ha in ((0.30, INK2, 'published expert 0.30 s', 'right'),
                            (0.50, MUTED, 'published novice 0.50 s', 'left')):
        ax.axvline(x, color=col, linewidth=1.1, zorder=5)
        ax.annotate(lab, xy=(x, 1.0), xycoords=('data', 'axes fraction'),
                    xytext=(-3 if ha == 'right' else 3, 4), textcoords='offset points',
                    ha=ha, va='bottom', fontsize=6.6, color=col, zorder=6)
    fig.savefig(f'{OUT}/fig_7_14_reaction_time_per_participant.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


# ── Figure 7.17 — benchmark-anchored scores per participant (small multiples) ──
def fig_7_17():
    pids = [f'P{i:04d}' for i in range(1, 12)]
    hs = [50, 67, 33, 100, 72, 67, 33, 46, 60, 32, 25]
    acc = [28, 25, 47, 43, 68, 100, 70, 100, 83, 64, 100]

    fig, axes = plt.subplots(1, 2, figsize=(W, 3.2), dpi=DPI,
                             sharey=True, gridspec_kw={'wspace': 0.10})
    spec = ((axes[0], hs, BLUE, 'Hostage Safety', 85, 40),
            (axes[1], acc, ORANGE, 'Accuracy', 100, 35))
    ys = list(range(len(pids)))[::-1]
    for ax, vals, col, title, exp, nov in spec:
        ax.barh(ys, vals, height=0.6, color=col, linewidth=0, zorder=3)
        for y, v in zip(ys, vals):
            inside = v >= 90          # keep the label clear of the expert rule
            ax.text(v - 2.5 if inside else v + 2.0, y, f'{v}%', va='center',
                    ha='right' if inside else 'left', fontsize=6.6,
                    color='white' if inside else INK2, zorder=4)
        ax.set_xlim(0, 122)
        ax.set_xticks([0, 25, 50, 75, 100])
        ax.set_title(title, fontsize=8.4, pad=18, loc='left')
        ax.set_xlabel('score (%)', fontsize=7.4)
        tidy(ax, xgrid=True)
        ax.grid(axis='x', visible=True, zorder=0)
        ax.tick_params(axis='y', length=0)
        for x, c, lab, ha in ((nov, MUTED, f'novice {nov}%', 'right'),
                              (exp, INK2, f'expert {exp}%', 'left')):
            ax.axvline(x, color=c, linewidth=1.1, zorder=5)
            ax.annotate(lab, xy=(x, 1.0), xycoords=('data', 'axes fraction'),
                        xytext=(-2 if ha == 'right' else 2, 3),
                        textcoords='offset points', ha=ha, va='bottom',
                        fontsize=6.3, color=c, zorder=6)
    axes[0].set_yticks(ys)
    axes[0].set_yticklabels(pids, fontsize=7.2)
    fig.savefig(f'{OUT}/fig_7_17_scores_vs_benchmarks.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


# ── Figure 7.20 — mean within-participant variability by score ─────────────────
def fig_7_20():
    rows = [('Operator Safety', 14.3, 'k = 8'), ('Speed', 16.0, 'k = 11'),
            ('Accuracy', 20.2, 'k = 11'), ('Overall', 26.5, 'k = 11'),
            ('Hostage Safety', 47.6, 'k = 11')]
    rows.sort(key=lambda r: r[1])
    labels = [r[0] for r in rows][::-1]
    vals = [r[1] for r in rows][::-1]

    fig, ax = plt.subplots(figsize=(W, 2.2), dpi=DPI)
    ys = range(len(vals))
    ax.barh(ys, vals, height=0.55, color=BLUE, linewidth=0, zorder=3)
    for y, v in zip(ys, vals):
        ax.text(v + 0.7, y, f'{v:.1f} pp', va='center', ha='left',
                fontsize=7.2, color=INK2, zorder=4)
    ax.set_yticks(list(ys))
    ax.set_yticklabels(labels, fontsize=7.8)
    ax.set_xlim(0, 56)
    ax.set_xlabel('mean within-participant standard deviation (percentage points)',
                  fontsize=7.4)
    ax.xaxis.set_major_locator(MultipleLocator(10))
    tidy(ax, xgrid=True)
    ax.grid(axis='x', visible=True, zorder=0)
    fig.savefig(f'{OUT}/fig_7_20_within_participant_variability.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


# ── Figure 5.8 — hostage distress scale, Freeze emphasised ────────────────────
def fig_5_8():
    rows = [('Calm / Freed', 0), ('Follow', 10), ('Fearful / Scared', 33),
            ('Held', 40), ('Panic', 66), ('Freeze', 72), ('Threatened', 75),
            ('Wounded', 95), ('Down', 100)]
    labels = [r[0] for r in rows][::-1]
    vals = [r[1] for r in rows][::-1]
    cols = [ACCENT if l == 'Freeze' else GREY for l in labels]

    fig, ax = plt.subplots(figsize=(W, 2.85), dpi=DPI)
    ys = range(len(vals))
    ax.barh(ys, vals, height=0.62, color=cols, linewidth=0, zorder=3)
    for y, v, l in zip(ys, vals, labels):
        bold = (l == 'Freeze')
        ax.text(v + 1.4, y, f'{v}', va='center', ha='left', fontsize=7.2,
                color=INK if bold else INK2,
                fontweight='bold' if bold else 'normal', zorder=4)
    ax.set_yticks(list(ys))
    ax.set_yticklabels(labels, fontsize=7.6)
    for t, l in zip(ax.get_yticklabels(), labels):
        if l == 'Freeze':
            t.set_color(INK)
            t.set_fontweight('bold')
    ax.set_xlim(0, 118)
    ax.set_xlabel('contribution to the session distress index (%)', fontsize=7.6)
    ax.xaxis.set_major_locator(MultipleLocator(20))
    tidy(ax, xgrid=True)
    ax.grid(axis='x', visible=True, zorder=0)

    fi = labels.index('Freeze')   # 'Freeze' row, counting from the bottom
    ax.annotate('scored above Panic — tonic immobility\npredicts worse trauma outcomes',
                xy=(72, fi + 0.35), xytext=(46, fi + 2.35),
                fontsize=6.6, color=ACCENT, ha='left', va='center',
                arrowprops=dict(arrowstyle='-', color=ACCENT, linewidth=0.9,
                                shrinkA=3, shrinkB=2,
                                connectionstyle='angle,angleA=0,angleB=90,rad=3'))
    fig.savefig(f'{OUT}/fig_5_8_hostage_distress_scale.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


for f in (fig_7_11, fig_7_14, fig_7_17, fig_7_20, fig_5_8):
    f()
    print('ok', f.__name__)
print('\n'.join(sorted(os.listdir(OUT))))
