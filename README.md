# DataService

一个基于 **.NET 8** 的工业设备数据采集演示服务。程序启动时自动初始化 MySQL 数据库、写入种子设备数据，并对多台设备**并行模拟采集**数据点，最后将采集结果**批量写入**数据库，全程通过 Serilog 输出结构化日志。

## 技术栈

| 组件                             | 说明                                        |
| -------------------------------- | ------------------------------------------- |
| .NET 8                           | 目标框架（`net8.0`）                      |
| EF Core 8.0                      | ORM 框架                                    |
| Pomelo.EntityFrameworkCore.MySql | MySQL 数据库驱动                            |
| Serilog                          | 结构化日志（读取`appsettings.json` 配置） |
| Microsoft.Extensions.Hosting     | 通用主机：依赖注入 / 配置 / 日志宿主        |

## 项目结构

```
DataService
├── Program.cs                          # 入口：配置加载、DI 注册、启动初始化流程
├── appsettings.json                    # 数据库连接串、采集参数、Serilog 配置
├── DataService.csproj
├── Models/
│   ├── Device.cs                       # 设备实体（DeviceCode 唯一索引）
│   ├── DataPoint.cs                    # 数据点实体（DeviceId / Timestamp / Value / Unit / Quality）
│   ├── Enums.cs                        # DataQuality 枚举（Good / Uncertain / Bad）
│   └── CollectorSettings.cs            # 采集参数配置类（IOptions 强类型绑定）
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext：实体、索引、外键、级联删除配置
│   ├── DataSeeder.cs                   # 建库 + 种子数据（3 台设备）
│   └── Configurations.cs               # （预留）IEntityTypeConfiguration 拆分
├── Services/
│   ├── CollectorService.cs             # 设备数据模拟采集（按设备编码模拟不同测点）
│   ├── DataStorageService.cs           # 数据批量入库
│   └── IServices/                      # 服务接口定义（面向接口编程）
│       ├── ICollectorService.cs
│       └── IDataStorageService.cs
└── Infrastructure/
    ├── ChannelBoundedBuffer.cs         # （预留）有界通道缓冲（生产者-消费者）
    └── AsyncBulkInserter.cs            # （预留）异步批量写入
```

## 快速开始

### 环境要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 数据库（本机或远程均可）

### 1. 配置数据库连接

编辑 `appsettings.json` 中的 `ConnectionStrings:DefaultConnection`：

```json
"DefaultConnection": "Server=localhost;Port=3306;Database=devicedata;User=root;Password=123456;"
```

### 2. 运行

```bash
dotnet run
```

### 启动后的执行流程

程序启动后（`Program.cs`）依次执行：

1. 读取 `appsettings.json`，配置 Serilog 日志；
2. 构建通用主机，向 DI 容器注册 `AppDbContext`、`DataSeeder`、`ICollectorService`、`IDataStorageService` 及 `CollectorSettings` 配置绑定；
3. 开一个 DI 作用域，调用 `DataSeeder.SeedAsync()`：
   - `EnsureCreatedAsync` 自动创建数据库与表（已有则跳过）；
   - 若 `Devices` 表为空，插入 3 台种子设备（`DEV001` 温度传感器、`DEV002` 压力传感器、`DEV003` 流量计）；
4. 从数据库取出设备列表（按 `DeviceCode` 排序取前 3 台），**并行**调用 `CollectorService.CollectDeviceAsync()` 模拟采集；
5. 汇总所有设备生成的数据点，调用 `DataStorageService.SaveBatchAsync()` 一次性批量写入 `DataPoints` 表；
6. 写入完成后程序退出（`host.RunAsync()` 当前被注释，未作为常驻服务运行）。

## 配置说明

`appsettings.json` 中 `CollectorSettings` 节点会被绑定为强类型的 `CollectorSettings` 类：

| 配置项                   | 默认值 | 实际使用情况                                                             |
| ------------------------ | ------ | ------------------------------------------------------------------------ |
| `DeviceCount`          | 3      | **未使用**。`Program.cs` 硬编码 `.Take(3)` 取前 3 台设备       |
| `IntervalSeconds`      | 1      | **未使用**。`CollectorService` 中采集间隔硬编码为 1 秒           |
| `DurationSeconds`      | 10     | **已使用**。决定每台设备采集的数据条数（循环次数），默认每台 10 条 |
| `BatchSize`            | 5      | **未使用**。预留批量写入大小                                       |
| `FlushIntervalSeconds` | 2      | **未使用**。预留刷盘间隔                                           |

> 说明：目前代码逻辑实际只消费 `DurationSeconds` 一个配置项。其余四项虽已声明并绑定，但尚未接入采集/写入流程，属于预留字段（`Infrastructure/` 下对应实现仍为空壳）。

## 数据模拟逻辑

`CollectorService.CreateDataPoint()` 根据设备编码模拟不同测点的数据（`Value` 与 `Unit`）：

| 设备编码   | 模拟测点 | 数值范围       | 单位  |
| ---------- | -------- | -------------- | ----- |
| `DEV001` | 温度     | 20.00 ~ 30.00  | °C   |
| `DEV002` | 压力     | 0.800 ~ 1.200  | MPa   |
| `DEV003` | 流量     | 10.00 ~ 100.00 | m³/h |

- 每台设备按 `DurationSeconds` 指定的次数采集，每 1 秒生成一条数据点；
- 数据点使用 `DateTime.UtcNow` 作为业务时间戳；
- `Quality` 固定为 `Good`。`CreateDataQuality()` 方法已写好质量模拟逻辑（90% Good / 8% Uncertain / 2% Bad），但**尚未接入** `CreateDataPoint()`。

## 数据库表结构

> 由 `DataSeeder` 通过 `EnsureCreatedAsync` 自动创建，无迁移文件。

### Devices（设备表）

| 列             | 类型         | 约束           |
| -------------- | ------------ | -------------- |
| `Id`         | int          | 主键，自增     |
| `DeviceCode` | varchar(50)  | 唯一索引，必填 |
| `DeviceName` | varchar(100) | 必填           |
| `DeviceType` | varchar(50)  | 可空           |

### DataPoints（数据点表）

| 列            | 类型          | 约束                                           |
| ------------- | ------------- | ---------------------------------------------- |
| `Id`        | int           | 主键，自增                                     |
| `DeviceId`  | int           | 外键 → Devices.Id，级联删除                   |
| `Timestamp` | datetime      | 含单列索引与`(DeviceId, Timestamp)` 复合索引 |
| `Value`     | decimal(18,4) | 采集数值                                       |
| `Unit`      | varchar(20)   | 可空                                           |
| `Quality`   | int           | 数据质量（0=Good，1=Uncertain，2=Bad）         |
