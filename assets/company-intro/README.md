# 会社紹介記事の素材

| ファイル | 内容 | 挿入位置 |
| --- | --- | --- |
| `advent-calendar.png` | アドカレのカレンダー（本人のスクショ） | はじめに |
| `vtuber-stage.png` | 社内MTGで使っている3Dステージ（本人のスクショ） | 社内MTGはVTuberで出ています |
| `rookie-list.png` | 新人研修の課題の一覧画面。顧客の列を塗りつぶして右と下の余白を切った | 入社してから |
| `tech-stack.png` | 言語・インフラ・コンテナ・DBのロゴ図。編集元は`tech-stack.drawio` | 仕事 |

`build_diagram.py`で`tech-stack.drawio`を生成し、draw.io Desktopで書き出しています。

```sh
python3 build_diagram.py
/Applications/draw.io.app/Contents/MacOS/draw.io -x -f png -s 2 -b 16 -o tech-stack.png tech-stack.drawio
```

## ロゴの配布元

色と縦横比を維持してdrawioへ埋め込んでいます。

| ファイル | 配布元 |
| --- | --- |
| `icons/csharp.svg` | .NET公式ブランドレポジトリ https://github.com/dotnet/brand の `logo/language-icons/csharp-72.svg` |
| `icons/php.svg` | https://www.php.net/download-logos.php の https://www.php.net/images/logos/new-php-logo.svg |
| `icons/go.svg` | https://go.dev/brand の https://go.dev/images/go-logo-blue.svg |
| `icons/aws.png` | https://a0.awsstatic.com/libra-css/images/logos/aws_logo_smile_1200x630.png （`icons/aws-og.png`）をロゴ周りで切り抜き |
| `icons/google-cloud.png` | Google公式配信ドメイン https://www.gstatic.com/images/branding/product/2x/google_cloud_512dp.png |
| `icons/docker.svg` | `assets/gemini-thinking-timeout/icons/docker.svg`の流用（Docker公式ZIPの`docker-mark-ocean-blue.svg`） |
| `icons/kubernetes.svg` | CNCF公式アートワーク https://github.com/cncf/artwork の `projects/kubernetes/icon/color/kubernetes-icon-color.svg` |
| `icons/mysql.svg` | https://labs.mysql.com/common/logos/mysql-logo.svg |
| `icons/spanner.svg` | Google Cloud公式アイコン https://services.google.com/fh/files/misc/core-products-icons.zip の `Unique Icons/Cloud Spanner/SVG/CloudSpanner-512-color.svg` |

各ロゴ・商標の権利はそれぞれの権利者に帰属します。
