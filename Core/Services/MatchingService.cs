using DisasterLogistics.Core.Enums;
using DisasterLogistics.Core.Models;

namespace DisasterLogistics.Core.Services
{
    /// <summary>
    /// Service for matching supplies to needs using priority-based allocation.
    /// Implements atomic operations with rollback capability on failure.
    /// </summary>
    public class MatchingService
    {
        private readonly PriorityManager _priorityManager;

        /// <summary>
        /// Configuration for the matching algorithm.
        /// </summary>
        public class MatchingConfig
        {
            /// <summary>
            /// Maximum distance (in km) to consider for proximity matching.
            /// Supplies beyond this distance are deprioritized.
            /// </summary>
            public double MaxProximityDistanceKm { get; init; } = 100.0;

            /// <summary>
            /// Weight factor for proximity in scoring (0-1).
            /// Higher = proximity matters more.
            /// </summary>
            public double ProximityWeight { get; init; } = 0.3;

            /// <summary>
            /// Weight factor for category match in scoring (0-1).
            /// </summary>
            public double CategoryMatchWeight { get; init; } = 0.5;

            /// <summary>
            /// Whether to allow partial fulfillment of needs.
            /// </summary>
            public bool AllowPartialFulfillment { get; init; } = true;

            /// <summary>
            /// Minimum percentage to fulfill in partial mode (0-100).
            /// </summary>
            public double MinPartialFulfillmentPercent { get; init; } = 10.0;

            public static MatchingConfig Default => new();
        }

        public MatchingConfig Config { get; }

        public MatchingService() : this(new PriorityManager(), MatchingConfig.Default) { }

        public MatchingService(PriorityManager priorityManager) : this(priorityManager, MatchingConfig.Default) { }

        public MatchingService(PriorityManager priorityManager, MatchingConfig config)
        {
            _priorityManager = priorityManager ?? throw new ArgumentNullException(nameof(priorityManager));
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Executes the matching algorithm to allocate supplies to needs.
        /// Operations are atomic—all changes are rolled back on failure.
        /// </summary>
        /// <param name="supplies">Available supplies inventory.</param>
        /// <param name="needs">List of needs to fulfill.</param>
        /// <returns>Result containing allocations and any errors.</returns>
        public MatchingResult ExecuteMatching(IList<Supply> supplies, IList<Need> needs)
        {
            if (supplies == null) throw new ArgumentNullException(nameof(supplies));
            if (needs == null) throw new ArgumentNullException(nameof(needs));

            var result = new MatchingResult();
            var transaction = new MatchingTransaction();

            try
            {
                // Get prioritized needs (using aging algorithm)
                var prioritizedNeeds = _priorityManager.GetPrioritizedList(needs, excludeFulfilled: true);

                if (!prioritizedNeeds.Any())
                {
                    result.Message = "No unfulfilled needs to process.";
                    return result;
                }

                // Track available supplies (working copies for atomic operation)
                var availableSupplies = supplies
                    .Where(s => !s.IsDeleted && !s.IsExpired && s.AllocatableQuantity > 0)
                    .ToList();

                // Process each need in priority order
                foreach (var need in prioritizedNeeds)
                {
                    if (need.IsFulfilled) continue;

                    var allocation = TryAllocateForNeed(need, availableSupplies, transaction);
                    if (allocation != null)
                    {
                        result.Allocations.Add(allocation);
                    }
                }

                // All allocations successful - commit the transaction
                transaction.Commit();
                result.Success = true;
                result.Message = $"Successfully created {result.Allocations.Count} allocations.";
            }
            catch (Exception ex)
            {
                // Rollback all changes on any error
                transaction.Rollback();
                result.Success = false;
                result.Message = $"Matching failed: {ex.Message}";
                result.Error = ex;
            }

            return result;
        }

        /// <summary>
        /// Attempts to allocate supplies for a single need.
        /// </summary>
        private Allocation? TryAllocateForNeed(
            Need need,
            List<Supply> availableSupplies,
            MatchingTransaction transaction)
        {
            var remainingQuantity = need.RemainingQuantity;
            if (remainingQuantity <= 0) return null;

            // Find matching supplies, scored by relevance
            var scoredSupplies = availableSupplies
                .Where(s => s.AllocatableQuantity > 0)
                .Select(s => new
                {
                    Supply = s,
                    Score = CalculateMatchScore(need, s)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();

            if (!scoredSupplies.Any())
            {
                return null;
            }

            var allocation = new Allocation
            {
                NeedId = need.Id,
                NeedTitle = need.Title,
                Priority = _priorityManager.GetEffectivePriorityLevel(need),
                RequestedQuantity = remainingQuantity,
                SupplyAllocations = new List<SupplyAllocation>()
            };

            var totalAllocated = 0;

            // Allocate from matching supplies (best matches first)
            foreach (var scored in scoredSupplies)
            {
                if (totalAllocated >= remainingQuantity) break;

                var supply = scored.Supply;
                var available = supply.AllocatableQuantity;
                var toAllocate = Math.Min(available, remainingQuantity - totalAllocated);

                if (toAllocate <= 0) continue;

                // Check minimum partial fulfillment threshold
                if (Config.AllowPartialFulfillment)
                {
                    var percentOfNeed = (double)toAllocate / need.QuantityRequired * 100;
                    if (percentOfNeed < Config.MinPartialFulfillmentPercent && totalAllocated == 0)
                    {
                        // Skip if this would be too small of a partial fulfillment
                        continue;
                    }
                }
                else if (toAllocate < remainingQuantity && totalAllocated == 0)
                {
                    // Partial fulfillment not allowed and can't fully satisfy
                    continue;
                }

                // Record the allocation
                var supplyAllocation = new SupplyAllocation
                {
                    SupplyId = supply.Id,
                    SupplyName = supply.Name,
                    QuantityAllocated = toAllocate,
                    MatchScore = scored.Score,
                    SourceLocation = supply.Location
                };

                allocation.SupplyAllocations.Add(supplyAllocation);

                // Track changes for atomic operation
                transaction.RecordSupplyChange(supply, toAllocate);
                transaction.RecordNeedChange(need, toAllocate);

                // Apply changes (will be rolled back if transaction fails)
                supply.Reserve(toAllocate);
                supply.DeductStock(toAllocate);

                totalAllocated += toAllocate;

                // Mark supply as exhausted if depleted
                if (supply.AllocatableQuantity <= 0)
                {
                    supplyAllocation.SupplyExhausted = true;
                }
            }

            if (totalAllocated > 0)
            {
                // Update the need's fulfilled quantity
                need.AddFulfilledQuantity(totalAllocated);

                allocation.AllocatedQuantity = totalAllocated;
                allocation.IsFullyFulfilled = need.IsFulfilled;
                allocation.FulfillmentPercentage = need.FulfillmentPercentage;

                return allocation;
            }

            return null;
        }

        /// <summary>
        /// Calculates a match score between a need and a supply.
        /// Higher score = better match.
        /// </summary>
        private double CalculateMatchScore(Need need, Supply supply)
        {
            double score = 0;

            // Category matching (most important)
            if (string.Equals(need.Category, supply.Category, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.0 * Config.CategoryMatchWeight;
            }
            else if (IsCategoryRelated(need.Category, supply.Category))
            {
                score += 0.5 * Config.CategoryMatchWeight;
            }
            else
            {
                // No category match - can't use this supply
                return 0;
            }

            // Proximity score (if locations available)
            var distance = CalculateDistance(need.Location, supply.Location);
            if (distance >= 0)
            {
                var proximityScore = Math.Max(0, 1 - (distance / Config.MaxProximityDistanceKm));
                score += proximityScore * Config.ProximityWeight;
            }

            // Bonus for supplies with more stock (prefer larger sources)
            var stockRatio = Math.Min(1.0, (double)supply.AllocatableQuantity / need.RemainingQuantity);
            score += stockRatio * 0.2;

            // Penalty for soon-to-expire supplies (use them first actually - invert)
            if (supply.IsExpiringSoon)
            {
                score += 0.1; // Slight bonus - use expiring supplies first
            }

            return score;
        }

        /// <summary>
        /// Checks if two categories are related (for flexible matching).
        /// </summary>
        private static bool IsCategoryRelated(string cat1, string cat2)
        {
            var relatedCategories = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Medical"] = new() { "Health", "FirstAid", "Medicine", "Pharmaceutical" },
                ["Food"] = new() { "Nutrition", "Supplies", "Rations", "Emergency" },
                ["Shelter"] = new() { "Housing", "Tents", "Blankets", "Bedding" },
                ["Water"] = new() { "Hydration", "Sanitation", "Hygiene" },
                ["Equipment"] = new() { "Tools", "Gear", "Machinery" }
            };

            foreach (var (key, related) in relatedCategories)
            {
                var allInGroup = new HashSet<string>(related, StringComparer.OrdinalIgnoreCase) { key };
                if (allInGroup.Contains(cat1) && allInGroup.Contains(cat2))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates distance between two locations using Haversine formula.
        /// Returns -1 if coordinates are not available.
        /// </summary>
        private static double CalculateDistance(Location loc1, Location loc2)
        {
            // If no coordinates, return -1 (proximity not applicable)
            if (loc1.Latitude == 0 && loc1.Longitude == 0) return -1;
            if (loc2.Latitude == 0 && loc2.Longitude == 0) return -1;

            const double earthRadiusKm = 6371.0;

            var lat1Rad = loc1.Latitude * Math.PI / 180;
            var lat2Rad = loc2.Latitude * Math.PI / 180;
            var deltaLat = (loc2.Latitude - loc1.Latitude) * Math.PI / 180;
            var deltaLon = (loc2.Longitude - loc1.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }
    }

    #region Transaction Support (Atomic Operations)

    /// <summary>
    /// Tracks changes for atomic rollback capability.
    /// </summary>
    internal class MatchingTransaction
    {
        private readonly List<(Supply Supply, int QuantityChange)> _supplyChanges = new();
        private readonly List<(Need Need, int QuantityChange)> _needChanges = new();
        private bool _committed = false;

        public void RecordSupplyChange(Supply supply, int quantity)
        {
            _supplyChanges.Add((supply, quantity));
        }

        public void RecordNeedChange(Need need, int quantity)
        {
            _needChanges.Add((need, quantity));
        }

        public void Commit()
        {
            _committed = true;
        }

        public void Rollback()
        {
            if (_committed) return;

            // Reverse supply changes
            foreach (var (supply, quantity) in _supplyChanges)
            {
                supply.AddStock(quantity);
                supply.ReleaseReservation(quantity);
            }

            // Reverse need changes (subtract fulfilled quantity)
            foreach (var (need, quantity) in _needChanges)
            {
                need.QuantityFulfilled = Math.Max(0, need.QuantityFulfilled - quantity);
                need.MarkAsUpdated();
            }
        }
    }

    #endregion

    #region Result Models

    /// <summary>
    /// Result of a matching operation.
    /// </summary>
    public class MatchingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Error { get; set; }
        public List<Allocation> Allocations { get; set; } = new();

        public int TotalAllocations => Allocations.Count;
        public int FullyFulfilledCount => Allocations.Count(a => a.IsFullyFulfilled);
        public int PartiallyFulfilledCount => Allocations.Count(a => !a.IsFullyFulfilled);
        public int TotalQuantityAllocated => Allocations.Sum(a => a.AllocatedQuantity);
    }

    /// <summary>
    /// Represents an allocation of supplies to a need.
    /// </summary>
    public class Allocation
    {
        public Guid NeedId { get; set; }
        public string NeedTitle { get; set; } = string.Empty;
        public PriorityLevel Priority { get; set; }
        public int RequestedQuantity { get; set; }
        public int AllocatedQuantity { get; set; }
        public bool IsFullyFulfilled { get; set; }
        public double FulfillmentPercentage { get; set; }
        public List<SupplyAllocation> SupplyAllocations { get; set; } = new();
        public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a supply used in an allocation.
    /// </summary>
    public class SupplyAllocation
    {
        public Guid SupplyId { get; set; }
        public string SupplyName { get; set; } = string.Empty;
        public int QuantityAllocated { get; set; }
        public double MatchScore { get; set; }
        public Location SourceLocation { get; set; }
        public bool SupplyExhausted { get; set; }
    }

    #endregion
}
