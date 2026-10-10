using DFrame;

var builder = DFrameApp.CreateBuilder(7312, 7313); // WebUI:7312, Worker接続:7313
builder.ConfigureWorker(options =>
{
    options.VirtualProcess = 10; // 1プロセスを10台のWorkerに見せる → Worker Limitを1→10まで上げられる
});
await builder.RunAsync();

public class SampleWorkload : Workload
{
    public override async Task ExecuteAsync(WorkloadContext context)
    {
        // 一瞬で終わると各段のlatencyが全部ほぼ0msになるので、
        // 段ごとに負荷が乗る様子が見えるよう軽く待つ(スクショ用)。
        await Task.Delay(5, context.CancellationToken);
    }
}
