using DisasterLogistics.Core.Models;

namespace DisasterLogistics.Core.Interfaces
{
    /// <summary>
    /// Interface for dashboard service operations.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Gets complete dashboard statistics.
        /// </summary>
        Services.DashboardStats GetStatistics(
            IEnumerable<Need> needs,
            IEnumerable<Supply> supplies,
            IEnumerable<Shipment> shipments);

        /// <summary>
        /// Gets top critical missing items.
        /// </summary>
        List<Services.CriticalMissingItem> GetTopCriticalMissingItems(IEnumerable<Need> needs, int count = 5);

        /// <summary>
        /// Checks for panic mode conditions.
        /// </summary>
        List<Services.PanicNeed> CheckPanicMode(IEnumerable<Need> needs);

        /// <summary>
        /// Generates a text-based console dashboard.
        /// </summary>
        string GenerateConsoleDashboard(Services.DashboardStats stats);
    }
}
