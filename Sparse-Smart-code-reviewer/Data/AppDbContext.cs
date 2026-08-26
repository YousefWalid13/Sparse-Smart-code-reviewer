using Microsoft.EntityFrameworkCore;
using Sparse_Smart_code_reviewer.Models;

namespace Sparse_Smart_code_reviewer.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<UserLogin> UserLogins { get; set; } = null!;
        public DbSet<Code> Codes { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Issue> Issues { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User -> Codes (One-to-Many)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Codes)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User -> Reviews (One-to-Many)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Reviews)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Code -> Reviews (One-to-Many)
            modelBuilder.Entity<Code>()
                .HasMany(c => c.Reviews)
                .WithOne(r => r.Code)
                .HasForeignKey(r => r.CodeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Review -> Issues (One-to-Many)
            modelBuilder.Entity<Review>()
                .HasMany(r => r.Issues)
                .WithOne(i => i.Review)
                .HasForeignKey(i => i.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            // User -> UserLogins (One-to-Many)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Logins)
                .WithOne(l => l.User)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Prevent duplicate provider accounts
            modelBuilder.Entity<UserLogin>()
                .HasIndex(l => new { l.Provider, l.ProviderKey })
                .IsUnique();
        }
    }
}