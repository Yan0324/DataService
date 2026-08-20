using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Models
{
    /// <summary>
    /// 采集数据质量
    /// </summary>

    public enum DataQuality
    {
        Good = 0,
        Uncertain = 1,
        Bad = 2
    }
}
