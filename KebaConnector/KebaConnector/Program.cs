// See https://aka.ms/new-console-template for more information

using KebaConnector;
using System.Net;
using System.Runtime.InteropServices;

KebaDeviceConnector kebaOutside;
KebaDeviceConnector kebaGarage;

Console.WriteLine("KebaConnector started");

Console.WriteLine("  - Connecting to Keba Outside...");
var ipsOutside = await Dns.GetHostAddressesAsync("keba-stellplatz");
if (ipsOutside is null || ipsOutside.Length == 0)
{
    Console.WriteLine("    Could not resolve keba-stellplatz");
    return;
}
kebaOutside = new KebaDeviceConnector(ipsOutside[0], 7090);
Console.WriteLine("    ...Done");

Console.WriteLine("  - Connecting to Keba Garage...");
var ipsGarage = await Dns.GetHostAddressesAsync("keba-garage");
if (ipsGarage is null || ipsGarage.Length == 0)
{
    Console.WriteLine("    Could not resolve keba-garage");
    return;
}
kebaGarage = new KebaDeviceConnector(ipsGarage[0], 7090);
Console.WriteLine("    ...Done");

var timer = new Timer(Update, null, 0, 5000);

Thread.Sleep(Timeout.Infinite);


void Update(object? state)
{
    kebaOutside.ReadDeviceData().ContinueWith((task) =>
    {
        if (task.IsCompletedSuccessfully)
        {
            var data = task.Result;
            Console.WriteLine($"Keba Outside: {data.PlugStatus}, {data.CurrentChargingPower}W, {data.EnergyCurrentChargingSession}Wh, {data.EnergyTotal}Wh");
        }
    });

    kebaGarage.ReadDeviceData().ContinueWith((task) =>
    {
        if (task.IsCompletedSuccessfully)
        {
            var data = task.Result;
            Console.WriteLine($"Keba Garage : {data.PlugStatus}, {data.CurrentChargingPower}W, {data.EnergyCurrentChargingSession}Wh, {data.EnergyTotal}Wh");
        }
    });
}