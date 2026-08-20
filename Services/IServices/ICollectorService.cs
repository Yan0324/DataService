using DataService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Services
{
    public interface ICollectorService
    {
        /// <summary>
        /// 异步采集一台设备的数据
        /// </summary>
        /// <param name="device">需要采集的设备</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本次采集生成的数据点</returns>
        
        Task<IReadOnlyList<DataPoint>> CollectDeviceAsync(
            Device device, 
            CancellationToken cancellationToken = default);
    }
}
