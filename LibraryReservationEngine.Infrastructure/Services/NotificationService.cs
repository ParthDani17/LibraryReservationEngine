using LibraryReservationEngine.Application.Interfaces;
using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Infrastructure.Services
{
    // Minimal stub — full implementation lands in feature/notifications.
    public class NotificationService : INotificationService
    {
        public Task SendAsync(string userId, NotificationType type, string message)
        {
            // No-op for now.
            return Task.CompletedTask;
        }
    }
}