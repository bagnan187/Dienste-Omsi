
using System.Text.Json.Serialization;

record Config(
    string apiBaseUrl,
    string depotSyncToken,
    string omsiRoot,
    string mapFolder,
    int udpPort = 47830,
    int pollSeconds = 60,
    string staticObjectFolder = "Sceneryobjects\\ROGISstatic",
    string staticObjectPattern = "ROGIS_{0}.sco"
);

record Slot(
    string slotId,
    string depot,
    string slotType,
    string tile,
    string objectId,
    string mapPath,
    double? x,
    double? y,
    double? z,
    double? heading
);

record DepotVehicle(
    string? vehicle,
    int? vehicleNumber,
    string? model,
    bool used,
    string[]? runs,
    string? startDepot,
    string? startSlot,
    string? endDepot,
    string? endSlot,
    string? slotType,
    string? startTime,
    string? endTime
);

record SlotReservation(
    int from,
    int to,
    string? vehicle,
    string? kind
);

record SlotConflict(
    string slotId,
    SlotReservation? a,
    SlotReservation? b
);

record DepotDay(
    bool ok,
    string date,
    int planVersion,
    int slotCount,
    Dictionary<string, object>? depotOccupancy,
    Dictionary<string, SlotReservation[]>? slotReservations,
    SlotConflict[]? slotConflicts,
    DepotVehicle[] vehicles
);
