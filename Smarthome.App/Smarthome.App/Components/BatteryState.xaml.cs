namespace Smarthome.App.Components;

public partial class BatteryState : ContentView
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(StackedProgressBar));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public BatteryState()
	{
		InitializeComponent();
	}
}