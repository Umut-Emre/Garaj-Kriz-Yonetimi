using DisasterLogistics.Core.Interfaces;
using DisasterLogistics.Core.Models;
using DisasterLogistics.Infrastructure.Services;

namespace DisasterLogistics.Infrastructure
{
    /// <summary>
    /// Factory class for creating storage service instances.
    /// Implements the Factory Pattern for dependency management.
    /// </summary>
    public static class ServiceFactory
    {
        private static string? _dataDirectory;

        /// <summary>
        /// Configures the default data directory for storage services.
        /// </summary>
        /// <param name="dataDirectory">The directory path for data storage.</param>
        public static void Configure(string dataDirectory)
        {
            _dataDirectory = dataDirectory;

            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        /// <summary>
        /// Creates a storage service for Need entities.
        /// </summary>
        public static IStorageService<Need> CreateNeedStorage()
        {
            return new JsonStorageService<Need>("needs.json", _dataDirectory);
        }

        /// <summary>
        /// Creates a storage service for Supply entities.
        /// </summary>
        public static IStorageService<Supply> CreateSupplyStorage()
        {
            return new JsonStorageService<Supply>("supplies.json", _dataDirectory);
        }

        /// <summary>
        /// Creates a storage service for Shipment entities.
        /// </summary>
        public static IStorageService<Shipment> CreateShipmentStorage()
        {
            return new JsonStorageService<Shipment>("shipments.json", _dataDirectory);
        }

        /// <summary>
        /// Creates a generic storage service for any BaseEntity type.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="fileName">The JSON file name.</param>
        public static IStorageService<T> CreateStorage<T>(string fileName) where T : BaseEntity
        {
            return new JsonStorageService<T>(fileName, _dataDirectory);
        }
    }
}
