using System.Text.Json;
using System.Text.Json.Serialization;
using DisasterLogistics.Core.Interfaces;
using DisasterLogistics.Core.Models;

namespace DisasterLogistics.Infrastructure.Services
{
    /// <summary>
    /// Implementation of IStorageService using JSON file persistence.
    /// Follows the Single Responsibility Principle (SRP) - handles JSON serialization only.
    /// </summary>
    /// <typeparam name="T">The type of entity to persist, must be a BaseEntity.</typeparam>
    public class JsonStorageService<T> : IStorageService<T> where T : BaseEntity
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        /// <summary>
        /// Creates a new JsonStorageService instance.
        /// </summary>
        /// <param name="fileName">The name of the JSON file (without path).</param>
        /// <param name="dataDirectory">Optional directory path. Defaults to "Data" in current directory.</param>
        public JsonStorageService(string fileName, string? dataDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            dataDirectory ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

            // Ensure directory exists
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            _filePath = Path.Combine(dataDirectory, fileName);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };
        }

        /// <inheritdoc />
        public async Task<bool> SaveAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                var json = JsonSerializer.Serialize(items.ToList(), _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                // In production, use proper logging (ILogger)
                Console.Error.WriteLine($"Error saving to {_filePath}: {ex.Message}");
                return false;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<T>> LoadAsync(CancellationToken cancellationToken = default)
        {
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_filePath))
                {
                    return Enumerable.Empty<T>();
                }

                var json = await File.ReadAllTextAsync(_filePath, cancellationToken);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return Enumerable.Empty<T>();
                }

                var items = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
                return items ?? Enumerable.Empty<T>();
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error deserializing {_filePath}: {ex.Message}");
                return Enumerable.Empty<T>();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading from {_filePath}: {ex.Message}");
                return Enumerable.Empty<T>();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveItemAsync(T item, CancellationToken cancellationToken = default)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var items = (await LoadAsync(cancellationToken)).ToList();

            var existingIndex = items.FindIndex(x => x.Id == item.Id);
            if (existingIndex >= 0)
            {
                items[existingIndex] = item;
            }
            else
            {
                items.Add(item);
            }

            return await SaveAsync(items, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var items = (await LoadAsync(cancellationToken)).ToList();

            var removedCount = items.RemoveAll(x => x.Id == id);

            if (removedCount == 0)
                return false;

            return await SaveAsync(items, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var items = await LoadAsync(cancellationToken);
            return items.FirstOrDefault(x => x.Id == id);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var items = await LoadAsync(cancellationToken);
            return items.Any(x => x.Id == id);
        }

        /// <inheritdoc />
        public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
        {
            return await SaveAsync(Enumerable.Empty<T>(), cancellationToken);
        }
    }
}
