using DataService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using DataService.Models;
using DataService.Services;
using DataService.Services.IServices;

// ========== 1. 加载配置 ==========
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

//========== 2. 配置日志 ==========
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("程序启动中");


    //========== 3. 构建Host ==========
    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddConfiguration(configuration);
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    //========== 4. 注册DbContext ==========
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

    //========== 5. 注册Seeder ==========
    builder.Services.AddScoped<DataSeeder>();

    //========== 6. 注册其他服务 ==========
    builder.Services.Configure<CollectorSettings>(builder.Configuration.GetSection("CollectorSettings"));
    builder.Services.AddScoped<ICollectorService, CollectorService>();
    builder.Services.AddScoped<IDataStorageService, DataStorageService>();


    //========== 7. 程序启动时执行初始化 ==========
    var host = builder.Build();
    using (var scope = host.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var collector = scope.ServiceProvider.GetRequiredService<ICollectorService>();

        var devices = await db.Devices
            .OrderBy(d => d.DeviceCode)
            .Take(3)
            .ToListAsync();

        var tasks = devices.Select(device => collector.CollectDeviceAsync(device));
        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < devices.Count; i++)
        {
            Log.Information(
                "设备 {DeviceCode} 采集完成，共生成 {Count} 条数据。",
                devices[i].DeviceCode,
                results[i].Count);
        }

        var totalCount = results.Sum(list => list.Count);
        Log.Information("总共采集完成 {TotalCount} 条数据。", totalCount);

        var storage = scope.ServiceProvider.GetRequiredService<IDataStorageService>();
        var allDataPoints = results.SelectMany(result => result).ToList();

        await storage.SaveBatchAsync(allDataPoints);

        Log.Information(
            "所有采集数据已保存到数据库，共 {Count} 条。",
            allDataPoints.Count);
    }
    // ========== 8. 运行主程序 ==========
    //await host.RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "程序异常中止");
}
finally
{
    Log.CloseAndFlush();
}

