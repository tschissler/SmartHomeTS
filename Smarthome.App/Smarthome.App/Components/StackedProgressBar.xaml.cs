namespace Smarthome.App.Components;

public partial class StackedProgressBar : ContentView
{
    public static readonly BindableProperty BarHeightProperty =
        BindableProperty.Create(nameof(BarHeight), typeof(double), typeof(StackedProgressBar), 20.0);

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(BarHeightProperty) / 4;
    }

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(StackedProgressBar));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public StackedProgressBar()
	{
		InitializeComponent();
	}
}