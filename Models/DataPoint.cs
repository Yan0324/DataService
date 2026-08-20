using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Models
{
    public class DataPoint
    {
        /// <summary>
        /// 主键 ID（自增）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 关联的设备 ID（外键）
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 数据采集时间（业务时间戳）
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 采集数值（精度 18,4）
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// 单位（最大长度20，可为空）
        /// </summary>
        public string? Unit { get; set; }

        // 导航属性（关联的设备）
        // public Device Device { get; set; }

        /// <summary>
        /// 数据质量
        /// </summary>
        public DataQuality Quality { get; set; }

    }
}
