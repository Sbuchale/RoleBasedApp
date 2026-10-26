using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoleBasedApp.Data
{
    public class ApplicationDbContextFactory
        : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(
            string[] args)
        {
            var directory = new DirectoryInfo(
                AppContext.BaseDirectory);

            while (directory != null &&
                   !File.Exists(
                       Path.Combine(
                           directory.FullName,
                           "appsettings.json")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException(
                    "Could not find appsettings.json. " +
                    "Check the project folder and build settings.");
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory.FullName)
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: false)
                .Build();

            var connectionString =
                configuration.GetConnectionString(
                    "DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection is missing from appsettings.json.");
            }

            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>();

            options.UseSqlServer(connectionString);

            return new ApplicationDbContext(options.Options);
        }
    }
}


