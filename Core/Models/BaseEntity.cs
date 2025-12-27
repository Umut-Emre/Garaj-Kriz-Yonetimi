namespace DisasterLogistics.Core.Models
{
    /// <summary>
    /// Base class for all domain entities providing common properties.
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Gets the unique identifier for this entity.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets the timestamp when this entity was created.
        /// </summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        /// Gets or sets the timestamp when this entity was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets whether this entity is marked as deleted (soft delete).
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Initializes a new instance of the BaseEntity class.
        /// </summary>
        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            IsDeleted = false;
        }

        /// <summary>
        /// Initializes a BaseEntity with specific values (for deserialization).
        /// </summary>
        protected BaseEntity(Guid id, DateTime createdAt, DateTime updatedAt)
        {
            Id = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            IsDeleted = false;
        }

        /// <summary>
        /// Updates the UpdatedAt timestamp to current UTC time.
        /// </summary>
        public void MarkAsUpdated()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Performs a soft delete on this entity.
        /// </summary>
        public void MarkAsDeleted()
        {
            IsDeleted = true;
            MarkAsUpdated();
        }
    }
}
