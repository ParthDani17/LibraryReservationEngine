using LibraryReservationEngine.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibraryReservationEngine.Web.BackgroundServices
{
    public class ReservationCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservationCleanupBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public ReservationCleanupBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReservationCleanupBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReservationCleanupBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
                    var borrowingService = scope.ServiceProvider.GetRequiredService<IBorrowingService>();

                    int expiredCount = await reservationService.ExpireOverdueReservationsAsync();
                    if (expiredCount > 0)
                    {
                        _logger.LogInformation("Expired {Count} overdue reservations and promoted next waitlisted users.", expiredCount);
                    }

                    int remindersSent = await borrowingService.SendOverdueRemindersAsync();
                    if (remindersSent > 0)
                    {
                        _logger.LogInformation("Sent {Count} overdue borrowing reminders.", remindersSent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during reservation cleanup or overdue reminders execution.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("ReservationCleanupBackgroundService stopped.");
        }
    }
}

