// This is the "DbContext" — the single object EF Core uses to talk to the
// actual database. Two jobs happen here:
//   1. DbSet<T> properties (like "users" below) tell EF Core which entities
//      become tables, and give the rest of the app a way to query them.
//   2. OnModelCreating is where we fine-tune exactly how each entity maps to
//      its table (required columns, max lengths, unique indexes, etc.) using
//      the "Fluent API" — an alternative to putting attributes on the entity
//      classes themselves.
using Microsoft.EntityFrameworkCore;
using db.Context.Model;

namespace db.Context;

public class AppDbcontext : DbContext
{
    // EF Core needs the connection info (DbContextOptions) to know which
    // database to connect to; Program.cs supplies that when it registers
    // this DbContext with the app's dependency injection container.
    public AppDbcontext(DbContextOptions<AppDbcontext> options) : base(options) {}

    // This DbSet is what turns "User" into a "Users" table, and is what
    // UserService will query/add/update/remove through (e.g. _context.users).
    public DbSet<User> users { get; set; }

    // Same idea for Product -> "Products" table, used by ProductService.
    public DbSet<Product> products { get; set; }

    // Order/OrderItem -> "orders"/"orderItems" tables, used by OrderService.
    public DbSet<Order> orders { get; set; }
    public DbSet<OrderItem> orderItems { get; set; }

    // Cached AI analytics reports, used by AnalyticsReportService.
    public DbSet<StoredReport> storedReports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            // Tell EF Core which column is the primary key.
            e.HasKey(u => u.Id);

            // Username and Email must always have a value, and are capped at
            // a sensible length so the database column isn't unbounded text.
            e.Property(u => u.Username).IsRequired().HasMaxLength(50);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);

            // A unique index means the database itself will reject a second
            // row with the same Username/Email — this is our safety net even
            // if the service layer's own duplicate check is ever bypassed.
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();

            e.Property(u => u.Password).IsRequired();

            // Role is a C# enum (UserRole.User / UserRole.Admin), but by
            // default EF Core would store it as a plain number (0, 1) in the
            // database. HasConversion<string>() makes it store the actual
            // word "User" or "Admin" instead — much easier to read if you
            // ever look directly at the database.
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);

            // Name is required and what "search products by name" filters
            // on. Unlike Username/Email, the spec never says product names
            // must be unique (two different products could share a name),
            // so no unique index here.
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);

            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.LongDescription).HasMaxLength(2000);

            // decimal(18,2): up to 18 total digits, 2 after the decimal
            // point — standard precision for money values. Without this,
            // EF Core would pick a default precision that can silently
            // truncate prices.
            e.Property(p => p.Price).HasColumnType("decimal(18,2)");

            // Optional (products created before this field existed have no
            // recorded creator). Restrict — same reasoning as Order's user
            // FKs — so a user with products on record can't be deleted out
            // from under them.
            e.HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);

            // Type (In/Out) and Status (Pending/Completed/Cancelled) are C#
            // enums, stored as readable strings — same reasoning as
            // User.Role above.
            e.Property(o => o.Type).HasConversion<string>().HasMaxLength(10);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

            e.Property(o => o.TotalPrice).HasColumnType("decimal(18,2)");

            // The user who created the order. Restrict (not Cascade) means
            // SQL Server refuses to delete a User who has orders on record —
            // deleting them would silently erase order history otherwise.
            e.HasOne(o => o.CreatedByUser)
                .WithMany()
                .HasForeignKey(o => o.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // The admin who completed/cancelled it. Optional (null while
            // still Pending), same Restrict reasoning as above.
            e.HasOne(o => o.ProcessedByUser)
                .WithMany()
                .HasForeignKey(o => o.ProcessedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(oi => oi.Id);

            e.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");

            // Cascade: deleting an Order also deletes its OrderItem lines —
            // a line can't meaningfully exist without its parent order.
            e.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: a Product that's been ordered can't be deleted out
            // from under its order history. There's currently no soft-delete
            // for Products (it's only a spec "optional extra"), so attempting
            // to delete a Product that's been ordered will fail at the
            // database level — an accepted limitation for now.
            e.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoredReport>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ReportJson).IsRequired();
        });
    }
}
