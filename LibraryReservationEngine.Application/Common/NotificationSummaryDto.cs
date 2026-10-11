using System;
using LibraryReservationEngine.Domain.Enums;

namespace LibraryReservationEngine.Application.Common
{
    public class NotificationSummaryDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string TypeName => Type.ToString();
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
