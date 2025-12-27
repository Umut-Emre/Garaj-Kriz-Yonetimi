namespace DisasterLogistics.Core.Interfaces
{
    /// <summary>
    /// Defines a generic contract for data persistence operations.
    /// Follows the Interface Segregation Principle (ISP) and Dependency Inversion Principle (DIP).
    /// </summary>
    /// <typeparam name="T">The type of entity to persist.</typeparam>
    public interface IStorageService<T> where T : class
    {
        /// <summary>
        /// Saves a collection of items to storage.
        /// </summary>
        /// <param name="items">The items to save.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if the save was successful.</returns>
        Task<bool> SaveAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads all items from storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>A collection of items, or empty if none exist.</returns>
        Task<IEnumerable<T>> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a single item (adds or updates).
        /// </summary>
        /// <param name="item">The item to save.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if the save was successful.</returns>
        Task<bool> SaveItemAsync(T item, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an item from storage by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the item to delete.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if the deletion was successful.</returns>
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an item by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the item to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>The item if found, or null.</returns>
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an item with the specified identifier exists.
        /// </summary>
        /// <param name="id">The identifier to check.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if the item exists.</returns>
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all items from storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if the clear was successful.</returns>
        Task<bool> ClearAsync(CancellationToken cancellationToken = default);
    }
}
