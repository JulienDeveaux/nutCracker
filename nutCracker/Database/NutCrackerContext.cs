using Microsoft.EntityFrameworkCore;
using nutCracker.Models;

namespace nutCracker.Database;

public class NutCrackerContext: DbContext
{
    public NutCrackerContext(DbContextOptions<NutCrackerContext> options) : base(options)
    {
        if(Database.GetPendingMigrations().Any())
            Database.Migrate();
    }
    
    public DbSet<HashResult> HashResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<HashResult>(model =>
        {
            model.HasKey(h => h.Hash);
            model.Property(h => h.Result);
        });
    }
}