using LibraryReservationEngine.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryReservationEngine.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(
            IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roles = { "Member", "Librarian" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }

            const string librarianEmail = "librarian@library.test";
            const string librarianPassword = "Librarian@123";

            var librarian =
                await userManager.FindByEmailAsync(librarianEmail);

            if (librarian == null)
            {
                librarian = new ApplicationUser
                {
                    FullName = "Library Librarian",
                    UserName = librarianEmail,
                    Email = librarianEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(
                    librarian,
                    librarianPassword);

                if (!result.Succeeded)
                {
                    throw new Exception(
                        "Failed to create default librarian.");
                }
            }

            if (!await userManager.IsInRoleAsync(
                    librarian,
                    "Librarian"))
            {
                await userManager.AddToRoleAsync(
                    librarian,
                    "Librarian");
            }
        }
    }
}