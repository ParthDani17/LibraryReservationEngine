using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using LibraryReservationEngine.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryReservationEngine.Tests
{
    public class NotificationServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SendAsync_PersistsNotificationForUser()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var service = new NotificationService(context);

            var userId = "student-123";
            await service.SendAsync(userId, NotificationType.BookReserved, "Your reservation is confirmed.");

            var notifications = await context.Notifications.ToListAsync();
            Assert.Single(notifications);
            var n = notifications[0];
            Assert.Equal(userId, n.UserId);
            Assert.Equal(NotificationType.BookReserved, n.Type);
            Assert.Equal("Your reservation is confirmed.", n.Message);
            Assert.False(n.IsRead);
        }

        [Fact]
        public async Task SendToLibrariansAsync_SendsNotificationToAllUsersInLibrarianRole()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);

            // Seed role and users
            var librarianRole = new IdentityRole { Id = "role-lib", Name = "Librarian", NormalizedName = "LIBRARIAN" };
            context.Roles.Add(librarianRole);

            var lib1 = new ApplicationUser { Id = "lib-1", UserName = "librarian1@test.com" };
            var lib2 = new ApplicationUser { Id = "lib-2", UserName = "librarian2@test.com" };
            var student = new ApplicationUser { Id = "student-1", UserName = "student1@test.com" };
            context.Users.AddRange(lib1, lib2, student);

            context.UserRoles.Add(new IdentityUserRole<string> { RoleId = librarianRole.Id, UserId = lib1.Id });
            context.UserRoles.Add(new IdentityUserRole<string> { RoleId = librarianRole.Id, UserId = lib2.Id });
            await context.SaveChangesAsync();

            var service = new NotificationService(context);
            await service.SendToLibrariansAsync(NotificationType.BookReserved, "Student reserved a book.");

            var lib1Notes = await service.GetMyNotificationsAsync("lib-1");
            var lib2Notes = await service.GetMyNotificationsAsync("lib-2");
            var studentNotes = await service.GetMyNotificationsAsync("student-1");

            Assert.Single(lib1Notes);
            Assert.Single(lib2Notes);
            Assert.Empty(studentNotes);
            Assert.Equal("Student reserved a book.", lib1Notes.First().Message);
        }

        [Fact]
        public async Task MarkAsReadAsync_UpdatesOnlyTargetNotificationForUser()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var service = new NotificationService(context);

            await service.SendAsync("user-a", NotificationType.BookIssued, "Book issued");
            await service.SendAsync("user-a", NotificationType.OverdueReminder, "Overdue reminder");

            var userNotes = (await service.GetMyNotificationsAsync("user-a")).ToList();
            Assert.Equal(2, userNotes.Count);

            var firstNoteId = userNotes[0].Id;
            var markResult = await service.MarkAsReadAsync(firstNoteId, "user-a");

            Assert.True(markResult.Success);
            Assert.Equal(1, await service.GetUnreadCountAsync("user-a"));

            var unauthorizedMark = await service.MarkAsReadAsync(userNotes[1].Id, "user-b");
            Assert.False(unauthorizedMark.Success);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_MarksAllUnreadNotificationsAsRead()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateContext(dbName);
            var service = new NotificationService(context);

            await service.SendAsync("user-test", NotificationType.FineIssued, "Fine 1");
            await service.SendAsync("user-test", NotificationType.FinePaid, "Fine paid");

            Assert.Equal(2, await service.GetUnreadCountAsync("user-test"));

            var result = await service.MarkAllAsReadAsync("user-test");
            Assert.True(result.Success);

            Assert.Equal(0, await service.GetUnreadCountAsync("user-test"));
        }
    }
}
