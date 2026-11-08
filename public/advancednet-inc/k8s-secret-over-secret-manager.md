---
title: DBのパスワードをSecret Managerではなくkubernetesで渡す
tags:
  - kubernetes
  - GKE
  - GoogleCloud
  - SecretManager
private: false
updated_at: ''
id: null
organization_url_name: advancednet-inc
slide: false
ignorePublish: true
posting_campaign_uuid: null
agreed_posting_campaign_term: false
---
# はじめに
GKEで動かしているAPIサーバーの設定ファイルに、DBの接続情報がそのまま書かれてレポジトリに入っていました。
このままだと本番の値もいずれ同じ場所に書くことになります。
今後は秘密鍵も扱う予定なので、その前に値をレポジトリから外す仕組みを作ることにしました。

GCPなら真っ先にSecret Managerが候補に出てくると思いますが、今回はkubernetesのSecretを選んだので、その理由と構成をまとめます。

実際の環境に入れる前に、検証用のGCPプロジェクトに一時的にGKE Autopilotのクラスタを作って挙動を試しました。
記事中のコマンドの出力は、そのクラスタのものです。
クラスタは45分ほどで消して、単価から計算した費用は15円くらいでした（請求額は見ていない）。

# 環境
GKE Autopilot 1.36.4-gke.2046003（検証用クラスタ）
DB: MySQL

# レポジトリから外すもの
設定ファイルに書いてある接続情報のうち、外すのはMySQLのパスワード2つだけにしました。

ホスト名やユーザー名は見られても困らないのでそのままです。

# Secret Managerにしなかった理由
## GCP側の設定が要る
GKEからSecret Managerを読む方法はいくつかありますが、公式のアドオンを使う場合はクラスタでアドオンを有効にして、kubernetesのServiceAccountに`roles/secretmanager.secretAccessor`を付与する必要があります。
ドキュメントの例はシークレットごとに付与する形でした。

https://cloud.google.com/secret-manager/docs/secret-manager-managed-csi-component?hl=ja

kubernetesのSecretなら、デプロイ先のnamespaceにkubectlで作るだけで、GCP側の設定を足さずにクラスタの中で済みます。

## アドオンだとファイルで渡ってくる
もう1つ、アドオンはシークレットをPodにファイルとしてマウントする作りです。
環境変数にはならないので、アプリ側でファイルを読む処理が要ります。
kubernetesのSecretへ同期したい場合は別の機能を案内されていました。

環境変数で受け取るアプリなら、kubernetesのSecretを環境変数にするだけで済みます。

検証用のプロジェクトならIAMを自分で触れるので、アドオンも試しました。
シークレットを1つ作ってSecretProviderClassとマウントするPodを用意し、IAMを付ける前に起動すると、Podは`ContainerCreating`のまま止まります。
イベントのFailedMountは、末尾が以下のようになっていました。

```
rpc error: code = PermissionDenied desc = Permission 'secretmanager.versions.access' denied on resource (or it may not exist).
```

原因はGCP側のIAMです。
シークレット単位で`roles/secretmanager.secretAccessor`を付けたら、Podを作り直さなくても2分ほどでマウントが通りました。
kubernetesのSecretなら、この手順自体が要りません。

マウント先は`/etc/sm/db-password`というファイルで、環境変数にはなりません。

```
$ kubectl exec sm-check -n app-dev -- cat /etc/sm/db-password
dummy-sm-password
```

# kubernetesのSecretとSecret Managerの比較
ざっくり並べると以下のような感じです。

| | kubernetesのSecret | Secret ManagerのGKEアドオン |
| --- | --- | --- |
| 置き場所 | クラスタのnamespace | GCPのプロジェクト |
| 作る人 | kubectlを叩ける人 | GCPのIAMを触れる人 |
| 読める人の制御 | kubernetesのRBACとGKEのIAM | シークレット単位のIAM |
| 値の履歴 | なし、上書き | バージョンで残る |
| Podへの渡し方 | 環境変数かファイル | ファイル |
| 料金 | 追加ではかからない | 1バージョン月`$0.06`から |

Secret Managerは無料枠もあるので、パスワード数個なら料金は気にしなくてよさそうです。

https://cloud.google.com/secret-manager/pricing?hl=ja

Secretの値はbase64なだけで、kubernetesの公式ドキュメントにも、デフォルトではetcdに暗号化されずに保存されると書かれています。

> KubernetesのSecretは、デフォルトでは、APIサーバーの基礎となるデータストア(etcd)に暗号化されずに保存されます。

https://kubernetes.io/ja/docs/concepts/configuration/secret/

kubectlで読める人には値が見えます。
検証用クラスタで`kubectl get secret -o yaml`すると以下のようになっていました。

```
$ kubectl get secret api-secrets -n app-dev -o yaml
data:
  HISTORY_DB_PASSWORD: ZHVtbXktaGlzdG9yeQ==
  MASTER_DB_PASSWORD: ZHVtbXktbWFzdGVy
```

`MASTER_DB_PASSWORD`の値を`base64 -d`に通すと`dummy-master`に戻りました。
読める人には平文と同じです。

GKEの場合は、Secretも含めて保存時にデフォルトで暗号化されています。
Cloud KMSの鍵でもう一段暗号化するアプリケーションレイヤーの暗号化もありますが、クラスタ側の設定なので今回は触っていません。
検証用クラスタの`databaseEncryption`も`state: DECRYPTED`でした。
これはアプリケーションレイヤーの暗号化をしていないという意味で、保存時の暗号化が無いという意味ではありません。

https://cloud.google.com/kubernetes-engine/docs/how-to/encrypting-secrets?hl=ja

# 構成
アプリはDBのパスワードを環境変数から読む前提です。

## Secretを作る
Secretはnamespaceごとにkubectlで作ります。
マニフェストに書くと値がレポジトリに入ってしまうので、ここだけ手作業です。

```sh
$ kubectl create secret generic api-secrets -n app-dev \
    --from-literal=MASTER_DB_PASSWORD='dummy-master' \
    --from-literal=HISTORY_DB_PASSWORD='dummy-history'
secret/api-secrets created
```

※`--from-literal`はシェルの履歴に値が残るので、本番はファイルから読ませる`--from-file`のほうが良さそうです。

## Deploymentでは1個ずつ渡す
Secretの値を環境変数にする方法は2つあります。
`envFrom`を使うと、Secretの中身を全部まとめて環境変数にできます。

```yaml:envFromの場合
          envFrom:
            - secretRef:
                name: api-secrets
```

1行で済むので楽ですが、Secretを作るときにキー名を打ち間違えたり、1つ入れ忘れたりしても、その環境変数が無いだけでPodは起動します。
アプリ側で環境変数が無いときにエラーにしていないと、パスワードが空のまま起動が進んで、DBに繋ぎに行くまで気づけません。

なので`secretKeyRef`で1個ずつ書きます。

```yaml:deployment.yaml
          env:
            - name: MASTER_DB_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: api-secrets
                  key: MASTER_DB_PASSWORD
            - name: HISTORY_DB_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: api-secrets
                  key: HISTORY_DB_PASSWORD
```

こちらはSecretに無いキーを指定するとPodが起動しません。
公式ドキュメントにも書かれています。

> Secretに存在しないキーへの参照（`secretKeyRef`フィールド）はPodの起動を妨げます。

https://kubernetes.io/ja/docs/concepts/configuration/secret/

書く量は増えますが、Secretの入れ忘れがデプロイの時点で止まってくれるというのがミソだと思います。

実際にどこまで止まるのか、アプリが3つ目の`REPLICA_DB_PASSWORD`を期待している想定で、Secretには2つしか入れずに試しました。
Secretの名前を`api-secret`、`api-key`と末尾の`s`を抜いて打ち間違えたパターンも見ています。

| 渡し方 | 状況 | 結果 |
| --- | --- | --- |
| `envFrom` | Secretはあるがキーが足りない | `Running`で起動。足りない環境変数は無いだけ |
| `envFrom` | Secretの名前が違う | `Error: secret "api-secret" not found`で止まる |
| `secretKeyRef` | Secretにキーが無い | `CreateContainerConfigError` |
| `secretKeyRef` | Secretの名前が違う | `Error: secret "api-secret" not found`で止まる |
| volumeのマウント | Secretの名前が違う | `ContainerCreating`のまま止まる |

止まらないのは、Secretはあるのにキーが足りないときの`envFrom`だけです。
volumeは秘密鍵の節で試しました。

止まったあとの様子は、動いているDeploymentで見ました。
`maxSurge: 1`、`maxUnavailable: 0`、readinessProbeありで、RollingUpdateの設定は実際のAPIと同じです。
`kubectl patch`で`REPLICA_DB_PASSWORD`の`secretKeyRef`を足して、Secretには入れませんでした。

```
$ kubectl get pods -n app-dev
NAME                   READY   STATUS                       RESTARTS   AGE
api-58f7b58bc5-6qdzd   0/1     CreateContainerConfigError   0          10s
api-59f97c566b-cwhdb   1/1     Running                      0          100s
```

新しいPodは止まって、古いPodはRunningのままです。
イベントには`Error: couldn't find key REPLICA_DB_PASSWORD in Secret app-dev/api-secrets`が出ていて、`kubectl rollout status`は待ち続けてタイムアウトします。

`progressDeadlineSeconds`の既定は600秒なので、10分後に`ProgressDeadlineExceeded`になりました。
自動で元には戻らず、古いPodが動き続けるだけです。

```
$ kubectl rollout status deployment/api -n app-dev --timeout=5s
error: deployment "api" exceeded its progress deadline
```

Secretにキーを足してみると、applyし直さなくても13秒くらいで止まっていた同じPodが起動して、古いPodが消えました。

```
$ kubectl patch secret api-secrets -n app-dev -p '{"stringData":{"REPLICA_DB_PASSWORD":"dummy-replica"}}'
secret/api-secrets patched
$ kubectl rollout status deployment/api -n app-dev --timeout=5s
deployment "api" successfully rolled out
```

Secretの作り忘れで止まっても、Secretに足せば直ります。

## パスワードを変えたらPodを作り直す
`MASTER_DB_PASSWORD`を`dummy-master`から`dummy-master-v2`に変えて、Secretの値とvolumeでマウントしたファイルを2秒おきに確認して、ファイルが変わった時点で環境変数も見ています。

```
19:36:41 secret=dummy-master-v2
19:37:47 file=dummy-master-v2
== env at 19:37:47
MASTER_DB_PASSWORD=dummy-master
```

ファイルは1分ちょっと後に新しい値になりましたが、環境変数は古いままです。
環境変数はコンテナ起動時に渡されるだけなので、**パスワードを変えたらPodを作り直す必要があります**。
値を変えたのは前の節でキーを足すより前だったので、キーを足したあとに起動したPodは新しい値を持っていました。

```
$ kubectl exec deploy/api -n app-dev -- env | grep _DB_PASSWORD
HISTORY_DB_PASSWORD=dummy-history
REPLICA_DB_PASSWORD=dummy-replica
MASTER_DB_PASSWORD=dummy-master-v2
```

## 秘密鍵はファイルで渡す
今後扱う秘密鍵は、Secretをファイルとしてマウントする予定です。

```yaml:deployment.yaml
    spec:
      containers:
        - name: api
          volumeMounts:
            - name: api-keys
              mountPath: /etc/api-keys
              readOnly: true
      volumes:
        - name: api-keys
          secret:
            secretName: api-keys
```

Secretのキーごとにファイルができるので、アプリはパスを読むだけです。
本物の鍵はまだ来ていないので、検証はダミーの文字列でやりました。

```
$ kubectl exec volume-ok -n app-dev -- ls -la /etc/api-keys
drwxr-xr-x 2 root root   60 Nov  8 10:35 ..2026_11_08_10_35_13.2955777699
lrwxrwxrwx 1 root root   32 Nov  8 10:35 ..data -> ..2026_11_08_10_35_13.2955777699
lrwxrwxrwx 1 root root   18 Nov  8 10:35 private.pem -> ..data/private.pem
$ kubectl exec volume-ok -n app-dev -- cat /etc/api-keys/private.pem
dummy-private-key
```

`private.pem`は`..data`を経由したシンボリックリンクになっていました。
実体の権限は`644 root:root`で、コンテナ内の誰でも読めます。
鍵なら`defaultMode`で絞るつもりです。（0400にすれば良さそうですが、まだ試してない）

`mount`で見るとマウント先は読み取り専用の`tmpfs`だったので、ノードのディスクには書かれていないっぽいです。

ハマりそうなのが、`secretName`を`api-key`と打ち間違えたときです。
envのときの`CreateContainerConfigError`にはならず、`ContainerCreating`のまま止まる。
`get pods`だけだと起動中に見えて、理由はイベントを見ないと分からない。

```
Warning   FailedMount   pod/volume-nosecret   MountVolume.SetUp failed for volume "api-keys" : secret "api-key" not found
```

# まとめ
本番の値をレポジトリから外すのに、今回はkubernetesのSecretを使うことにしました。
バージョンが残る分Secret Managerのほうが便利ですが、GCP側の設定を足さずにクラスタの中で済むほうを選んだ感じです。
実際の環境への投入はこれからです。

Secretを手で作る運用なので、どの環境に何を入れたかを別で残しておかないとそのうち事故りそうです。
