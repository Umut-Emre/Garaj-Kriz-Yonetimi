using System.Text.Json;
using System.Text.Json.Serialization;
using DisasterLogistics.Core.Enums;

namespace DisasterLogistics.Core.Services
{
    /// <summary>
    /// Audit event types for logging operations.
    /// </summary>
    public enum AuditEventType
    {
        NeedCreated,
        NeedUpdated,
        NeedFulfilled,
        SupplyCreated,
        SupplyUpdated,
        SupplyDepleted,
        MatchMade,
        MatchFailed,
        ShipmentCreated,
        ShipmentDispatched,
        ShipmentDelivered,
        ShipmentCancelled,
        PanicModeTriggered,
        SystemAlert,
        UserAction
    }

    /// <summary>
    /// Represents a single audit log entry.
    /// </summary>
    public class AuditLogEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public AuditEventType EventType { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? EntityId { get; init; }
        public string? EntityType { get; init; }
        public string? UserId { get; init; }
        public PriorityLevel? Priority { get; init; }
        public Dictionary<string, object>? Metadata { get; init; }

        public override string ToString()
        {
            var priority = Priority.HasValue ? $"[{Priority}] " : "";
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {priority}{EventType}: {Message}";
        }
    }

    /// <summary>
    /// Service for recording audit trail of all system operations.
    /// Thread-safe implementation for concurrent access.
    /// </summary>
    public class AuditLogger
    {
        private readonly List<AuditLogEntry> _logs = new();
        private readonly object _lock = new();
        private readonly string? _logFilePath;
        private readonly int _maxInMemoryLogs;

        /// <summary>
        /// Event raised when a new log entry is added.
        /// </summary>
        public event Action<AuditLogEntry>? OnLogAdded;

        /// <summary>
        /// Creates an AuditLogger with optional file persistence.
        /// </summary>
        /// <param name="logFilePath">Optional path to persist logs to JSON file.</param>
        /// <param name="maxInMemoryLogs">Maximum logs to keep in memory (default 1000).</param>
        public AuditLogger(string? logFilePath = null, int maxInMemoryLogs = 1000)
        {
            _logFilePath = logFilePath;
            _maxInMemoryLogs = maxInMemoryLogs;

            if (!string.IsNullOrEmpty(_logFilePath))
            {
                var dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        /// <summary>
        /// Logs an audit event.
        /// </summary>
        public void Log(AuditEventType eventType, string message, 
            string? entityId = null, string? entityType = null,
            PriorityLevel? priority = null, Dictionary<string, object>? metadata = null)
        {
            var entry = new AuditLogEntry
            {
                EventType = eventType,
                Message = message,
                EntityId = entityId,
                EntityType = entityType,
                Priority = priority,
                Metadata = metadata
            };

            lock (_lock)
            {
                _logs.Add(entry);

                // Trim old logs if exceeding max
                if (_logs.Count > _maxInMemoryLogs)
                {
                    _logs.RemoveRange(0, _logs.Count - _maxInMemoryLogs);
                }
            }

            // Persist to file if configured
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                AppendToFile(entry);
            }

            // Raise event for subscribers
            OnLogAdded?.Invoke(entry);
        }

        /// <summary>
        /// Logs a match operation.
        /// </summary>
        public void LogMatch(Guid needId, string needTitle, int quantity, bool partial, PriorityLevel priority)
        {
            var status = partial ? "PARTIAL" : "FULL";
            Log(AuditEventType.MatchMade,
                $"Match [{status}]: {quantity} units allocated to '{needTitle}'",
                entityId: needId.ToString(),
                entityType: "Need",
                priority: priority,
                metadata: new Dictionary<string, object>
                {
                    ["quantity"] = quantity,
                    ["partial"] = partial
                });
        }

        /// <summary>
        /// Logs a shipment dispatch.
        /// </summary>
        public void LogShipmentDispatch(Guid shipmentId, string trackingNumber, string destination, PriorityLevel priority)
        {
            Log(AuditEventType.ShipmentDispatched,
                $"Shipment {trackingNumber} dispatched to {destination}",
                entityId: shipmentId.ToString(),
                entityType: "Shipment",
                priority: priority);
        }

        /// <summary>
        /// Logs a panic mode trigger.
        /// </summary>
        public void LogPanicMode(int criticalNeedsCount)
        {
            Log(AuditEventType.PanicModeTriggered,
                $"⚠️ PANIC MODE: {criticalNeedsCount} critical need(s) unmatched for over 1 hour!",
                priority: PriorityLevel.Critical);
        }

        /// <summary>
        /// Gets recent log entries.
        /// </summary>
        public List<AuditLogEntry> GetRecentLogs(int count = 50)
        {
            lock (_lock)
            {
                return _logs.TakeLast(count).Reverse().ToList();
            }
        }

        /// <summary>
        /// Gets logs filtered by event type.
        /// </summary>
        public List<AuditLogEntry> GetLogsByType(AuditEventType eventType, int count = 50)
        {
            lock (_lock)
            {
                return _logs.Where(l => l.EventType == eventType)
                           .TakeLast(count)
                           .Reverse()
                           .ToList();
            }
        }

        /// <summary>
        /// Gets logs within a time range.
        /// </summary>
        public List<AuditLogEntry> GetLogsByTimeRange(DateTime from, DateTime to)
        {
            lock (_lock)
            {
                return _logs.Where(l => l.Timestamp >= from && l.Timestamp <= to)
                           .OrderByDescending(l => l.Timestamp)
                           .ToList();
            }
        }

        private void AppendToFile(AuditLogEntry entry)
        {
            try
            {
                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    Converters = { new JsonStringEnumConverter() }
                });
                File.AppendAllText(_logFilePath!, json + Environment.NewLine);
            }
            catch
            {
                // Silently fail file writes - don't crash the system
            }
        }

        /// <summary>
        /// Exports all logs to a JSON file.
        /// </summary>
        public async Task ExportLogsAsync(string filePath)
        {
            List<AuditLogEntry> snapshot;
            lock (_lock)
            {
                snapshot = _logs.ToList();
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var json = JsonSerializer.Serialize(snapshot, options);
            await File.WriteAllTextAsync(filePath, json);
        }
    }
}
