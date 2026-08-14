# DataService 项目分析与 C# 学习指南

> 项目类型：`.NET 8` 控制台应用（通用主机 Generic Host），基于 EF Core + MySQL 的数据服务
> 写作日期：2026-08-10
> 目的：梳理"这个项目现在实现了什么、怎么实现的、背后是什么 C# 知识点"，供边做项目边学 C#。

---

## 目录

1. [项目定位](#1-项目定位)
2. [项目结构总览](#2-项目结构总览)
3. [已实现的功能与实现方式](#3-已实现的功能与实现方式)
4. [程序的一次完整运行流程](#4-程序的一次完整运行流程)
5. [预留的模块与设计意图](#5-预留的模块与设计意图)
6. [C# 知识点地图](#6-c-知识点地图)
7. [下一步练习建议](#7-下一步练习建议)
8. [常见问题 Q&amp;A](#8-常见问题-qa)

---

## 1. 项目定位

这是一个**物联网/遥测数据服务**的雏形。设计目标（从代码和配置可以推断出来）：

- 有若干台"设备"（`Device`）在持续产生数据点（`DataPoint`）。
- 程序把设备产生的数据**采集 → 缓冲 → 批量写入数据库**。
- 数据库用 MySQL。

目前**已经实现**的部分是数据层的底座：配置、日志、数据库连接、建表、种子数据。
**还没有实现**的是采集和写入流水线（见第 5 节）。

---

## 2. 项目结构总览

```
DataService/
├── DataService.sln              # 解决方案文件（可以包含多个项目）
├── DataService.csproj           # 项目文件：目标框架、NuGet 包引用
├── appsettings.json             # 配置文件：连接字符串、采集参数、日志
├── Program.cs                   # 程序入口（Top-level statements）
├── Data/
│   ├── AppDbContext.cs          # EF Core 数据库上下文（核心）
│   ├── DataSeeder.cs            # 种子数据初始化
│   └── Configurations.cs        # ⚠️ 空壳：预留实体配置类
├── Models/
│   ├── Device.cs                # 设备实体
│   ├── DataPoint.cs             # 数据点实体
│   └── Enums.cs                 # ⚠️ 空壳：预留枚举
├── Services/
│   ├── CollctorService.cs       # ⚠️ 空壳：预留采集服务（注意拼写少了 e）
│   ├── DataStorageService.cs    # ⚠️ 空壳：预留存储服务
│   └── IServices/
│       ├── ICollectorService.cs     # ⚠️ 空壳：预留接口（现在误写成了 class）
│       └── IDataStorageService.cs   # ⚠️ 空壳：预留接口
└── Infrastructure/
    ├── AsynBulkInserter.cs      # ⚠️ 空壳：预留批量插入器（拼写 Asyn→Async）
    └── ChannelBoundedBuffer.cs  # ⚠️ 空壳：预留有界缓冲（生产者-消费者）
```

> 📌 **第 5 节**会详细解释那些空壳文件代表什么、应该怎么填。

---

## 3. 已实现的功能与实现方式

### 功能 1：加载配置文件

**代码位置**：`Program.cs:10-13`

```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();
```

**做了什么**：从当前目录读取 `appsettings.json`，解析成 `IConfiguration` 对象。

**知识点**：

| 知识点                                       | 说明                                                                                                                  |
| -------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `ConfigurationBuilder`                     | 配置系统的"建造器"（Builder 模式），可以链式叠加多个配置源                                                            |
| `AddJsonFile`                              | 添加 JSON 配置源。`optional: false` = 文件必须存在，缺了就报错；`reloadOnChange: true` = 文件被修改时自动重新加载 |
| `SetBasePath`                              | 指定配置文件所在目录                                                                                                  |
| `Build()`                                  | 把多个配置源合并成一个只读的`IConfiguration`                                                                        |
| `GetConnectionString("DefaultConnection")` | 读取`ConnectionStrings` 节点下指定名字的字符串                                                                      |

> 后面连接数据库、配日志都要从这里取值。**配置和代码分离**是工程化的第一课。

---

### 功能 2：Serilog 结构化日志

**代码位置**：`Program.cs:16-18`、`Program.cs:28-29`、`appsettings.json` 的 `Serilog` 节点

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)   // 从配置文件读取日志设置
    .CreateLogger();
```

```csharp
builder.Logging.ClearProviders();            // 清掉默认日志提供器
builder.Logging.AddSerilog();                // 换成 Serilog
```

**知识点**：

| 知识点                     | 说明                                                                                                                                                                 |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 结构化日志                 | 日志不再是一段拼好的字符串，而是"模板 + 参数"，方便检索和过滤。对比：`Log.Information("设备 {Code} 已创建", code)` 优于 `Log.Information($"设备 {code} 已创建")` |
| `LoggerConfiguration`    | Serilog 的配置入口，链式调用                                                                                                                                         |
| `ReadFrom.Configuration` | 读取`appsettings.json` 里的 `Serilog` 节点                                                                                                                       |
| **Sink（输出目标）** | 日志写到哪：Console / File / 数据库等。本项目只配了`Console`                                                                                                       |
| `MinimumLevel`           | 日志级别门槛：`Verbose < Debug < Information < Warning < Error < Fatal`，低于门槛的不输出                                                                          |
| `Log` 静态类             | 全局日志入口：`Log.Information / Log.Warning / Log.Error / Log.Fatal`                                                                                              |
| `Log.CloseAndFlush()`    | 程序退出前把缓冲的日志写完再关（否则最后几条可能丢）                                                                                                                 |

> ⚠️ 之前程序"静默退出"的根因之一，就是 appsettings.json 里没有 `Serilog` 节点、没配任何 Sink，日志全被丢掉了。现在加了 `Console` Sink，错误才能看到。

---

### 功能 3：通用主机（Generic Host）与依赖注入（DI）

**代码位置**：`Program.cs:26-42`

```csharp
var builder = Host.CreateApplicationBuilder(args);   // 创建主机建造器
builder.Configuration.AddConfiguration(configuration);
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var connectionString = configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<DataSeeder>();

var host = builder.Build();                          // 构建主机
```

**知识点**：

| 知识点                                          | 说明                                                                                                             |
| ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `Host.CreateApplicationBuilder`               | 一套开箱即用的"应用骨架"：自带 DI 容器、配置系统、日志系统、生命周期管理                                         |
| `builder.Services`（IServiceCollection）      | **依赖注入容器**：把所有服务注册到容器里，需要时自动创建并注入                                             |
| `AddDbContext<AppDbContext>`                  | 注册 EF Core 上下文，默认**作用域（Scoped）**生命周期                                                            |
| `AddScoped<T>`                                | 注册生命周期为 Scoped：每次请求/作用域内是同一个实例                                                             |
| 三种生命周期                                    | **Singleton**（整个程序一个实例）、**Scoped**（每次请求一个）、**Transient**（每次获取都新建） |
| `builder.Build()`                             | 把注册好的服务编译成可用的`IHost`                                                                              |
| `CreateScope()` + `GetRequiredService<T>()` | 手动从容器里"取"服务；EF Core 的`DbContext` 是 Scoped，必须在一个作用域里用                                    |

> 💡 **为什么要用 DI？** 好处：对象统一由容器创建和销毁、代码解耦（调用方不关心被调用方的依赖从哪来）、方便测试时替换成假实现。这是 .NET 生态最重要的架构思想之一。

---

### 功能 4：EF Core + MySQL 数据访问

**代码位置**：`Data/AppDbContext.cs`、`DataService.csproj`、`Program.cs:33-34`

```csharp
// AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<Device> Devices { get; set; }
    public DbSet<DataPoint> DataPoints { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
```

```csharp
// Program.cs
options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
```

**知识点**：

| 知识点                            | 说明                                                                                                                                          |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| ORM（对象关系映射）               | 用 C# 对象操作数据库表，不用手写 SQL。EF Core 是微软官方 ORM                                                                                  |
| `DbContext`                     | 数据库会话的抽象：负责连接、跟踪实体、生成 SQL                                                                                                |
| `DbSet<T>`                      | 对应一张表；`db.Devices` 就是"设备表"                                                                                                       |
| `DbContextOptions`              | 告诉 DbContext 用哪个数据库、怎么连                                                                                                           |
| Pomelo 驱动                       | `Pomelo.EntityFrameworkCore.MySql` 是 EF Core 8 的主流 MySQL 驱动（微软官方只支持 SQL Server，MySQL 用第三方的 Pomelo）                     |
| `UseMySql(conn, ServerVersion)` | 指定 MySQL 提供程序。`ServerVersion.AutoDetect` 启动时会连一次服务器探测版本（也可以写死，如 `new MySqlServerVersion(new Version(8,0))`） |
| 连接字符串                        | `Server=localhost;Port=3306;Database=devicedata;User=root;Password=123456;`                                                                 |

> 📌 数据库 provider 和连接字符串格式**必须匹配**。之前用 SQL Server 的 `UseSqlServer` + SQLite 格式的字符串，等于让 SQL Server 去连一台叫 `devicedata.db` 的服务器，必然失败。

---

### 功能 5：实体建模 + Fluent API 配置

**代码位置**：`Models/Device.cs`、`Models/DataPoint.cs`、`AppDbContext.cs` 的 `OnModelCreating`

```csharp
// Device.cs（简化）
public class Device
{
    public int Id { get; set; }                       // 主键（按约定）
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceType { get; set; }           // ? = 可空
}
```

```csharp
// AppDbContext.cs OnModelCreating（Fluent API 示例）
modelBuilder.Entity<Device>(entity =>
{
    entity.HasKey(e => e.Id);                        // 主键
    entity.HasIndex(e => e.DeviceCode).IsUnique();   // 唯一索引
    entity.Property(e => e.DeviceCode).HasMaxLength(50).IsRequired();
});

modelBuilder.Entity<DataPoint>(entity =>
{
    entity.HasIndex(e => e.Timestamp);
    entity.HasIndex(e => new { e.DeviceId, e.Timestamp });  // 复合索引
    entity.Property(e => e.Value).HasPrecision(18, 4);      // decimal(18,4)
    entity.HasOne<Device>().WithMany().HasForeignKey(e => e.DeviceId)
          .OnDelete(DeleteBehavior.Cascade);         // 外键 + 级联删除
});
```

**知识点**：

| 知识点                          | 说明                                                                                          |
| ------------------------------- | --------------------------------------------------------------------------------------------- |
| POCO 实体类                     | 纯 C# 类代表表结构，字段就是列                                                                |
| 自动属性`{ get; set; }`       | C# 最常用的属性写法；`= string.Empty` 是初始化默认值，避免 null                             |
| 可空引用类型`string?`         | 声明这个字段"可以是 null"。项目开启`<Nullable>enable</Nullable>` 后，编译器会帮你检查空引用 |
| 约定优于配置                    | EF Core 有默认规则：`Id` 属性默认是主键、`DeviceId` 结尾默认是外键等                      |
| **Fluent API**            | 在`OnModelCreating` 里用链式方法描述约束，比在实体上写特性（Attribute）更集中、更灵活       |
| 主键/唯一索引/普通索引/复合索引 | 数据库调优基础：唯一索引保证不重复；复合索引`(DeviceId, Timestamp)` 加速"按设备+时间查询"   |
| `HasPrecision(18, 4)`         | 小数总共 18 位、小数位 4 位（`decimal` 才支持，`float`/`double` 不行）                  |
| 外键 +`OnDelete(Cascade)`     | 删除 Device 时，关联的 DataPoint 自动一起删                                                   |
| Lambda`e => e.Id`             | 表达式树，EF Core 用它把 C# 表达式翻译成 SQL                                                  |

> 💡 还有第二种配置方式：**数据注解（Data Annotation）**，直接在属性上写 `[Key]`、`[MaxLength(50)]`。两种都常见，Fluent API 更受大型项目欢迎。

---

### 功能 6：数据库自动初始化

**代码位置**：`DataSeeder.cs:30`

```csharp
await _db.Database.EnsureCreatedAsync(ct);
```

**知识点**：

| 知识点                           | 说明                                                                                                                                     |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `EnsureCreatedAsync`           | **如果数据库不存在就创建**（包括表），已存在就跳过。适合学习/原型阶段                                                              |
| `EnsureCreated` vs `Migrate` | `Migrate()` 基于**迁移文件**逐步升级，生产环境用它；`EnsureCreated` 不会更新已存在的库的表结构，模型改了它也不管，不能用于生产 |
| 幂等                             | 重复执行结果一样（不会重复建表、重复删数据），这是好习惯                                                                                 |

> 以后模型经常变动时，建议升级到 EF Core **Migrations**（`dotnet ef migrations add xxx`）。文档后面有引导。

---

### 功能 7：种子数据（Seed）

**代码位置**：`DataSeeder.cs`

```csharp
// 1. 建库
await _db.Database.EnsureCreatedAsync(ct);

// 2. 已有数据就跳过（幂等）
if (await _db.Devices.AnyAsync(ct))
{
    _logger.LogInformation("数据库中已有数据，跳过种子数据插入。");
    return;
}

// 3. 插入 3 台设备
var devices = new List<Device> { ... };
await _db.Devices.AddRangeAsync(devices, ct);
await _db.SaveChangesAsync(ct);
```

**知识点**：

| 知识点                | 说明                                                                                                               |
| --------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `AnyAsync`          | 判断集合是否非空（EF Core 翻译成`EXISTS`，效率高）                                                               |
| `AddRangeAsync`     | 批量加入多个实体到跟踪状态                                                                                         |
| `SaveChangesAsync`  | 把跟踪的改动真正写进数据库（生成 INSERT 语句），返回影响行数                                                       |
| `CancellationToken` | 异步操作的"取消令牌"，用户 Ctrl+C 或程序关闭时能优雅地中止                                                         |
| 构造器注入            | `DataSeeder` 的依赖（`AppDbContext`、`ILogger<DataSeeder>`）由 DI 容器自动传入——这就是"依赖注入"的字面体现 |

> 💡 **EF Core 的"跟踪"**：`Add`/`AddRange` 只是把对象放进内存跟踪器，**只有调用 `SaveChangesAsync` 才发 SQL**。理解这一点是理解 EF Core 的关键。

---

### 功能 8：全程异步编程

**代码位置**：几乎所有方法

```csharp
await seeder.SeedAsync();          // Program.cs:46
await _db.Database.EnsureCreatedAsync(ct);
await _db.Devices.AnyAsync(ct);
await _db.SaveChangesAsync(ct);
```

**知识点**：

| 知识点                          | 说明                                                                                                       |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `async` / `await`           | 异步方法语法。`async` 标记方法，`await` 等待一个异步操作                                               |
| `Task` / `Task<T>`          | 表示"将来会完成的操作"。`Task` = 无返回值，`Task<T>` = 有返回值                                        |
| 为什么用异步                    | 数据库/网络 IO 很慢，异步让线程去干别的，而不是傻等；用`async` 避免阻塞线程（尤其 Web 服务器线程极宝贵） |
| 命名规范                        | 异步方法名以`Async` 结尾（`SeedAsync`、`SaveChangesAsync`）                                          |
| `await` 不阻塞                | `await` 期间当前线程被释放，操作完成后自动回来继续执行                                                   |
| 不要到处`.Result`/`.Wait()` | 同步等待异步会死锁，用`await`                                                                            |

---

### 功能 9：异常兜底处理

**代码位置**：`Program.cs:20-59`

```csharp
try
{
    // ...整个启动流程
}
catch (Exception ex)
{
    Log.Fatal(ex, "程序异常中止");   // 记录致命错误
}
finally
{
    Log.CloseAndFlush();              // 无论如何都要冲洗日志
}
```

**知识点**：

| 知识点                            | 说明                                                               |
| --------------------------------- | ------------------------------------------------------------------ |
| `try` / `catch` / `finally` | 异常处理三件套：尝试 → 捕获 → 收尾（finally 无论成不成功都执行） |
| `Exception`                     | .NET 所有异常基类，`ex` 里带堆栈和 Message                       |
| 异常会向上冒泡                    | 内层没 catch 就一层层往外抛，直到被捕获或程序崩溃                  |
| `Log.Fatal`                     | 记录致命错误，输出堆栈信息                                         |
| `CloseAndFlush` 放 finally      | 保证日志一定被写出来                                               |

> ⚠️ 这里有个**需要注意的坑**：catch 吞掉异常后程序退出码还是 0（外部看起来"正常运行结束了"）。之前那个"静默退出"就有这个因素。生产环境通常要么 `Environment.ExitCode = 1`，要么直接让异常抛出去让进程以非零码退出，便于外部监控发现。

---

### 功能 10：Top-level statements（项目骨架）

**代码位置**：`Program.cs` 第一行就是语句，没有看到 `class Program { static void Main() }`

**知识点**：

| 知识点               | 说明                                                                                                                                                                         |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Top-level statements | C# 9 起的语法糖：编译器自动生成`Main` 方法，你只管写逻辑                                                                                                                   |
| `using` 指令       | 引入命名空间，代码里才能直接用里面的类型（`Microsoft.EntityFrameworkCore` 提供 `UseMySql`）                                                                              |
| `ImplicitUsings`   | csproj 里`<ImplicitUsings>enable</ImplicitUsings>` 会自动 using 常用命名空间（`System`、`System.Collections.Generic`、`System.Linq`、`System.Threading.Tasks` 等） |
| `Nullable`         | `<Nullable>enable</Nullable>` 开启可空引用类型检查，减少空引用 bug                                                                                                         |
| `var`              | 类型推断：`var x = 1;` 等价于 `int x = 1;`                                                                                                                               |
| 字符串插值           | `$"...{变量}..."`                                                                                                                                                          |

---

## 4. 程序的一次完整运行流程

```
1. 读取 appsettings.json                         (Program.cs:10)
2. 配置 Serilog 日志                              (Program.cs:16)
3. 创建通用主机，注册 DbContext、DataSeeder        (Program.cs:26-42)
4. 构建主机                                       (Program.cs:42)
5. 开一个 DI 作用域，取 DataSeeder                 (Program.cs:43-46)
6. Seeder：EnsureCreated 建库建表                  (DataSeeder.cs:30)
7. Seeder：没有数据 → 插入 3 台设备                 (DataSeeder.cs:34-50)
8. 运行主机，等待停止信号（Ctrl+C）                (Program.cs:49)
```

> 观察到的实际输出：
>
> ```
> [17:57:12 INF] 程序启动中
> [17:57:14 INF] 正在检查数据库...
> [17:57:16 INF] 数据库已就绪。
> [17:57:17 INF] 种子数据已插入。
> ```
>
> 第二次运行会看到 `数据库中已有数据，跳过种子数据插入。`（幂等生效）。

---

## 5. 预留的模块与设计意图

这些空壳文件揭示了项目**想做什么**。`appsettings.json` 里的 `CollectorSettings` 是关键线索：

```json
"CollectorSettings": {
  "DeviceCount": 3,          // 模拟 3 台设备
  "IntervalSeconds": 1,      // 每 1 秒采一次
  "DurationSeconds": 10,     // 持续 10 秒
  "BatchSize": 5,            // 每批写 5 条
  "FlushIntervalSeconds": 2  // 每 2 秒刷一次库
}
```

设计的是一条**采集 → 缓冲 → 批量落库**的流水线：

```
设备(模拟) ──采集──> 有界缓冲(Channel) ──消费──> 批量插入器 ──> MySQL
   CollctorService     ChannelBoundedBuffer        AsynBulkInserter
                                                      DataStorageService
```

| 空壳文件                                                        | 设计意图                                                               | 该用什么知识点补                                                                                 |
| --------------------------------------------------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `Services/CollctorService.cs`                                 | 模拟设备产生数据点（按`IntervalSeconds` 定时）                       | `BackgroundService` / `IHostedService` 常驻后台任务；`IServiceCollection.AddHostedService` |
| `Services/IServices/ICollectorService.cs`                     | 采集服务的**接口**（现在误写成了 `class`，应为 `interface`） | 接口、依赖倒置                                                                                   |
| `Infrastructure/ChannelBoundedBuffer.cs`                      | 有界缓冲，解耦生产者和消费者、防内存溢出                               | `System.Threading.Channels.Channel<T>`，`Channel.CreateBounded<T>()`                         |
| `Infrastructure/AsynBulkInserter.cs`                          | 批量把数据点写进数据库（每批 5 条、每 2 秒刷一次）                     | 事务、`SaveChanges` 批处理、缓冲区攒够一批再写                                                 |
| `Services/DataStorageService.cs` + `IDataStorageService.cs` | 存储服务，包装批量写入                                                 | 接口、依赖注入                                                                                   |
| `Models/Enums.cs`                                             | 放枚举，比如设备类型`DeviceType`                                     | `enum`                                                                                         |
| `Data/Configurations.cs`                                      | 放`IEntityTypeConfiguration<T>`，把 Fluent API 拆到独立文件          | EF Core`IEntityTypeConfiguration`                                                              |

> ⚠️ 顺便指出几个**待修正的命名问题**（也是学习点）：
>
> - `CollctorService` → 应为 `CollectorService`（拼写）
> - `AsynBulkInserter` → 应为 `AsyncBulkInserter`（拼写）
> - `ICollectorService` 和 `IDataStorageService` 目前是 `class` 且一个是空类，一个与所在文件夹 `IServices` 的命名空间不一致（`ICollectorService.cs` 声明在 `DataService.Services`，而 `IDataStorageService.cs` 在 `DataService.Services.IServices`）——接口要 `interface` 关键字，并且一个文件一个命名空间约定要统一。

---

## 6. C# 知识点地图

按"从入门到进阶"排序，标注本项目用到了哪一级：

### 第 1 级：语言基础（本项目已用到）

- 变量、类型（`int`、`string`、`decimal`、`bool`）
- 可空类型 `string?`
- 自动属性、字段初始化 `= string.Empty`
- `var` 类型推断
- 字符串插值 `$"..."`、`@"..."` 原义字符串
- `List<T>` 泛型集合
- Lambda 表达式 `e => e.Id`
- `foreach`、`if`、`try/catch/finally`

### 第 2 级：面向对象（本项目已用到一部分）

- 类、`class`、封装（`public`/`internal`/`private`）
- 构造器（`AppDbContext` 的构造器注入）
- 命名空间 `namespace` 与 `using`
- 继承（`AppDbContext : DbContext`）
- 接口 `interface`（**本项目预留，还没写**，待补）

### 第 3 级：异步编程（本项目已用到）

- `async` / `await`、`Task`
- `CancellationToken`
- 异步方法命名规范

### 第 4 级：.NET 生态（本项目核心）

- **配置系统**：`ConfigurationBuilder`、`AddJsonFile`、`IConfiguration`
- **依赖注入**：`IServiceCollection`、`AddScoped/AddSingleton/AddTransient`、`CreateScope`
- **通用主机**：`Host.CreateApplicationBuilder`、`Build`、`RunAsync`
- **日志**：Serilog、结构化日志、Sink、日志级别
- **EF Core**：`DbContext`、`DbSet`、Fluent API、`EnsureCreated`、`SaveChanges`、外键/索引
- **Top-level statements**、`ImplicitUsings`、`Nullable`

### 第 5 级：进阶（本项目预留，下一步学）

- 常驻后台任务：`BackgroundService` / `IHostedService`
- 生产者-消费者：`System.Threading.Channels`
- 事务与批量写入、EF Core Migrations
- Options 模式：把 `CollectorSettings` 绑定成强类型类
- 接口编程与依赖倒置（`ICollectorService`）

---

## 7. 下一步练习建议

按顺序做，每步都是一个小型练习：

**练习 1：实现后台采集服务（重点学 BackgroundService）**

- 把 `CollctorService` 改造成 `BackgroundService` 子类，继承 `ExecuteAsync`
- 在 `Program.cs` 里 `builder.Services.AddHostedService<...>()` 注册
- 按 `IntervalSeconds` 每 1 秒生成一条模拟数据点（循环 + `Task.Delay`）

**练习 2：给采集服务补接口**

- 把 `ICollectorService.cs` 从 `class` 改成 `interface`，定义 `StartAsync`/`StopAsync` 或采集方法
- 让 `CollctorService` 实现它，体会"面向接口编程"

**练习 3：用 Channel 连接生产和消费（重点学 Channels）**

- 在 `ChannelBoundedBuffer.cs` 里用 `Channel.CreateBounded<DataPoint>(capacity)`
- 生产者 `channel.Writer.WriteAsync`，消费者 `channel.Reader.ReadAllAsync` + `await foreach`

**练习 4：实现批量写入（重点学事务与批处理）**

- 在 `AsynBulkInserter.cs` 里攒够 `BatchSize` 条或每 `FlushIntervalSeconds` 秒执行一次 `SaveChangesAsync`
- 学习 `DbContext.AddRange` 的自动批处理

**练习 5：把配置绑定成强类型**

- 用 Options 模式：`builder.Services.Configure<CollectorSettings>(configuration.GetSection("CollectorSettings"))`
- 注入 `IOptions<CollectorSettings>` 读参数，告别 `GetConnectionString` 式的裸取值

**练习 6：上 Migrations**

- 安装 `dotnet-ef` 工具，`dotnet ef migrations add Init`、`dotnet ef database update`
- 把 `EnsureCreatedAsync` 换成 `db.Database.MigrateAsync()`，体会模型变更管理

**练习 7：修命名 & 补全枚举**

- 改 `CollctorService` → `CollectorService`、`AsynBulkInserter` → `AsyncBulkInserter`
- 在 `Enums.cs` 里定义 `enum DeviceType { Sensor, Meter, ... }`

---

## 8. 常见问题 Q&A

**Q1：为什么程序运行后一直不退？**
因为最后执行了 `host.RunAsync()`——通用主机会一直运行、等待停止信号（Ctrl+C）。这是"常驻服务"的正常行为。之前"秒退"是数据库连不上的 bug。

**Q2：为什么之前连接失败但没有任何报错？**
两个原因叠加：① 连接字符串和数据库 Provider 不匹配，连的是一台不存在的服务器，15 秒超时抛异常；② Serilog 没配 Console Sink，异常日志被吞了。两处都已修复。

**Q3：`EnsureCreatedAsync` 和 `MigrateAsync` 有什么区别？**
`EnsureCreated` 只建库建表、不记录变更历史、模型改了不更新已有表——适合原型；`Migrate` 用迁移文件逐步升级、能记录版本——适合生产。学习阶段先用 `EnsureCreated`，练习 6 再换 `Migrate`。

**Q4：为什么从容器里取 `DataSeeder` 要先 `CreateScope()`？**
因为 `DbContext`（`AppDbContext`）默认是 **Scoped** 生命周期，只能在作用域内使用。`CreateScope()` 就是新建一个作用域，作用域结束由容器统一释放（`DbContext` 是非线程安全的，这点必须遵守）。

**Q5：什么时候用 `decimal` 什么时候用 `double`？**
涉及钱的精确计算、数据库 `decimal` 列 → `decimal`；科学计算、图形、性能敏感 → `double`/`float`。本项目 `Value` 用 `decimal(18,4)`，符合遥测数据的精度需求。

**Q6：`string? DeviceType` 和 `string DeviceCode` 差在哪？**
`?` 声明该字段允许为 null。开了 `<Nullable>enable</Nullable>` 后，编译器会对可为空的变量做空引用检查（用到前先判空），帮助减少 `NullReferenceException`。`DeviceCode` 声明不可空且给了默认值 `string.Empty`，更安全。

---

*文档完。建议配合代码逐行阅读，边改边跑，理解会更深。*
