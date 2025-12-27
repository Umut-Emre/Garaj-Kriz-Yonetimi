namespace DisasterLogistics.Core.Enums
{
    /// <summary>
    /// Defines priority levels for logistics operations during disaster response.
    /// </summary>
    public enum PriorityLevel
    {
        /// <summary>
        /// Life-threatening situations requiring immediate action.
        /// </summary>
        Critical = 0,

        /// <summary>
        /// Urgent needs that should be addressed within hours.
        /// </summary>
        High = 1,

        /// <summary>
        /// Important but not immediately life-threatening needs.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// Non-urgent needs that can be scheduled for later fulfillment.
        /// </summary>
        Low = 3
    }
}
