using DILifeCycle;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// 依存性注入コンテナへのサービス登録
// AddTransient、AddScoped、AddSingleton を切り替えて試す
// builder.Services.AddTransient<IService, ServiceA>();
// builder.Services.AddScoped<IService, ServiceA>();
builder.Services.AddSingleton<IService, ServiceA>();

var app = builder.Build();

// ルーティングの設定
app.MapGet("/serve", (IService service1, [FromServices] IService service2) =>
{
    var id1 = service1.Serve();
    var id2 = service2.Serve();
    return Results.Ok(new { Message = "Service Called", id1, id2 });
});

// アプリケーションの実行
app.Run();
