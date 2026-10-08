using CatalogStore.BackendAPI.Models.Status;
using CatalogStore.BackendAPI.Models.EventLogs;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.Product;
using CatalogStore.BackendAPI.Models.ProductImage;
using CatalogStore.BackendAPI.Models.Inventory;
using CatalogStore.BackendAPI.Models.InventoryLine;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;
namespace CatalogStore.BackendAPI.Data
{
    public sealed class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
    {
        public DbSet<Status> Statuses { get; set; }
        public DbSet<Eventlog> Eventlogs { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<InventoryLine> InventoryLines { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
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
                entity.Property(e => e.ProfitPercentage)
                    .HasDefaultValue(1.07m)
                    .HasPrecision(6, 4);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Product_ProfitPercentage_Valid", "[ProfitPercentage] >= 1 AND [ProfitPercentage] <= 10");
                });
                entity.Property(e => e.Price)
                    .HasPrecision(10,2);
                entity.Property(e => e.PriceCalcIVA)
                    .HasPrecision(10,2);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.HasIndex(e => new { e.ProductName, e.categoria})
                    .HasDatabaseName("IX_Products_ProductName_Categoria");
                entity.HasIndex(e=>e.ExternalProductID)
                    .IsUnique()
                    .HasFilter("[ExternalProductID] IS NOT NULL")
                    .HasDatabaseName("IX_Products_ExternalProductID_UQ");
                entity.HasOne(e => e.Status)
                    .WithMany()
                    .HasForeignKey(e => e.StatusID)
                    .HasConstraintName("FK_Product_Status_StatusID")
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<ProductImage>(entity =>
            {
                entity.Property(e => e.Url)
                    .HasMaxLength(250).IsRequired();
                entity.Property(e => e.ContentType)
                    .HasMaxLength(100);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.HasIndex(e => new { e.ProductID, e.Url })
                    .HasDatabaseName("IX_ProductImage_ProductID_Url");
                entity.HasOne(e => e.Product)
                    .WithMany(p => p.Images)
                    .HasForeignKey(e => e.ProductID)
                    .HasConstraintName("FK_ProductImage_Product_ProductID")
                    .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Inventory>(entity =>
            {
                entity.Property(e => e.Name)
                    .HasMaxLength(150).IsRequired();
                entity.Property(e => e.Descripcion)
                    .HasMaxLength(250);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.HasIndex(e => e.Name)
                    .HasDatabaseName("IX_Inventory_Name");
                entity.HasOne(e => e.Status)
                    .WithMany()
                    .HasForeignKey(e => e.StatusID)
                    .HasConstraintName("FK_Inventory_Status_StatusID")
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<InventoryLine>(entity =>
            {
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_InventoryLine_Quantity_NonNegative", "[Quantity] >= 0");
                    t.HasCheckConstraint("CK_InventoryLine_QuantityOnHold_NonNegative", "[QuantityOnHold] >= 0");
                    t.HasCheckConstraint("CK_InventoryLine_OnHold_LTE_Quantity", "[QuantityOnHold] <= [Quantity]");
                });
                entity.Property(e => e.QuantityAvailable)
                    .HasComputedColumnSql("[Quantity] - [QuantityOnHold]", stored: true);
                entity.HasIndex(e => new { e.InventoryID, e.ProductID })
                    .HasDatabaseName("IX_InventoryLine_InventoryID_ProductID")
                    .IsUnique();
                entity.HasOne(e => e.Status)
                    .WithMany()
                    .HasForeignKey(e => e.StatusID)
                    .HasConstraintName("FK_InventoryLine_Status_StatusID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Inventory)
                    .WithMany()
                    .HasForeignKey(e => e.InventoryID)
                    .HasConstraintName("FK_InventoryLine_Inventory_InventoryID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductID)
                    .HasConstraintName("FK_InventoryLine_Product_ProductID")
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<InventoryTransaction>(entity =>
            {
                entity.Property(e => e.ReferenceID)
                    .HasMaxLength(50);
                entity.Property(e => e.Reason)
                    .HasMaxLength(250);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_InventoryTransaction_TransactionQuantity_Positive", "[TransactionQuantity] > 0");
                    t.HasCheckConstraint("CK_InventoryTransaction_QuantityBefore_NonNegative", "[QuantityBefore] >= 0");
                    t.HasCheckConstraint("CK_InventoryTransaction_QuantityAfter_NonNegative", "[QuantityAfter] >= 0");
                    t.HasCheckConstraint("CK_InventoryTransaction_OnHoldBefore_NonNegative", "[OnHoldBefore] >= 0");
                    t.HasCheckConstraint("CK_InventoryTransaction_OnHoldAfter_NonNegative", "[OnHoldAfter] >= 0");
                });
                entity.HasIndex(e => new { e.InventoryLineID, e.CreatedOn })
                    .HasDatabaseName("IX_InventoryTransaction_InventoryLineID_CreatedOn");
                entity.HasIndex(e => new { e.ReferenceReason, e.ReferenceID })
                    .HasDatabaseName("IX_InventoryTransaction_ReferenceReason_ReferenceID");
                entity.HasOne(e => e.InventoryLine)
                    .WithMany()
                    .HasForeignKey(e => e.InventoryLineID)
                    .HasConstraintName("FK_InventoryTransaction_InventoryLine_InventoryLineID")
                    .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Order>(entity =>
            {
                entity.Property(e => e.OrderNumber)
                    .HasMaxLength(50).IsRequired();
                entity.Property(e => e.Notes)
                    .HasMaxLength(500);
                entity.Property(e => e.OrderTotalAmount)
                    .HasPrecision(12, 2);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.HasIndex(e => e.ClientID)
                    .IsUnique()
                    .HasFilter($"[OrderStatus] = {(int)OrderStatus.InProcess}")
                    .HasDatabaseName("IX_Order_ClientID_InProcess_UQ");
                entity.HasIndex(e => new { e.ClientID , e.CreatedOn})
                    .HasDatabaseName("IX_Order_ClientID_CreatedOn");
                entity.HasIndex(e => new { e.OrderStatus })
                    .HasDatabaseName("IX_Order_OrderStatus");
                entity.HasIndex(e => new { e.OrderNumber })
                    .HasDatabaseName("IX_Order_OrderNumber")
                    .IsUnique();
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientID)
                    .HasConstraintName("FK_Order_Client_ClientID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Order_TotalAmount_NonNegative", "[OrderTotalAmount] >= 0");
                });
            }).HasSequence<int>("OrderNumberSeq").StartsAt(1).IncrementsBy(1);
            builder.Entity<OrderLine>(entity =>
            {
                entity.Property(e => e.ProductCode)
                    .HasMaxLength(20);
                entity.Property(e => e.ProductName)
                    .HasMaxLength(150).IsRequired();
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.Property(e => e.UnitPrice)
                    .HasPrecision(10, 2);
                entity.Property(e => e.UnitPriceIVA)
                    .HasPrecision(10, 2);
                entity.Property(e => e.LineTotalPrice)
                    .HasPrecision(12, 2);
                entity.Property(e => e.Discount)
                    .HasPrecision(5, 2)
                    .HasDefaultValue(0.0m);
                entity.HasIndex(e => new { e.OrderID, e.ProductID })
                    .HasDatabaseName("IX_OrderLine_OrderID_ProductID")
                    .IsUnique();
                entity.HasOne(e => e.Order)
                    .WithMany(o => o.OrderLines)
                    .HasForeignKey(e => e.OrderID)
                    .HasConstraintName("FK_OrderLine_Order_OrderID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductID)
                    .HasConstraintName("FK_OrderLine_Product_ProductID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_OrderLine_Quantity_Positive", "[Quantity] > 0");
                    t.HasCheckConstraint("CK_OrderLine_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    t.HasCheckConstraint("CK_OrderLine_UnitPriceIVA_NonNegative", "[UnitPriceIVA] >= 0");
                    t.HasCheckConstraint("CK_OrderLine_LineTotalPrice_NonNegative", "[LineTotalPrice] >= 0");
                    t.HasCheckConstraint("CK_OrderLine_Discount_Valid", "[Discount] >= 0 AND [Discount] <= 99.99");
                });
            });
        }
    }
}
