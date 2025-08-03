---
title: DIコンテナがService Locatorに！実際に陥ったアンチパターン
tags:
  - C#
  - .NET
  - デザインパターン
  - DependencyInjection
private: true
updated_at: '2025-08-03T23:15:55+09:00'
id: 35d1031de2276776e569
organization_url_name: null
slide: false
ignorePublish: false
---
# はじめに
大規模案件（API数100個超、テーブル数500個弱）のサーバーサイドプログラムをDIコンテナを使って実装しました。
最初のリリースは無事に乗り切りましたが、振り返ってみると、DIコンテナを使っていたはずのコードが実際にはService Locatorという一般的にアンチパターンとされる設計に陥っていました。
この問題により、結合度が高く、テストやメンテナンスが非常に困難になった経験を共有します。

# 対象読者
・オブジェクト指向はある程度理解してるけどコード設計をしたことがない人
・Service LocatorとDependency Injectionという設計パターンがある中で、前者がアンチパターンと呼ばれてる理由がピンときていない人

# 環境
言語 C# .NET8

# Service Locatorとは
こちらのページが非常にわかりやすいので理解していない方は読むことをオススメします。
アンチパターンと呼ばれている理由が記述されていて、本記事はそれで実際に失敗した話になります。

https://www.nuits.jp/entry/servicelocator-vs-dependencyinjection

今回の案件では以下のようになっていました。

![image.png](https://qiita-image-store.s3.ap-northeast-1.amazonaws.com/0/855584/098f0788-c593-3122-11ed-13d528d495a4.png)

コードはめちゃくちゃ省略しますが以下のような感じです。
**自社で**DIContainerなるライブラリを用意しています。

```c#:Container
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

```c#:各インターフェース
public interface IApplication
{
    public void Initialize(DIContainer container);
}

public abstract class AApplication : IApplication
{
    protected DIContainer Container;
 
    public void Initialize(DIContainer container)
    {
        Container = container;
    }
}

public interface IService
{
    public void Initialize(DIContainer container);
}

public abstract class AService : IService
{
    protected DIContainer Container;
 
    public void Initialize(DIContainer container)
    {
        Container = container;
    }
}

// Repositoryも同様の実装
```

実際に使用するときは以下のような実装になります。

```c#
public class InfoController
{
    [HttpPost("/api/info")]
    public ActionResult Index()
    {
        return Container.GetApplication<InfoApplication>().Invoke(req);
    }
}

public class InfoApplication : AApplication
{
    public async Task<InfoOutput> Invoke(object req)
    {
        // 主処理
        var result = Container.GetService<InfoService>().Invoke(req);

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
        var result = Container.GetRepository<InfoRepository>().Fetch();
    }
}
```

# 問題点
## Service Locatorになっている
この基盤を作ったときはこれがDIだと思っていました。なぜならDIコンテナを使用しているから。
しかし、DIコンテナからGet〇〇でオブジェクトを取り出している時点でこれはService Locatorです。
表面上はDIコンテナを使っているように見えますが、実際には依存関係が隠され、オブジェクト内部で依存関係を解決しているため、外部からはどのクラスが何に依存しているのかが明示されていません。
全くの見当違いなので、一般的にアンチパターンと呼ばれる状態になっています。

http://blog.a-way-out.net/blog/2015/08/31/your-dependency-injection-is-wrong-as-I-expected/

## Container内で初期化している
DIContainer内でオブジェクトが直接初期化されています。

```c#
application = new TApplication();
```

これにより、各クラスのインスタンス化の責任がコンテナに集中してしまい、クラス間の結合度が非常に高くなっています。
結合度が高い設計はテストが難しくなり、モックを差し替えるのも困難になります。

## 途中参画厳しすぎ
依存関係が`Container.Get〇〇<>()`を通じて動的に解決されているため、各`Application`がどのサービスに依存しているのかが明示されていません。これは、プロジェクトに途中参加した開発者にとって非常に理解が難しく、コード量が増えるほど依存関係が散在し、把握するのに大幅な時間を費やすことになります。

具体的には、サービスがどこでどのように呼び出されているかが分かりづらく、**どのMockを作成すべきか、あるいはどの依存関係が必要か**を判断するだけで、多くの時間がかかる状況になってしまいました。

```c#
public class InfoApplication : AApplication
{
    public async Task<InfoOutput> Invoke(object req)
    {
        Container.GetService<FooService>().Invoke(req);

        // 10行くらいの処理

        Container.GetService<BarService>().Invoke(req);

        // 10行くらいの処理

        Container.GetService<HogeService>().Invoke(req);

        // 10行くらいの処理

        return new InfoOutput()
        {
          
        };
    }
}
```

## テストの可読性低下
DIコンテナを利用しているにも関わらず、`Get〇〇`メソッドでオブジェクトを取得している時点で実質的にService Locatorパターンとなっています。
これにより、依存関係が隠蔽され、結果としてテストやコードの理解が非常に困難になっていました。

```c#
public class ATestApplication
{
  protected DIContainer CreateContainer()
  {
      var container = new Mock<DIContainer>()
      container.GetRepository<HogeRepository>().Returns(hoge);
      container.GetRepository<FugaRepostiroy>().Returns(fuga);
      container.GetRepository<BarRepository>().Returns(bar);
      // どのApplicationでも使えるように無限に増えていく
  }
}

public class TestInfoApplication : ATestApplication
{
  [Fact]
  public void TestInfo()
  {
      // Containerのモックを作成
      var container = CreateContainer();
      container.GetService<HogeService>().Returns(hoge);

      var application = new InfoApplication();
      application.Initialize(container);

      var result = await application.Invoke(req);
      Assert.IsType<InfoOutput>(result);
  }

  [Fact]
  public void TestLogin()
  {
      // FugaRepostiroyしかいらない場合でも全てのmockを注入している
      var container = CreateContainer();
      container.GetService<FugaService>().Returns(hoge);

      var application = new LoginApplication();
      application.Initialize(container);

      var result = await application.Invoke(req);
      Assert.IsType<LoginOutput>(result);
  }
}
```

## .NETのDIコンテナを使用していない
.NETではDIコンテナが予め用意されていて、DIパターンについては強力にサポートされています。

https://learn.microsoft.com/ja-jp/dotnet/core/extensions/dependency-injection#service-registration-methods

しかし、本プロジェクトではその機能を活用せず、独自のDIコンテナを使用した結果、依存関係の解決が複雑化してしまいました。
.NETのDIコンテナを活用すれば、**サービスのライフサイクル管理**や**依存関係の自動解決**が簡潔に行え、設計の見通しが良くなります。

```c#
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<DIContainer>();
    }
}
```

長いプロジェクトだったので、.NET5のころの書き方なのは御愛嬌。
この結果、コードは不必要に複雑になり、フレームワークが本来持っている便利な機能を活用できていません。

# 改善策
まだまだ問題はあると思うのですが、とりあえずこのService Locatorを脱却しないと始まらないと思っています。

## Dependency Injection
DIContainerそのものを取っ払ってシンプルに考え直します。
例えば以下のようにコンストラクタから注入するようにするだけでメンバ変数が依存先を表しているので一気に見通しがよくなります。
また、Applicationの外でインスタンス化することで疎結合にすることができます。

### Dependency Injection の改善ポイント
- **依存関係の明示化**: コンストラクタからサービスを注入することで、クラスの依存関係がコード上で明確に見えるようになります。
- **疎結合の実現**: DIパターンを用いることで、サービス同士の結合度が低くなり、クラスが柔軟でテスト可能になります。
- **テストのしやすさ**: 各クラスの依存関係が明確になり、何をモックするか一目瞭然になります。

```c#
public class InfoController : Controller
{
    [HttpPost("/api/info")]
    public ActionResult Index()
    {
        var foo = new FooService();
        var bar = new BarService();
        var hoge = new HogeService();
        var application = new InfoApplication(foo, bar, hoge);
        return application.Invoke(req);
    }
}

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

DIを使うことで、テストでは直接モックを注入できるため、依存関係を手軽にコントロールできます。
これにより、テストコードの可読性が向上し、特定の依存サービスを注入するだけでテスト対象のメソッドの動作を容易に検証できるようになります。

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

## .NETのDIコンテナ
Dependency Injectionを理解して始めて有用になるのがDIコンテナです。
DIパターンで実装していると、外でインスタンス化して注入する部分がめんどくさいという発想が出てきます。

```c#
    public ActionResult Index()
    {
        // ---- 以下の部分
        var foo = new FooService();
        var bar = new BarService();
        var hoge = new HogeService();
        var application = new InfoApplication(foo, bar, hoge);
        // ----
        return application.Invoke(req);
    }
```

.NETでは```IServiceCollection```に依存性を注入することで依存関係と名前解決を自動で行ってくれるようになります。
```c#:Startup.cs(古い書き方)
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<FooService>();
        services.AddScoped<BarService>();
        services.AddScoped<HogeService>();
        services.AddScoped<InfoApplication>();
    }
}

```

```c#:Program.cs(今の書き方)
var services = new ServiceCollection();
services.AddScoped<FooService>();
services.AddScoped<BarService>();
services.AddScoped<HogeService>();
services.AddScoped<InfoApplication>();
```

このように登録することでコンストラクタで初期化している部分をフレームワーク内で名前解決してくれるので以下のように呼び出すことが可能になります。
利用者側(Controller)からはServiceLocatorのように呼び出しているように見えますが、中身がDIなので疎結合が維持されている状態になります。

```c#
public class InfoController : Controller
{
    private readonly InfoApplication _infoApplication;

    public InfoController(InfoApplication infoApplication)
    {
        _infoApplication = infoApplication;
    }

    [HttpPost("/api/info")]
    public ActionResult Index()
    {
        return _infoApplication.Invoke(req);
    }
}
```

テストをする際はいつも通り依存しているオブジェクトをモック化するだけなので可読性が落ちるということもありません。

```c#
[Fact]
public async Task TestInfo()
{
    var mockFooService = new Mock<FooService>();
    var mockBarService = new Mock<BarService>();
    var mockHogeService = new Mock<HogeService>();

    // モックのメソッドの振る舞いをセットアップ
    mockFooService.Setup(f => f.Invoke(It.IsAny<object>()));
    mockBarService.Setup(b => b.Invoke(It.IsAny<object>()));
    mockHogeService.Setup(h => h.Invoke(It.IsAny<object>()));

    // モックしたサービスをInfoApplicationに注入
    var application = new InfoApplication(mockFooService.Object, mockBarService.Object, mockHogeService.Object);

    var req = new object();  // リクエストのダミーオブジェクト

    // Act
    var result = await application.Invoke(req);

    // Assert
    // 各サービスのInvokeメソッドが呼ばれたことを検証
    mockFooService.Verify(f => f.Invoke(It.IsAny<object>()), Times.Once);
    mockBarService.Verify(b => b.Invoke(It.IsAny<object>()), Times.Once);
    mockHogeService.Verify(h => h.Invoke(It.IsAny<object>()), Times.Once);

    // 戻り値の型が正しいことを確認
    Assert.IsType<InfoOutput>(result);
}
```

# まとめ
本記事では、DIコンテナを使用していたつもりがService Locatorのアンチパターンに陥っていた事例を紹介しました。以下が主な教訓です。
- DIコンテナを使うだけでは依存関係注入の利点を活かせない。サービスの取得方法に依存すると、結合度が高まり、保守性が低下を招いた。
- コンストラクタを通じた依存関係の注入により、クラス内の見通しがよくなる。
- .NETの組み込みDIコンテナを活用することで、フレームワークの機能を最大限に利用し、コードの可読性と保守性を向上させることが可能になる。

要は勉強不足。
以上。
