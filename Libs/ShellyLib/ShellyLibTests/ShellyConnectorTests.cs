using FluentAssertions;
using ShellyLib;
using System.Net;

namespace ShellyLibTests
{
    [TestClass]
    public class ShellyConnectorTests
    {
        [TestMethod]
        public void ReadPowerFromV1Plug()
        {
            var result = 0.0;
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.ShellyPlugS,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 177 })
            };

            ShellyConnector.TurnRelayOn(device);
            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (result == 0.0)
                {
                    result = ShellyConnector.ReadPower(device);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }
            ShellyConnector.TurnRelayOff(device);
            result.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void ReadPowerFromV1PlugPerformanceTest()
        {
            List<double> result = new();
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.ShellyPlugS,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 177 })
            };

            ShellyConnector.TurnRelayOn(device);
            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (true)
                {
                    result.Add(ShellyConnector.ReadPower(device));
                    if (cts.Token.IsCancellationRequested)
                        break;
                }
            }
            ShellyConnector.TurnRelayOff(device);
            Console.WriteLine($"Could read power from ShellyV1Plug {result.Count.ToString()} times");
            result.Count().Should().BeGreaterThan(200);
        }

        [TestMethod]
        public void ReadPowerFromV2Plug()
        {
            var result = 0.0;
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.ShellyPlusPlugS,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 57 })
            };

            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (result == 0.0)
                {
                    result = ShellyConnector.ReadPower(device);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }
            result.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void ReadPowerFromV2PlugPerformanceTest()
        {
            List<double> result = new();
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.ShellyPlusPlugS,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 57 })
            };

            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (true)
                {
                    result.Add(ShellyConnector.ReadPower(device));
                    if (cts.Token.IsCancellationRequested)
                        break;
                }
            }
            Console.WriteLine($"Could read power from ShellyV2Plug {result.Count.ToString()} times");
            result.Count().Should().BeGreaterThan(200);
        }

        [TestMethod]
        public void ReadPowerFromPlus1PM()
        {
            var result = 0.0;
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.ShellyPlus1PM,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 198 })
            };

            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (result == 0.0)
                {
                    result = ShellyConnector.ReadPower(device);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }
            result.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void ReadPowerFromPlus1PMPerformanceTest()
        {
            List<double> result = new();
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.ShellyPlus1PM,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 198 })
            };

            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (true)
                {
                    result.Add(ShellyConnector.ReadPower(device));
                    if (cts.Token.IsCancellationRequested)
                        break;
                }
            }
            Console.WriteLine($"Could read power from ShellyPlus1PM {result.Count.ToString()} times");
            result.Count().Should().BeGreaterThan(200);
        }

        [TestMethod]
        public void ReadPowerFrom3EM()
        {
            var result = 0.0;
            var device = new ShellyDevice()
            {
                DeviceType = DeviceType.Shelly3EM,
                IPAddress = new IPAddress(new byte[] { 192, 168, 178, 179 })
            };

            using (CancellationTokenSource cts = new CancellationTokenSource(20000))
            {
                while (result == 0.0)
                {
                    result = ShellyConnector.ReadPower(device);
                    cts.Token.ThrowIfCancellationRequested();
                }
            }
            result.Should().BeGreaterThan(0);
        }
    }
}