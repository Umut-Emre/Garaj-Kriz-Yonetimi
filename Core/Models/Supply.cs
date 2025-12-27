using DisasterLogistics.Core.Enums;

namespace DisasterLogistics.Core.Models
{
    /// <summary>
    /// Represents available supplies or resources for disaster relief.
    /// </summary>
    public class Supply : BaseEntity
    {
        /// <summary>
        /// Gets or sets the name of the supply item.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the supply.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the supply (e.g., Medical, Food, Equipment).
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total quantity available.
        /// </summary>
        public int QuantityAvailable { get; set; }

        /// <summary>
        /// Gets or sets the quantity currently reserved for shipments.
        /// </summary>
        public int QuantityReserved { get; set; }

        /// <summary>
        /// Gets or sets the unit of measurement.
        /// </summary>
        public string Unit { get; set; } = "units";

        /// <summary>
        /// Gets or sets the storage location of this supply.
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// Gets or sets the supplier or donor name.
        /// </summary>
        public string SupplierName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expiration date if applicable.
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the minimum stock level for alerts.
        /// </summary>
        public int MinimumStockLevel { get; set; }

        /// <summary>
        /// Gets or sets the SKU or internal tracking code.
        /// </summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the condition of the supply (New, Used, Damaged).
        /// </summary>
        public string Condition { get; set; } = "New";

        /// <summary>
        /// Gets the quantity that can be allocated (available minus reserved).
        /// </summary>
        public int AllocatableQuantity => Math.Max(0, QuantityAvailable - QuantityReserved);

        /// <summary>
        /// Gets whether the supply is below minimum stock level.
        /// </summary>
        public bool IsBelowMinimumStock => AllocatableQuantity < MinimumStockLevel;

        /// <summary>
        /// Gets whether the supply is expired.
        /// </summary>
        public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.UtcNow;

        /// <summary>
        /// Gets whether the supply is expiring soon (within 7 days).
        /// </summary>
        public bool IsExpiringSoon => ExpirationDate.HasValue &&
            ExpirationDate.Value >= DateTime.UtcNow &&
            ExpirationDate.Value <= DateTime.UtcNow.AddDays(7);

        /// <summary>
        /// Creates a new Supply instance.
        /// </summary>
        public Supply() : base() { }

        /// <summary>
        /// Creates a new Supply with essential properties.
        /// </summary>
        public Supply(string name, string category, int quantityAvailable, Location location)
            : base()
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            QuantityAvailable = quantityAvailable >= 0 ? quantityAvailable : throw new ArgumentException("Quantity cannot be negative", nameof(quantityAvailable));
            Location = location;
        }

        /// <summary>
        /// Reserves a quantity of this supply for a shipment.
        /// </summary>
        /// <param name="quantity">The quantity to reserve.</param>
        /// <returns>True if reservation was successful.</returns>
        public bool Reserve(int quantity)
        {
            if (quantity <= 0 || quantity > AllocatableQuantity)
                return false;

            QuantityReserved += quantity;
            MarkAsUpdated();
            return true;
        }

        /// <summary>
        /// Releases a previously reserved quantity.
        /// </summary>
        /// <param name="quantity">The quantity to release.</param>
        /// <returns>True if release was successful.</returns>
        public bool ReleaseReservation(int quantity)
        {
            if (quantity <= 0 || quantity > QuantityReserved)
                return false;

            QuantityReserved -= quantity;
            MarkAsUpdated();
            return true;
        }

        /// <summary>
        /// Deducts quantity from available stock (after shipment dispatch).
        /// </summary>
        /// <param name="quantity">The quantity to deduct.</param>
        /// <returns>True if deduction was successful.</returns>
        public bool DeductStock(int quantity)
        {
            if (quantity <= 0 || quantity > QuantityAvailable)
                return false;

            QuantityAvailable -= quantity;
            if (QuantityReserved >= quantity)
                QuantityReserved -= quantity;

            MarkAsUpdated();
            return true;
        }

        /// <summary>
        /// Adds stock to this supply.
        /// </summary>
        /// <param name="quantity">The quantity to add.</param>
        /// <returns>True if addition was successful.</returns>
        public bool AddStock(int quantity)
        {
            if (quantity <= 0) return false;

            QuantityAvailable += quantity;
            MarkAsUpdated();
            return true;
        }

        /// <summary>
        /// Resupplies the warehouse with additional stock and resets reservations.
        /// </summary>
        /// <param name="quantity">The quantity to add.</param>
        /// <returns>True if resupply was successful.</returns>
        public bool Resupply(int quantity)
        {
            if (quantity <= 0) return false;

            QuantityAvailable += quantity;
            QuantityReserved = 0; // Reset reservations for fresh matching
            MarkAsUpdated();
            return true;
        }

        /// <summary>
        /// Resets the reservation quantity to zero for fresh matching.
        /// </summary>
        public void ResetReservation()
        {
            QuantityReserved = 0;
        }
    }
}
