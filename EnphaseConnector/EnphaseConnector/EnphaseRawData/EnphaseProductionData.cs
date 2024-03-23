using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnphaseConnector.EnphaseRawData
{

    public record EnphaseProductionData
        (
        List<ProductionData> Production,
        List<ConsumptionData> Consumption,
        List<StorageData> Storage
        );

    public record ProductionData
        (
        string Type,
        int ActiveCount,
        long ReadingTime,
        double WNow,
        double WhLifetime,
        double? VarhLeadLifetime = null,
        double? VarhLagLifetime = null,
        double? VahLifetime = null,
        double? RmsCurrent = null,
        double? RmsVoltage = null,
        double? ReactPwr = null,
        double? ApprntPwr = null,
        double? PwrFactor = null,
        double? WhToday = null,
        double? WhLastSevenDays = null,
        double? VahToday = null,
        double? VarhLeadToday = null,
        double? VarhLagToday = null,
        string? MeasurementType = null
        );

    public record ConsumptionData(
    string Type,
    int ActiveCount,
    string MeasurementType,
    long ReadingTime,
    double WNow,
    double WhLifetime,
    double VarhLeadLifetime,
    double VarhLagLifetime,
    double VahLifetime,
    double RmsCurrent,
    double RmsVoltage,
    double ReactPwr,
    double ApprntPwr,
    double PwrFactor,
    double WhToday,
    double WhLastSevenDays,
    double VahToday,
    double VarhLeadToday,
    double VarhLagToday);

    public record StorageData(
        string Type,
        int ActiveCount,
        long ReadingTime,
        double WNow,
        double WhNow,
        string State);
}
