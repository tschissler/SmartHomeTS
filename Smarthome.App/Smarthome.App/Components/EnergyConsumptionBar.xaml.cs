namespace Smarthome.App.Components;

public partial class EnergyConsumptionBar : ContentView
{
    public static readonly BindableProperty BarHeightProperty =
    BindableProperty.Create(nameof(BarHeight), typeof(double), typeof(EnergyConsumptionBar), 20.0);

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(BarHeightProperty) / 4;
    }

    public static readonly BindableProperty GridFeedProperty =
        BindableProperty.Create(nameof(GridFeed), typeof(double), typeof(EnergyConsumptionBar));

    public double GridFeed
    {
        get => (double)GetValue(GridFeedProperty);
        set => SetValue(GridFeedProperty, value);
    }

    public static readonly BindableProperty BatteryCharingProperty =
    BindableProperty.Create(nameof(BatteryCharging), typeof(double), typeof(EnergyConsumptionBar));

    public double BatteryCharging
    {
        get => (double)GetValue(BatteryCharingProperty);
        set => SetValue(BatteryCharingProperty, value);
    }

    public static readonly BindableProperty HouseConsumptionProperty =
    BindableProperty.Create(nameof(HouseConsumption), typeof(double), typeof(EnergyConsumptionBar));

    public double HouseConsumption
    {
        get => (double)GetValue(HouseConsumptionProperty);
        set => SetValue(HouseConsumptionProperty, value);
    }
    public EnergyConsumptionBar()
	{
		InitializeComponent();
	}
}