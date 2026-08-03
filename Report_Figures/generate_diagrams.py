"""Figures 5.4 and 5.6 - the two Chapter 5 diagrams, drawn rather than placeheld.

Palette: the same validated instance used by charts.py.
  ordinal blue ramp for the AI-tier gating (light -> dark = capability accumulating)
    step 100 #cde2fb  all tiers
    step 200 #9ec5f4  Intermediate and above
    step 300 #6da7ec  Advanced only
  Tier identity is ALSO carried by a text tag on each gated box, so colour is never
  the sole channel (and the diagram survives greyscale printing).
"""
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch
import os

OUT = 'charts'
os.makedirs(OUT, exist_ok=True)

T_ALL, T_INT, T_ADV = '#cde2fb', '#9ec5f4', '#6da7ec'
NEUTRAL, TERMINAL = '#f0efec', '#e1e0d9'
EDGE, EDGE_SOFT = '#1c5cab', '#898781'
INK, INK2, MUTED = '#0b0b0b', '#52514e', '#898781'
ACCENT = '#eb6834'

plt.rcParams.update({
    'font.family': 'sans-serif',
    'font.sans-serif': ['Segoe UI', 'DejaVu Sans'],
    'font.size': 8, 'text.color': INK,
    'figure.facecolor': 'white', 'savefig.facecolor': 'white',
})
DPI = 220
W = 5.6


def box(ax, x, y, w, h, text, fill=NEUTRAL, edge=EDGE, tag=None,
        fs=7.4, bold=True, lw=1.1, dashed=False):
    p = FancyBboxPatch((x - w / 2, y - h / 2), w, h,
                       boxstyle='round,pad=0.02,rounding_size=0.10',
                       linewidth=lw, edgecolor=edge, facecolor=fill,
                       linestyle='--' if dashed else '-', zorder=3)
    ax.add_patch(p)
    ax.text(x, y + (0.085 if tag else 0), text, ha='center', va='center',
            fontsize=fs, color=INK, fontweight='bold' if bold else 'normal',
            zorder=4, linespacing=1.25)
    if tag:
        ax.text(x, y - h / 2 + 0.115, tag, ha='center', va='center',
                fontsize=5.9, color=INK2, style='italic', zorder=4)
    return dict(x=x, y=y, w=w, h=h)


def arrow(ax, a, b, label=None, side='auto', rad=0.0, lfs=6.3,
          color=EDGE, lw=1.0, ldy=0.0, ldx=0.0, style='-|>'):
    """Arrow between two box dicts, from edge to edge."""
    ax_, ay = a['x'], a['y']
    bx, by = b['x'], b['y']
    if side == 'auto':
        if abs(bx - ax_) >= abs(by - ay):
            sx = ax_ + (a['w'] / 2) * (1 if bx > ax_ else -1)
            sy = ay
            ex = bx - (b['w'] / 2) * (1 if bx > ax_ else -1)
            ey = by
        else:
            sx, sy = ax_, ay + (a['h'] / 2) * (1 if by > ay else -1)
            ex, ey = bx, by - (b['h'] / 2) * (1 if by > ay else -1)
    elif side == 'top':
        sx, sy = ax_, ay + a['h'] / 2
        ex, ey = bx, by + b['h'] / 2
    elif side == 'bottom':
        sx, sy = ax_, ay - a['h'] / 2
        ex, ey = bx, by - b['h'] / 2
    ax.add_patch(FancyArrowPatch(
        (sx, sy), (ex, ey), arrowstyle=style, mutation_scale=9,
        connectionstyle=f'arc3,rad={rad}', linewidth=lw, color=color, zorder=2,
        shrinkA=1, shrinkB=1))
    if label:
        mx, my = (sx + ex) / 2 + ldx, (sy + ey) / 2 + ldy
        ax.text(mx, my, label, ha='center', va='center', fontsize=lfs,
                color=INK2, zorder=5,
                bbox=dict(boxstyle='round,pad=0.16', fc='white', ec='none'))


# ══════════════════════ Figure 5.4 ════════════════════════════════════════════
def fig_5_4():
    fig, (a1, a2) = plt.subplots(
        2, 1, figsize=(W, 5.15), dpi=DPI,
        gridspec_kw={'height_ratios': [3.35, 2.45], 'hspace': 0.16})
    for ax in (a1, a2):
        ax.set_axis_off()

    # ---- upper panel: individual finite-state machine -----------------------
    a1.set_xlim(0, 10.4); a1.set_ylim(0, 3.70)
    a1.text(0, 3.62, 'Individual layer — terrorist finite-state machine',
            fontsize=8.6, fontweight='bold', color=INK, va='top')

    BW, BH = 1.72, 0.56
    idle = box(a1, 0.95, 1.85, BW, BH, 'Idle', T_ALL)
    alert = box(a1, 3.45, 2.78, BW, BH, 'Alert', T_ALL)
    inv = box(a1, 3.45, 0.92, BW, BH, 'Investigate', T_INT, fs=6.9)
    eng = box(a1, 5.95, 1.85, BW, BH, 'Engage', T_ALL)
    cov = box(a1, 8.55, 2.78, BW + 0.38, BH, 'Cover / Retreat', T_ADV, fs=6.9)
    down = box(a1, 8.55, 0.92, BW, BH, 'Down', TERMINAL, edge=EDGE_SOFT)
    a1.text(3.45, 0.54, 'Intermediate +', fontsize=6.0, color=INK2, style='italic',
            ha='center', va='center')
    a1.text(8.55, 3.14, 'Advanced only', fontsize=6.0, color=INK2, style='italic',
            ha='center', va='center')

    def elabel(x, y, t):
        """Edge label at the arrow's own midpoint, in the gap between two boxes."""
        a1.text(x, y, t, fontsize=6.0, color=INK2, ha='center', va='center',
                linespacing=1.3,
                bbox=dict(boxstyle='round,pad=0.10', fc='white', ec='none'), zorder=5)

    arrow(a1, idle, alert);  elabel(2.20, 2.32, 'PlayerSeen')
    arrow(a1, idle, inv);    elabel(2.20, 1.38, 'GunshotHeard')
    arrow(a1, alert, eng);   elabel(4.62, 2.32, 'TargetConfirmed')
    # one bidirectional edge instead of two crossing arrows
    arrow(a1, inv, eng, style='<|-|>')
    elabel(4.62, 1.38, 'TargetConfirmed /\nPlayerLost')
    arrow(a1, eng, cov);     elabel(7.10, 2.32, 'escalation /\nwounded')
    arrow(a1, eng, down);    elabel(7.10, 1.38, 'health = 0')
    arrow(a1, cov, down, color=EDGE_SOFT, lw=0.9)
    a1.text(9.86, 1.85, 'health = 0', fontsize=6.3, color=INK2, ha='center', va='center',
            rotation=90, zorder=5)

    # tier key
    for i, (c, lab) in enumerate([(T_ALL, 'all tiers'),
                                  (T_INT, 'Intermediate and above'),
                                  (T_ADV, 'Advanced only')]):
        x = 0.20 + i * 2.75
        a1.add_patch(FancyBboxPatch((x, 0.03), 0.28, 0.19,
                                    boxstyle='round,pad=0.01,rounding_size=0.05',
                                    linewidth=0.9, edgecolor=EDGE, facecolor=c, zorder=3))
        a1.text(x + 0.38, 0.125, lab, fontsize=6.4, color=INK2, va='center')

    # ---- lower panel: shared squad hunt lifecycle ---------------------------
    a2.set_xlim(0, 10.4); a2.set_ylim(0, 3.05)
    a2.text(0, 2.99, 'Squad layer — shared, persistent hunt lifecycle',
            fontsize=8.6, fontweight='bold', color=INK, va='top')

    SW, SH = 2.16, 0.66
    xs = [1.24, 3.86, 6.48, 9.10]
    yb = 1.52
    s1 = box(a2, xs[0], yb, SW, SH, 'ReportConfirmed-\nContact', T_ALL, fs=6.5)
    s2 = box(a2, xs[1], yb, SW, SH, 'BeginHunt\nanchor: PointLastSeen', T_ALL, fs=6.2)
    s3 = box(a2, xs[2], yb, SW, SH, 'Radius grows\n= speed x elapsed', T_ALL, fs=6.2)
    s4 = box(a2, xs[3], yb, SW, SH, 'EndHunt\nbudget approx. 45 s', T_ALL, fs=6.2)
    for a, b in ((s1, s2), (s2, s3), (s3, s4)):
        arrow(a2, a, b)

    # three re-anchor arrows returning to BeginHunt, labels stacked clear of the title
    for i, (src, lab, r, y) in enumerate([(s3, 'fresh sighting', 0.26, 2.14),
                                          (s3, 'fresh gunshot', 0.40, 2.40),
                                          (s4, 'squad-mate down', 0.30, 2.66)]):
        a2.add_patch(FancyArrowPatch(
            (src['x'] - 0.20 - i * 0.22, src['y'] + SH / 2),
            (s2['x'] + 0.34, s2['y'] + SH / 2), arrowstyle='-|>', mutation_scale=8,
            connectionstyle=f'arc3,rad={r}', linewidth=0.9, color=ACCENT, zorder=2))
        a2.text(6.90, y, lab, fontsize=6.2, color=ACCENT, ha='left', va='center',
                bbox=dict(boxstyle='round,pad=0.14', fc='white', ec='none'), zorder=5)
    a2.text(6.90, 2.92, 're-anchor the hunt on:', fontsize=6.2, color=ACCENT,
            ha='left', style='italic', va='center', zorder=5)

    # the blackboard, read by every member
    bb = box(a2, 5.17, 0.62, 8.40, 0.50,
             'Shared squad blackboard  —  PointLastSeen  ·  hunt state  ·  leader',
             '#fdf0e8', edge=ACCENT, fs=6.8, dashed=True)
    a2.text(5.17, 0.20, 'read and written by every squad member, not private to the sighter',
            fontsize=6.0, color=INK2, ha='center', va='center', style='italic', zorder=5)
    for x in xs:
        a2.plot([x, x], [yb - SH / 2 - 0.03, bb['y'] + bb['h'] / 2 + 0.02],
                linestyle=(0, (2, 2)), linewidth=0.8, color=ACCENT, zorder=1)

    fig.savefig(f'{OUT}/fig_5_4_terrorist_fsm_and_hunt.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


# ══════════════════════ Figure 5.6 ════════════════════════════════════════════
def fig_5_6():
    fig, ax = plt.subplots(figsize=(W, 4.85), dpi=DPI)
    ax.set_axis_off()
    ax.set_xlim(0, 10.6); ax.set_ylim(0, 10.4)

    def dbox(x, y, w, h, t, fs=6.9, fill='#fdf0e8', edge=ACCENT):
        """decision diamond, drawn as a rounded box with an italic question"""
        return box(ax, x, y, w, h, t, fill, edge=edge, fs=fs, bold=False)

    st = box(ax, 4.10, 9.82, 7.0, 0.62,
             'Enemy-caused stimulus:  PlayerSeen  |  GunshotHeard  |  TargetConfirmed',
             T_ALL, fs=6.3)
    d1 = dbox(4.10, 8.62, 5.0, 0.62, 'Is a response window already open?')
    r1 = box(ax, 8.72, 8.62, 2.4, 0.56, 'ignore stimulus', TERMINAL,
             edge=EDGE_SOFT, fs=6.6, bold=False)
    d2 = dbox(4.10, 7.34, 5.9, 0.74,
              'Is any channel already above its threshold\nat the instant the stimulus fires?')
    r2 = box(ax, 8.72, 7.34, 2.4, 0.74,
             'disqualify —\nmotion already\nunderway', TERMINAL, edge=EDGE_SOFT,
             fs=6.4, bold=False)
    win = box(ax, 4.10, 6.16, 4.4, 0.58, 'Open the 3 s response window', T_ALL, fs=6.9)

    # four parallel channels
    ch = [('head\nabove 90 deg/s\ntoward threat', 1.28), ('hands\nabove 1.0 m/s', 3.22),
          ('body\nabove 0.6 m/s', 5.06), ("trigger\nthe trainee's\nown shot", 6.92)]
    ax.text(4.10, 5.50, 'four independent response channels, first crossing wins',
            fontsize=6.3, color=INK2, ha='center', style='italic', zorder=6,
            bbox=dict(boxstyle='round,pad=0.18', fc='white', ec='none'))
    cboxes = []
    for t, x in ch:
        cboxes.append(box(ax, x, 4.78, 1.76, 0.72, t, '#eaf2fd', fs=6.2, bold=False))
    for cb in cboxes:
        ax.add_patch(FancyArrowPatch(
            (win['x'], win['y'] - win['h'] / 2), (cb['x'], cb['y'] + cb['h'] / 2),
            arrowstyle='-|>', mutation_scale=7, connectionstyle='arc3,rad=0.0',
            linewidth=0.8, color=EDGE, zorder=2, shrinkA=1, shrinkB=1))

    d3 = dbox(4.10, 3.42, 5.0, 0.62, 'First crossing earlier than 80 ms?')
    r3 = box(ax, 8.72, 3.42, 2.4, 0.72, 'reject —\ntracking noise,\nnot a reaction',
             TERMINAL, edge=EDGE_SOFT, fs=6.4, bold=False)
    rec = box(ax, 4.10, 2.20, 5.2, 0.62,
              'Record  reactionTime = t(crossing) - t(stimulus)', T_INT, fs=6.9)
    cd = box(ax, 4.10, 1.06, 4.6, 0.58,
             '2 s cooldown suppresses burst stimuli', T_ALL, fs=6.9)
    # the no-response outcome sits in the free space beside the channels row
    miss = box(ax, 9.15, 4.78, 2.5, 1.10,
               'no crossing before\nthe window closes:\nrecord an explicit miss\n'
               '(reactionTime = -1)', '#f3f3ef', edge=EDGE_SOFT, fs=6.2, bold=False)

    for a, b, lab in ((st, d1, None), (d1, d2, 'no'), (d2, win, 'no'),
                      (d3, rec, 'no'), (rec, cd, None)):
        arrow(ax, a, b, lab, lfs=6.2, ldx=0.42)
    for cb in cboxes:
        ax.add_patch(FancyArrowPatch(
            (cb['x'], cb['y'] - cb['h'] / 2), (d3['x'], d3['y'] + d3['h'] / 2),
            arrowstyle='-|>', mutation_scale=7, connectionstyle='arc3,rad=0.0',
            linewidth=0.8, color=EDGE, zorder=2, shrinkA=1, shrinkB=1))
    for a, b in ((d1, r1), (d2, r2), (d3, r3)):
        arrow(ax, a, b, 'yes', color=EDGE_SOFT, lw=0.9, lfs=6.2, ldy=0.15)
    # the no-response branch
    ax.add_patch(FancyArrowPatch(
        (win['x'] + win['w'] / 2, win['y']), (miss['x'] - miss['w'] / 2, miss['y'] + 0.28),
        arrowstyle='-|>', mutation_scale=7, connectionstyle='arc3,rad=-0.22',
        linewidth=0.9, color=EDGE_SOFT, linestyle=(0, (3, 2)), zorder=2,
        shrinkA=2, shrinkB=2))

    ax.text(0.02, 0.20, 'The three validity rules are the two grey-branch rejections and the '
                        'cooldown: motion already underway is\ndisqualified, the first 80 ms is '
                        'never counted, and a burst of automatic fire cannot produce duplicates.',
            fontsize=6.2, color=INK2, va='center', ha='left', linespacing=1.5)

    fig.savefig(f'{OUT}/fig_5_6_reaction_time_validity_flow.png',
                bbox_inches='tight', pad_inches=0.06)
    plt.close(fig)


fig_5_4(); print('ok fig_5_4')
fig_5_6(); print('ok fig_5_6')
