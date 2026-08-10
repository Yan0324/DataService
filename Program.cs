using DataService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

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

    //========== 7. 程序启动时执行初始化 ==========
    var host = builder.Build();
    using (var scope = host.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(); // 异步初始化
    }
    // ========== 8. 运行主程序 ==========
    await host.RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "程序异常中止");
}
finally
{
    Log.CloseAndFlush();
}

