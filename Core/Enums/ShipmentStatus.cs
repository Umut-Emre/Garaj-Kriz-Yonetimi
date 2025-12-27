namespace DisasterLogistics.Core.Enums
{
    /// <summary>
    /// Represents the current status of a shipment in the logistics pipeline.
    /// </summary>
    public enum ShipmentStatus
    {
        /// <summary>
        /// Shipment has been created but not yet processed.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Shipment has been approved and is being prepared.
        /// </summary>
        Approved = 1,

        /// <summary>
        /// Shipment is currently in transit to destination.
        /// </summary>
        InTransit = 2,

        /// <summary>
        /// Shipment has arrived at intermediate distribution point.
        /// </summary>
        AtDistributionCenter = 3,

        /// <summary>
        /// Shipment is out for final delivery.
        /// </summary>
        OutForDelivery = 4,

        /// <summary>
        /// Shipment has been successfully delivered.
        /// </summary>
        Delivered = 5,

        /// <summary>
        /// Shipment has been cancelled.
        /// </summary>
        Cancelled = 6,

        /// <summary>
        /// Shipment delivery failed and needs rescheduling.
        /// </summary>
        Failed = 7
    }
}
