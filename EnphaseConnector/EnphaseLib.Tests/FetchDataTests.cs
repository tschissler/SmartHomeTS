using EnphaseConnector;
using FluentAssertions;
using SmartHomeHelpers.Configuration;

[TestClass]
public class FetchDataTests
{
    [TestMethod]
    public void FetchData()
    {
        var enphaseAuth = new EnphaseLocalAuth();
        string userName = Enphase.EnphaseUserName;
        string password = Enphase.EnphasePassword;
        string envoySerial = Enphase.EnvoyM1Serial;
        var token = enphaseAuth.GetTokenAsync(userName, password, envoySerial).Result;

        var target = new EnphaseConnector.EnphaseLib();

        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        var actual = target.FetchDataAsync(token, "envoym1").Result;
        sw.Stop();
        System.Console.WriteLine($"FetchDataAsync took {sw.ElapsedMilliseconds}ms");

        actual.PowerToHouse.Should().BeGreaterThan(0);

        sw.Start();
        actual = target.FetchDataAsync(token, "envoym1").Result;
        sw.Stop();
        System.Console.WriteLine($"FetchDataAsync took {sw.ElapsedMilliseconds}ms");

        actual.PowerToHouse.Should().BeGreaterThan(0);
    }
}
