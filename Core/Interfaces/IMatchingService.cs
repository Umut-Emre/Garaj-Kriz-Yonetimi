using DisasterLogistics.Core.Enums;
using DisasterLogistics.Core.Models;

namespace DisasterLogistics.Core.Interfaces
{
    /// <summary>
    /// Interface for matching service operations.
    /// </summary>
    public interface IMatchingService
    {
        /// <summary>
        /// Executes the matching algorithm to allocate supplies to needs.
        /// </summary>
        Services.MatchingResult ExecuteMatching(IList<Supply> supplies, IList<Need> needs);
    }
}
