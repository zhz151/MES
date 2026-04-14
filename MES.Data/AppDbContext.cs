using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MES.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && 
                   (e.State == EntityState.Added || e.State == EntityState.Modified));
        
        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            if (entry.State == EntityState.Added)
            {
                entity.CreatedTime = DateTime.Now;
                entity.CreatedBy = GetCurrentUser();
            }
            entity.UpdatedTime = DateTime.Now;
            entity.UpdatedBy = GetCurrentUser();
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private string GetCurrentUser()
    {
        return "System";
    }
}

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedTime { get; set; }
    public string CreatedBy { get; set; }
    public DateTime UpdatedTime { get; set; }
    public string UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}
