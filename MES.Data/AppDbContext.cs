using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities;
using MES.Shared.Entities;

namespace MES.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext).GetMethod(nameof(SetSoftDeleteFilter), 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(null, new object[] { builder });
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var now = DateTimeOffset.Now;
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedTime = now;
                entry.Entity.UpdatedTime = now;
                if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                    entry.Entity.CreatedBy = "system";
                entry.Entity.UpdatedBy = entry.Entity.CreatedBy;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedTime = now;
                entry.Entity.UpdatedBy = "system";
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
