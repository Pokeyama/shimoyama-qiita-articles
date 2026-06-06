<?php
declare(strict_types=1);

// 要素数。差が観測できる規模に増やしている
const N = 1_000_000;
// 複数回の平均をとってブレを抑える
const RUNS = 7;

printf("== PHP %s ==\n", PHP_VERSION);
printf("N = %s / RUNS = %d（平均値）\n\n", number_format(N), RUNS);

// PHP の配列は内部的にハッシュテーブルで、件数に応じて自動でリサイズされる。
// 通常の配列にはユーザーランドから容量を指定する手段が無いため、
// 「容量を確保できる SplFixedArray」と「自動リサイズの通常配列」を比較する。

echo "[連番配列（可変長配列の比較）]\n";
measure("容量指定なし  \$a[] = i（通常配列）", 'buildArrayNoCap');
measure("容量指定あり  new SplFixedArray(N)", 'buildSplFixed');
echo "\n";
echo "[連想配列（string キーのハッシュマップ）]\n";
measure("容量指定なし  \$a[\"key\".i]（通常配列のみ）", 'buildAssoc');

// buildをRUNS回実行し、実行時間とピークメモリ使用量の平均を出す。
// memory_get_peak_usage(true)はOSから確保した実メモリのピーク。
function measure(string $label, callable $build): void
{
    $totalMs = 0.0;
    $totalBytes = 0.0;
    for ($r = 0; $r < RUNS; $r++) {
        gc_collect_cycles();
        memory_reset_peak_usage(); // ピークメモリ計測をこの試行用にリセット

        $start = hrtime(true);
        $v = $build();
        $elapsedNs = hrtime(true) - $start;

        $peak = memory_get_peak_usage(true);
        unset($v); // 次の試行のために解放

        $totalMs += $elapsedNs / 1e6;
        $totalBytes += $peak;
    }
    $ms = $totalMs / RUNS;
    $mb = $totalBytes / RUNS / 1024 / 1024;
    printf("  %-40s time = %8.1f ms   peak = %8.1f MB\n", $label, $ms, $mb);
}

// ---- 計測対象 ----
function buildArrayNoCap(): array
{
    $a = [];
    for ($i = 0; $i < N; $i++) {
        $a[] = $i; // 末尾追加。内部ハッシュテーブルが自動リサイズされる
    }
    return $a;
}

function buildSplFixed(): SplFixedArray
{
    // 件数が分かっているので固定長で最初から確保する
    $a = new SplFixedArray(N);
    for ($i = 0; $i < N; $i++) {
        $a[$i] = $i;
    }
    return $a;
}

function buildAssoc(): array
{
    // ツイートと同じく string キー。PHP では容量指定の手段が無い
    $a = [];
    for ($i = 0; $i < N; $i++) {
        $a["key" . $i] = $i;
    }
    return $a;
}
