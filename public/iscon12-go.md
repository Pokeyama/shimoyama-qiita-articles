---
title: ISUCON12予選問題をGoで性能改善してみた（初学者ログ）
tags:
  - Go
  - ISUCON
  - パフォーマンス
private: false
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
ISUCON12予選問題をGoで触ってみたので、Go初学者目線でやったことをまとめます。

筆者はC#とPHPがメインで、Goはほぼ初学者です。

この記事はローカルDockerでの検証なので、スコアはけっこうブレます。参考値として見てもらえると嬉しいです。

# 前提
- 対象: 主に`isucon12-qualify` の `webapp/go/isuports.go`の修正
- 実行: `make run-bench-noload`（整合性）→ `make run-bench`（本走行）
- 方針: まず正しさ、次にクエリ回数削減・行数削減
- 留意事項: ローカルなので実大会でのスコアと比較はしない

forkしたレポジトリで進めました。

https://github.com/Pokeyama/isucon12-qualify/tree/practice-go

# 今回やったこと
## 1. インデックス追加
サロゲートPK中心の設計だったので、実クエリに合わせて複合インデックスを追加しました。

- `player_score(tenant_id, competition_id)`
- `player_score(tenant_id, competition_id, player_id, row_num DESC)`
- `player_score(tenant_id, competition_id, row_num DESC, player_id)`
- `competition(tenant_id, created_at DESC, id)`
- `competition(tenant_id, created_at ASC)`
- `player(tenant_id, created_at DESC)`

`tenant` 側は `init.sh` でも `CREATE INDEX IF NOT EXISTS` するようにして、`/initialize` のたびに効くようにしました。

```sql:修正前
-- 例: tenantスキーマ（player_score）
CREATE TABLE player_score (
  id VARCHAR(255) NOT NULL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  player_id VARCHAR(255) NOT NULL,
  competition_id VARCHAR(255) NOT NULL,
  score BIGINT NOT NULL,
  row_num BIGINT NOT NULL,
  created_at BIGINT NOT NULL,
  updated_at BIGINT NOT NULL
);
```
↓
```sql:修正後
CREATE TABLE player_score (
  id VARCHAR(255) NOT NULL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  player_id VARCHAR(255) NOT NULL,
  competition_id VARCHAR(255) NOT NULL,
  score BIGINT NOT NULL,
  row_num BIGINT NOT NULL,
  created_at BIGINT NOT NULL,
  updated_at BIGINT NOT NULL
);

CREATE INDEX idx_player_score_tenant_competition
  ON player_score (tenant_id, competition_id);

CREATE INDEX idx_player_score_tenant_competition_player_rownum
  ON player_score (tenant_id, competition_id, player_id, row_num DESC);

CREATE INDEX idx_competition_tenant_created_at_id
  ON competition (tenant_id, created_at DESC, id);
```

## 1.5 `SELECT *` をやめる
`SELECT *` が多かったので、必要なカラムだけ取得するようにしました。
取得列を絞ることでI/Oを減らし、クエリの意図も読みやすくなりました。
あまり効果なさそうなので、ここではベンチマーク回しませんでした。

```sql:修正前
SELECT *
FROM player_score
WHERE tenant_id = ? AND competition_id = ?
ORDER BY row_num DESC
```
↓
```sql:修正後
SELECT player_id, score, row_num
FROM player_score
WHERE tenant_id = ? AND competition_id = ?
ORDER BY row_num DESC
```

## 2. N+1の解消
ここは典型的なN+1を解消したところです。
CSVの `player_id` 存在確認で、以前は1件ずつ `SELECT` していました。

- 以前: `player_id` ごとに `SELECT id FROM player WHERE id = ?` を繰り返す
- 変更後: `SELECT id FROM player WHERE id IN (?)` で一括取得して、Go側で不足IDを判定

```go:修正前
for _, playerID := range playerIDs {
    var p PlayerRow
    if err := tenantDB.GetContext(ctx, &p, "SELECT id FROM player WHERE id = ?", playerID); err != nil {
        return echo.NewHTTPError(http.StatusBadRequest, fmt.Sprintf("player not found: %s", playerID))
    }
}
```
↓
```go:修正後
query, args, err := sqlx.In("SELECT id FROM player WHERE id IN (?)", playerIDs)
if err != nil {
    return fmt.Errorf("error sqlx.In: %w", err)
}

var players []PlayerRow
if err := tenantDB.SelectContext(ctx, &players, query, args...); err != nil {
    return fmt.Errorf("error Select player: ids=%v, %w", playerIDs, err)
}

found := make(map[string]struct{}, len(players))
for _, p := range players {
    found[p.ID] = struct{}{}
}
for _, playerID := range playerIDs {
    if _, ok := found[playerID]; !ok {
        return echo.NewHTTPError(http.StatusBadRequest, fmt.Sprintf("player not found: %s", playerID))
    }
}
```

## 2.5 SQL集約（N+1とは別）
ランキング計算の集約をSQL側に寄せた改善です。
`player_score` を大量にGoで集計していたので、SQLで「playerごとの最新row」を先に絞ってからJOINしています。
この部分はGo側でやったほうが速い気もしてますが、ベンチ取るの忘れてました。。。

```go:修正前
pss := []PlayerScoreRow{}
if err := tenantDB.SelectContext(
    ctx,
    &pss,
    "SELECT player_id, score, row_num FROM player_score WHERE tenant_id = ? AND competition_id = ? ORDER BY row_num DESC",
    tenant.ID,
    competitionID,
); err != nil {
    return fmt.Errorf("error Select player_score: %w", err)
}

playerIDs := make([]string, 0, len(pss))
seen := make(map[string]struct{}, len(pss))
for _, ps := range pss {
    if _, ok := seen[ps.PlayerID]; ok {
        continue
    }
    seen[ps.PlayerID] = struct{}{}
    playerIDs = append(playerIDs, ps.PlayerID)
}

if _, err := getPlayersByIDs(ctx, tenantDB, playerIDs); err != nil {
    return err
}
```
↓
```go:修正後
var rows []LatestScoreRow
query := `
SELECT ps.player_id, ps.score, ps.row_num, pl.display_name
FROM player_score ps
JOIN (
  SELECT player_id, MAX(row_num) AS max_row_num
  FROM player_score
  WHERE tenant_id = ? AND competition_id = ?
  GROUP BY player_id
) latest ON ps.player_id = latest.player_id AND ps.row_num = latest.max_row_num
JOIN player pl ON ps.player_id = pl.id AND ps.tenant_id = pl.tenant_id
WHERE ps.tenant_id = ? AND ps.competition_id = ?
`

if err := tenantDB.SelectContext(
    ctx,
    &rows,
    query,
    tenant.ID, competitionID,
    tenant.ID, competitionID,
); err != nil {
    return fmt.Errorf("select latest scores: %w", err)
}
```

## 3. CSVスコア投入のバルクINSERT化
1行ずつ `INSERT` していたところを、`VALUES (...), (...), ...` のバルク形式に変更し、
100件ずつ登録するように修正。

```go:修正前
for _, ps := range playerScoreRows {
    if _, err := tenantDB.NamedExecContext(
        ctx,
        "INSERT INTO player_score (id, tenant_id, player_id, competition_id, score, row_num, created_at, updated_at) VALUES (:id, :tenant_id, :player_id, :competition_id, :score, :row_num, :created_at, :updated_at)",
        ps,
    ); err != nil {
        return fmt.Errorf("error Insert player_score: %w", err)
    }
}
```
↓
```go:修正後
values := make([]string, 0, len(playerScoreRows))
args := make([]interface{}, 0, len(playerScoreRows)*8)
for _, ps := range playerScoreRows {
    values = append(values, "(?, ?, ?, ?, ?, ?, ?, ?)")
    args = append(args, ps.ID, ps.TenantID, ps.PlayerID, ps.CompetitionID, ps.Score, ps.RowNum, ps.CreatedAt, ps.UpdatedAt)
}

for i := 0; i < len(playerScoreRows); i += 100 {
    end := i + 100
    if end > len(playerScoreRows) {
        end = len(playerScoreRows)
    }
    query := "INSERT INTO player_score (id, tenant_id, player_id, competition_id, score, row_num, created_at, updated_at) VALUES " + strings.Join(values[i:end], ",")
    if _, err := tenantDB.ExecContext(ctx, query, args[i*8:end*8]...); err != nil {
        return fmt.Errorf("error Insert player_score: %w", err)
    }
}
```

## 4. ID採番をDB依存からGo側へ
`REPLACE INTO id_generator` 方式から、SnowflakeでID生成する方式に変更。
ここは効果がかなり大きかったです（後述）。
修正前のロジック見たときは頭のいい採番の仕方あるなあと思ってたんですが。。。

```go:修正前
func dispenseID(ctx context.Context) (string, error) {
    ret, err := adminDB.ExecContext(ctx, "REPLACE INTO id_generator (stub) VALUES (?)", "a")
    if err != nil {
        return "", err
    }
    id, err := ret.LastInsertId()
    if err != nil {
        return "", err
    }
    return fmt.Sprintf("%x", id), nil
}
```
↓
```go:修正後
var sfNode *snowflake.Node

func init() {
    var err error
    sfNode, err = snowflake.NewNode(1)
    if err != nil {
        panic(err)
    }
}

func dispenseID(_ context.Context) (string, error) {
    id := sfNode.Generate().Int64()
    return fmt.Sprintf("%x", id), nil
}
```

## ex. ベンチ運用の整備（比較しやすく）
改善の比較がしやすいように、ベンチ結果の3行サマリ（`Error` / `PASSED` / `SCORE`）をファイル保存する仕組みも追加しました。
`development/BENCH_WORKFLOW.md` に手順も残して、次回ベンチ時に迷わないようにしています。

修正前（抜粋）:
```make:修正前
run-bench:
	docker compose -f docker-compose-common.yml exec bench \
		go run cmd/bench/main.go -target-url https://t.isucon.localhost -target-addr nginx:443
```

修正後（抜粋）:
```make:修正後
BENCH_SCORE_LOG?=bench_score_history.log

run-bench-save:
	@set -e; \
	ts=$$(date '+%Y-%m-%d %H:%M:%S'); \
	out=$$(docker compose -f docker-compose-common.yml exec bench \
		go run cmd/bench/main.go -target-url https://t.isucon.localhost -target-addr nginx:443 2>&1); \
	{ \
		echo "[$$ts]"; \
		echo "$$out" | grep -E 'Error [0-9]+ \(Critical:[0-9]+\)|PASSED:|SCORE:' || true; \
		echo; \
	} >> $(BENCH_SCORE_LOG)
```

# ベンチ結果
| 改善項目 | 平均SCORE |
|---|---:|
| 0. 初期状態 | 5447 |
| 1. Index追加 | 6094 |
| 2. N+1解消 | 6511 |
| 3. バルクINSERT化 | 6905 |
| 4. ID生成をGo側へ | 13603 |

改善1〜4は基本3回ずつベンチを回して平均を見ました。
※ `0.初期状態` は別環境で1回だけ実測した値です。

- 以前の主レンジ: `5,000〜6,000` 前後
- 改善後: `11,000〜15,000` が出るように

さらに「ID生成だけ」A/Bで比較しました（3回ずつ）。

- Snowflake版: `14973 / 11324 / 10961`（平均 `12419`）
- 旧DB採番版: `6709 / 6516 / 6864`（平均 `6696`）

この比較だと、ID生成の寄与がかなり大きそうです。
最初見たときはインデックス貼ったところが一番効果ある気がしてたのですが、
言うほどスコアに寄与しなかったのが意外でした。

ただ、最後に入れた改修がDB採番の変更なので、他の改修の影響が混ざっている可能性は大いにあります。

# ハマったところ
パフォーマンスというより、Goの書き方やローカル実行でハマったところです。

## SQLiteのインデックス
100個くらいDBあるしどうやって貼るねんということでCodexさんにやってもらいました。
`webapp/sql/init.sh` がベンチ実行時の初期化で毎回走るので、ここに追記しました。

```sh
#!/bin/sh
省略
# SQLiteのデータベースを初期化
rm -f ../tenant_db/*.db
cp -r ../../initial_data/*.db ../tenant_db/

# ここから追記
# tenant DBの検索性能向上用インデックスを付与
for db in ../tenant_db/*.db; do
  sqlite3 "$db" <<'SQL'
CREATE INDEX IF NOT EXISTS idx_player_score_tenant_competition
  ON player_score (tenant_id, competition_id);
CREATE INDEX IF NOT EXISTS idx_player_score_tenant_competition_player_rownum
  ON player_score (tenant_id, competition_id, player_id, row_num DESC);
CREATE INDEX IF NOT EXISTS idx_player_score_tenant_competition_rownum_player
  ON player_score (tenant_id, competition_id, row_num DESC, player_id);
CREATE INDEX IF NOT EXISTS idx_competition_tenant_created_at_id
  ON competition (tenant_id, created_at DESC, id);
CREATE INDEX IF NOT EXISTS idx_competition_tenant_created_at_asc
  ON competition (tenant_id, created_at ASC);
CREATE INDEX IF NOT EXISTS idx_player_tenant_created_at
  ON player (tenant_id, created_at DESC);
SQL
done

```

確認は、対象クエリを `EXPLAIN QUERY PLAN` で実行して行いました。
```sh
docker compose -f development/docker-compose-go.yml -f development/docker-compose-common.yml exec webapp \
  sh -lc 'sqlite3 /home/isucon/webapp/tenant_db/1.db "
PRAGMA index_list(\"player_score\");
EXPLAIN QUERY PLAN
SELECT player_id, score, row_num
FROM player_score
WHERE tenant_id = 1
  AND competition_id = \"9fa52529\"
ORDER BY row_num DESC
LIMIT 100;
"'

0|idx_player_score_tenant_competition_rownum_player|0|c|0
1|idx_player_score_tenant_competition_player_rownum|0|c|0
2|idx_player_score_tenant_competition|0|c|0
3|sqlite_autoindex_player_score_1|1|pk|0
QUERY PLAN
--SEARCH TABLE player_score USING INDEX idx_player_score_tenant_competition_rownum_player (tenant_id=? AND competition_id=?)
```

ちゃんと効いてます。

## Snowflakeの初期化位置
`snowflake.NewNode(1)` を都度生成するとシーケンスが維持されず、ID衝突したため、
`init()` で1回だけ生成しました。

```go
var sfNode *snowflake.Node

// サーバー実行時に１度だけ呼ばれる
func init() {
	var err error
	sfNode, err = snowflake.NewNode(1)
	if err != nil {
		panic(err)
	}
}

func dispenseID(_ context.Context) (string, error) {
	id := sfNode.Generate().Int64()
	return fmt.Sprintf("%x", id), nil
}
```

# 効果ありそうだがやらなかったこと
## SQLiteのMySQL化
100個ほどDBがあり都度繋げていたので効果はありそうですが、今回は見送りました。
テナントDBの前提が変わるので、スキーマ移行・初期データ作り直し・整合性確認まで一気に必要になり、Goの勉強という学習範囲を超えると判断しました。

## ゴルーチンで並列化
抜本的に書き換えが必要そうで、壊しそうな予感しかしなかったので見送りました。
ある程度できあがってる同期処理を後から並列化すると、競合や順序保証の問題が増えてデバッグが急に重くなるので（実体験）、まずはSQL・インデックス改善を優先しました。

# Go初学者目線での感想
参照を意識する場面が多くて、最初は書き方のクセに戸惑いました。

C#でいうと、普段よく使うのは `List<T>`（可変長）か `T[]`（固定長）だと思います。
PHPも `array` をそのまま伸ばして使うことが多いので、
「とりあえず入れておく」がやりやすいです。

Goはそこが少し違って、
- `[N]T` は固定長（C# の `T[]` に近い）
- `[]T`（slice）は可変長っぽく使えるけど、実体は内部配列の上に乗っている

という整理になります。

なので `append` をたくさん呼ぶコードだと、
最初に `make([]T, 0, 1000)` みたいに容量を確保しておくかどうかで、
裏での再確保回数が変わってきます。

このあたりは他言語より「配列の動き」を意識しやすくて、
言語レベルでパフォーマンスを考えやすいなと感じました。


# まとめ
何か改善すると目に見えてスコアが伸びるので楽しく勉強できました。
ISUCONの過去問を見るとメジャーな言語は用意されているので、
パフォーマンスチューニングを抜きにしても初めて触る言語の入門として使えるのではないでしょうか。
