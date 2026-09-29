using ImmichFrame.WebApi.HomeAssistant;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.HomeAssistant
{
    [TestFixture]
    public class HomeAssistantConfigTests
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "immichframe-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, true);

        private HomeAssistantConfig Load() => HomeAssistantConfig.Load(_dir, NullLogger.Instance);

        [Test]
        public void NoFile_IsDisabled()
        {
            Assert.That(Load().Enabled, Is.False);
        }

        [Test]
        public void File_IsReadWithTokenFileAndSensors()
        {
            var tokenFile = Path.Combine(_dir, "token");
            File.WriteAllText(tokenFile, "secret-token\n");
            File.WriteAllText(Path.Combine(_dir, HomeAssistantConfig.FileName), $"""
                Url: http://home-assistant.home-assistant.svc.cluster.local:8123/
                TokenFile: {tokenFile}
                Sensors:
                  - Entity: climate.salon_poele_thermostat
                    Icon: "🛋️"
                  - Entity: sensor.jardin_thermometre
                    Icon: "🌳"
                    Unit: " °C"
                """);

            var config = Load();

            Assert.That(config.Enabled, Is.True);
            Assert.That(config.Token, Is.EqualTo("secret-token"));
            Assert.That(config.WebSocketUri.ToString(), Is.EqualTo("ws://home-assistant.home-assistant.svc.cluster.local:8123/api/websocket"));
            Assert.That(config.Sensors.Select(s => s.Entity), Is.EqualTo(new[] { "climate.salon_poele_thermostat", "sensor.jardin_thermometre" }));
            Assert.That(config.Sensors[0].Icon, Is.EqualTo("🛋️"));
            Assert.That(config.Sensors[0].Domain, Is.EqualTo("climate"));
            Assert.That(config.Sensors[1].Unit, Is.EqualTo(" °C"));
        }

        [Test]
        public void Https_UsesSecureWebSocket()
        {
            var config = new HomeAssistantConfig { Url = "https://ha.example.org" };

            Assert.That(config.WebSocketUri.ToString(), Is.EqualTo("wss://ha.example.org/api/websocket"));
        }

        [Test]
        public void BrokenFile_DisablesInsteadOfCrashing()
        {
            File.WriteAllText(Path.Combine(_dir, HomeAssistantConfig.FileName), "Url: [unclosed");

            Assert.That(Load().Enabled, Is.False);
        }
    }
}
