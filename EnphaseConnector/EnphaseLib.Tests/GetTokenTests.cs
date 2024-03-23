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
            string envoySerial = Enphase.EnvoyM1Serial;
            var token = enphaseAuth.GetTokenAsync(userName, password, envoySerial).Result;
            token.Token.Should().NotBeNullOrEmpty(); 
        }
    }
}
