using CatalogStore.BackendAPI.Models.Status;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Data
{
    public sealed class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
    {
        public DbSet<Status> Statuses { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.SendNotifications)
                    .HasDefaultValue(true);
                entity.Property(e => e.FullName)
                    .HasMaxLength(150);
            });
            builder.Entity<Status>(entity => 
            {
                entity.Property(e => e.name)
                    .HasMaxLength(100);
            });
            builder.HasDefaultSchema("dbo");
        }
    }
}
