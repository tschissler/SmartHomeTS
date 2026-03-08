using System.Text.Json;
using System.Text.Json.Serialization;

namespace BMWConnector.Models;

public class VehicleState
{
    private readonly string _name;

    public VehicleState(string name) => _name = name;

    public int? Battery { get; private set; }
    public string? ChargingStatus { get; private set; }
    public string? HvChargingStatus { get; private set; }
    public int ChargingTarget { get; private set; } = 100;
    public DateTime? ChargingEndTime { get; private set; }
    public bool? ChargerConnected { get; private set; }

    public double? RemainingRange { get; private set; }
    public double? PredictedRange { get; private set; }
    public double? Mileage { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public bool? Moving { get; private set; }
    public double? ChargingPower { get; private set; }
    public double? MaxEnergy { get; private set; }
    public string? ChargingMode { get; private set; }
    public int? PlugEventId { get; private set; }
    public double? AvgConsumption { get; private set; }
    public double? AcVoltage { get; private set; }
    public double? AcAmpere { get; private set; }
    public DateTime LastUpdate { get; private set; } = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>Returns true if the field was recognized and applied, false if unknown.</summary>
    public bool Apply(string field, BmwDataPoint point)
    {
        LastUpdate = point.Timestamp;

        switch (field)
        {
            case "vehicle.drivetrain.batteryManagement.header":  // Mini: real-time HV battery SoC (%)
                Battery = point.Value.ValueKind == JsonValueKind.Number ? (int)point.Value.GetDouble() : null;
                break;

            case "vehicle.drivetrain.batteryManagement.maxEnergy":  // BMW: battery capacity (kWh)
                MaxEnergy = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.drivetrain.electricEngine.charging.status":
                ChargingStatus = point.Value.ValueKind == JsonValueKind.String ? point.Value.GetString() : null;
                break;

            case "vehicle.drivetrain.electricEngine.charging.hvStatus":
                HvChargingStatus = point.Value.ValueKind == JsonValueKind.String ? point.Value.GetString() : null;
                break;

            case "vehicle.powertrain.electric.battery.stateOfCharge.target":
                if (point.Value.ValueKind == JsonValueKind.Number)
                    ChargingTarget = (int)point.Value.GetDouble();
                break;

            case "vehicle.drivetrain.electricEngine.charging.timeRemaining":
                if (point.Value.ValueKind == JsonValueKind.Number)
                {
                    var minutes = point.Value.GetDouble();
                    ChargingEndTime = minutes > 0 ? DateTime.UtcNow.AddMinutes(minutes) : null;
                }
                break;
            // timeRemaining is not sent by BMW streaming API — ChargingEndTime is estimated below

            case "vehicle.body.chargingPort.status":
                ChargerConnected = point.Value.ValueKind == JsonValueKind.String
                    && point.Value.GetString() == "CONNECTED";
                break;

            case "vehicle.drivetrain.electricEngine.kombiRemainingElectricRange":
                RemainingRange = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.drivetrain.electricEngine.remainingElectricRange":
                PredictedRange = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.vehicle.travelledDistance":
                Mileage = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.cabin.infotainment.navigation.currentLocation.latitude":
                Latitude = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.cabin.infotainment.navigation.currentLocation.longitude":
                Longitude = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.isMoving":
                Moving = point.Value.ValueKind == JsonValueKind.True ? true
                    : point.Value.ValueKind == JsonValueKind.False ? false : null;
                break;

            case "vehicle.powertrain.electric.battery.charging.power":
                ChargingPower = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.drivetrain.electricEngine.charging.chargingMode":
                ChargingMode = point.Value.ValueKind == JsonValueKind.String ? point.Value.GetString() : null;
                break;

            case "vehicle.body.chargingPort.plugEventId":
                PlugEventId = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetInt32() : null;
                break;

            case "vehicle.drivetrain.avgElectricRangeConsumption":
                AvgConsumption = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.drivetrain.electricEngine.charging.acVoltage":
                AcVoltage = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            case "vehicle.drivetrain.electricEngine.charging.acAmpere":
                AcAmpere = point.Value.ValueKind == JsonValueKind.Number ? point.Value.GetDouble() : null;
                break;

            default:
                return false;
        }

        if (ChargingStatus != "CHARGINGACTIVE")
            ChargingEndTime = null;

        return true;
    }

    public string ToJson()
    {
        var output = new
        {
            brand = _name,
            name = _name,
            battery = Battery,
            chargingStatus = ChargingStatus,
            hvChargingStatus = HvChargingStatus,
            chargingTarget = ChargingTarget,
            chargingEndTime = ChargingEndTime?.ToString("o"),
            chargerConnected = ChargerConnected,
            remainingRange = RemainingRange,
            predictedRange = PredictedRange,
            mileage = Mileage,
            position = Latitude.HasValue && Longitude.HasValue
                ? new { latitude = Latitude.Value, longitude = Longitude.Value }
                : null,
            moving = Moving,
            chargingPower = ChargingPower,
            maxEnergy = MaxEnergy,
            chargingMode = ChargingMode,
            plugEventId = PlugEventId,
            avgConsumption = AvgConsumption,
            acVoltage = AcVoltage,
            acAmpere = AcAmpere,
            lastUpdate = LastUpdate.ToString("o"),
        };

        return JsonSerializer.Serialize(output, JsonOptions);
    }
}
