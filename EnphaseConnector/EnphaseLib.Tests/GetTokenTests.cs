using EnphaseConnector;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmartHomeHelpers.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnphaseLib.Tests
{
    [TestClass]
    public class GetTokenTests
    {
        [TestMethod]
        public void GetTokenForLocalAccess()
        {
            var enphaseAuth = new EnphaseLocalAuth();
            string userName = Enphase.EnphaseUserName;
            string password = Enphase.EnphasePassword;
            var token = enphaseAuth.GetTokenAsync(userName, password, Enphase.EnvoyM1Serial).Result;
            token.Token.Should().NotBeNullOrEmpty(); 
            Thread.Sleep(1000);
            var token2 = enphaseAuth.GetTokenAsync(userName, password, Enphase.EnvoyM3Serial).Result;
            token2.Token.Should().NotBeNullOrEmpty();
        }
    }
}
