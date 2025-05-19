---
title: nxbt-demo
tags:
  - nxbt
  - RaspberryPi
  - NintendoSwitch
  - Bluetooth
private: true
updated_at: '2025-05-19'
id: null
organization_url_name: null
slide: false
ignorePublish: false
---

# はじめに

---

# 環境

- **ホスト**：MacBook Pro M3（macOS Sonoma 最新）  
- **ラズパイ**：Raspberry Pi Zero 2 W（Raspberry Pi OS Lite 32-bit）  
- **Switch**：Nintendo Switch 本体（最新システム Ver.16.0.0 以上推奨）  

---

# 前準備

## OS 書き込み
Raspberry Pi Imager を使って SD カードに Raspberry Pi OS Lite (32-bit) を書き込み
その際、バージョンは以下のものにする。（2025/05/19時点）
![スクリーンショット 2025-05-12 22.52.17.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/f9ba72e7-4e50-4d7c-b54c-17679c64a8f2.png)

また、公開鍵方式での認証をONにしておく。

![スクリーンショット 2025-05-12 21.35.04.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/7138da4f-41ac-4c5b-b80f-701efaf3a0e2.png)

以下からはホスト名が`nxbt.local`な前提で進めます。

## SSH接続
書き込み後、`~/.ssh`以下に公開鍵ができているので適当に移動させておく。
```sh
$ cp id_rsa keys/nxbt
$ cp id_rsa.pub keys/nxbt
```

`~/.ssh/config`に保存した公開鍵を使用するように以下を追加しておく。
```
Host nxbt
  HostName nxbt.local # OS書き込み時のホスト名
  User shimoyama # OS書き込み時のユーザー名
  IdentityFile ~/.ssh/keys/nxbt/id_rsa
  IdentitiesOnly yes
```

sshで接続できればOK。
```sh
$ ssh nxbt
Linux nxbt 6.1.21-v7+ #1642 SMP Mon Apr  3 17:20:52 BST 2023 armv7l

The programs included with the Debian GNU/Linux system are free software;
the exact distribution terms for each program are described in the
individual files in /usr/share/doc/*/copyright.

Debian GNU/Linux comes with ABSOLUTELY NO WARRANTY, to the extent
permitted by applicable law.
Last login: Mon May 19 18:25:28 2025 from 
shimoyama@nxbt:~ $
```

パッケージをアップデートしておきます。
```sh
$ sudo apt update && sudo apt full-upgrade -y
$ sudo reboot
```

# nxbtインストール

## 汎用パッケージ準備
Liteだと`vi`しか入っていなくて使いづらいので`vim`を入れておきます。
また、nxbtはPythonパッケージなのでpipも入れておきます。
tmuxはスクリプト実行時、ホストPCのセッションが切れても動かす用。
```sh
$ sudo apt install -y python3-pip vim tmux
```

## Bluetooth周り
### 初期化及びAPI有効化
初期設定だとnxbtで使用されているBluetooth周りの動作が不安定なので一旦初期化して強制的にONにしておきます。
```sh
# 一度削除してほぼ初期状態に戻す
$ sudo apt purge -y bluez
$ sudo rm -f /etc/bluetooth/main.conf
$ sudo rm -f /etc/systemd/system/bluetooth.service.d/override.conf

# 再インストール
$ sudo apt install -y bluez
```
設定ファイルも以下のように編集
```sh
$ vim /etc/bluetooth/main.conf
# 以下を編集
[General]
ControllerMode = dual    # Classic + LE 両対応
Experimental   = true    # experimental API を有効化

[Policy]
AutoEnable     = true    # 起動時に Powered=on
```

設定が終わったら再起動しておきます。
```sh
$ sudo systemctl daemon-reload
$ sudo systemctl restart bluetooth
```

### inputプラグイン無効化
inputプラグインを無効化しないとPython側で見失うときがあるので、オフにしておきます。

```sh
$ sudo systemctl edit bluetooth.service
```

nanoが開くので以下の項目を追加もしくは上書き
```
[Service]
ExecStart=
ExecStart=/usr/libexec/bluetooth/bluetoothd --experimental --noplugin=input
```

こちらも設定が終わったら再起動しておきます。
```sh
$ sudo systemctl daemon-reload
$ sudo systemctl restart bluetooth
```

## nxbtインストール

```sh
$ sudo pip3 install --upgrade nxbt
```

使用されているモジュールのバージョンが一致していないため、依存しているバージョンに合わせて再インストール

```sh
# 1. 既存の Flask/Jinja2/itsdangerous/Werkzeug を一度アンインストール
sudo pip3 uninstall -y Flask Jinja2 itsdangerous Werkzeug

# 2. nxbt が動作確認済みの推奨バージョンをまとめてインストール
sudo pip3 install \
  Flask==2.0.3 \
  Jinja2==3.0.3 \
  itsdangerous==2.0.1 \
  Werkzeug==2.0.3
```

# ブラウザで確認
以下でサーバー起動。
```sh
sudo nxbt webapp --port 8080
```
以下にアクセスし、Switch側ではコントローラーの順番画面で待機しておき、UIの指示通りペアリングできればOK。
http://nxbt.local:8080/

GUI版は不安定なので動作確認のみ。

# スクリプト実行
ラズパイ側のルートディレクトリに以下の`spam_a.py`ファイルを作成

```python: spam_a.py
#!/usr/bin/env python3
import time
import nxbt
from nxbt import Buttons, Sticks

nx = nxbt.Nxbt()
ctr = nx.create_controller(nxbt.PRO_CONTROLLER)
print("Waiting for controller…")
nx.wait_for_connection(ctr)
print("Connected! Starting macro…")

try:
    # 初回操作例
    nx.press_buttons(ctr, [Buttons.A], down=0.05); time.sleep(3.0)
    nx.press_buttons(ctr, [Buttons.A], down=0.05); time.sleep(1.0)
    nx.press_buttons(ctr, [Buttons.B], down=0.05); time.sleep(1.0)
    # 右スティック上下
    nx.tilt_stick(ctr, Sticks.RIGHT_STICK, 0, 100, tilted=1.0); time.sleep(1.0)
    nx.tilt_stick(ctr, Sticks.RIGHT_STICK, -100, 0, tilted=1.0); time.sleep(1.0)
    # メインループ：A連打＆再接続
    while True:
        try:
            nx.press_buttons(ctr, [Buttons.A], down=0.05)
        except:
            print("Connection lost — retrying")
            nx.wait_for_connection(ctr)
            print("Reconnected — resuming")
        time.sleep(0.05)

except KeyboardInterrupt:
    print("Stopping…")
    nx.remove_controller(ctr)

```

実行権限を付与
```sh
$ chmod +x spam_a.py
```

Switch側の画面をコントローラーの順序にしておいて、以下を実行
```sh
$ sudo ./spam_a.py
```

なんか動けばOK