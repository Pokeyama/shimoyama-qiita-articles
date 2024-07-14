using Microsoft.AspNetCore.Mvc;

namespace c_sharp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SampleController(ILogger<SampleController> logger) : ControllerBase
{
    [HttpGet("sync")]
    public int Index1()
    {
        logger.LogInformation("Index1 endpoint was called.");
        Thread.Sleep(100);
        return 1;
    }

    [HttpGet("async")]
    public async Task<int> Index2()
    {
        logger.LogInformation("Index2 endpoint was called.");
        await Task.Delay(100);
        return 1;
    }
}