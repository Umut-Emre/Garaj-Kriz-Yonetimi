using DisasterLogistics.Core.Enums;
using DisasterLogistics.Core.Models;
using DisasterLogistics.Core.Services;

namespace DisasterLogistics.Core.Interfaces
{
    /// <summary>
    /// Interface for priority management services.
    /// Follows Dependency Inversion Principle for testability.
    /// </summary>
    public interface IPriorityManager
    {
        /// <summary>
        /// Calculates the effective priority score for a need.
        /// </summary>
        double CalculateEffectivePriority(Need need);

        /// <summary>
        /// Gets the effective priority level after aging.
        /// </summary>
        PriorityLevel GetEffectivePriorityLevel(Need need);

        /// <summary>
        /// Creates a priority queue from needs.
        /// </summary>
        PriorityQueue<Need, double> CreatePriorityQueue(IEnumerable<Need> needs, bool excludeFulfilled = true);

        /// <summary>
        /// Returns a prioritized list of needs.
        /// </summary>
        List<Need> GetPrioritizedList(IEnumerable<Need> needs, bool excludeFulfilled = true);

        /// <summary>
        /// Gets detailed priority information.
        /// </summary>
        PriorityInfo GetPriorityInfo(Need need);
    }
}
