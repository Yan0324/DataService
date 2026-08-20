using DataService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Services
{
    public class CollectorService:ICollectorService
    {
        private readonly CollectorSettings _settings;
        private readonly ILogger<CollectorService> _logger;

        public CollectorService(IOptions<CollectorSettings> options,ILogger<CollectorService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        //将右侧返回的元组 (decimal, string) 拆解为两个独立变量；value 接收第一个元素（数值），unit 接收第二个元素（单位）
        private DataPoint CreateDataPoint(Device device)
        {
            var (value, unit) = device.DeviceCode switch
            {
                "DEV001" => (
                    decimal.Round(20m + (decimal)Random.Shared.NextDouble() * 10m, 2),
                    "°C"
                ),
                "DEV002" => (
                    decimal.Round(0.8m + (decimal)Random.Shared.NextDouble() * 0.4m, 3),
                    "MPa"
                ),
                "DEV003" => (
                    decimal.Round(10m + (decimal)Random.Shared.NextDouble() * 90m, 2),
                    "m³/h"
                ),
                _ => throw new InvalidOperationException(
                    $"不支持设备 {device.DeviceCode} 的数据模拟。")
            };


            return new DataPoint
            {
                DeviceId = device.Id,
                Timestamp = DateTime.UtcNow,
                Value = value,
                Unit = unit,
                Quality = DataQuality.Good
            };
        }

        public async Task<IReadOnlyList<DataPoint>> CollectDeviceAsync(
            Device device, 
            CancellationToken cancellationToken = default)
        {
            var dataPoints = new List<DataPoint>();
            _logger.LogInformation("开始采集设备 {DeviceCode} 的数据。", device.DeviceCode);
            for (var i = 0; i < _settings.DurationSeconds; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dataPoint = CreateDataPoint(device);
                dataPoints.Add(dataPoint);

                _logger.LogInformation(
                    "设备{DeviceCode}第{Index}条数据，{Value}{Unit}，质量：{Quality}",
                    device.DeviceCode, i + 1, dataPoint.Value, dataPoint.Unit, dataPoint.Quality);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            _logger.LogInformation("完成采集设备 {DeviceCode} 的数据,共{Count}条。", 
                device.DeviceCode, dataPoints.Count);
            return dataPoints;
        }

        //完成质量模拟
        private DataQuality CreateDataQuality()
        {
            var rand = Random.Shared.Next(100);
            return rand switch
            {
                < 90 => DataQuality.Good,
                < 98 => DataQuality.Uncertain,
                _ => DataQuality.Bad
            };
        }


    }
}
