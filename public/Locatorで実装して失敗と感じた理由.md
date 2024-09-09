---
title: 【DI】Service LocatorパターンとDIパターンを理解せず実装して失敗と感じた理由
tags:
  - 'DI'
private : true
updated_at: ''
id: null
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
大規模案件（API数100個超、テーブル数500個弱）のサーバーサイドプログラムをエセService Locatorパターンで実装していました。（エセと言っている理由は後術）
最初のリリースは乗り切ったのですが、終わってみて大失敗だったなと思ったので言語化して供養します。

# 対象読者
・オブジェクト指向はある程度理解してるけどコード設計をしたことがない人
・Service LocatorとDependency Injectionという設計パターンがある中で、前者がアンチパターンと呼ばれてる理由がピンときていない人

# 環境
言語 C# .NET8
DB Mysql Spanner

# Service Locatorとは
こちらのページが非常にわかりやすいので理解していない方は読むことをオススメします。

https://www.nuits.jp/entry/servicelocator-vs-dependencyinjection

今回の案件では以下のようになっていました。

![image.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/098f0788-c593-3122-11ed-13d528d495a4.png)

コードはめちゃくちゃ省略しますが以下のような感じです。

```c#
public abstract class AContainer
{
    protected static ConcurrentDictionary<string, string> _userParams = new ConcurrentDictionary<string, string>();

    protected static string GetUserParam(string name)
    {
      return AContainer._userParams[name];
    }

    protected virtual void SetUserObject(string name, object value)
    {
      if (this._userObjects == null)
        this._userObjects = new Dictionary<string, object>();
      this._userObjects[name] = value;
    }
}

public class DIContainer : AContainer
{
    public TApplication GetApplication<TApplication>() where TApplication : class, IApplication, new()
    {
        var name = typeof(TApplication).FullName;
        var application = GetUserObject<TApplication>(name);
        if (application == null)
        {
            application = new TApplication();
            application.Initialize(this);

            SetUserObject(name, application);
        }

        return application;
    }

    public TService GetService<TService>() where TService : class, IService, new()
    {
        var name = typeof(TService).FullName!;
        var service = GetUserObject<TService>(name);
        if (service == null)
        {
            service = new TService();
            service.Initialize(this);

            SetUserObject(name, service);
        }

        return service;
    }

    // GetRepository()もほぼ同じ実装
}
```

ControllerとApplicationがDIコンテナを依存しているのに（Service Locator）
**ServiceもDIコンテナに依存してしまっているため**エセService Locatorと表現しました。
実際に使用する際はこれまた超省略しますが、以下のようになります。

```c#
public class InfoController
{
    [HttpPost("/api/info")]
    public ActionResult Index()
    {
        return DIContainer.GetApplication<InfoApplication>().Invoke(req);
    }
}

public class InfoApplication : AApplication
{
    public async Task<InfoOutput> Invoke(object req)
    {
        // 主処理
        var result = DIContainer.GetService<InfoService>().Invoke(req);

        return new InfoOutput()
        {
          
        };
    }
}

public class InfoService : AService
{
    public void Invoke()
    {
        // IO処理
        var result = DIContainer.GetRepository<InfoRepository>().Fetch();
    }
}
```

# 問題点
## コンストラクタが封印されている
Get〇〇と置いているメソッドが以下のようにコンストラクタを考慮しない作りになってしまっています。
普段オブジェクト指向的な考えでコードを書いている人からすると違和感ありまくりです。
単純に書きづらい。

```c#
application = new TApplication();
```

## 途中参画厳しすぎ
コンストラクタが封印されていることで、各Applicationの依存しているServiceが不明瞭です。
Application内でServiceが複数呼び出されることは普通にあるので、コード量が増すとそれだけで可読性が著しく低下します。
ある程度進んだ状態でこのプロジェクトにアサインされると以下のようになっていて、どうMockセットすればいいかだけで半日余裕で使います。

```c#
public class InfoApplication : AApplication
{
    public async Task<InfoOutput> Invoke(object req)
    {
        DIContainer.GetService<FooService>().Invoke(req);

        // 10行くらいの処理

        DIContainer.GetService<BarService>().Invoke(req);

        // 10行くらいの処理

        DIContainer.GetService<HogeService>().Invoke(req);

        // 10行くらいの処理

        return new InfoOutput()
        {
          
        };
    }
}
```

## テストの可読性低下
すべての層でDIコンテナが依存してしまっているので、全てのテストでコンテナのMockが必要になります。
ここのコンテナは全て一緒なわけで共通化された”なんでも使える”モックコンテナを作成しがちになります。
そうなるとテスト側でもどのService、Repositoryを使用しているか判断できないので非常に可読性が落ちます。

```c#
public class ATestApplication
{
  protected DIContainer CreateContainer()
  {
      applicationContainer.GetRepository<HogeRepository>().Returns(hoge);
      applicationContainer.GetRepository<FugaRepostiroy>().Returns(fuga);
      applicationContainer.GetRepository<BarRepository>().Returns(bar);
      // どのApplicationでも使えるように無限に増えていく
  }
}

public class TestInfoApplication : ATestApplication
{
  [Fact]
  public void TestTrue()
  {
      // Containerのモックを作成
      var applicationContainer = CreateContainer();
      applicationContainer.GetService<HogeService>().Returns(hoge);

      var application = new InfoApplication();
      application.Initialize(applicationContainer);

      var result = await application.Invoke(req);
      Assert.IsType<InfoOutput>(result);
  }
}
```

# 改善策
まだまだ問題はあると思うのですが、とりあえずこのエセService Locatorを脱却しないと始まらないと思っています。

## Dependency Injection
DIContainerそのものを取っ払ってシンプルに考え直します。
例えば以下のようにコンストラクタから注入するようにするだけでメンバ変数が依存先を表しているので一気に見通しがよくなります。

```c#
public class InfoApplication
{
    private readonly FooService _fooService;

    private readonly BarService _barService;

    private readonly HogeService _hogeService;

    public InfoApplication(FooService fooService, BarService barService, HogeService hogeService)
    {
        _fooService = fooService;
        _barService = barService;
        _hogeService = hogeService;

    }

    public async Task<InfoOutput> Invoke(object req)
    {
        _fooService.Invoke(req);

        // 10行くらいの処理

        _barService.Invoke(req);

        // 10行くらいの処理

        _hogeService.Invoke(req);

        // 10行くらいの処理

        return new InfoOutput()
        {
          
        };
    }
}
```

テストにおいてもコンストラクタから挿入することを**強制**するので何をMockすればいいかひと目でわかるようになります。

```c#
public class TestInfoApplication
{
  [Fact]
  public void TestTrue()
  {
      var fooService = new Mock<FooService>();
      fooService.Invoke.Returns(foo);
      var barService = new Mock<BarService>();
      barService.Invoke.Returns(bar);
      var hogeService = new Mock<HogeService>();
      hogeService.Invoke.Returns(hoge);

      var application = new InfoApplication(fooService, barService, hogeService);

      var result = await application.Invoke(req);
      Assert.IsType<InfoOutput>(result);
  }
}
```