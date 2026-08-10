using DataService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Data
{
    public class DataSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DataSeeder> _logger;

        public DataSeeder(AppDbContext db, ILogger<DataSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 异步初始化数据库并插入种子数据
        /// </summary>
        public async Task SeedAsync(CancellationToken ct = default)
        {
           //1.异步创建数据库
           _logger.LogInformation("正在检查数据库...");
            await _db.Database.EnsureCreatedAsync(ct);
            _logger.LogInformation("数据库已就绪。");

            //2.检查是否已有数据
            if(await _db.Devices.AnyAsync(ct))
            {
                _logger.LogInformation("数据库中已有数据，跳过种子数据插入。");
                return;
            }

            //3.异步插入种子数据
            var devices = new List<Device>
            {
                new Models.Device { DeviceCode = "DEV001", DeviceName = "温度传感器", DeviceType = "传感器" },
                new Models.Device { DeviceCode = "DEV002", DeviceName = "压力传感器", DeviceType = "传感器" },
                new Models.Device { DeviceCode = "DEV003", DeviceName = "流量计", DeviceType = "仪表" }
            };

            await _db.Devices.AddRangeAsync(devices, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("种子数据已插入。"); 
        }
    }
}
