using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Models;

namespace RoleBasedApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Menu> Menus => Set<Menu>();

        public DbSet<Permission> Permissions => Set<Permission>();

        public DbSet<RolePermission> RolePermissions =>
            Set<RolePermission>();

        public DbSet<UserRole> UserRoles => Set<UserRole>();

        public DbSet<UserChangeRequest> UserChangeRequests =>
            Set<UserChangeRequest>();

        public DbSet<ApprovalHistory> ApprovalHistory =>
            Set<ApprovalHistory>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(
      ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RolePermission>()
                .HasKey(x => new
                {
                    x.RoleId,
                    x.MenuId,
                    x.PermissionId
                });

            builder.Entity<UserRole>()
                .HasKey(x => new
                {
                    x.UserId,
                    x.RoleId
                });

            builder.Entity<Permission>()
                .HasIndex(x => x.Code)
                .IsUnique();

            builder.Entity<ApplicationRole>()
                .HasIndex(x => x.NormalizedName)
                .IsUnique()
                .HasFilter("[NormalizedName] IS NOT NULL");

            builder.Entity<Menu>()
                .HasIndex(x => x.Route)
                .IsUnique();

            builder.Entity<AuditLog>()
                .HasIndex(x => x.TimestampUtc);

            builder.Entity<UserChangeRequest>()
                .HasIndex(x => new
                {
                    x.Status,
                    x.CreatedAtUtc
                });

            builder.Entity<ApprovalHistory>()
                .HasIndex(x => x.RequestId);

            builder.Entity<RolePermission>()
                .HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RolePermission>()
                .HasOne<Menu>()
                .WithMany()
                .HasForeignKey(x => x.MenuId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RolePermission>()
                .HasOne<Permission>()
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserRole>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserRole>()
                .HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
