using DataService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Services.IServices
{
    public interface IDataStorageService
    {
        Task SaveBatchAsync(
            IReadOnlyCollection<DataPoint> dataPoints,
            CancellationToken cancellationToken = default
            );
    }
}
