using LibraryReservationEngine.Application.Common;
using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person B. Both people will call this from their own services.
    public interface INotificationService
    {
        Task SendAsync(string userId, NotificationType type, string message);
        Task SendToLibrariansAsync(NotificationType type, string message);
        Task<IEnumerable<NotificationSummaryDto>> GetMyNotificationsAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<Result> MarkAsReadAsync(int notificationId, string userId);
        Task<Result> MarkAllAsReadAsync(string userId);
    }
}
