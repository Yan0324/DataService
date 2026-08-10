using DataService.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService.Data
{
    public class AppDbContext : DbContext
    {
        //数据表
        public DbSet<Device> Devices { get; set; }
        public DbSet<DataPoint> DataPoints { get; set; }

        //构造函数
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        //配置实体
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            //配置Device实体
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.Id);
                // 唯一索引，确保设备编码唯一
                entity.HasIndex(e => e.DeviceCode).IsUnique();
                entity.Property(e => e.DeviceCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.DeviceName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.DeviceType).HasMaxLength(50);
            });
            //配置DataPoint实体
            modelBuilder.Entity<DataPoint>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);  // 按时间查询优化
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });  // 复合索引

                entity.Property(e => e.Value).HasPrecision(18, 4);  // 高精度小数
                entity.Property(e => e.Unit).HasMaxLength(20);
                // 外键关系
                entity.HasOne<Device>()
                      .WithMany()
                      .HasForeignKey(e => e.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
