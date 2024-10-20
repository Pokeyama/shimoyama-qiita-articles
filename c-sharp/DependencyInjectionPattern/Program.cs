using Microsoft.Extensions.DependencyInjection;
using System;

namespace DependencyInjectionPattern
{
    // サービスインターフェース
    public interface IService
    {
        void Serve();
    }

    // サービス実装
    public class ServiceA : IService
    {
        

        public void Serve()
        {
            Console.WriteLine("ServiceA is serving.");
        }
    }

    // クライアントクラス (依存性注入を使用)
    public class Client
    {
        private readonly IService _service;

        // コンストラクタで依存関係を受け取る
        public Client(IService service)
        {
            _service = service;
        }

        public void Start()
        {
            _service.Serve();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // DIコンテナのセットアップ
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<IService, ServiceA>(); // サービスの登録
            serviceCollection.AddTransient<Client>(); // クライアントの登録

            // サービスプロバイダーを作成
            var serviceProvider = serviceCollection.BuildServiceProvider();

            // DIでクライアントを解決して使用
            var client = serviceProvider.GetRequiredService<Client>();
            client.Start();
        }
    }
}
