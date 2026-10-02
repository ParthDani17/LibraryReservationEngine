using System;
using System.Collections.Generic;
using System.Text;
using LibraryReservationEngine.Application.Common;

namespace LibraryReservationEngine.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetStatsAsync();
    }
}
