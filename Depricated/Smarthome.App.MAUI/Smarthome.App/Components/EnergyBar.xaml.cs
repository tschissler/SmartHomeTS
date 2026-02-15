namespace Smarthome.App.Components;

public partial class EnergyBar : ContentView
{
    public static readonly BindableProperty BarHeightProperty =
    BindableProperty.Create(nameof(BarHeight), typeof(double), typeof(EnergyBar), 15.0);

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(BarHeightProperty) / 16;
    }

    public static readonly BindableProperty PVProductionProperty =
        BindableProperty.Create(nameof(PVProduction), typeof(double), typeof(EnergyBar));

    public double PVProduction
    {
        get => (double)GetValue(PVProductionProperty);
        set => SetValue(PVProductionProperty, value);
    }

    public static readonly BindableProperty BatteryChargingProperty =
    BindableProperty.Create(nameof(BatteryCharging), typeof(double), typeof(EnergyBar));

    public double BatteryCharging
    {
        get => (double)GetValue(BatteryChargingProperty);
        set => SetValue(BatteryChargingProperty, value);
    }

    public static readonly BindableProperty GridConsumptionProperty =
        BindableProperty.Create(nameof(GridConsumption), typeof(double), typeof(EnergyBar));
    public double GridConsumption
    {
        get => (double)GetValue(GridConsumptionProperty);
        set => SetValue(GridConsumptionProperty, value);
    }

    public static readonly BindableProperty HouseConsumptionProperty =
        BindableProperty.Create(nameof(HouseConsumption), typeof(double), typeof(EnergyBar));
    public double HouseConsumption
    {
        get => (double)GetValue(HouseConsumptionProperty);
        set => SetValue(HouseConsumptionProperty, value);
    }

    public static readonly BindableProperty GarageChargingProperty =
        BindableProperty.Create(nameof(GarageCharging), typeof(double), typeof(EnergyBar));
    public double GarageCharging
    {
        get => (double)GetValue(GarageChargingProperty);
        set => SetValue(GarageChargingProperty, value);
    }

    public static readonly BindableProperty OutsideChargingProperty =
        BindableProperty.Create(nameof(OutsideCharging), typeof(double), typeof(EnergyBar));
    public double OutsideCharging
    {
        get => (double)GetValue(OutsideChargingProperty);
        set => SetValue(OutsideChargingProperty, value);
    }

    public EnergyBar()
	{
        InitializeComponent();
	}
}