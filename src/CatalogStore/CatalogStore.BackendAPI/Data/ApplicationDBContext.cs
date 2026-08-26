using CatalogStore.BackendAPI.Models.Status;
using CatalogStore.BackendAPI.Models.EventLogs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Data
{
    public sealed class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
    {
        public DbSet<Status> Statuses { get; set; }
        public DbSet<Eventlog> Eventlogs { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema("dbo");

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.SendNotifications)
                    .HasDefaultValue(true); 
                entity.Property(e => e.FullName)
                    .HasMaxLength(150);
                
            });
            builder.Entity<ApplicationUser>().HasIndex(u => u.Email).IsUnique();
            builder.Entity<Status>(entity => 
            {
                entity.Property(e => e.name)
                    .HasMaxLength(100);
            });
            builder.Entity<Eventlog>(entity => 
            {
                entity.Property(e => e.ModuleName)
                    .HasMaxLength(100);
                entity.Property(e => e.TableName)
                    .HasMaxLength(100);
                entity.Property(e => e.EventDesc)
                    .HasMaxLength(350);
                entity.Property(e => e.RecordID)
                    .HasMaxLength(100).IsRequired();
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(100).IsRequired();
                entity.HasIndex(e => new { e.TableName, e.RecordID })
                    .HasDatabaseName("IX_Eventlogs_TableName_RecordID");
                entity.HasIndex(e => e.CreatedOn)
                    .HasDatabaseName("IX_Eventlogs_CreatedOn");
            });
        }
    }
}
