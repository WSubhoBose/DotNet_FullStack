using Microsoft.EntityFrameworkCore;

namespace BreweryApi.Infrastructure.Persistence;

public sealed class BreweryDbContext(DbContextOptions<BreweryDbContext> options) : DbContext(options)
{
    public DbSet<BreweryRecord> Breweries => Set<BreweryRecord>();
    public DbSet<CatalogMetadata> CatalogMetadata => Set<CatalogMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BreweryRecord>(entity =>
        {
            entity.ToTable("Breweries");
            entity.HasKey(brewery => brewery.Id);
            entity.HasIndex(brewery => brewery.Name);
            entity.HasIndex(brewery => brewery.City);
            entity.Property(brewery => brewery.Name).IsRequired();
            entity.Property(brewery => brewery.BreweryType).IsRequired();
            entity.Property(brewery => brewery.City).IsRequired();
            entity.Property(brewery => brewery.StateProvince).IsRequired();
            entity.Property(brewery => brewery.PostalCode).IsRequired();
            entity.Property(brewery => brewery.Country).IsRequired();
        });

        modelBuilder.Entity<CatalogMetadata>(entity =>
        {
            entity.ToTable("CatalogMetadata");
            entity.HasKey(value => value.Key);
        });
    }
}

public sealed class BreweryRecord
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string BreweryType { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public required string City { get; set; }
    public required string StateProvince { get; set; }
    public required string PostalCode { get; set; }
    public required string Country { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? Latitude { get; set; }
    public string? Phone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? State { get; set; }
    public string? Street { get; set; }
}

public sealed class CatalogMetadata
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<AccessTokenRecord> AccessTokens => Set<AccessTokenRecord>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<AuthUserRecord> Users => Set<AuthUserRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessTokenRecord>(entity =>
        {
            entity.ToTable("AccessTokens");
            entity.HasKey(token => token.TokenHash);
            entity.HasIndex(token => token.ExpiresAtUtc);
        });

        modelBuilder.Entity<RefreshTokenRecord>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(token => token.TokenHash);
            entity.HasIndex(token => token.ExpiresAtUtc);
        });

        modelBuilder.Entity<AuthUserRecord>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Username);
            entity.Property(user => user.CanUseAutocomplete).HasConversion<int>();
        });
    }
}

public sealed class AccessTokenRecord
{
    public required string TokenHash { get; set; }
    public required string Subject { get; set; }
    public required string CreatedAtUtc { get; set; }
    public required string ExpiresAtUtc { get; set; }
    public string? RevokedAtUtc { get; set; }
}

public sealed class RefreshTokenRecord
{
    public required string TokenHash { get; set; }
    public required string Subject { get; set; }
    public required string CreatedAtUtc { get; set; }
    public required string ExpiresAtUtc { get; set; }
    public string? RevokedAtUtc { get; set; }
}

public sealed class AuthUserRecord
{
    public required string Username { get; set; }
    public bool CanUseAutocomplete { get; set; }
}
