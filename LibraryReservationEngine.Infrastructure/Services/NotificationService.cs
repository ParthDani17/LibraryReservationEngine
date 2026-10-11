using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Entities;
using LibraryReservationEngine.Domain.Enums;
using LibraryReservationEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryReservationEngine.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendAsync(string userId, NotificationType type, string message)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendToLibrariansAsync(NotificationType type, string message)
        {
            var librarianRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.NormalizedName == "LIBRARIAN");

            if (librarianRole is null)
            {
                return;
            }

            var librarianIds = await _context.UserRoles
                .Where(ur => ur.RoleId == librarianRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();

            if (librarianIds.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var notifications = librarianIds.Select(id => new Notification
            {
                UserId = id,
                Type = type,
                Message = message,
                IsRead = false,
                CreatedAt = now
            });

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<NotificationSummaryDto>> GetMyNotificationsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Enumerable.Empty<NotificationSummaryDto>();
            }

            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationSummaryDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Type = n.Type,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<Result> MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification is null)
            {
                return Result.Fail("Notification not found.");
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Result.Ok("Notification marked as read.");
        }

        public async Task<Result> MarkAllAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Count > 0)
            {
                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                }

                await _context.SaveChangesAsync();
            }

            return Result.Ok("All notifications marked as read.");
        }
    }
}