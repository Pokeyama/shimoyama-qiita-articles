# GitBucket + gitbucket-ci-plugin + .NET SDK
#
# 公式イメージ gitbucket/gitbucket はDocker Hub上が4.38.4(2022年)で止まっているため、
# 公式リポジトリのDockerfileと同じ構成(eclipse-temurin:17 + gitbucket.war)を自前で組む。
# ci-pluginはGitBucketのJVMプロセス内でシェルを叩くので、.NET SDKも同じコンテナに入れる。
FROM eclipse-temurin:17

# GitBucketのバージョンと.NET SDKのチャンネル
ARG GITBUCKET_VERSION=4.47.0
ARG DOTNET_CHANNEL=8.0

# ビルドに使うツールと.NETの実行時依存パッケージ
# libicuを入れておかないとdotnetがグローバリゼーション関連で落ちる
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      curl \
      git \
      unzip \
      libicu-dev \
      libssl-dev \
      zlib1g \
 && rm -rf /var/lib/apt/lists/*

# gitbucket.warを取得
ADD https://github.com/gitbucket/gitbucket/releases/download/${GITBUCKET_VERSION}/gitbucket.war /opt/gitbucket.war

# .NET SDKはdotnet-install.shで入れる
# スクリプトがarm64/amd64を自動判別するのでApple Silicon/x86どちらでも同じ手順で通る
ENV DOTNET_ROOT=/usr/share/dotnet
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && chmod +x /tmp/dotnet-install.sh \
 && /tmp/dotnet-install.sh --channel ${DOTNET_CHANNEL} --install-dir ${DOTNET_ROOT} \
 && ln -s ${DOTNET_ROOT}/dotnet /usr/local/bin/dotnet \
 && rm /tmp/dotnet-install.sh

# ci-pluginはビルド時にHOMEをビルド用の一時ディレクトリへ差し替える。
# そのままだとNuGetキャッシュがビルドごとに消えて毎回パッケージを取り直すので、
# キャッシュ位置をHOMEの外に固定しておく。
ENV PATH="${PATH}:/usr/share/dotnet" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    NUGET_PACKAGES=/opt/nuget/packages \
    DOTNET_CLI_HOME=/opt/dotnet-home
RUN mkdir -p /opt/nuget/packages /opt/dotnet-home \
 && dotnet --info

# ここから先は公式gitbucket-dockerのDockerfileと同じ
RUN ln -s /gitbucket /root/.gitbucket
VOLUME /gitbucket
EXPOSE 8080 29418
CMD ["sh", "-c", "java -jar /opt/gitbucket.war"]
