---
title: GKE環境で機密情報をSecret Managerではなくkubernetesで渡す
tags:
  - kubernetes
  - GKE
  - GoogleCloud
  - secretmanager
private: false
updated_at: '2026-11-10T11:34:14+09:00'
id: 78bf559009a4be21ed2a
organization_url_name: advancednet-inc
slide: false
ignorePublish: false
posting_campaign_uuid: null
agreed_posting_campaign_term: false
---
# はじめに
昨今情報流出が止まらないですよね。
もしレポジトリのソースコードが漏れたとき認証情報などのパスワードが平文で載っていると被害が甚大になります。
コード上からそういった情報を一掃しようという記事になります。

GCPなら真っ先にSecret Managerが候補に出てくると思いますが、今回はkubernetesのSecretを選んだので、その理由と構成をまとめます。

# 環境
GKE Autopilot 1.36.4-gke.2046003（検証用クラスタ）
DB: MySQL

# Secret Managerにしなかった理由
## GCP側の設定が要る
GKEからSecret Managerを読む方法はいくつかありますが、GKEの「Secret Managerアドオン」（Secret ManagerのシークレットをPodにファイルとしてマウントする機能）を使う場合は、クラスタでアドオンを有効にして、kubernetesのServiceAccountに`roles/secretmanager.secretAccessor`を付与する必要があります。
ドキュメントの例はシークレットごとに付与する形でした。

https://cloud.google.com/secret-manager/docs/secret-manager-managed-csi-component?hl=ja

kubernetesのSecretなら、デプロイ先のnamespaceにkubectlで作るだけで、GCP側の設定を足さずにクラスタの中で済みます。

## アドオンだとファイルで渡ってくる
もう1つ、アドオンはシークレットをPodにファイルとしてマウントする作りです。
環境変数にはならないので、アプリ側でファイルを読む処理が要ります。
kubernetesのSecretへ同期したい場合は別の機能を案内されていました。

https://docs.cloud.google.com/secret-manager/docs/sync-k8-secrets?hl=ja

環境変数で受け取るアプリなら、kubernetesのSecretを環境変数にするだけで済みます。

アドオンでも試してみたのですが、IAMの設定がいるのでkubernetesを使用しているなら直接Secretを使用するのが素直かなと思います。

### Tips: Secret Managerでマウントしてみる
シークレットを1つ作ってSecretProviderClassとマウントするPodを用意し、IAMを付ける前に起動すると、Podは`ContainerCreating`のまま止まります。
イベントのFailedMountは、末尾が以下のようになっていました。

```
rpc error: code = PermissionDenied desc = Permission 'secretmanager.versions.access' denied on resource (or it may not exist).
```

原因は前述の通りGCP側のIAMでした。
シークレット単位で`roles/secretmanager.secretAccessor`を付けたら、Podを作り直さなくても2分ほどでマウントされました。
kubernetesのSecretなら、この手順自体が要りません。

マウント先は`/etc/sm/db-password`というファイルで、環境変数にはなりません。

```console
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
`MASTER_DB_PASSWORD`に`dummy-master`を入れたSecret（`api-secrets`）を`kubectl get secret -o yaml`で見ると、以下のようになっていました。

```console
$ kubectl get secret api-secrets -n app-dev -o yaml
data:
  HISTORY_DB_PASSWORD: ZHVtbXktaGlzdG9yeQ==
  MASTER_DB_PASSWORD: ZHVtbXktbWFzdGVy
```

`MASTER_DB_PASSWORD`の値を`base64 -d`に通すと`dummy-master`に戻りました。

GKEの場合は、Secretも含めて保存時にデフォルトで暗号化されています。

https://cloud.google.com/kubernetes-engine/docs/how-to/encrypting-secrets?hl=ja

以下の場合はSecret Managerを使用することになりますね。
- バージョニングをしたい
- シークレット単位で読める人を絞りたい

# 構成
アプリはDBのパスワードを環境変数から読むようにしています。

## Secretを作る
Secretはnamespaceごとにkubectlで作ります。
マニフェストに書くと値がレポジトリに入ってしまうので、ここだけ手作業になります。

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

1行で済むので楽ですが、Secretはあるのに中のキーが足りないと、その環境変数が無いだけでPodは起動してしまいます。
アプリ側で環境変数が無いときにエラーにしていないと、パスワードが空のまま起動が進んで、DBに繋ぎに行くまで気づけません。

なので`secretKeyRef`で1個ずつ書くのがよさそうです。

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

書く量は増えますが、キーの入れ忘れがデプロイの時点で止まってくれるというのがミソだと思います。

### どのパターンで止まるか
実際にどこまで止まるのか、アプリが3つ目の`REPLICA_DB_PASSWORD`を期待している想定で、Secretには2つしか入れずに試してみました。

- `envfrom-ok`: envFromで`api-secrets`を読む。`REPLICA_DB_PASSWORD`は入っていない
- `envfrom-nosecret`: envFromで`api-secret`を読む（Secretの名前を`s`抜いて打ち間違えたパターン）
- `keyref-nosecret`: secretKeyRefで`api-secret`を読む（Secretの名前を`s`抜いて打ち間違えたパターン）

```console
$ kubectl get pods -n app-dev
NAME                   READY   STATUS                       RESTARTS   AGE
envfrom-nosecret       0/1     CreateContainerConfigError   0          13s
envfrom-ok             1/1     Running                      0          13s
keyref-nosecret        0/1     CreateContainerConfigError   0          12s
```

```console
$ kubectl get events -n app-dev --field-selector involvedObject.name=envfrom-nosecret
LAST SEEN   TYPE      REASON      OBJECT                 MESSAGE
12s         Warning   Failed      pod/envfrom-nosecret   Error: secret "api-secret" not found
```

`keyref-nosecret`のイベントも同じ`Error: secret "api-secret" not found`でした。

止まらなかったのは`envfrom-ok`だけです。
同じenvFromでも、Secretそのものが無い`envfrom-nosecret`は止まっていて、envFromが見逃すのはSecretの中のキーが足りないときだけでした。

### デプロイ中に止まったらどうなるか
動いているDeploymentに`kubectl patch`で`REPLICA_DB_PASSWORD`の`secretKeyRef`を足して、Secretには入れずに挙動を見てみました。
変更前のPodが`api-59f97c566b-cwhdb`、変更後のPodが`api-58f7b58bc5-6qdzd`です。

patchした直後です。

```console
$ kubectl get pods -n app-dev
NAME                   READY   STATUS                       RESTARTS   AGE
api-58f7b58bc5-6qdzd   0/1     CreateContainerConfigError   0          10s
api-59f97c566b-cwhdb   1/1     Running                      0          100s
$ kubectl rollout status deployment/api -n app-dev --timeout=15s
Waiting for deployment "api" rollout to finish: 1 old replicas are pending termination...
error: timed out waiting for the condition
```

変更後のPodは止まって、変更前のPodはRunningのままです。
イベントには`Error: couldn't find key REPLICA_DB_PASSWORD in Secret app-dev/api-secrets`が出ていて、`kubectl rollout status`は待ち続けてタイムアウトします。

`progressDeadlineSeconds`の既定は600秒なので、10分後に`ProgressDeadlineExceeded`になりました。
以下は10分後の出力です。
Podの部分だけ載せています。

```console
$ kubectl get deploy,rs,pods -n app-dev
NAME                       READY   STATUS                       RESTARTS   AGE
pod/api-58f7b58bc5-6qdzd   0/1     CreateContainerConfigError   0          10m
pod/api-59f97c566b-cwhdb   1/1     Running                      0          11m
$ kubectl rollout status deployment/api -n app-dev --timeout=5s
error: deployment "api" exceeded its progress deadline
```

自動で元には戻らず、変更前のPodが動き続けるだけです。

Secretにキーを足してみると、applyし直さなくても13秒くらいで、止まっていた変更後のPodがそのまま起動して、変更前のPodが消えました。
頭がいい。

```console
$ kubectl patch secret api-secrets -n app-dev -p '{"stringData":{"REPLICA_DB_PASSWORD":"dummy-replica"}}'
secret/api-secrets patched
$ kubectl get pods -n app-dev -l app=api
NAME                   READY   STATUS    RESTARTS   AGE
api-58f7b58bc5-6qdzd   1/1     Running   0          12m
$ kubectl rollout status deployment/api -n app-dev --timeout=5s
deployment "api" successfully rolled out
```

## pemのような鍵ファイルはマウントして渡す
pemのような鍵ファイルは、Secretをファイルとしてマウントできます。

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

Secretのキーごとにファイルができるので、アプリはパスを読む必要があります。
鍵ファイルは、ファイル名をキーの名前にしてSecretに入れておきます。
検証ではダミーの文字列を`private.pem`というキーで入れました。

```sh
$ kubectl create secret generic api-keys -n app-dev \
    --from-literal=private.pem='dummy-private-key'
secret/api-keys created
```

マウントすると、キーの`private.pem`が`/etc/api-keys/private.pem`というファイルになります。

```console
$ kubectl exec volume-ok -n app-dev -- ls -la /etc/api-keys
drwxr-xr-x 2 root root   60 Nov  8 10:35 ..2026_11_08_10_35_13.2955777699
lrwxrwxrwx 1 root root   32 Nov  8 10:35 ..data -> ..2026_11_08_10_35_13.2955777699
lrwxrwxrwx 1 root root   18 Nov  8 10:35 private.pem -> ..data/private.pem
$ kubectl exec volume-ok -n app-dev -- cat /etc/api-keys/private.pem
dummy-private-key
```

`private.pem`は`..data`を経由したシンボリックリンクになっていました。
実体の権限は`644 root:root`で、コンテナ内の誰でも読めそうです。

```console
$ kubectl exec volume-ok -n app-dev -- stat -L -c "%a %U:%G" /etc/api-keys/private.pem
644 root:root
```

`mount`で見るとマウント先は読み取り専用の`tmpfs`だったので、ノードのディスクには書かれていないっぽいです。

```console
$ kubectl exec volume-ok -n app-dev -- sh -c 'mount | grep api-'
tmpfs on /etc/api-keys type tmpfs (ro,relatime,size=28926996k,noswap)
tmpfs on /etc/api-secrets type tmpfs (ro,relatime,size=28926996k,noswap)
```

## パスワードを変えたらPodを作り直す
同じSecretを環境変数とファイルの両方で渡したPodで、値を変えてみました。
`MASTER_DB_PASSWORD`を`dummy-master`から`dummy-master-v2`に変えて、Secretの値とvolumeでマウントしたファイルを2秒おきに確認して、ファイルが変わった時点で環境変数も見ています。

```
19:36:41 secret=dummy-master-v2
19:37:47 file=dummy-master-v2
== env at 19:37:47
MASTER_DB_PASSWORD=dummy-master
```

ファイルは1分ちょっと後に新しい値になりましたが、環境変数は古いままでした。ん？
環境変数はコンテナ起動時に渡されるだけなので、**パスワードを変えたらPodを作り直す必要があります**。

起動し直したPodには新しい値が入ります。
値を変えたのはデプロイ中に止まった話でキーを足すより前だったので、キーを足したあとに起動した`api`のPodには新しい値が入っていました。

```console
$ kubectl exec deploy/api -n app-dev -- env | grep _DB_PASSWORD
HISTORY_DB_PASSWORD=dummy-history
REPLICA_DB_PASSWORD=dummy-replica
MASTER_DB_PASSWORD=dummy-master-v2
```

# まとめ
バージョンが残る分Secret Managerのほうが便利ですが、GCPの設定をおいそれといじれない環境にいるときもできますよーという記事でした。
大AI時代で.envを普通にpushしちゃってるレポジトリも多々見られるので、最低限こういった設定はしておきたいですね。
