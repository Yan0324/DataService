# DataService

一个基于 **.NET 8** 的工业设备数据采集演示服务。程序启动时自动初始化 MySQL 数据库、写入种子设备数据，并对多台设备**并行模拟采集**数据点，最后将采集结果**批量写入**数据库，全程通过 Serilog 输出日志。

## 技术栈

| 组件 | 说明 |
| ---- | ---- |
| .NET 8 | 目标框架（`net8.0`） |
| EF Core 8.0 | ORM 框架 |
| Pomelo.EntityFrameworkCore.MySql | MySQL 数据库驱动 |
| Serilog | 结构化日志（读取 `appsettings.json` 配置） |
| Microsoft.Extensions.Hosting | 依赖注入 / 配置 / 日志宿主 |

## 项目结构

```
DataService
├── Program.cs                          # 入口：配置加载、DI 注册、启动初始化流程
├── appsettings.json                    # 数据库连接串、采集参数、Serilog 配置
├── DataService.csproj
├── Models/
│   ├── Device.cs                       # 设备实体（DeviceCode 唯一）
│   ├── DataPoint.cs                    # 数据点实体（DeviceId / Timestamp / Value / Unit / Quality）
│   ├── Enums.cs                        # DataQuality 枚举（Good / Uncertain / Bad）
│   └── CollectorSettings.cs            # 采集参数配置类（强类型绑定）
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext，实体与索引、外键配置
│   ├── DataSeeder.cs                   # 建库 + 种子数据（3 台设备）
│   └── Configurations.cs               # （预留）实体配置
├── Services/
│   ├── CollectorService.cs             # 设备数据模拟采集（按设备编码模拟不同测点）
│   ├── DataStorageService.cs           # 数据批量入库
│   └── IServices/                      # 服务接口定义
│       ├── ICollectorService.cs
│       └── IDataStorageService.cs
└── Infrastructure/
    ├── ChannelBoundedBuffer.cs         # （预留）有界通道缓冲
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

启动后程序会自动执行：

1. 连接数据库，`EnsureCreated` 自动创建数据库与表；
2. 若无数据则插入种子设备（`DEV001` 温度传感器、`DEV002` 压力传感器、`DEV003` 流量计）；
3. 并发采集前 3 台设备的数据；
4. 将全部数据点一次性批量写入 `DataPoints` 表；
5. 写入完成后程序退出（`host.RunAsync()` 当前被注释，未作为常驻服务运行）。

## 配置说明

`appsettings.json` 中 `CollectorSettings` 节点说明：

| 配置项 | 默认值 | 说明 |
| ------ | ------ | ---- |
| `DeviceCount` | 3 | 参与采集的设备数量（程序当前取前 3 台） |
| `IntervalSeconds` | 1 | 采样间隔（秒） |
| `DurationSeconds` | 10 | 每台设备的采集时长（秒），即每台生成 10 条数据 |
| `BatchSize` | 5 | 批写入大小（预留，当前未使用） |
| `FlushIntervalSeconds` | 2 | 刷盘间隔（秒，预留，当前未使用） |

> 说明：当前实现实际生效的参数为 `DurationSeconds`（控制每台设备的采集条数）。`BatchSize`、`FlushIntervalSeconds` 已配置但尚未接入逻辑，属于预留字段。

## 数据模拟逻辑

`CollectorService` 根据设备编码模拟不同测点的数据（`Value` 与 `Unit`）：

| 设备编码 | 模拟测点 | 数值范围 | 单位 |
| -------- | -------- | -------- | ---- |
| `DEV001` | 温度 | 20.00 ~ 30.00 | °C |
| `DEV002` | 压力 | 0.800 ~ 1.200 | MPa |
| `DEV003` | 流量 | 10.00 ~ 100.00 | m³/h |

数据点使用 `DateTime.UtcNow` 作为业务时间戳，`Quality` 固定为 `Good`（`CreateDataQuality()` 方法已预留质量模拟逻辑，暂未接入）。

## 数据库表结构

### Devices（设备表）

| 列 | 类型 | 说明 |
| -- | ---- | ---- |
| `Id` | int | 主键，自增 |
| `DeviceCode` | varchar(50) | 设备编码，唯一索引，必填 |
| `DeviceName` | varchar(100) | 设备名称，必填 |
| `DeviceType` | varchar(50) | 设备类型，可空 |

### DataPoints（数据点表）

| 列 | 类型 | 说明 |
| -- | ---- | ---- |
| `Id` | int | 主键，自增 |
| `DeviceId` | int | 外键 → Devices.Id，级联删除 |
| `Timestamp` | datetime | 采集时间，含单列索引与 `(DeviceId, Timestamp)` 复合索引 |
| `Value` | decimal(18,4) | 采集数值 |
| `Unit` | varchar(20) | 单位，可空 |
| `Quality` | int | 数据质量（0=Good，1=Uncertain，2=Bad） |
