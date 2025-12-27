using DisasterLogistics.Core.Enums;

namespace DisasterLogistics.Core.Models
{
    /// <summary>
    /// Represents a shipment of supplies from source to destination.
    /// </summary>
    public class Shipment : BaseEntity
    {
        /// <summary>
        /// Gets or sets the shipment tracking number.
        /// </summary>
        public string TrackingNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the shipment.
        /// </summary>
        public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

        /// <summary>
        /// Gets or sets the priority level of this shipment.
        /// </summary>
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        /// <summary>
        /// Gets or sets the ID of the associated need being fulfilled.
        /// </summary>
        public Guid? NeedId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the supply being shipped.
        /// </summary>
        public Guid? SupplyId { get; set; }

        /// <summary>
        /// Gets or sets the origin location.
        /// </summary>
        public Location Origin { get; set; }

        /// <summary>
        /// Gets or sets the destination location.
        /// </summary>
        public Location Destination { get; set; }

        /// <summary>
        /// Gets or sets the quantity being shipped.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets the unit of measurement.
        /// </summary>
        public string Unit { get; set; } = "units";

        /// <summary>
        /// Gets or sets the item description.
        /// </summary>
        public string ItemDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scheduled dispatch date.
        /// </summary>
        public DateTime? ScheduledDispatchDate { get; set; }

        /// <summary>
        /// Gets or sets the actual dispatch date.
        /// </summary>
        public DateTime? ActualDispatchDate { get; set; }

        /// <summary>
        /// Gets or sets the estimated arrival date.
        /// </summary>
        public DateTime? EstimatedArrivalDate { get; set; }

        /// <summary>
        /// Gets or sets the actual delivery date.
        /// </summary>
        public DateTime? ActualDeliveryDate { get; set; }

        /// <summary>
        /// Gets or sets the carrier or transport provider name.
        /// </summary>
        public string Carrier { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the vehicle or transport identifier.
        /// </summary>
        public string VehicleId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the driver or responsible person name.
        /// </summary>
        public string DriverName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the driver contact information.
        /// </summary>
        public string DriverContact { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipient name.
        /// </summary>
        public string RecipientName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipient contact information.
        /// </summary>
        public string RecipientContact { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes or special instructions.
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the signature or proof of delivery.
        /// </summary>
        public string ProofOfDelivery { get; set; } = string.Empty;

        /// <summary>
        /// Gets whether the shipment is active (not delivered, cancelled, or failed).
        /// </summary>
        public bool IsActive => Status != ShipmentStatus.Delivered &&
                                Status != ShipmentStatus.Cancelled &&
                                Status != ShipmentStatus.Failed;

        /// <summary>
        /// Gets whether the shipment is delayed.
        /// </summary>
        public bool IsDelayed => EstimatedArrivalDate.HasValue &&
                                 DateTime.UtcNow > EstimatedArrivalDate.Value &&
                                 IsActive;

        /// <summary>
        /// Creates a new Shipment instance with auto-generated tracking number.
        /// </summary>
        public Shipment() : base()
        {
            TrackingNumber = GenerateTrackingNumber();
        }

        /// <summary>
        /// Creates a new Shipment with essential properties.
        /// </summary>
        public Shipment(Location origin, Location destination, int quantity, string itemDescription, PriorityLevel priority = PriorityLevel.Medium)
            : base()
        {
            TrackingNumber = GenerateTrackingNumber();
            Origin = origin;
            Destination = destination;
            Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be positive", nameof(quantity));
            ItemDescription = itemDescription ?? throw new ArgumentNullException(nameof(itemDescription));
            Priority = priority;
        }

        /// <summary>
        /// Generates a unique tracking number for the shipment.
        /// </summary>
        private static string GenerateTrackingNumber()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"DL-{timestamp}-{random}";
        }

        /// <summary>
        /// Updates the shipment status with timestamp tracking.
        /// </summary>
        /// <param name="newStatus">The new status to set.</param>
        /// <returns>True if the status transition is valid.</returns>
        public bool UpdateStatus(ShipmentStatus newStatus)
        {
            // Validate status transitions
            if (!IsValidStatusTransition(Status, newStatus))
                return false;

            Status = newStatus;
            MarkAsUpdated();

            // Update relevant timestamps
            switch (newStatus)
            {
                case ShipmentStatus.InTransit:
                    ActualDispatchDate ??= DateTime.UtcNow;
                    break;
                case ShipmentStatus.Delivered:
                    ActualDeliveryDate = DateTime.UtcNow;
                    break;
            }

            return true;
        }

        /// <summary>
        /// Validates if a status transition is allowed.
        /// </summary>
        private static bool IsValidStatusTransition(ShipmentStatus from, ShipmentStatus to)
        {
            // Allow any transition if cancelled or failed
            if (to == ShipmentStatus.Cancelled || to == ShipmentStatus.Failed)
                return from != ShipmentStatus.Delivered;

            // Define valid forward transitions
            return (from, to) switch
            {
                (ShipmentStatus.Pending, ShipmentStatus.Approved) => true,
                (ShipmentStatus.Approved, ShipmentStatus.InTransit) => true,
                (ShipmentStatus.InTransit, ShipmentStatus.AtDistributionCenter) => true,
                (ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery) => true,
                (ShipmentStatus.AtDistributionCenter, ShipmentStatus.OutForDelivery) => true,
                (ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered) => true,
                (ShipmentStatus.InTransit, ShipmentStatus.Delivered) => true,
                _ => false
            };
        }
    }
}
