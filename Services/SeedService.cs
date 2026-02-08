using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;

namespace UserRoles.Services
{
    public class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedService>>();

            try
            {
                // Ensure the database is ready
                logger.LogInformation("Ensuring the database is created.");
                await context.Database.EnsureCreatedAsync();

                // Add roles
                logger.LogInformation("Seeding roles.");
                await AddRoleAsync(roleManager, "Admin");
                await AddRoleAsync(roleManager, "User");
                await AddRoleAsync(roleManager, "Manager");



                // -----------------------------
                // Seed Team Columns
                // -----------------------------
                logger.LogInformation("Seeding team columns.");

                if (!await context.TeamColumns.AnyAsync())
                {
                    context.TeamColumns.AddRange(

                        // Development Team
                        new TeamColumn { TeamName = "Development", ColumnName = "ToDo", Order = 1 },
                        new TeamColumn { TeamName = "Development", ColumnName = "Doing", Order = 2 },
                        new TeamColumn { TeamName = "Development", ColumnName = "Review", Order = 3 },
                        new TeamColumn { TeamName = "Development", ColumnName = "Complete", Order = 4 },

                        // Testing Team
                        new TeamColumn { TeamName = "Testing", ColumnName = "To Test", Order = 1 },
                        new TeamColumn { TeamName = "Testing", ColumnName = "Testing", Order = 2 },
                        new TeamColumn { TeamName = "Testing", ColumnName = "Bug Found", Order = 3 },
                        new TeamColumn { TeamName = "Testing", ColumnName = "Verified", Order = 4 },

                        // Sales Team
                        new TeamColumn { TeamName = "Sales", ColumnName = "Leads", Order = 1 },
                        new TeamColumn { TeamName = "Sales", ColumnName = "Follow Up", Order = 2 },
                        new TeamColumn { TeamName = "Sales", ColumnName = "Negotiation", Order = 3 },
                        new TeamColumn { TeamName = "Sales", ColumnName = "Closed", Order = 4 }
                    );

                    await context.SaveChangesAsync();
                    logger.LogInformation("Team columns seeded successfully.");
                }
                else
                {
                    logger.LogInformation("Team columns already exist. Skipping seeding.");
                }


                // Add admin user
                logger.LogInformation("Seeding admin user.");
                var adminEmail = "admin@gmail.com";
                if (await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    var adminUser = new Users
                    {
                        FirstName = "Admin",
                        LastName = "Admin",
                        UserName = adminEmail,
                        NormalizedUserName = adminEmail.ToUpper(),
                        Email = adminEmail,
                        NormalizedEmail = adminEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning Admin role to the admin user.");
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                    else
                    {
                        logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }

                // Add Manager user
            //    logger.LogInformation("Seeding Manager user.");
            //    var ManagerEmail = "manager@gmail.com";
            //    if (await userManager.FindByEmailAsync(ManagerEmail) == null)
            //    {
            //        var ManagerUser = new Users
            //        {
            //            Name = "Manager",
            //            UserName = ManagerEmail,
            //            NormalizedUserName = ManagerEmail.ToUpper(),
            //            Email = ManagerEmail,
            //            NormalizedEmail = ManagerEmail.ToUpper(),
            //            EmailConfirmed = true,
            //            SecurityStamp = Guid.NewGuid().ToString()
            //        };

            //        var result = await userManager.CreateAsync(ManagerUser, "Manager@123");
            //        if (result.Succeeded)
            //        {
            //            logger.LogInformation("Assigning Manager role to the Manager user.");
            //            await userManager.AddToRoleAsync(ManagerUser, "Manager");
            //        }
            //        else
            //        {
            //            logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            //        }
            //    }
            }
            catch (Exception ex)
            {
               logger.LogError(ex, "An error occurred while seeding the database.");

            }

        }

        private static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
