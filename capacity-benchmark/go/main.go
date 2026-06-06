package main

import (
	"fmt"
	"runtime"
	"strconv"
	"time"
)

// 要素数。差が観測できる規模に増やしている
const N = 1_000_000

// 複数回の平均をとってブレを抑える
const Runs = 7

func main() {
	fmt.Printf("== Go (%s) ==\n", runtime.Version())
	fmt.Printf("N = %d / Runs = %d（平均値）\n\n", N, Runs)

	// ウォームアップ
	buildMapNoCap()
	buildMapCap()
	buildSliceNoCap()
	buildSliceCap()

	fmt.Println("[map[string]struct{}]")
	measure("容量指定なし  make(map)", buildMapNoCap)
	measure("容量指定あり  make(map, N)", buildMapCap)
	fmt.Println()
	fmt.Println("[[]int slice]")
	measure("容量指定なし  var s []int", buildSliceNoCap)
	measure("容量指定あり  make([]int, 0, N)", buildSliceCap)
}

// buildをRuns回実行し、実行時間と「累計アロケーション量(TotalAlloc)」の平均を出す。
// TotalAllocは途中のリサイズで捨てられたメモリも含むので、再確保の無駄がそのまま差になる。
func measure(label string, build func() any) {
	var totalNs int64
	var totalAlloc uint64
	for r := 0; r < Runs; r++ {
		runtime.GC()
		var before, after runtime.MemStats
		runtime.ReadMemStats(&before)

		start := time.Now()
		v := build()
		elapsed := time.Since(start)

		runtime.ReadMemStats(&after)
		runtime.KeepAlive(v) // 最適化で消されないように参照を保持

		totalNs += elapsed.Nanoseconds()
		totalAlloc += after.TotalAlloc - before.TotalAlloc
	}
	ms := float64(totalNs) / float64(Runs) / 1e6
	mb := float64(totalAlloc) / float64(Runs) / 1024 / 1024
	fmt.Printf("  %-34s time = %8.1f ms   alloc = %8.1f MB\n", label, ms, mb)
}

// ---- 計測対象 ----
func buildMapNoCap() any {
	m := make(map[string]struct{})
	for i := 0; i < N; i++ {
		m["key"+strconv.Itoa(i)] = struct{}{}
	}
	return m
}

func buildMapCap() any {
	// 入れる件数が分かっているので最初から確保しておく
	m := make(map[string]struct{}, N)
	for i := 0; i < N; i++ {
		m["key"+strconv.Itoa(i)] = struct{}{}
	}
	return m
}

func buildSliceNoCap() any {
	var s []int
	for i := 0; i < N; i++ {
		s = append(s, i)
	}
	return s
}

func buildSliceCap() any {
	// 長さ0・容量Nで確保しておけばappendで再確保が起きない
	s := make([]int, 0, N)
	for i := 0; i < N; i++ {
		s = append(s, i)
	}
	return s
}
