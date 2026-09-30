using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ImmichFrame.WebApi.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Controllers
{
    [TestFixture]
    [NonParallelizable] // manipulates process-wide environment variables
    public class MemoriesControllerTests
    {
        private string _configDir;
        private WebApplicationFactory<Program> _factory;

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
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            Environment.SetEnvironmentVariable("IMMICHFRAME_CONFIG_PATH", null);
            if (Directory.Exists(_configDir))
            {
                Directory.Delete(_configDir, true);
            }
        }

        private static async Task<bool> ReadEnabled(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            return json["enabled"]!.GetValue<bool>();
        }

        [Test]
        public async Task Get_StartsDisabled()
        {
            var client = _factory.CreateClient();

            Assert.That(await ReadEnabled(await client.GetAsync("/api/Memories")), Is.False);
        }

        [Test]
        public async Task Put_SurvivesARestart()
        {
            var client = _factory.CreateClient();
            await client.PutAsJsonAsync("/api/Memories", new { enabled = true });
            client.Dispose();
            _factory.Dispose();

            // Same config directory, fresh process: the API's last word still holds.
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                        services.UseMockHandler(new Mock<HttpMessageHandler>().WithServerVersion()));
                });

            Assert.That(await ReadEnabled(await _factory.CreateClient().GetAsync("/api/Memories")), Is.True);
        }

        [Test]
        public async Task Put_Only_LeavesEnabledAsItWas()
        {
            var client = _factory.CreateClient();
            await client.PutAsJsonAsync("/api/Memories", new { enabled = true });

            var response = await client.PutAsJsonAsync("/api/Memories", new { only = true });

            response.EnsureSuccessStatusCode();
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.That(json["enabled"]!.GetValue<bool>(), Is.True);
            Assert.That(json["only"]!.GetValue<bool>(), Is.True);
            var overlay = JsonNode.Parse(await client.GetStringAsync("/api/Overlay"))!;
            Assert.That(overlay["memoriesOnly"]!.GetValue<bool>(), Is.True);
        }

        [Test]
        public async Task Put_TogglesTheState()
        {
            var client = _factory.CreateClient();

            Assert.That(await ReadEnabled(await client.PutAsJsonAsync("/api/Memories", new { enabled = false })), Is.False);
            Assert.That(await ReadEnabled(await client.GetAsync("/api/Memories")), Is.False);

            Assert.That(await ReadEnabled(await client.PutAsJsonAsync("/api/Memories", new { enabled = true })), Is.True);
            Assert.That(await ReadEnabled(await client.GetAsync("/api/Memories")), Is.True);
        }
    }
}
