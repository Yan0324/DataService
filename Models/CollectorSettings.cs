using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Models
{
    public class CollectorSettings
    {
        public int DeviceCount { get; set; }
        public int IntervalSeconds{ get; set; }
        public int DurationSeconds { get; set; }
        public int BatchSize { get; set; }
        public int FlushIntervalSeconds { get; set; }
    }
}
