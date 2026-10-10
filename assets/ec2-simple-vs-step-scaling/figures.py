# 記事の図（イメージ図）を出力する。実測ではなくドキュメントのルールから描いたもの
# 実行: python figures.py（matplotlibが必要）
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager

OUT = Path(__file__).parent
for w in ("W3", "W6"):
    font_manager.fontManager.addfont(f"/System/Library/Fonts/ヒラギノ角ゴシック {w}.ttc")
plt.rcParams.update({
    "font.family": "Hiragino Sans",
    "font.size": 10,
    "axes.edgecolor": "#c3c2b7",
    "axes.linewidth": 1,
    "xtick.color": "#898781",
    "ytick.color": "#898781",
    "xtick.labelcolor": "#52514e",
    "ytick.labelcolor": "#52514e",
    "figure.facecolor": "#fcfcfb",
    "axes.facecolor": "#fcfcfb",
})
INK, INK2, MUTED, GRID = "#0b0b0b", "#52514e", "#898781", "#e1e0d9"
BLUE, ORANGE, SURFACE = "#2a78d6", "#eb6834", "#fcfcfb"
RAMP = ["#86b6ef", "#3987e5", "#184f95"]  # +1 / +2 / +4（validate_palette.js --ordinal でPASS）


def clean(ax):
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    ax.tick_params(length=0)


def warmup_timeline():
    t = list(range(13))
    cpu = [50, 55, 65, 67, 70, 78, 85, 80, 72, 64, 58, 54, 52]
    # 希望キャパシティ: 2分で+1、6分で+2の範囲だが差分の+1
    desired = [10, 10, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12]
    # 現在のキャパシティ（ウォームアップ300秒を過ぎた台数）
    current = [10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 12, 12]

    fig, (ax1, ax2) = plt.subplots(
        2, 1, figsize=(8, 5.8), dpi=200, sharex=True,
        gridspec_kw={"height_ratios": [3, 2], "hspace": 0.12},
    )
    fig.subplots_adjust(left=0.09, right=0.84, top=0.86, bottom=0.1)
    fig.text(0.09, 0.95, "ウォームアップ中の台数の変化（イメージ）", fontsize=13, weight="bold", color=INK)
    fig.text(0.09, 0.91, "10台のグループ　ステップ：60%以上で+1、80%以上で+2、90%以上で+4　ウォームアップ300秒",
             fontsize=9, color=INK2)

    # CPU使用率
    for y, label in ((60, "60%〜 +1"), (80, "80%〜 +2"), (90, "90%〜 +4")):
        ax1.axhline(y, color=GRID, lw=1, zorder=0)
        ax1.text(12.3, y, label, va="center", fontsize=9, color=INK2)
    ax1.plot(t, cpu, color=BLUE, lw=2, solid_joinstyle="round", solid_capstyle="round")
    events = [
        (2, 65, "①65%　+1で11台"),
        (4, 70, "②70%　同じステップなので11台のまま"),
        (6, 85, "③85%　+2の範囲だけど、差分の+1で12台"),
    ]
    for x, y, label in events:
        ax1.plot(x, y, "o", ms=8, color=BLUE, mec=SURFACE, mew=2, zorder=3)
    ax1.annotate(events[0][2], (2, 65), (0.3, 75), fontsize=9, color=INK,
                 arrowprops=dict(arrowstyle="-", color=MUTED, lw=1))
    ax1.annotate(events[1][2], (4, 70), (4.3, 57), fontsize=9, color=INK,
                 arrowprops=dict(arrowstyle="-", color=MUTED, lw=1))
    ax1.annotate(events[2][2], (6, 85), (6.4, 92.5), fontsize=9, color=INK,
                 arrowprops=dict(arrowstyle="-", color=MUTED, lw=1))
    ax1.set_ylim(40, 100)
    ax1.set_yticks([40, 60, 80, 100])
    ax1.set_ylabel("CPU使用率（%）", color=INK2)
    clean(ax1)

    # 台数
    for y in (10, 11, 12):
        ax2.axhline(y, color=GRID, lw=1, zorder=0)
    ax2.fill_between(t, current, desired, step="post", color=BLUE, alpha=0.1, lw=0)
    ax2.step(t, desired, where="post", color=BLUE, lw=2)
    ax2.step(t, current, where="post", color=ORANGE, lw=2)
    ax2.text(12.3, 12.15, "desired\ncapacity", fontsize=9, color=INK2, va="center", linespacing=1.2)
    ax2.text(12.3, 11.75, "メトリクスに\n入る台数", fontsize=9, color=INK2, va="top", linespacing=1.3)
    ax2.plot([12.1, 12.25], [12.15, 12.15], color=BLUE, lw=2, clip_on=False)
    ax2.plot([12.1, 12.25], [11.62, 11.62], color=ORANGE, lw=2, clip_on=False)
    ax2.text(2.2, 10.5, "ウォームアップ中\n（この間スケールインは止まる）", fontsize=9, color=INK2, va="center", linespacing=1.4)
    ax2.set_ylim(9.5, 12.6)
    ax2.set_yticks([10, 11, 12])
    ax2.set_ylabel("台数", color=INK2)
    ax2.set_xlim(0, 12)
    ax2.set_xticks(range(0, 13, 2))
    ax2.set_xlabel("経過時間（分）", color=INK2)
    clean(ax2)

    fig.savefig(OUT / "warmup-timeline.png", facecolor=SURFACE)
    plt.close(fig)


def step_adjustment():
    fig, ax = plt.subplots(figsize=(8, 3.3), dpi=200)
    fig.subplots_adjust(left=0.2, right=0.96, top=0.78, bottom=0.08)
    fig.text(0.03, 0.91, "ステップの範囲とstep_adjustmentの書き方", fontsize=13, weight="bold", color=INK)

    y, h, gap = 3.0, 0.9, 0.25
    segs = [
        (40, 60, "#e1e0d9", "アラームにならない", INK2),
        (60, 80, RAMP[0], "+1", INK),
        (80, 90, RAMP[1], "+2", "white"),
        (90, 100, RAMP[2], "+4", "white"),
    ]
    for x0, x1, color, label, tc in segs:
        ax.add_patch(plt.Rectangle((x0 + gap, y), x1 - x0 - 2 * gap, h, color=color, lw=0))
        ax.text((x0 + x1) / 2, y + h / 2, label, ha="center", va="center", fontsize=11, weight="bold", color=tc)
    ax.annotate("", (103, y + h / 2), (100, y + h / 2),
                arrowprops=dict(arrowstyle="-|>", color=RAMP[2], lw=2), annotation_clip=False)
    ax.plot([60, 60], [y - 0.15, y + h + 0.35], color=INK, lw=1)
    ax.text(60, y + h + 0.45, "アラームの閾値 60%", ha="center", fontsize=9, color=INK)

    rows = [
        (2.0, "コンソール（絶対値）", ["60 〜 80", "80 〜 90", "90 〜 上限なし"]),
        (0.9, "Terraform\n（閾値からの差分）", ["0 〜 20", "20 〜 30", "30 〜 上限なし"]),
    ]
    centers = [70, 85, 95]
    for ry, title, cells in rows:
        ax.text(38, ry, title, ha="right", va="center", fontsize=9, color=INK2, linespacing=1.3)
        ax.axhline(ry + 0.55, xmin=0, xmax=1, color=GRID, lw=1)
        for cx, cell in zip(centers, cells):
            ax.text(cx, ry, cell, ha="center", va="center", fontsize=8.5, color=INK, linespacing=1.4)

    ax.set_xlim(40, 100)
    ax.set_ylim(0.2, 4.6)
    ax.axis("off")
    ax.text(38, y + h / 2, "CPU使用率", ha="right", va="center", fontsize=9, color=INK2)
    fig.savefig(OUT / "step-adjustment.png", facecolor=SURFACE)
    plt.close(fig)


if __name__ == "__main__":
    warmup_timeline()
    step_adjustment()
