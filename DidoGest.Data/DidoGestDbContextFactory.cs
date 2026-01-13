using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DidoGest.Data;

/// <summary>
/// Factory per la creazione del DbContext in fase di design (migrations)
/// </summary>
public class DidoGestDbContextFactory : IDesignTimeDbContextFactory<DidoGestDbContext>
{
    public DidoGestDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DidoGestDbContext>();
        
        // Usa SQLite per default (può essere cambiato in SQL Server)
        optionsBuilder.UseSqlite(DidoGestDb.GetConnectionString());
        
        return new DidoGestDbContext(optionsBuilder.Options);
    }
}
