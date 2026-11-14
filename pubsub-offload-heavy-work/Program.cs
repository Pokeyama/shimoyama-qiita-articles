using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using MySqlConnector;

static string Env(string name, string? def = null) =>
    Environment.GetEnvironmentVariable(name) ?? def ?? throw new InvalidOperationException($"{name} が未設定");
static void Log(string msg) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {msg}");

var project = Env("GOOGLE_CLOUD_PROJECT");
var db = Env("DB", "Server=127.0.0.1;Port=3308;User=root;Password=pass;Database=lab");
var heavyMs = int.Parse(Env("HEAVY_MS", "3000"));

switch (args.FirstOrDefault())
{
    case "api": await RunApi(); break;
    case "worker": await RunWorker(); break;
    default: throw new ArgumentException("usage: dotnet run -- api|worker");
}

async Task RunApi()
{
    var flow = Env("FLOW", "publish-then-commit");
    var delayBeforeCommit = int.Parse(Env("DELAY_BEFORE_COMMIT_MS", "0"));
    var failCommit = Env("FAIL_COMMIT", "0") == "1";
    var crashAfterCommit = Env("CRASH_AFTER_COMMIT", "0") == "1";

    // 1回だけ作る: リクエストごとに作るとストリームの確立が毎回走る
    var publisher = await new PublisherClientBuilder
    {
        TopicName = TopicName.FromProjectTopic(project, Env("TOPIC", "pubsub-lab-orders")),
    }.BuildAsync();

    var builder = WebApplication.CreateBuilder();
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    var app = builder.Build();
    app.Urls.Add("http://127.0.0.1:5080");

    async Task<(MySqlConnection, MySqlTransaction, long)> BeginInsert()
    {
        var conn = new MySqlConnection(db);
        await conn.OpenAsync();
        var tx = await conn.BeginTransactionAsync();
        var cmd = new MySqlCommand("INSERT INTO orders (status) VALUES ('pending')", conn, tx);
        await cmd.ExecuteNonQueryAsync();
        var id = cmd.LastInsertedId;
        Log($"insert order={id}");
        return (conn, tx, id);
    }

    app.MapPost("/orders/sync", async () =>
    {
        var (conn, tx, id) = await BeginInsert();
        await using var _ = conn;
        await Task.Delay(heavyMs);
        await new MySqlCommand(
            "UPDATE orders SET status='done', process_count=process_count+1, done_at=NOW(3) WHERE order_id=@id",
            conn, tx) { Parameters = { new("@id", id) } }.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        Log($"commit order={id}");
        return Results.Json(new { orderId = id });
    });

    app.MapPost("/orders/async", async () =>
    {
        var (conn, tx, id) = await BeginInsert();
        await using var _ = conn;
        string messageId;
        if (flow == "commit-then-publish")
        {
            await tx.CommitAsync();
            Log($"commit order={id}");
            if (crashAfterCommit) { Log("crash after commit"); Environment.Exit(1); }
            messageId = await publisher.PublishAsync(id.ToString());
            Log($"published order={id} msgId={messageId}");
        }
        else
        {
            messageId = await publisher.PublishAsync(id.ToString());
            Log($"published order={id} msgId={messageId}");
            await Task.Delay(delayBeforeCommit);
            if (failCommit)
            {
                await tx.RollbackAsync();
                Log($"rollback order={id}");
                return Results.StatusCode(500);
            }
            await tx.CommitAsync();
            Log($"commit order={id}");
        }
        return Results.Json(new { orderId = id, messageId });
    });

    await app.RunAsync();
    await publisher.ShutdownAsync(TimeSpan.FromSeconds(5));
}

async Task RunWorker()
{
    var mode = Env("WORKER_MODE", "normal");
    var concurrency = long.Parse(Env("WORKER_CONCURRENCY", "1"));
    var crashBeforeAck = Env("CRASH_BEFORE_ACK", "0") == "1";
    var recheckMs = int.Parse(Env("RECHECK_MS", "0"));
    var ackIfMissing = Env("ACK_IF_MISSING", "0") == "1";
    var ackDeadlineSec = Environment.GetEnvironmentVariable("ACK_DEADLINE_SEC");
    var ackDeadline = ackDeadlineSec is null ? (TimeSpan?)null : TimeSpan.FromSeconds(int.Parse(ackDeadlineSec));

    var d = SubscriberClient.DefaultFlowControlSettings;
    Log($"defaults ackDeadline={SubscriberClient.DefaultAckDeadline} ackExtensionWindow={SubscriberClient.DefaultAckExtensionWindow} " +
        $"maxTotalAckExtension={SubscriberClient.DefaultMaxTotalAckExtension} minAckDeadline={SubscriberClient.MinimumAckDeadline} " +
        $"maxAckDeadline={SubscriberClient.MaximumAckDeadline} flowElements={d.MaxOutstandingElementCount} flowBytes={d.MaxOutstandingByteCount}");
    Log($"config mode={mode} clientCount=1 maxOutstandingElementCount={concurrency} heavyMs={heavyMs} ackDeadline={ackDeadline?.ToString() ?? "default"} recheckMs={recheckMs} ackIfMissing={ackIfMissing}");

    var subscriber = await new SubscriberClientBuilder
    {
        SubscriptionName = SubscriptionName.FromProjectSubscription(project, Env("SUBSCRIPTION", "pubsub-lab-orders-worker")),
        ClientCount = 1,
        Settings = new SubscriberClient.Settings { FlowControlSettings = new FlowControlSettings(concurrency, null), AckDeadline = ackDeadline },
    }.BuildAsync();

    async Task<long> Update(MySqlConnection conn, long id, bool onlyPending) =>
        await new MySqlCommand(
            "UPDATE orders SET status='done', process_count=process_count+1, done_at=NOW(3) WHERE order_id=@id"
            + (onlyPending ? " AND status='pending'" : ""),
            conn) { Parameters = { new("@id", id) } }.ExecuteNonQueryAsync();

    SubscriberClient.Reply Ack(string why) { Log($"ack ({why})"); return SubscriberClient.Reply.Ack; }

    var run = subscriber.StartAsync(async (msg, ct) =>
    {
        var order = msg.Data.ToStringUtf8();
        Log($"recv msgId={msg.MessageId} order={order} publishTime={msg.PublishTime.ToDateTime().ToLocalTime():HH:mm:ss.fff}");
        var id = long.Parse(order);
        switch (mode)
        {
            case "nack":
                Log("nack (mode=nack)");
                return SubscriberClient.Reply.Nack;
            case "hang":
                await Task.Delay(Timeout.Infinite, ct);
                return SubscriberClient.Reply.Nack;
            case "naive":
            {
                await using var conn = new MySqlConnection(db);
                await conn.OpenAsync(ct);
                await Task.Delay(heavyMs, ct);
                var n = await Update(conn, id, false);
                Log($"update order={id} affected={n}");
                if (crashBeforeAck) { Log("crash before ack"); Environment.Exit(1); }
                return Ack("naive");
            }
            default:
            {
                await using var conn = new MySqlConnection(db);
                await conn.OpenAsync(ct);
                async Task<string?> Select() => await new MySqlCommand("SELECT status FROM orders WHERE order_id=@id", conn)
                    { Parameters = { new("@id", id) } }.ExecuteScalarAsync(ct) as string;
                var status = await Select();
                if (status is null && recheckMs > 0)
                {
                    await Task.Delay(recheckMs, ct);
                    status = await Select();
                    Log($"recheck order={id} found={status is not null}");
                }
                // コミットより先に届いた場合。ここでackすると取りこぼす
                if (status is null && ackIfMissing) return Ack("row not found");
                if (status is null) { Log("nack (row not found)"); return SubscriberClient.Reply.Nack; }
                if (status == "done") return Ack("already done");
                await Task.Delay(heavyMs, ct);
                var n = await Update(conn, id, true);
                Log($"update order={id} affected={n}");
                if (crashBeforeAck) { Log("crash before ack"); Environment.Exit(1); }
                return Ack("processed");
            }
        }
    });

    var stop = new TaskCompletionSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
    using var term = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM, _ => stop.TrySetResult());
    await Task.WhenAny(stop.Task, run);
    Log("stopping");
    try { await subscriber.StopAsync(new SubscriberClient.ShutdownOptions { Timeout = TimeSpan.FromSeconds(5) }, CancellationToken.None); } catch (OperationCanceledException) { }
    await run;
}
