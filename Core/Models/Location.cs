namespace DisasterLogistics.Core.Models
{
    /// <summary>
    /// Represents a geographic location for disaster logistics operations.
    /// </summary>
    public readonly struct Location : IEquatable<Location>
    {
        /// <summary>
        /// Gets the latitude coordinate.
        /// </summary>
        public double Latitude { get; init; }

        /// <summary>
        /// Gets the longitude coordinate.
        /// </summary>
        public double Longitude { get; init; }

        /// <summary>
        /// Gets the human-readable address or description.
        /// </summary>
        public string Address { get; init; }

        /// <summary>
        /// Gets the city or district name.
        /// </summary>
        public string City { get; init; }

        /// <summary>
        /// Gets the region or province name.
        /// </summary>
        public string Region { get; init; }

        /// <summary>
        /// Creates a new Location instance.
        /// </summary>
        public Location(double latitude, double longitude, string address = "", string city = "", string region = "")
        {
            Latitude = latitude;
            Longitude = longitude;
            Address = address ?? string.Empty;
            City = city ?? string.Empty;
            Region = region ?? string.Empty;
        }

        /// <summary>
        /// Creates a Location from an address string only.
        /// </summary>
        public static Location FromAddress(string address)
        {
            return new Location(0, 0, address);
        }

        /// <summary>
        /// Creates a Location from coordinates.
        /// </summary>
        public static Location FromCoordinates(double latitude, double longitude)
        {
            return new Location(latitude, longitude);
        }

        /// <summary>
        /// Creates a Location from coordinates with address.
        /// </summary>
        public static Location FromCoordinates(double latitude, double longitude, string address)
        {
            return new Location(latitude, longitude, address);
        }

        public bool Equals(Location other)
        {
            return Latitude.Equals(other.Latitude) &&
                   Longitude.Equals(other.Longitude) &&
                   Address == other.Address;
        }

        public override bool Equals(object? obj)
        {
            return obj is Location other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Latitude, Longitude, Address);
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Address))
                return Address;

            return $"({Latitude:F6}, {Longitude:F6})";
        }

        public static bool operator ==(Location left, Location right) => left.Equals(right);
        public static bool operator !=(Location left, Location right) => !left.Equals(right);
    }
}
