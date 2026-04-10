using Microsoft.EntityFrameworkCore;

namespace UTB.Minute.Db;

public class MenzaContext(DbContextOptions<MenzaContext> options) : DbContext(options)
{
    public DbSet<Food> Foods { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>()
            .HasIndex(m => new { m.Date, m.FoodId })
            .IsUnique();

        modelBuilder.Entity<Food>()
            .Property(f => f.Price)
            .HasColumnType("numeric(10,2)");

        modelBuilder.Entity<MenuItem>()
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
