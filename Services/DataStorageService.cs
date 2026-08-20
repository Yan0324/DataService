using DataService.Data;
using DataService.Models;
using DataService.Services.IServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Services
{
    public class DataStorageService : IDataStorageService
    {
        private readonly AppDbContext _db;
        public DataStorageService(AppDbContext db)
        {
            _db = db;
        }
        public async Task SaveBatchAsync(IReadOnlyCollection<DataPoint> dataPoints, 
            CancellationToken cancellationToken = default)
        {
            if (dataPoints.Count == 0)
            {
                return;
            }
            await _db.DataPoints.AddRangeAsync(dataPoints, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
