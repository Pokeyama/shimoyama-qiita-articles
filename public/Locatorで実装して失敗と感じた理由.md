---
title: 【DI】Service LocatorパターンとDIパターンを理解せず実装して失敗と感じた理由
tags:
  - ''
private: false
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

# 問題点
## テスト大変すぎ


## 途中参画厳しすぎ

## 