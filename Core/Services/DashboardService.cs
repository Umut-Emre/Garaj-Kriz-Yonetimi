using DisasterLogistics.Core.Enums;
using DisasterLogistics.Core.Models;

namespace DisasterLogistics.Core.Services
{
    /// <summary>
    /// Dashboard statistics snapshot.
    /// </summary>
    public class DashboardStats
    {
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        // Needs Statistics
        public int TotalNeeds { get; init; }
        public int FulfilledNeeds { get; init; }
        public int PartiallyFulfilledNeeds { get; init; }
        public int UnfulfilledNeeds { get; init; }
        public double PercentageNeedsMet { get; init; }

        // Supply Statistics
        public int TotalSupplies { get; init; }
        public int DepletedSupplies { get; init; }
        public int LowStockSupplies { get; init; }

        // Shipment Statistics
        public int TotalActiveShipments { get; init; }
        public int PendingShipments { get; init; }
        public int InTransitShipments { get; init; }
        public int DeliveredToday { get; init; }

        // Critical Items
        public List<CriticalMissingItem> TopCriticalMissingItems { get; init; } = new();

        // Panic Mode
        public bool PanicModeActive { get; init; }
        public List<PanicNeed> PanicNeeds { get; init; } = new();
    }

    /// <summary>
    /// Represents a critical missing item.
    /// </summary>
    public class CriticalMissingItem
    {
        public Guid NeedId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public int QuantityMissing { get; init; }
        public PriorityLevel Priority { get; init; }
        public double HoursWaiting { get; init; }
        public Location Location { get; init; }
    }

    /// <summary>
    /// Represents a need in panic mode status.
    /// </summary>
    public class PanicNeed
    {
        public Guid NeedId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public int QuantityRequired { get; init; }
        public int QuantityFulfilled { get; init; }
        public double HoursOverdue { get; init; }
        public Location Location { get; init; }
        public string? ContactInfo { get; init; }
    }

    /// <summary>
    /// Service providing real-time dashboard statistics and panic mode detection.
    /// This is the operator's control layer for monitoring the entire system.
    /// </summary>
    public class DashboardService
    {
        private readonly PriorityManager _priorityManager;
        private readonly AuditLogger _auditLogger;
        
        /// <summary>
        /// Hours threshold for panic mode (default: 1 hour).
        /// </summary>
        public double PanicModeThresholdHours { get; set; } = 1.0;

        /// <summary>
        /// Event raised when panic mode is triggered.
        /// </summary>
        public event Action<List<PanicNeed>>? OnPanicModeTriggered;

        public DashboardService(PriorityManager priorityManager, AuditLogger auditLogger)
        {
            _priorityManager = priorityManager ?? throw new ArgumentNullException(nameof(priorityManager));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <summary>
        /// Generates a complete dashboard statistics snapshot.
        /// </summary>
        public DashboardStats GetStatistics(
            IEnumerable<Need> needs,
            IEnumerable<Supply> supplies,
            IEnumerable<Shipment> shipments)
        {
            var needsList = needs.Where(n => !n.IsDeleted).ToList();
            var suppliesList = supplies.Where(s => !s.IsDeleted).ToList();
            var shipmentsList = shipments.Where(s => !s.IsDeleted).ToList();

            // Calculate needs statistics
            var fulfilledNeeds = needsList.Count(n => n.IsFulfilled);
            var partiallyFulfilled = needsList.Count(n => !n.IsFulfilled && n.FulfillmentPercentage > 0);
            var unfulfilled = needsList.Count(n => n.FulfillmentPercentage == 0);

            var totalQuantityRequired = needsList.Sum(n => n.QuantityRequired);
            var totalQuantityFulfilled = needsList.Sum(n => n.QuantityFulfilled);
            var percentageMet = totalQuantityRequired > 0
                ? (double)totalQuantityFulfilled / totalQuantityRequired * 100
                : 0;

            // Calculate supply statistics
            var depletedSupplies = suppliesList.Count(s => s.AllocatableQuantity == 0);
            var lowStockSupplies = suppliesList.Count(s => s.IsBelowMinimumStock && s.AllocatableQuantity > 0);

            // Calculate shipment statistics
            var activeShipments = shipmentsList.Where(s => s.IsActive).ToList();
            var pendingShipments = activeShipments.Count(s => s.Status == ShipmentStatus.Pending || s.Status == ShipmentStatus.Approved);
            var inTransitShipments = activeShipments.Count(s => s.Status == ShipmentStatus.InTransit || 
                                                                 s.Status == ShipmentStatus.AtDistributionCenter ||
                                                                 s.Status == ShipmentStatus.OutForDelivery);
            var deliveredToday = shipmentsList.Count(s => s.Status == ShipmentStatus.Delivered &&
                                                          s.ActualDeliveryDate?.Date == DateTime.UtcNow.Date);

            // Get top 5 critical missing items
            var criticalItems = GetTopCriticalMissingItems(needsList, 5);

            // Check panic mode
            var panicNeeds = CheckPanicMode(needsList);
            var panicModeActive = panicNeeds.Any();

            if (panicModeActive)
            {
                _auditLogger.LogPanicMode(panicNeeds.Count);
                OnPanicModeTriggered?.Invoke(panicNeeds);
            }

            return new DashboardStats
            {
                TotalNeeds = needsList.Count,
                FulfilledNeeds = fulfilledNeeds,
                PartiallyFulfilledNeeds = partiallyFulfilled,
                UnfulfilledNeeds = unfulfilled,
                PercentageNeedsMet = percentageMet,

                TotalSupplies = suppliesList.Count,
                DepletedSupplies = depletedSupplies,
                LowStockSupplies = lowStockSupplies,

                TotalActiveShipments = activeShipments.Count,
                PendingShipments = pendingShipments,
                InTransitShipments = inTransitShipments,
                DeliveredToday = deliveredToday,

                TopCriticalMissingItems = criticalItems,
                PanicModeActive = panicModeActive,
                PanicNeeds = panicNeeds
            };
        }

        /// <summary>
        /// Gets the top N critical missing items based on priority and wait time.
        /// </summary>
        public List<CriticalMissingItem> GetTopCriticalMissingItems(IEnumerable<Need> needs, int count = 5)
        {
            return needs
                .Where(n => !n.IsFulfilled && !n.IsDeleted)
                .Select(n => new
                {
                    Need = n,
                    EffectivePriority = _priorityManager.CalculateEffectivePriority(n),
                    HoursWaiting = (DateTime.UtcNow - n.CreatedAt).TotalHours
                })
                .OrderBy(x => x.EffectivePriority) // Lower = more critical
                .ThenByDescending(x => x.HoursWaiting)
                .Take(count)
                .Select(x => new CriticalMissingItem
                {
                    NeedId = x.Need.Id,
                    Title = x.Need.Title,
                    Category = x.Need.Category,
                    QuantityMissing = x.Need.RemainingQuantity,
                    Priority = _priorityManager.GetEffectivePriorityLevel(x.Need),
                    HoursWaiting = x.HoursWaiting,
                    Location = x.Need.Location
                })
                .ToList();
        }

        /// <summary>
        /// Checks for panic mode conditions.
        /// Returns list of critical needs that haven't been matched for over the threshold.
        /// </summary>
        public List<PanicNeed> CheckPanicMode(IEnumerable<Need> needs)
        {
            var panicNeeds = new List<PanicNeed>();

            foreach (var need in needs.Where(n => !n.IsDeleted && !n.IsFulfilled))
            {
                // Check if it's a Critical priority need
                var effectivePriority = _priorityManager.GetEffectivePriorityLevel(need);
                if (effectivePriority != PriorityLevel.Critical)
                    continue;

                // Check if it's been waiting longer than threshold
                var hoursWaiting = (DateTime.UtcNow - need.CreatedAt).TotalHours;
                if (hoursWaiting < PanicModeThresholdHours)
                    continue;

                // Check if it has received ANY fulfillment (if 0%, it's panic)
                // Or if it's been over 2x threshold with partial fulfillment
                if (need.FulfillmentPercentage == 0 || hoursWaiting > PanicModeThresholdHours * 2)
                {
                    panicNeeds.Add(new PanicNeed
                    {
                        NeedId = need.Id,
                        Title = need.Title,
                        Category = need.Category,
                        QuantityRequired = need.QuantityRequired,
                        QuantityFulfilled = need.QuantityFulfilled,
                        HoursOverdue = hoursWaiting - PanicModeThresholdHours,
                        Location = need.Location,
                        ContactInfo = need.ContactInfo
                    });
                }
            }

            return panicNeeds.OrderByDescending(p => p.HoursOverdue).ToList();
        }

        /// <summary>
        /// Gets a percentage breakdown of fulfillment by category.
        /// </summary>
        public Dictionary<string, double> GetFulfillmentByCategory(IEnumerable<Need> needs)
        {
            return needs
                .Where(n => !n.IsDeleted)
                .GroupBy(n => n.Category)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var totalRequired = g.Sum(n => n.QuantityRequired);
                        var totalFulfilled = g.Sum(n => n.QuantityFulfilled);
                        return totalRequired > 0 ? (double)totalFulfilled / totalRequired * 100 : 0;
                    });
        }

        /// <summary>
        /// Gets supply availability by category.
        /// </summary>
        public Dictionary<string, int> GetSupplyByCategory(IEnumerable<Supply> supplies)
        {
            return supplies
                .Where(s => !s.IsDeleted && !s.IsExpired)
                .GroupBy(s => s.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(s => s.AllocatableQuantity));
        }

        /// <summary>
        /// Generates a text-based dashboard for console output.
        /// </summary>
        public string GenerateConsoleDashboard(DashboardStats stats)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           🎛️  DISASTER LOGISTICS CONTROL DASHBOARD  🎛️           ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            // Panic Mode Alert
            if (stats.PanicModeActive)
            {
                sb.AppendLine("║  ⚠️⚠️⚠️  PANIC MODE ACTIVE  ⚠️⚠️⚠️                                    ║");
                sb.AppendLine($"║  {stats.PanicNeeds.Count} CRITICAL NEED(S) UNMATCHED > 1 HOUR                          ║");
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            }

            // Needs Met Percentage
            var pctBar = GenerateProgressBar(stats.PercentageNeedsMet, 30);
            sb.AppendLine($"║  📊 NEEDS MET: {stats.PercentageNeedsMet,5:F1}%  {pctBar}         ║");
            sb.AppendLine($"║     Fulfilled: {stats.FulfilledNeeds} | Partial: {stats.PartiallyFulfilledNeeds} | Unfulfilled: {stats.UnfulfilledNeeds,-10}  ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            // Active Shipments
            sb.AppendLine($"║  🚚 ACTIVE SHIPMENTS: {stats.TotalActiveShipments,-5}                                       ║");
            sb.AppendLine($"║     Pending: {stats.PendingShipments} | In Transit: {stats.InTransitShipments} | Delivered Today: {stats.DeliveredToday,-5}   ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            // Supply Status
            sb.AppendLine($"║  📦 SUPPLIES: {stats.TotalSupplies} total | {stats.DepletedSupplies} depleted | {stats.LowStockSupplies} low stock          ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");

            // Top 5 Critical Missing Items
            sb.AppendLine("║  🚨 TOP 5 CRITICAL MISSING ITEMS:                                ║");
            if (stats.TopCriticalMissingItems.Any())
            {
                foreach (var item in stats.TopCriticalMissingItems.Take(5))
                {
                    var itemLine = $"     [{item.Priority}] {item.Title}: {item.QuantityMissing} units ({item.HoursWaiting:F1}h)";
                    sb.AppendLine($"║  {itemLine,-64}║");
                }
            }
            else
            {
                sb.AppendLine("║     ✓ No critical missing items                                  ║");
            }

            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║  Generated: {stats.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC                              ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");

            return sb.ToString();
        }

        private static string GenerateProgressBar(double percentage, int width)
        {
            var filled = (int)(percentage / 100 * width);
            var empty = width - filled;
            return $"[{"█".PadRight(filled, '█')}{"░".PadRight(empty, '░')}]";
        }

        /// <summary>
        /// Gets the recent audit log entries.
        /// </summary>
        public List<AuditLogEntry> GetRecentActivityLog(int count = 10)
        {
            return _auditLogger.GetRecentLogs(count);
        }
    }
}
