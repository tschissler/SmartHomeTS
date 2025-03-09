using CommunityToolkit.Mvvm.ComponentModel;
using SharedContracts;
using SmartHomeWebManagers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smarthome.App.DataContracts
{
    public partial class ChargingViewModel : ObservableObject
    {
        [ObservableProperty]
        private ChargingSituation chargingSituation = new();

        [ObservableProperty]
        private CarStatusData bwmStatusData = new();

        [ObservableProperty]
        private CarStatusData miniStatusData = new();

        [ObservableProperty]
        private CarStatusData vwStatusData = new();

        [ObservableProperty]
        private AvailabilityData availabilityData = new();

        [ObservableProperty]
        private ConsumptionData consumptionData = new();

        [ObservableProperty]
        private SharedContracts.ChargingSettings chargingSettings = new();
    }
}
