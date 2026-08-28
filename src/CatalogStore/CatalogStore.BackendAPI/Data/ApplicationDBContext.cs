using CatalogStore.BackendAPI.Models.Status;
using CatalogStore.BackendAPI.Models.EventLogs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.Product;

namespace CatalogStore.BackendAPI.Data
{
    public sealed class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
    {
        public DbSet<Status> Statuses { get; set; }
        public DbSet<Eventlog> Eventlogs { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Product> Products { get; set; }
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
            builder.Entity<Client>(entity =>
            {
                entity.Property(e => e.Identification)
                    .HasMaxLength(20).IsRequired();
                entity.Property(e => e.ClientName)
                    .HasMaxLength(250).IsRequired();
                entity.Property(e => e.ClientPhone)
                    .HasMaxLength(10).IsRequired();
                entity.Property(e => e.ClientEmail)
                    .HasMaxLength(250).IsRequired();
                entity.Property(e => e.ClientAddress)
                    .HasMaxLength(350);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.HasIndex(e => new { e.Identification})
                    .IsUnique()
                    .HasDatabaseName("IX_Client_Identification_UQ");
                entity.HasOne(e => e.Status)
                    .WithMany()
                    .HasForeignKey(e => e.StatusID)
                    .HasConstraintName("FK_Client_Status_StatusID")
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Product>(entity =>
            {
                entity.Property(e => e.ProductCode)
                    .HasMaxLength(20);
                entity.Property(e => e.ProductName)
                    .HasMaxLength(150).IsRequired();
                entity.Property(e => e.ProductDesc)
                    .HasMaxLength(250).IsRequired();
                entity.Property(e => e.Price)
                    .HasPrecision(10,2);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.HasOne(e => e.Status)
                    .WithMany()
                    .HasForeignKey(e => e.StatusID)
                    .HasConstraintName("FK_Product_Status_StatusID")
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
