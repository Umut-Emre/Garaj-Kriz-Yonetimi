using DisasterLogistics.Core.Enums;

namespace DisasterLogistics.Core.Models
{
    /// <summary>
    /// Represents a need or request for resources during a disaster.
    /// </summary>
    public class Need : BaseEntity
    {
        /// <summary>
        /// Gets or sets the title/name of the need.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detailed description of the need.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the need (e.g., Medical, Food, Shelter).
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the priority level of this need.
        /// </summary>
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        /// <summary>
        /// Gets or sets the quantity required.
        /// </summary>
        public int QuantityRequired { get; set; }

        /// <summary>
        /// Gets or sets the unit of measurement (e.g., units, kg, liters).
        /// </summary>
        public string Unit { get; set; } = "units";

        /// <summary>
        /// Gets or sets the quantity that has been fulfilled.
        /// </summary>
        public int QuantityFulfilled { get; set; }

        /// <summary>
        /// Gets or sets the location where this need exists.
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// Gets or sets the name of the requester or organization.
        /// </summary>
        public string RequesterName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the contact information for the requester.
        /// </summary>
        public string ContactInfo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the deadline by which this need should be fulfilled.
        /// </summary>
        public DateTime? Deadline { get; set; }

        /// <summary>
        /// Gets or sets notes or additional information.
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets whether this need has been fully fulfilled.
        /// </summary>
        public bool IsFulfilled => QuantityFulfilled >= QuantityRequired;

        /// <summary>
        /// Gets the fulfillment percentage (0-100).
        /// </summary>
        public double FulfillmentPercentage =>
            QuantityRequired > 0 ? Math.Min(100, (double)QuantityFulfilled / QuantityRequired * 100) : 0;

        /// <summary>
        /// Gets the remaining quantity needed.
        /// </summary>
        public int RemainingQuantity => Math.Max(0, QuantityRequired - QuantityFulfilled);

        /// <summary>
        /// Creates a new Need instance.
        /// </summary>
        public Need() : base() { }

        /// <summary>
        /// Creates a new Need with essential properties.
        /// </summary>
        public Need(string title, string category, int quantityRequired, Location location, PriorityLevel priority = PriorityLevel.Medium)
            : base()
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            QuantityRequired = quantityRequired > 0 ? quantityRequired : throw new ArgumentException("Quantity must be positive", nameof(quantityRequired));
            Location = location;
            Priority = priority;
        }

        /// <summary>
        /// Adds fulfilled quantity to this need.
        /// </summary>
        /// <param name="quantity">The quantity to add.</param>
        /// <returns>True if the operation was successful.</returns>
        public bool AddFulfilledQuantity(int quantity)
        {
            if (quantity <= 0) return false;

            QuantityFulfilled = Math.Min(QuantityRequired, QuantityFulfilled + quantity);
            MarkAsUpdated();
            return true;
        }
    }
}
