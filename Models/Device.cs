using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Models
{
    public class Device
    {
        /// <summary>
        /// 主键 ID（自增）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 设备编码（唯一，不可为空，最大长度50）
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// 设备名称（不可为空，最大长度100）
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 设备类型（最大长度50，可为空）
        /// </summary>
        public string? DeviceType { get; set; }

        // 如果需要，可以添加导航属性（一对多）
        // public ICollection<DataPoint> DataPoints { get; set; }
    }
}
