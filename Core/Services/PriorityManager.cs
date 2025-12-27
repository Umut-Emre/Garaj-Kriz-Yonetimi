using DisasterLogistics.Core.Enums;
using DisasterLogistics.Core.Models;

namespace DisasterLogistics.Core.Services
{
    /// <summary>
    /// Manages priority queuing for needs with an aging algorithm to prevent starvation.
    /// 
    /// TIME COMPLEXITY ANALYSIS:
    /// -------------------------
    /// .NET's PriorityQueue uses a binary min-heap internally.
    /// 
    /// - Enqueue: O(log n) - Element is added and bubbled up to maintain heap property
    /// - Dequeue: O(log n) - Root removed, last element moved to root and bubbled down
    /// - Peek:    O(1)     - Simply returns the root element
    /// - Count:   O(1)     - Maintained as a field
    /// 
    /// For processing n needs:
    /// - Building the priority queue: O(n log n)
    /// - Dequeuing all elements: O(n log n)
    /// - Total: O(n log n)
    /// 
    /// This is more efficient than sorting (which is also O(n log n)) when you need
    /// to dynamically add/remove elements, as each operation is O(log n) vs O(n) for
    /// maintaining a sorted list.
    /// </summary>
    public class PriorityManager
    {
        /// <summary>
        /// Configuration for the aging algorithm.
        /// </summary>
        public class AgingConfig
        {
            /// <summary>
            /// Hours after which Low priority starts aging toward Medium.
            /// </summary>
            public double LowToMediumHours { get; init; } = 24.0;

            /// <summary>
            /// Hours after which Medium priority starts aging toward High.
            /// </summary>
            public double MediumToHighHours { get; init; } = 12.0;

            /// <summary>
            /// Hours after which High priority starts aging toward Critical.
            /// </summary>
            public double HighToCriticalHours { get; init; } = 6.0;

            /// <summary>
            /// Default configuration with standard aging thresholds.
            /// </summary>
            public static AgingConfig Default => new();

            /// <summary>
            /// Aggressive aging for emergency scenarios.
            /// </summary>
            public static AgingConfig Emergency => new()
            {
                LowToMediumHours = 6.0,
                MediumToHighHours = 3.0,
                HighToCriticalHours = 1.0
            };
        }

        private readonly AgingConfig _config;

        /// <summary>
        /// Creates a PriorityManager with default aging configuration.
        /// </summary>
        public PriorityManager() : this(AgingConfig.Default) { }

        /// <summary>
        /// Creates a PriorityManager with custom aging configuration.
        /// </summary>
        /// <param name="config">The aging configuration to use.</param>
        public PriorityManager(AgingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Calculates the effective priority considering the aging algorithm.
        /// Lower numeric values = higher priority (Critical = 0 is highest).
        /// </summary>
        /// <param name="need">The need to evaluate.</param>
        /// <returns>A priority score where lower is more urgent.</returns>
        public double CalculateEffectivePriority(Need need)
        {
            var hoursWaiting = (DateTime.UtcNow - need.CreatedAt).TotalHours;
            var basePriority = (int)need.Priority;

            // Calculate aging factor based on wait time
            double agingBonus = need.Priority switch
            {
                PriorityLevel.Low => CalculateAgingBonus(hoursWaiting, _config.LowToMediumHours, 3), // Can drop up to 3 levels
                PriorityLevel.Medium => CalculateAgingBonus(hoursWaiting, _config.MediumToHighHours, 2), // Can drop up to 2 levels
                PriorityLevel.High => CalculateAgingBonus(hoursWaiting, _config.HighToCriticalHours, 1), // Can drop up to 1 level
                PriorityLevel.Critical => 0, // Already highest priority
                _ => 0
            };

            // Additional urgency factors
            double deadlineBonus = 0;
            if (need.Deadline.HasValue)
            {
                var hoursUntilDeadline = (need.Deadline.Value - DateTime.UtcNow).TotalHours;
                if (hoursUntilDeadline <= 0)
                {
                    deadlineBonus = 2.0; // Past deadline - major urgency boost
                }
                else if (hoursUntilDeadline <= 6)
                {
                    deadlineBonus = 1.0; // Deadline approaching
                }
                else if (hoursUntilDeadline <= 24)
                {
                    deadlineBonus = 0.5;
                }
            }

            // Fulfillment factor - more urgent if almost complete
            double fulfillmentBonus = 0;
            if (need.FulfillmentPercentage >= 80 && !need.IsFulfilled)
            {
                fulfillmentBonus = 0.5; // Almost done, prioritize completion
            }

            // Final effective priority (lower = more urgent)
            var effectivePriority = basePriority - agingBonus - deadlineBonus - fulfillmentBonus;

            // Clamp to valid range (0 = Critical, 3 = Low)
            return Math.Clamp(effectivePriority, 0.0, 3.0);
        }

        /// <summary>
        /// Calculates the aging bonus based on hours waiting.
        /// Uses a logarithmic curve for smooth priority escalation.
        /// </summary>
        private static double CalculateAgingBonus(double hoursWaiting, double thresholdHours, int maxLevels)
        {
            if (hoursWaiting <= 0 || thresholdHours <= 0)
                return 0;

            // After threshold, each additional period adds diminishing bonus
            var periodsElapsed = hoursWaiting / thresholdHours;

            if (periodsElapsed < 1)
                return 0;

            // Logarithmic scaling: log2(periodsElapsed) gives smooth escalation
            var agingBonus = Math.Log2(periodsElapsed + 1);

            return Math.Min(agingBonus, maxLevels);
        }

        /// <summary>
        /// Gets the effective priority level as an enum (for display purposes).
        /// </summary>
        public PriorityLevel GetEffectivePriorityLevel(Need need)
        {
            var effectivePriority = CalculateEffectivePriority(need);
            return effectivePriority switch
            {
                < 0.5 => PriorityLevel.Critical,
                < 1.5 => PriorityLevel.High,
                < 2.5 => PriorityLevel.Medium,
                _ => PriorityLevel.Low
            };
        }

        /// <summary>
        /// Creates a priority queue from a list of needs using the aging algorithm.
        /// 
        /// Uses .NET 6+ PriorityQueue with a min-heap (lower priority value = dequeued first).
        /// </summary>
        /// <param name="needs">The list of needs to prioritize.</param>
        /// <param name="excludeFulfilled">Whether to exclude already-fulfilled needs.</param>
        /// <returns>A priority queue ordered by effective priority.</returns>
        public PriorityQueue<Need, double> CreatePriorityQueue(
            IEnumerable<Need> needs,
            bool excludeFulfilled = true)
        {
            if (needs == null)
                throw new ArgumentNullException(nameof(needs));

            // PriorityQueue<TElement, TPriority> - lower priority values are dequeued first
            var queue = new PriorityQueue<Need, double>();

            foreach (var need in needs)
            {
                // Skip fulfilled or deleted needs if requested
                if (excludeFulfilled && (need.IsFulfilled || need.IsDeleted))
                    continue;

                var effectivePriority = CalculateEffectivePriority(need);

                // Enqueue: O(log n) - binary heap insertion
                queue.Enqueue(need, effectivePriority);
            }

            return queue;
        }

        /// <summary>
        /// Returns a prioritized list of needs (convenience method).
        /// </summary>
        /// <param name="needs">The needs to prioritize.</param>
        /// <param name="excludeFulfilled">Whether to exclude fulfilled needs.</param>
        /// <returns>A list ordered by effective priority (most urgent first).</returns>
        public List<Need> GetPrioritizedList(IEnumerable<Need> needs, bool excludeFulfilled = true)
        {
            var queue = CreatePriorityQueue(needs, excludeFulfilled);
            var result = new List<Need>(queue.Count);

            // Dequeue all: O(n log n) total
            while (queue.Count > 0)
            {
                // Dequeue: O(log n) - heap extraction
                result.Add(queue.Dequeue());
            }

            return result;
        }

        /// <summary>
        /// Gets detailed priority information for a need.
        /// </summary>
        public PriorityInfo GetPriorityInfo(Need need)
        {
            var hoursWaiting = (DateTime.UtcNow - need.CreatedAt).TotalHours;
            var effectivePriority = CalculateEffectivePriority(need);
            var effectiveLevel = GetEffectivePriorityLevel(need);

            return new PriorityInfo
            {
                NeedId = need.Id,
                OriginalPriority = need.Priority,
                EffectivePriority = effectiveLevel,
                PriorityScore = effectivePriority,
                HoursWaiting = hoursWaiting,
                WasAged = effectiveLevel != need.Priority,
                AgingLevels = (int)need.Priority - (int)effectiveLevel
            };
        }
    }

    /// <summary>
    /// Contains detailed priority calculation information.
    /// </summary>
    public class PriorityInfo
    {
        public Guid NeedId { get; init; }
        public PriorityLevel OriginalPriority { get; init; }
        public PriorityLevel EffectivePriority { get; init; }
        public double PriorityScore { get; init; }
        public double HoursWaiting { get; init; }
        public bool WasAged { get; init; }
        public int AgingLevels { get; init; }

        public override string ToString()
        {
            var aged = WasAged ? $" (aged +{AgingLevels} levels)" : "";
            return $"Priority: {EffectivePriority}{aged} | Score: {PriorityScore:F2} | Waiting: {HoursWaiting:F1}h";
        }
    }
}
