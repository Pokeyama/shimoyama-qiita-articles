using Microsoft.Extensions.DependencyInjection;
using System;

namespace DependencyInjectionPattern
{
    // サービスインターフェース
    public interface IService
    {
        string Serve();
    }

    // サービス実装
    public class ServiceA : IService
    {
        public string Serve()
        {
            return "Call to non Mock.";
        }
    }

    // クライアントクラス (依存性注入を使用)
    // public class Client
    // {
    //     private readonly IService _service;

    //     // コンストラクタで依存関係を受け取る
    //     public Client(IService service)
    //     {
    //         _service = service;
    //     }

    //     public void Start()
    //     {
    //         _service.Serve();
    //     }
    // }

    public class UseCase
    {
        private readonly IService _serviceA;

        public UseCase(IService service)
        {
            _serviceA = service;
        }

        public string Invoke()
        {
            return _serviceA.Serve();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var serviceA = new ServiceA();
            var useCase = new UseCase(serviceA);
            var result = useCase.Invoke();

            Console.WriteLine(result);
            // DIコンテナで書く場合
            // // DIコンテナのセットアップ
            // var serviceCollection = new ServiceCollection();
            // serviceCollection.AddTransient<IService, ServiceA>(); // サービスの登録
            // serviceCollection.AddTransient<UseCase>(); // クライアントの登録

            // // サービスプロバイダーを作成
            // var serviceProvider = serviceCollection.BuildServiceProvider();

            // // DIでクライアントを解決して使用
            // var client = serviceProvider.GetRequiredService<UseCase>();
            // client.Invoke();
        }
    }
}
