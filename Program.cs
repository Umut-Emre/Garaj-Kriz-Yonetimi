using DisasterLogistics.Core.Enums;
using DisasterLogistics.Core.Models;
using DisasterLogistics.Core.Services;
using DisasterLogistics.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AFAD Lojistik Yönetim API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AFAD Lojistik Yönetim API v1");
    c.RoutePrefix = "swagger";
});

var dataPath = Path.Combine(app.Environment.ContentRootPath, "Data");
if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);

ServiceFactory.Configure(dataPath);
var auditLogger = new AuditLogger(Path.Combine(dataPath, "audit_web.log"));
var priorityManager = new PriorityManager();
var dashboardService = new DashboardService(priorityManager, auditLogger);
var matchingService = new MatchingService(priorityManager);
var random = new Random();

var efficiencyMetrics = new EfficiencyMetrics();
var aiAnalytics = new AIAnalyticsEngine();

// AFAD Category names in Turkish
var categoryNames = new Dictionary<string, string>
{
    ["Medical"] = "Tıbbi Malzeme",
    ["Water"] = "Su ve İçecek",
    ["Food"] = "Gıda",
    ["Shelter"] = "Barınma",
    ["Equipment"] = "Ekipman",
    ["Hygiene"] = "Hijyen",
    ["Clothing"] = "Giysi",
    ["Fuel"] = "Yakıt"
};

// Istanbul addresses
var istanbulAddresses = new Dictionary<string, string>
{
    ["Zone A"] = "Taksim Meydanı, Beyoğlu",
    ["Zone B"] = "Kadıköy İskelesi, Kadıköy",
    ["Zone C"] = "Beşiktaş Meydanı, Beşiktaş",
    ["Zone D"] = "Üsküdar Meydanı, Üsküdar",
    ["Zone E"] = "Bakırköy Sahili, Bakırköy",
    ["Central Warehouse"] = "İkitelli OSB, Başakşehir",
    ["Warehouse A"] = "Tuzla Organize Sanayi, Tuzla",
    ["Warehouse B"] = "Esenyurt Sanayi, Esenyurt",
    ["Warehouse C"] = "Pendik Depo Alanı, Pendik",
    ["Warehouse D"] = "Ümraniye Lojistik, Ümraniye",
    ["Warehouse E"] = "Zeytinburnu Depo, Zeytinburnu",
};

var zoneCoordinates = new Dictionary<string, (double Lat, double Lng)>
{
    ["Zone A"] = (41.0082, 28.9784),
    ["Zone B"] = (41.0136, 28.9550),
    ["Zone C"] = (41.0370, 29.0340),
    ["Zone D"] = (41.0766, 29.0267),
    ["Zone E"] = (41.0500, 28.9900),
    ["Central Warehouse"] = (41.0195, 28.8947),
    ["Warehouse A"] = (41.0041, 28.9260),
    ["Warehouse B"] = (41.0550, 28.9450),
    ["Warehouse C"] = (40.9910, 29.0270),
    ["Warehouse D"] = (41.0870, 29.0530),
    ["Warehouse E"] = (41.0450, 28.8990),
};

Location GetLocationWithCoords(string name)
{
    if (zoneCoordinates.TryGetValue(name, out var coords))
    {
        var address = istanbulAddresses.TryGetValue(name, out var addr) ? addr : name;
        return Location.FromCoordinates(coords.Lat, coords.Lng, address);
    }
    return Location.FromAddress(name);
}

string GetCategoryDisplayName(string category) => categoryNames.TryGetValue(category, out var name) ? name : category;

// In-memory data with AFAD terminology
var needs = new List<Need>
{
    new Need("Acil İnsani Yardım Talebi", "Medical", 200, GetLocationWithCoords("Zone A"), PriorityLevel.Critical),
    new Need("Tıbbi Müdahale Kiti", "Medical", 50, GetLocationWithCoords("Zone B"), PriorityLevel.Critical),
    new Need("Su Temini İhtiyacı", "Water", 300, GetLocationWithCoords("Zone C"), PriorityLevel.Medium),
    new Need("Barınma Desteği", "Shelter", 150, GetLocationWithCoords("Zone D"), PriorityLevel.High),
    new Need("Hijyen Paketi Talebi", "Hygiene", 100, GetLocationWithCoords("Zone E"), PriorityLevel.High),
    new Need("Gıda Yardımı", "Food", 200, GetLocationWithCoords("Zone A"), PriorityLevel.Medium)
};
needs[2].AddFulfilledQuantity(150);

var supplies = new List<Supply>
{
    new Supply("Tıbbi Malzeme Deposu Alfa", "Medical", 200, GetLocationWithCoords("Warehouse A")),
    new Supply("Tıbbi Malzeme Deposu Beta", "Medical", 50, GetLocationWithCoords("Warehouse B")),
    new Supply("Su ve İçecek Merkezi", "Water", 1000, GetLocationWithCoords("Warehouse C")),
    new Supply("Barınma Malzeme Deposu", "Shelter", 300, GetLocationWithCoords("Warehouse D")),
    new Supply("Gıda Dağıtım Merkezi", "Food", 600, GetLocationWithCoords("Warehouse E")),
    new Supply("Hijyen Ürün Deposu", "Hygiene", 250, GetLocationWithCoords("Central Warehouse")),
};

var shipments = new List<Shipment>
{
    new Shipment(GetLocationWithCoords("Central Warehouse"), GetLocationWithCoords("Zone A"), 100, "Tıbbi Malzeme Sevkiyatı", PriorityLevel.High),
};

var matchRoutes = new List<MatchRoute>();
var logMessages = new List<LogMessage>();

void AddLog(string message, string type = "info")
{
    logMessages.Insert(0, new LogMessage(DateTime.UtcNow, message, type));
    if (logMessages.Count > 150) logMessages.RemoveAt(150);
}

double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371;
    var dLat = (lat2 - lat1) * Math.PI / 180;
    var dLon = (lon2 - lon1) * Math.PI / 180;
    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
}

double EstimateTravelTime(double distanceKm) => distanceKm / 40 * 60;

double CalculateWeightedScore(PriorityLevel priority, double waitTimeMinutes, double distanceKm)
{
    int priorityWeight = priority switch
    {
        PriorityLevel.Critical => 100,
        PriorityLevel.High => 75,
        PriorityLevel.Medium => 50,
        PriorityLevel.Low => 25,
        _ => 50
    };
    return (priorityWeight * 0.6) + (waitTimeMinutes * 0.3) - (distanceKm * 0.1);
}

// Multi-source matching with AI analytics
List<MatchRoute> ExecuteMultiSourceMatching(List<Need> activeNeeds, List<Supply> activeSupplies)
{
    var routes = new List<MatchRoute>();
    var startTime = DateTime.UtcNow;
    int totalAllocations = 0;
    int multiSourceMatches = 0;
    int noSupplyCount = 0;
    
    var prioritizedNeeds = activeNeeds
        .Where(n => !n.IsDeleted && !n.IsFulfilled)
        .OrderBy(n => priorityManager.CalculateEffectivePriority(n))
        .ToList();
    
    foreach (var need in prioritizedNeeds)
    {
        int remainingQuantity = need.QuantityRequired - need.QuantityFulfilled;
        if (remainingQuantity <= 0) continue;
        
        var waitTimeMinutes = (DateTime.UtcNow - need.CreatedAt).TotalMinutes;
        var needShortId = need.Id.ToString().Substring(0, 6);
        var categoryDisplay = GetCategoryDisplayName(need.Category);
        
        // Track consumption for AI
        aiAnalytics.RecordDemand(need.Category, remainingQuantity, need.Location.Latitude, need.Location.Longitude);
        
        AddLog($"🔍 Saha bildirimi işleniyor #{needShortId}: {need.Title}", "info");
        
        var matchingSupplies = activeSupplies
            .Where(s => !s.IsDeleted && s.Category == need.Category && s.AllocatableQuantity > 0)
            .Select(s => new {
                Supply = s,
                Distance = CalculateDistance(s.Location.Latitude, s.Location.Longitude, 
                                            need.Location.Latitude, need.Location.Longitude)
            })
            .OrderBy(x => x.Distance)
            .ToList();
        
        if (matchingSupplies.Count == 0)
        {
            noSupplyCount++;
            AddLog($"   ❌ {categoryDisplay} kategorisinde stok bulunamadı!", "error");
            routes.Add(new MatchRoute(
                Guid.Empty, "YOK", 0, 0,
                need.Id, need.Title, need.Location.Latitude, need.Location.Longitude,
                0, 0, 0, need.Priority.ToString(), 0,
                false, remainingQuantity, "NoSupply", need.Category
            ));
            continue;
        }
        
        int sourceCount = 0;
        int totalAllocated = 0;
        
        foreach (var match in matchingSupplies)
        {
            if (remainingQuantity <= 0) break;
            
            var supply = match.Supply;
            int availableQty = supply.AllocatableQuantity;
            int allocateQty = Math.Min(availableQty, remainingQuantity);
            
            if (allocateQty <= 0) continue;
            
            // Track consumption for AI depletion prediction
            aiAnalytics.RecordConsumption(supply.Id, supply.Category, allocateQty);
            
            supply.Reserve(allocateQty);
            need.AddFulfilledQuantity(allocateQty);
            remainingQuantity -= allocateQty;
            totalAllocated += allocateQty;
            sourceCount++;
            totalAllocations++;
            
            var distance = match.Distance;
            var travelTime = EstimateTravelTime(distance);
            var score = CalculateWeightedScore(need.Priority, waitTimeMinutes, distance);
            
            bool isPartial = remainingQuantity > 0;
            string fulfillmentStatus = isPartial ? "Partial" : "Full";
            
            if (isPartial)
            {
                AddLog($"   📦 [KISMİ] {allocateQty} adet → {supply.Name}", "warning");
            }
            else
            {
                AddLog($"   ✅ [TAM] {allocateQty} adet → {supply.Name}", "success");
            }
            
            routes.Add(new MatchRoute(
                supply.Id, supply.Name, supply.Location.Latitude, supply.Location.Longitude,
                need.Id, need.Title, need.Location.Latitude, need.Location.Longitude,
                allocateQty, distance, travelTime, need.Priority.ToString(), score,
                sourceCount > 1, remainingQuantity, fulfillmentStatus, need.Category
            ));
        }
        
        if (sourceCount > 1)
        {
            multiSourceMatches++;
        }
    }
    
    var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
    efficiencyMetrics.AddMatchRun(totalAllocations, elapsedMs, multiSourceMatches);
    
    // AI Analysis logs
    AddLog($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", "info");
    AddLog($"✅ Eşleştirme tamamlandı: {totalAllocations} tahsis | {elapsedMs:F0}ms", "success");
    
    // Generate AI insights
    var insights = aiAnalytics.GenerateInsights(activeSupplies, GetCategoryDisplayName);
    foreach (var insight in insights)
    {
        AddLog(insight.Message, insight.Type);
    }
    
    return routes;
}

AddLog("AFAD Lojistik Yönetim Sistemi başlatıldı", "success");
AddLog("🤖 AI Tahmin Motoru aktif", "info");
AddLog($"{needs.Count} saha bildirimi, {supplies.Count} sevkiyat merkezi yüklendi", "info");

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// ============ API ENDPOINTS ============

app.MapGet("/api/dashboard-stats", () =>
{
    var stats = dashboardService.GetStatistics(needs, supplies, shipments);
    var categoryFulfillment = dashboardService.GetFulfillmentByCategory(needs);
    
    return Results.Ok(new
    {
        stats.TotalNeeds,
        stats.FulfilledNeeds,
        stats.PartiallyFulfilledNeeds,
        stats.UnfulfilledNeeds,
        stats.PercentageNeedsMet,
        stats.TotalSupplies,
        stats.DepletedSupplies,
        stats.LowStockSupplies,
        stats.TotalActiveShipments,
        stats.PendingShipments,
        stats.InTransitShipments,
        stats.DeliveredToday,
        stats.PanicModeActive,
        CriticalNeeds = stats.TopCriticalMissingItems.Select(i => new
        {
            i.NeedId, i.Title, i.Category, 
            CategoryDisplay = GetCategoryDisplayName(i.Category),
            i.QuantityMissing, Priority = i.Priority.ToString(), i.HoursWaiting
        }),
        PanicNeeds = stats.PanicNeeds.Select(p => new
        {
            p.NeedId, p.Title, p.Category, p.QuantityRequired, p.QuantityFulfilled, p.HoursOverdue
        }),
        CategoryFulfillment = categoryFulfillment,
        GeneratedAt = stats.GeneratedAt
    });
}).WithTags("Dashboard");

app.MapGet("/api/efficiency", () =>
{
    return Results.Ok(new
    {
        efficiencyMetrics.TotalMatchRuns,
        efficiencyMetrics.TotalAllocations,
        efficiencyMetrics.TotalAlgorithmTimeMs,
        efficiencyMetrics.EstimatedManualMinutes,
        efficiencyMetrics.TimeSavedMinutes,
        efficiencyMetrics.MultiSourceMatches,
        History = efficiencyMetrics.GetHistory()
    });
}).WithTags("Dashboard");

app.MapGet("/api/needs", () =>
{
    return Results.Ok(needs.Where(n => !n.IsDeleted).Select(n => new
    {
        n.Id, n.Title, n.Category, 
        CategoryDisplay = GetCategoryDisplayName(n.Category),
        n.QuantityRequired, n.QuantityFulfilled,
        RemainingQuantity = n.QuantityRequired - n.QuantityFulfilled,
        n.FulfillmentPercentage, n.IsFulfilled,
        Status = n.IsFulfilled ? "Fulfilled" : n.QuantityFulfilled > 0 ? "PartiallyFulfilled" : "Pending",
        Priority = n.Priority.ToString(),
        EffectivePriority = priorityManager.GetEffectivePriorityLevel(n).ToString(),
        Address = n.Location.Address,
        Latitude = n.Location.Latitude,
        Longitude = n.Location.Longitude,
        CreatedAt = n.CreatedAt
    }));
}).WithTags("Saha Bildirimleri");

app.MapPost("/api/needs", ([FromBody] CreateNeedRequest request) =>
{
    var priority = Enum.TryParse<PriorityLevel>(request.Priority, true, out var p) ? p : PriorityLevel.Medium;
    var location = GetLocationWithCoords(request.Location ?? "Zone A");
    var need = new Need(request.Title, request.Category, request.Quantity, location, priority);
    needs.Add(need);
    
    // AI tracking
    aiAnalytics.RecordDemand(request.Category, request.Quantity, location.Latitude, location.Longitude);
    
    AddLog($"📥 Yeni saha bildirimi: {request.Title}", "success");
    return Results.Created($"/api/needs/{need.Id}", new { need.Id, need.Title });
}).WithTags("Saha Bildirimleri");

app.MapGet("/api/supplies", () =>
{
    return Results.Ok(supplies.Where(s => !s.IsDeleted).Select(s => {
        var prediction = aiAnalytics.GetDepletionPrediction(s.Id, s.Category, s.AllocatableQuantity);
        return new
        {
            s.Id, s.Name, s.Category,
            CategoryDisplay = GetCategoryDisplayName(s.Category),
            s.QuantityAvailable, s.QuantityReserved, s.AllocatableQuantity,
            s.IsBelowMinimumStock, IsExpired = s.IsExpired,
            Address = s.Location.Address,
            Latitude = s.Location.Latitude,
            Longitude = s.Location.Longitude,
            Status = s.AllocatableQuantity == 0 ? "Depleted" : s.IsBelowMinimumStock ? "Low Stock" : "Available",
            AIPrediction = prediction
        };
    }));
}).WithTags("Sevkiyat Merkezleri");

app.MapPost("/api/supplies/{id}/resupply", (Guid id) =>
{
    var supply = supplies.FirstOrDefault(s => s.Id == id);
    if (supply == null) return Results.NotFound(new { error = "Sevkiyat merkezi bulunamadı" });
    
    supply.Resupply(500);
    aiAnalytics.ResetConsumption(supply.Id);
    
    AddLog($"➕ Tedarik tamamlandı: {supply.Name}", "success");
    AddLog($"   📦 +500 adet → Toplam: {supply.AllocatableQuantity} adet", "info");
    
    return Results.Ok(new { 
        success = true, 
        supplyId = supply.Id, 
        name = supply.Name,
        newStock = supply.AllocatableQuantity,
        status = "Available"
    });
}).WithTags("Sevkiyat Merkezleri");

app.MapGet("/api/shipments", () =>
{
    return Results.Ok(shipments.Where(s => !s.IsDeleted).Select(s => new
    {
        s.Id, s.TrackingNumber, Status = s.Status.ToString(), Priority = s.Priority.ToString(),
        s.ItemDescription, s.Quantity,
        Origin = s.Origin.Address, OriginLat = s.Origin.Latitude, OriginLng = s.Origin.Longitude,
        Destination = s.Destination.Address, DestLat = s.Destination.Latitude, DestLng = s.Destination.Longitude,
        s.IsActive, s.CreatedAt
    }));
}).WithTags("Sevkiyatlar");

app.MapGet("/api/match-routes", () => Results.Ok(matchRoutes)).WithTags("Eşleştirme");

// AI Heatmap data
app.MapGet("/api/ai/heatmap", () =>
{
    var heatmapData = aiAnalytics.GetHeatmapData(needs);
    return Results.Ok(heatmapData);
}).WithTags("AI Analytics");

// AI Insights
app.MapGet("/api/ai/insights", () =>
{
    var insights = aiAnalytics.GenerateInsights(supplies, GetCategoryDisplayName);
    return Results.Ok(insights);
}).WithTags("AI Analytics");

// GUIDED CHAOS: 3 Full, 4 Partial, 3 NoSupply
app.MapPost("/api/simulate", () =>
{
    AddLog("🌪️ AFAD KAOS SİMÜLASYONU BAŞLATILIYOR...", "warning");
    
    needs.Clear();
    supplies.Clear();
    matchRoutes.Clear();
    aiAnalytics.Reset();
    
    string[] addresses = { "Fatih Merkez, Fatih", "Beşiktaş Sahil, Beşiktaş", "Kadıköy Moda, Kadıköy", 
                          "Üsküdar Çarşı, Üsküdar", "Bakırköy Merkez, Bakırköy", "Şişli Meydanı, Şişli", 
                          "Maltepe Sahil, Maltepe", "Ataşehir Merkez, Ataşehir", "Pendik Merkez, Pendik",
                          "Kartal Sahil, Kartal" };
    
    // === 3 FULL MATCH NEEDS (Medical - enough stock) ===
    needs.Add(new Need("Acil İnsani Yardım #1", "Medical", 100, 
        Location.FromCoordinates(40.97, 28.87, addresses[0]), PriorityLevel.Critical));
    needs.Add(new Need("Tıbbi Müdahale Kiti #2", "Medical", 80, 
        Location.FromCoordinates(40.99, 28.92, addresses[1]), PriorityLevel.Critical));
    needs.Add(new Need("İlk Yardım Desteği #3", "Medical", 70, 
        Location.FromCoordinates(41.01, 28.97, addresses[2]), PriorityLevel.High));
    
    // === 4 PARTIAL MATCH NEEDS (Water - limited stock) ===
    needs.Add(new Need("Su Temini Talebi #4", "Water", 200, 
        Location.FromCoordinates(41.03, 29.02, addresses[3]), PriorityLevel.High));
    needs.Add(new Need("İçme Suyu İhtiyacı #5", "Water", 180, 
        Location.FromCoordinates(41.05, 29.07, addresses[4]), PriorityLevel.Medium));
    needs.Add(new Need("Temiz Su Desteği #6", "Water", 160, 
        Location.FromCoordinates(41.07, 28.89, addresses[5]), PriorityLevel.Medium));
    needs.Add(new Need("Acil Su Yardımı #7", "Water", 150, 
        Location.FromCoordinates(41.09, 28.94, addresses[6]), PriorityLevel.Medium));
    
    // === 3 NO SUPPLY NEEDS (Clothing, Fuel - zero stock) ===
    needs.Add(new Need("Giysi Yardımı #8", "Clothing", 100, 
        Location.FromCoordinates(41.02, 29.05, addresses[7]), PriorityLevel.High));
    needs.Add(new Need("Battaniye Talebi #9", "Clothing", 150, 
        Location.FromCoordinates(41.04, 28.86, addresses[8]), PriorityLevel.Medium));
    needs.Add(new Need("Yakıt Desteği #10", "Fuel", 200, 
        Location.FromCoordinates(41.06, 29.00, addresses[9]), PriorityLevel.Critical));
    
    // === WAREHOUSES ===
    supplies.Add(new Supply("AFAD Tıbbi Malzeme Deposu", "Medical", 250, 
        Location.FromCoordinates(41.00, 28.90, "AFAD İkitelli Lojistik Üssü, Başakşehir")));
    supplies.Add(new Supply("AFAD Su Dağıtım Merkezi", "Water", 300, 
        Location.FromCoordinates(41.04, 29.00, "AFAD Tuzla Lojistik Merkezi, Tuzla")));
    supplies.Add(new Supply("AFAD Barınma Deposu", "Shelter", 500, 
        Location.FromCoordinates(41.08, 29.04, "AFAD Hadımköy Deposu, Arnavutköy")));
    
    AddLog($"📊 Simülasyon: {needs.Count} bildirim, {supplies.Count} merkez", "success");
    AddLog($"   🟢 3 tam | 🟡 4 kısmi | 🔴 3 stok yok", "warning");
    
    matchRoutes.Clear();
    matchRoutes.AddRange(ExecuteMultiSourceMatching(needs, supplies));
    
    // Generate AI analysis after simulation
    AddLog($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", "info");
    AddLog($"🤖 [AI ANALİZ] Simülasyon sonuçları değerlendiriliyor...", "ai");
    
    var criticalCategories = needs.Where(n => !n.IsFulfilled && n.QuantityFulfilled == 0)
        .Select(n => n.Category).Distinct().ToList();
    
    if (criticalCategories.Count > 0)
    {
        var catNames = string.Join(", ", criticalCategories.Select(c => GetCategoryDisplayName(c)));
        AddLog($"🤖 [AI UYARI] {catNames} kategorilerinde stok yetersizliği tespit edildi!", "ai");
        AddLog($"   ↳ Acil tedarik planlaması önerilir", "ai");
    }
    
    return Results.Ok(new
    {
        Success = true,
        NeedsGenerated = needs.Count,
        SuppliesGenerated = supplies.Count,
        MatchesCreated = matchRoutes.Count,
        Routes = matchRoutes
    });
}).WithTags("Simülasyon");

app.MapPost("/api/match", () =>
{
    AddLog("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", "info");
    AddLog("🚀 Eşleştirme Motoru başlatılıyor...", "info");
    
    matchRoutes.Clear();
    matchRoutes.AddRange(ExecuteMultiSourceMatching(needs, supplies));
    
    return Results.Ok(new
    {
        Success = true,
        TotalRoutes = matchRoutes.Count,
        Routes = matchRoutes,
        Efficiency = new
        {
            efficiencyMetrics.TotalAllocations,
            efficiencyMetrics.TimeSavedMinutes,
            efficiencyMetrics.MultiSourceMatches
        }
    });
}).WithTags("Eşleştirme");

app.MapPost("/api/dispatch/{shipmentId}", (Guid shipmentId) =>
{
    var shipment = shipments.FirstOrDefault(s => s.Id == shipmentId);
    if (shipment == null) return Results.NotFound(new { error = "Sevkiyat bulunamadı" });
    
    if (shipment.Status == ShipmentStatus.Pending) shipment.UpdateStatus(ShipmentStatus.Approved);
    if (shipment.Status == ShipmentStatus.Approved) shipment.UpdateStatus(ShipmentStatus.InTransit);
    
    var distance = CalculateDistance(shipment.Origin.Latitude, shipment.Origin.Longitude,
                                     shipment.Destination.Latitude, shipment.Destination.Longitude);
    var travelTime = EstimateTravelTime(distance);
    
    AddLog($"🚚 Sevkiyat: {shipment.TrackingNumber} → {distance:F1}km", "success");
    
    return Results.Ok(new { shipment.Id, shipment.TrackingNumber, Status = shipment.Status.ToString(), Distance = distance, TravelTimeMinutes = travelTime });
}).WithTags("Sevkiyatlar");

app.MapGet("/api/logs", () => Results.Ok(logMessages.Take(60))).WithTags("Sistem Kayıtları");

app.MapGet("/api/audit-logs", () =>
{
    return Results.Ok(auditLogger.GetRecentLogs(20).Select(l => new
    {
        l.Id, l.Timestamp, EventType = l.EventType.ToString(), l.Message, Priority = l.Priority?.ToString()
    }));
}).WithTags("Sistem Kayıtları");

app.Run();

// ============ MODELS ============

record CreateNeedRequest(string Title, string Category, int Quantity, string? Location, string? Priority);
record LogMessage(DateTime Timestamp, string Message, string Type);
record MatchRoute(
    Guid SupplyId, string SupplyName, double SupplyLat, double SupplyLng,
    Guid NeedId, string NeedTitle, double NeedLat, double NeedLng,
    int Quantity, double DistanceKm, double TravelTimeMin, string Priority, double WeightedScore,
    bool IsMultiSource = false, int RemainingQuantity = 0, string FulfillmentStatus = "Full", string Category = ""
);

record AIInsight(string Message, string Type);
record DepletionPrediction(int MinutesToDepletion, double ConsumptionVelocity, bool IsCritical);
record HeatmapPoint(double Lat, double Lng, double Intensity);

class EfficiencyMetrics
{
    public int TotalMatchRuns { get; private set; }
    public int TotalAllocations { get; private set; }
    public double TotalAlgorithmTimeMs { get; private set; }
    public int MultiSourceMatches { get; private set; }
    
    public double EstimatedManualMinutes => TotalAllocations * 3;
    public double TimeSavedMinutes => EstimatedManualMinutes - (TotalAlgorithmTimeMs / 60000);
    
    private List<(DateTime Time, int Allocations, double TimeMs)> history = new();
    
    public void AddMatchRun(int allocations, double timeMs, int multiSource)
    {
        TotalMatchRuns++;
        TotalAllocations += allocations;
        TotalAlgorithmTimeMs += timeMs;
        MultiSourceMatches += multiSource;
        history.Add((DateTime.UtcNow, allocations, timeMs));
        if (history.Count > 20) history.RemoveAt(0);
    }
    
    public object GetHistory() => history.Select(h => new { h.Time, h.Allocations, h.TimeMs });
}

// AI Analytics Engine
class AIAnalyticsEngine
{
    private Dictionary<string, List<(DateTime Time, int Quantity)>> categoryDemandHistory = new();
    private Dictionary<Guid, List<(DateTime Time, int Quantity)>> supplyConsumptionHistory = new();
    private Dictionary<string, List<(double Lat, double Lng, int Quantity)>> demandLocations = new();
    
    public void Reset()
    {
        categoryDemandHistory.Clear();
        supplyConsumptionHistory.Clear();
        demandLocations.Clear();
    }
    
    public void RecordDemand(string category, int quantity, double lat, double lng)
    {
        if (!categoryDemandHistory.ContainsKey(category))
            categoryDemandHistory[category] = new();
        categoryDemandHistory[category].Add((DateTime.UtcNow, quantity));
        
        if (!demandLocations.ContainsKey(category))
            demandLocations[category] = new();
        demandLocations[category].Add((lat, lng, quantity));
        
        // Keep only last 100 records
        if (categoryDemandHistory[category].Count > 100)
            categoryDemandHistory[category].RemoveAt(0);
    }
    
    public void RecordConsumption(Guid supplyId, string category, int quantity)
    {
        if (!supplyConsumptionHistory.ContainsKey(supplyId))
            supplyConsumptionHistory[supplyId] = new();
        supplyConsumptionHistory[supplyId].Add((DateTime.UtcNow, quantity));
        
        if (supplyConsumptionHistory[supplyId].Count > 50)
            supplyConsumptionHistory[supplyId].RemoveAt(0);
    }
    
    public void ResetConsumption(Guid supplyId)
    {
        if (supplyConsumptionHistory.ContainsKey(supplyId))
            supplyConsumptionHistory[supplyId].Clear();
    }
    
    public DepletionPrediction GetDepletionPrediction(Guid supplyId, string category, int currentStock)
    {
        if (currentStock <= 0)
            return new DepletionPrediction(0, 0, true);
        
        // Calculate consumption velocity (units per minute)
        double velocity = 0;
        if (supplyConsumptionHistory.TryGetValue(supplyId, out var history) && history.Count >= 2)
        {
            var recentRecords = history.TakeLast(10).ToList();
            if (recentRecords.Count >= 2)
            {
                var totalConsumed = recentRecords.Sum(r => r.Quantity);
                var timeSpan = (recentRecords.Last().Time - recentRecords.First().Time).TotalMinutes;
                if (timeSpan > 0)
                    velocity = totalConsumed / timeSpan;
            }
        }
        
        // If no consumption history, estimate based on category demand rate
        if (velocity == 0 && categoryDemandHistory.TryGetValue(category, out var catHistory) && catHistory.Count >= 1)
        {
            var avgDemand = catHistory.Average(h => h.Quantity);
            velocity = avgDemand / 30; // Assume demand fulfilled over 30 min
        }
        
        // Default velocity for demo purposes
        if (velocity == 0) velocity = random.Next(5, 20);
        
        int minutesToDepletion = velocity > 0 ? (int)(currentStock / velocity) : 999;
        bool isCritical = minutesToDepletion < 30;
        
        return new DepletionPrediction(minutesToDepletion, Math.Round(velocity, 2), isCritical);
    }
    
    private static Random random = new Random();
    
    public List<AIInsight> GenerateInsights(List<Supply> supplies, Func<string, string> getCategoryName)
    {
        var insights = new List<AIInsight>();
        
        // Check for high demand areas
        foreach (var category in categoryDemandHistory.Keys)
        {
            var history = categoryDemandHistory[category];
            if (history.Count >= 3)
            {
                var recent = history.TakeLast(5).Sum(h => h.Quantity);
                var earlier = history.Take(5).Sum(h => h.Quantity);
                
                if (recent > earlier * 1.3 && earlier > 0)
                {
                    var increase = ((double)recent / earlier - 1) * 100;
                    var catName = getCategoryName(category);
                    insights.Add(new AIInsight(
                        $"🤖 [AI ANALİZ] {catName} talebinde %{increase:F0} artış tespit edildi",
                        "ai"
                    ));
                }
            }
        }
        
        // Check for depleting warehouses
        foreach (var supply in supplies.Where(s => s.AllocatableQuantity > 0))
        {
            var prediction = GetDepletionPrediction(supply.Id, supply.Category, supply.AllocatableQuantity);
            if (prediction.IsCritical && prediction.MinutesToDepletion > 0)
            {
                insights.Add(new AIInsight(
                    $"🤖 [AI UYARI] {supply.Name}: {prediction.MinutesToDepletion} dk içinde tükenebilir!",
                    "ai"
                ));
            }
        }
        
        return insights;
    }
    
    public List<HeatmapPoint> GetHeatmapData(List<Need> needs)
    {
        var points = new List<HeatmapPoint>();
        
        // Unmet/critical needs have higher intensity
        foreach (var need in needs.Where(n => !n.IsFulfilled))
        {
            double intensity = need.Priority switch
            {
                PriorityLevel.Critical => 1.0,
                PriorityLevel.High => 0.8,
                PriorityLevel.Medium => 0.5,
                _ => 0.3
            };
            
            // Increase intensity for unmet needs
            if (need.QuantityFulfilled == 0) intensity *= 1.5;
            
            points.Add(new HeatmapPoint(need.Location.Latitude, need.Location.Longitude, Math.Min(1.0, intensity)));
        }
        
        return points;
    }
}
