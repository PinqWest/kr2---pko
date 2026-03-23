using Microsoft.EntityFrameworkCore;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class AppDbContextFactory
{
    private readonly string _connectionString;

    public AppDbContextFactory(string connectionString = "Data Source=timetracker.db")
    {
        _connectionString = connectionString;
    }

    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(_connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
