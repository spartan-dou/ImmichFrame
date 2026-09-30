using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ImmichFrame.WebApi.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Controllers
{
    /// <summary>Notification in through the API, out through the overlay the slideshow polls.</summary>
    [TestFixture]
    [NonParallelizable] // manipulates process-wide environment variables
    public class NotificationControllerTests
    {
        private string _configDir;
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;

        [SetUp]
        public void Setup()
        {
            _configDir = Path.Combine(Path.GetTempPath(), "immichframe-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_configDir);
            Environment.SetEnvironmentVariable("IMMICHFRAME_CONFIG_PATH", _configDir);

            var versionHandler = new Mock<HttpMessageHandler>().WithServerVersion();
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services => services.UseMockHandler(versionHandler));
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
            Environment.SetEnvironmentVariable("IMMICHFRAME_CONFIG_PATH", null);
            if (Directory.Exists(_configDir))
            {
                Directory.Delete(_configDir, true);
            }
        }

        private async Task<JsonNode> Overlay()
        {
            var response = await _client.GetAsync("/api/Overlay");
            response.EnsureSuccessStatusCode();
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }

        private Task<HttpResponseMessage> PostRaw(string json)
            => _client.PostAsync("/api/Notification", new StringContent(json, Encoding.UTF8, "application/json"));

        private async Task<JsonArray> Notifications() => (await Overlay())["notifications"]!.AsArray();

        [Test]
        public async Task Overlay_BeforeAnyPush_HasNoSensorsNorNotification()
        {
            var overlay = await Overlay();

            Assert.That(overlay["sensors"]!.AsArray(), Is.Empty);
            Assert.That(overlay["notifications"]!.AsArray(), Is.Empty);
            Assert.That(overlay["memoriesEnabled"]!.GetValue<bool>(), Is.False);
        }

        [Test]
        public async Task Post_AsHomeAssistantRestNotifySendsIt_ShowsUpInTheOverlay()
        {
            // REST notify renders every templated field as text. "replace" is what an
            // integration from before the queue still sends: ignored, not rejected.
            var response = await PostRaw("""{"message": "📦 Colis déposé", "link": "/lovelace/cameras", "duration": "30", "replace": "True"}""");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            var notification = (await Notifications()).Single()!;
            Assert.That(notification["message"]!.GetValue<string>(), Is.EqualTo("📦 Colis déposé"));
            Assert.That(notification["link"]!.GetValue<string>(), Is.EqualTo("/lovelace/cameras"));
            Assert.That(notification["until"]!.GetValue<long>(), Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(29).ToUnixTimeMilliseconds()));
        }

        [Test]
        public async Task Post_EmptyOptionalFields_MeanNoLinkAndNoEnd()
        {
            (await PostRaw("""{"message": "Bonjour", "link": "", "duration": ""}""")).EnsureSuccessStatusCode();

            var notification = (await Notifications()).Single()!;
            Assert.That(notification["link"]!.GetValue<string>(), Is.Empty);
            Assert.That(notification["until"], Is.Null);
        }

        [Test]
        public async Task Post_Several_ShowNewestFirst()
        {
            await _client.PostAsJsonAsync("/api/Notification", new { message = "Premier" });
            await _client.PostAsJsonAsync("/api/Notification", new { message = "Second" });

            var messages = (await Notifications()).Select(n => n!["message"]!.GetValue<string>());
            Assert.That(messages, Is.EqualTo(new[] { "Second", "Premier" }));
        }

        [Test]
        public async Task Delete_ClearsThemAll()
        {
            await _client.PostAsJsonAsync("/api/Notification", new { message = "Premier" });
            await _client.PostAsJsonAsync("/api/Notification", new { message = "Second" });

            var response = await _client.DeleteAsync("/api/Notification");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await Notifications(), Is.Empty);
        }

        [Test]
        public async Task Sensors_PushedByHomeAssistant_ShowInTheOverlayInOrder()
        {
            var response = await _client.PutAsJsonAsync("/api/Overlay/Sensors", new
            {
                sensors = new object[]
                {
                    new { icon = "🛋️", value = "20.5", unit = "°C" },
                    new { icon = "🌳", value = (string?)null, unit = "°C" }
                }
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            var sensors = (await Overlay())["sensors"]!.AsArray();
            Assert.That(sensors.Select(s => s!["icon"]!.GetValue<string>()), Is.EqualTo(new[] { "🛋️", "🌳" }));
            Assert.That(sensors[0]!["value"]!.GetValue<string>(), Is.EqualTo("20.5"));
            Assert.That(sensors[1]!["value"], Is.Null);
        }

        [Test]
        public async Task Post_InvalidDuration_IsRejected()
        {
            var response = await PostRaw("""{"message": "Bonjour", "duration": "demain"}""");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }
}
