using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Application.Interfaces
{
    // Owned by Person B. Both people will call this from their own services.
    public interface INotificationService
    {
        Task SendAsync(string userId, NotificationType type, string message);
    }
}
