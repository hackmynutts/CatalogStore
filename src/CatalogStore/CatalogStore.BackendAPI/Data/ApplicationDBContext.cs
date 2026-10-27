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
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Models.SalesLine;
using CatalogStore.BackendAPI.Models.SalesPayment;
using CatalogStore.BackendAPI.Models.EmailOutbox;
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
        public DbSet<SalesHeader> SalesHeaders { get; set; }
        public DbSet<SalesLine> SalesLines { get; set; }
        public DbSet<SalesPayment> SalesPayments { get; set; }
        public DbSet<EmailOutbox> EmailOutbox { get; set; }
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
                entity.HasIndex( e => e.CC)
                    .HasDatabaseName("IX_Client_CC");
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.CC)
                    .HasMaxLength(140);
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
                entity.Property(e => e.CostPrice)
                    .HasPrecision(10, 2);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Product_CostPrice_NonNegative", "[CostPrice] IS NULL OR [CostPrice] >= 0");
                    // CABYS: vacío mientras no se carga, o exactamente 13 dígitos.
                    t.HasCheckConstraint("CK_Product_CabysCode_Format", "[CabysCode] IS NULL OR (LEN([CabysCode]) = 13 AND [CabysCode] NOT LIKE '%[^0-9]%')");
                });
                entity.Property(e => e.CabysCode)
                    .HasMaxLength(13);
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
                entity.Property(e => e.UnitCost)
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
            builder.Entity<SalesHeader>(entity =>
            {
                entity.Property(e => e.SaleNumber)
                    .HasMaxLength(20);
                entity.Property(e => e.ClientIdentification)
                    .HasMaxLength(20);
                entity.Property(e => e.ClientPhone)
                    .HasMaxLength(10);
                entity.Property(e => e.ClientName)
                    .HasMaxLength(250);
                entity.Property(e => e.ClientAddress)
                    .HasMaxLength(350).IsRequired();
                entity.Property(e => e.ClientEmail)
                    .HasMaxLength(250);
                entity.Property(e => e.ClientCC)
                    .HasMaxLength(140);
                entity.Property(e => e.Subtotal)
                    .HasPrecision(12, 2);
                entity.Property(e => e.DiscountTotal)
                    .HasPrecision(12, 2);
                entity.Property(e => e.IvaTotal)
                    .HasPrecision(12, 2);
                entity.Property(e => e.TotalAmount)
                    .HasPrecision(12, 2);
                entity.Property(e => e.AmountPaid)
                    .HasPrecision(12, 2);
                entity.Property(e => e.Balance)
                    .HasPrecision(12, 2)
                    .HasComputedColumnSql("[TotalAmount] - [AmountPaid]", stored: true);
                entity.Property(e => e.Notes)
                    .HasMaxLength(500);
                entity.Property(e => e.Seller)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.ModifiedBy)
                    .HasMaxLength(140);
                entity.Property(e => e.VoidedBy)
                    .HasMaxLength(140);
                entity.Property(e => e.VoidedReason)
                    .HasMaxLength(250);
                entity.HasIndex(e => e.SaleNumber)
                    .HasDatabaseName("IX_SalesHeader_SaleNumber")
                    .IsUnique(); 
                entity.HasIndex(e => new { e.OrderID })
                    .HasDatabaseName("IX_SalesHeader_OrderID")
                    .IsUnique();
                entity.HasOne(e => e.Order)
                    .WithOne()
                    .HasForeignKey<SalesHeader>(e => e.OrderID)
                    .HasConstraintName("FK_SalesHeader_Order_OrderID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.ClientID, e.SaleDate })
                    .HasDatabaseName("IX_SalesHeader_ClientID_SaleDate");
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientID)
                    .HasConstraintName("FK_SalesHeader_Client_ClientID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.DueDate})
                    .HasDatabaseName("IX_SalesHeader_DueDate");
                entity.HasIndex(e => new { e.PaymentStatus, e.DueDate })
                    .HasDatabaseName("IX_SalesHeader_PaymentStatus_DueDate");
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_SalesHeader_AmountPaid_NonNegative", "[AmountPaid] >= 0");
                    t.HasCheckConstraint("CK_SalesHeader_AmountPaid_NotAboveTotal", "[AmountPaid] <= [TotalAmount]");
                    t.HasCheckConstraint("CK_SalesHeader_Void_Consistent",
                                        "([PaymentStatus] != 3 AND [VoidedBy] IS NULL AND [VoidedOn] IS NULL AND [VoidedReason] IS NULL) " +
                                        "OR ([PaymentStatus] = 3 AND [VoidedBy] IS NOT NULL AND [VoidedOn] IS NOT NULL AND [VoidedReason] IS NOT NULL)");
                });
            }).HasSequence<int>("SaleNumberSeq").StartsAt(1).IncrementsBy(1);
            builder.Entity<SalesLine>(entity =>
            {
                entity.Property(e => e.ProductCode)
                    .HasMaxLength(20);
                entity.Property(e => e.ProductName)
                    .HasMaxLength(150).IsRequired();
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.UnitPrice)
                    .HasPrecision(10, 2);
                entity.Property(e => e.UnitPriceIVA)
                    .HasPrecision(10, 2);
                entity.Property(e => e.Discount)
                    .HasPrecision(5, 2);
                entity.Property(e => e.LineSubtotal)
                    .HasPrecision(12, 2);
                entity.Property(e => e.LineIva)
                    .HasPrecision(12, 2);
                entity.Property(e => e.LineTotalPrice)
                    .HasPrecision(12, 2);
                entity.HasIndex(e => new { e.SalesHeaderID, e.ProductID })
                    .HasDatabaseName("IX_SalesLine_SalesHeaderID_ProductID").IsUnique();
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductID)
                    .HasConstraintName("FK_SalesLine_Product_ProductID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.SalesHeader)
                    .WithMany(h => h.Lines)
                    .HasForeignKey(e => e.SalesHeaderID)
                    .HasConstraintName("FK_SalesLine_SalesHeader_SalesHeaderID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_SalesLine_Quantity_Positive", "[Quantity] > 0");
                    t.HasCheckConstraint("CK_SalesLine_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    t.HasCheckConstraint("CK_SalesLine_UnitPriceIVA_NonNegative", "[UnitPriceIVA] >= 0");
                    t.HasCheckConstraint("CK_SalesLine_LineTotalPrice_NonNegative", "[LineTotalPrice] >= 0");
                    t.HasCheckConstraint("CK_SalesLine_Discount_Valid", "[Discount] >= 0 AND [Discount] <= 99.99");
                });
            });
            builder.Entity<SalesPayment>(entity =>
            {
                entity.Property(e => e.Amount)
                    .HasPrecision(12, 2);
                entity.Property(e => e.Reference)
                    .HasMaxLength(75);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                entity.Property(e => e.VoidedBy)
                    .HasMaxLength(140);
                entity.Property(e => e.Notes)
                    .HasMaxLength(250);
                entity.Property(e => e.VoidedReason)
                    .HasMaxLength(250);
                entity.HasOne(e => e.SalesHeader)
                    .WithMany(h => h.Payments)
                    .HasForeignKey(e => e.SalesHeaderID)
                    .HasConstraintName("FK_SalesPayment_SalesHeader_SalesHeaderID")
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.SalesHeaderID, e.PaymentDate })
                    .HasDatabaseName("IX_SalesPayment_SalesHeaderID_PaymentDate");
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_SalesPayment_Amount_Positive", "[Amount] > 0");
                    t.HasCheckConstraint("CK_SalesPayment_Method_Valid", "[PaymentMethod] IN (1,2,3,4,6,7,99)");
                    t.HasCheckConstraint("CK_SalesPayment_Void_Consistent",
                                        "([IsVoided] = 0 AND [VoidedBy] IS NULL AND [VoidedOn] IS NULL AND [VoidedReason] IS NULL) " +
                                        "OR ([IsVoided] = 1 AND [VoidedBy] IS NOT NULL AND [VoidedOn] IS NOT NULL AND [VoidedReason] IS NOT NULL)");
                });
            });
            builder.Entity<EmailOutbox>(entity =>
            {
                entity.Property(e => e.ToAddress)
                    .HasMaxLength(256).IsRequired();
                entity.Property(e => e.ToName)
                    .HasMaxLength(140);
                entity.Property(e => e.Subject)
                    .HasMaxLength(200).IsRequired();
                entity.Property(e => e.HtmlBody)
                    .IsRequired();
                entity.Property(e => e.Kind)
                    .HasMaxLength(50).IsRequired();
                entity.Property(e => e.RelatedTable)
                    .HasMaxLength(50);
                entity.Property(e => e.RelatedID)
                    .HasMaxLength(50);
                entity.Property(e => e.LastError)
                    .HasMaxLength(1000);
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(140).IsRequired();
                // El worker busca los pendientes cuyo próximo intento ya llegó.
                entity.HasIndex(e => new { e.Status, e.NextAttemptOn })
                    .HasDatabaseName("IX_EmailOutbox_Status_NextAttemptOn");
                entity.HasIndex(e => new { e.RelatedTable, e.RelatedID })
                    .HasDatabaseName("IX_EmailOutbox_RelatedTable_RelatedID");
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_EmailOutbox_Status_Valid", "[Status] IN (0,1,2)");
                    t.HasCheckConstraint("CK_EmailOutbox_Attempts_NonNegative", "[Attempts] >= 0");
                    t.HasCheckConstraint("CK_EmailOutbox_SentOn_Consistent", "([Status] = 1 AND [SentOn] IS NOT NULL) OR ([Status] <> 1 AND [SentOn] IS NULL)");
                });
            });
        }
    }
}
