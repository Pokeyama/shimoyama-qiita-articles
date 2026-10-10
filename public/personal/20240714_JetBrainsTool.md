---
title: 【JetBrains】Database Tools and SQLでクエリを実行すると"SET autocommit = 0"が効かないとき
tags:
  - PhpStorm
  - JetBrains
  - Rider
private: false
updated_at: '2024-07-14T20:46:01+09:00'
id: 2ff77834d7cec372498d
organization_url_name: null
slide: false
ignorePublish: false
---

# 環境
mysql mariadb
JetBrains系IDE
RiderとPHPStormを常用していますが、他も同じ仕様だと思われる

# 問題
IDEAとかRiderとかでDBをGUIで操作できる「Database Tools and SQL」という超便利機能があります。
さっくり試したいときに多用しているんですが、autocommitを明示的に書いても効かない。

```sql
SET autocommit = 0;

-- テーブル作成
CREATE TABLE IF NOT EXISTS test_table (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- データ挿入
INSERT INTO test_table (name) VALUES
    ('Test Name 1'),
    ('Test Name 2'),
    ('Test Name 3');
```



```text:別セッションで取得
+--+-----------+-------------------+
|id|name       |created_at         |
+--+-----------+-------------------+
|1 |Test Name 1|2024-07-01 02:47:40|
|2 |Test Name 2|2024-07-01 02:47:40|
|3 |Test Name 3|2024-07-01 02:47:40|
+--+-----------+-------------------+
```

# 対策
理由はわからないのですが、ここで"SET autocommit = 0;"を記述しても効かないみたいです。（たぶん↓の設定で上書きされている気がする）

勝手にcommitされたくない場合は以下の「Transaction control」を「Manual」にする必要がありました。

![スクリーンショット 2024-07-01 3.38.56.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/73e226f8-2b70-9bce-2a17-2fa1926e0711.png)

この設定をした上で**IDEを再起動**するとコンソール画面に「commit」と「rollback」のボタンが追加されます。

![スクリーンショット 2024-07-01 3.41.43.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/1f8a1668-0c52-2dee-8117-2ecb0e309cf1.png)

この設定を適用するとautocommitがオフになるようで、「SET autocommit = 0」を書かなくても暗黙的にcommitされることはありません。

# まとめ
結局「SET autocommit = 0」を書いても適用されない理由はわからなかった。
1行ずつ違うセッションで実行されているのかと思ったが、だとするとトランザクションがそもそも意味をなさなくなるので違う。

# 関係ありそうな公式フォーラム
https://intellij-support.jetbrains.com/hc/en-us/community/posts/205993349-Autocommit-setting-buggy
