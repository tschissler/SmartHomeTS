using SharedContracts;
using Smarthome.App.DataContracts;
using SmartHome.Web.Services;
using SmartHomeWebManagers;

namespace Smarthome.App
{
    public partial class MainPage : ContentPage
    {
        private Model model = new();
        private MQTTService MqttService;

        public MainPage(MQTTService MqttService)
        {
            InitializeComponent();
            this.MqttService = MqttService;
            this.BindingContext = model;
            UpdateChargingSituation();
            UpdateChargingSettings();

            MqttService.OnMessageReceived += UpdateData;
        }

        private void UpdateData(object sender, MqttMessageReceivedEventArgs args)
        {
            if (args.Topic.StartsWith("data/charging/"))
            {
                UpdateChargingSituation();
            }
            else if (args.Topic == "config/charging/settings")
            {
                UpdateChargingSettings();
            }
        }

        private void UpdateChargingSituation()
        {
            model.ChargingSituation = MqttService.ChargingSituation;
            model.BwmStatusData = MqttService.bmwStatusData ?? new CarStatusData();
            model.MiniStatusData = MqttService.miniStatusData ?? new CarStatusData();
            model.VwStatusData = MqttService.vwStatusData ?? new CarStatusData();

            model.AvailabilityData = ChargingSituationManager.CalculateAvailabilityData(
                model.ChargingSituation.PowerFromPV,
                model.ChargingSituation.PowerFromBattery,
                model.ChargingSituation.PowerFromGrid);
            model.ConsumptionData = ChargingSituationManager.CalculateConsumptionData(
                model.ChargingSituation.HouseConsumptionPower - model.ChargingSituation.InsideCurrentChargingPower - model.ChargingSituation.OutsideCurrentChargingPower,
                model.ChargingSituation.PowerFromBattery,
                model.ChargingSituation.PowerFromGrid,
                model.ChargingSituation.InsideCurrentChargingPower,
                model.ChargingSituation.OutsideCurrentChargingPower);
        }

        private void UpdateChargingSettings()
        {
            model.ChargingSettings = MqttService.ChargingSettings;
        }
    }
}
