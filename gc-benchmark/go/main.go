package main

import (
	"fmt"
	"os"
	"runtime"
	"runtime/debug"
)

// パッケージ変数に代入することでエスケープ解析を抜け、ヒープ確保を強制する。
// （ローカル変数のままだと最適化でスタック確保され GC 圧がかからない）
var blackhole []byte

func main() {
	fmt.Printf("== Go GC demo (%s) ==\n", runtime.Version())
	gogc := os.Getenv("GOGC")
	if gogc == "" {
		gogc = "100 (default)"
	}
	fmt.Printf("GOGC = %s, GOMAXPROCS = %d\n\n", gogc, runtime.GOMAXPROCS(0))

	demo1AllocPressure()
	demo2GOGC()
}

// ----------------------------------------------------------------------------
// Demo1: アロケーション量が GC 回数・停止時間を決める
// 同じ仕事量でも「毎回 make」か「使い回す」かで NumGC が激変する。
// Go は世代別ではなく、ヒープが GOGC 分だけ増えるたびに GC が走る。
// ----------------------------------------------------------------------------
func demo1AllocPressure() {
	fmt.Println("[Demo1] アロケーション圧 -> GC 回数と停止時間")
	const N = 10_000_000

	// (A) 毎回 make([]byte, 64)（短命オブジェクトを大量生産）
	numGC, pauseMs, allocMb := run(func() {
		for i := 0; i < N; i++ {
			blackhole = make([]byte, 64)
			blackhole[0] = byte(i)
		}
	})
	fmt.Printf("  毎回 make([]byte,64): NumGC=%4d  GC停止合計=%6.1f ms  TotalAlloc=%5d MB\n", numGC, pauseMs, allocMb)

	// (B) 1個を使い回す（アロケーションなし）
	numGC, pauseMs, allocMb = run(func() {
		buf := make([]byte, 64)
		for i := 0; i < N; i++ {
			buf[0] = byte(i)
		}
		blackhole = buf
	})
	fmt.Printf("  1個を使い回し       : NumGC=%4d  GC停止合計=%6.1f ms  TotalAlloc=%5d MB\n", numGC, pauseMs, allocMb)
	fmt.Println()
}

// GC 回数・停止時間(ms)・累計アロケーション量(MB)の差分をとって返す
func run(body func()) (numGC uint32, pauseMs float64, allocMb uint64) {
	runtime.GC()
	var s0, s1 runtime.MemStats
	runtime.ReadMemStats(&s0)
	body()
	runtime.ReadMemStats(&s1)
	return s1.NumGC - s0.NumGC,
		float64(s1.PauseTotalNs-s0.PauseTotalNs) / 1e6,
		(s1.TotalAlloc - s0.TotalAlloc) / 1024 / 1024
}

// ----------------------------------------------------------------------------
// Demo2: GOGC を変えると GC 回数と停止時間のトレードオフが変わる
// GOGC が小さい = こまめに GC（メモリ低・CPU高）、大きい = GC をサボる（メモリ高・CPU低）
// ----------------------------------------------------------------------------
func demo2GOGC() {
	fmt.Println("[Demo2] GOGC とトレードオフ（同じ仕事量で GOGC だけ変える）")
	const N = 5_000_000
	for _, pct := range []int{50, 100, 200, 400} {
		debug.SetGCPercent(pct)
		numGC, pauseMs, _ := run(func() {
			for i := 0; i < N; i++ {
				blackhole = make([]byte, 64)
				blackhole[0] = byte(i)
			}
		})
		fmt.Printf("  GOGC=%4d : NumGC=%4d  GC停止合計=%6.1f ms\n", pct, numGC, pauseMs)
	}
	debug.SetGCPercent(100) // 後始末
	fmt.Println()
}
