using System.Text.Json.Nodes;
using ImmichFrame.WebApi.HomeAssistant;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.HomeAssistant
{
    [TestFixture]
    public class HomeAssistantStateStoreTests
    {
        private HomeAssistantStateStore _store;

        [SetUp]
        public void Setup()
        {
            _store = new HomeAssistantStateStore();
            _store.SetConnected(true);
            // Shapes copied from Home Assistant's websocket_api/messages.py.
            Apply("""
                {"a": {
                    "sensor.jardin": {"s": "12.5", "a": {"unit_of_measurement": "°C", "friendly_name": "Jardin"}, "c": "01H", "lc": 1.0},
                    "climate.salon": {"s": "heat", "a": {"current_temperature": 20.5, "temperature": 21}, "c": "01H", "lc": 1.0}
                }}
                """);
        }

        private void Apply(string json) => _store.Apply(JsonNode.Parse(json)!.AsObject());

        [Test]
        public void Add_StoresStateAndAttributes()
        {
            Assert.That(_store.Get("sensor.jardin")!.State, Is.EqualTo("12.5"));
            Assert.That(_store.Get("climate.salon")!.Attributes["current_temperature"]!.GetValue<double>(), Is.EqualTo(20.5));
        }

        [Test]
        public void Change_MergesStateAndAttributes()
        {
            Apply("""{"c": {"sensor.jardin": {"+": {"s": "13.0", "lu": 2.0}}}}""");
            Apply("""{"c": {"climate.salon": {"+": {"a": {"current_temperature": 19.8}, "lu": 2.0}}}}""");

            Assert.That(_store.Get("sensor.jardin")!.State, Is.EqualTo("13.0"));
            Assert.That(_store.Get("sensor.jardin")!.Attributes["unit_of_measurement"]!.GetValue<string>(), Is.EqualTo("°C"));
            var salon = _store.Get("climate.salon")!;
            Assert.That(salon.State, Is.EqualTo("heat"));
            Assert.That(salon.Attributes["current_temperature"]!.GetValue<double>(), Is.EqualTo(19.8));
            Assert.That(salon.Attributes["temperature"]!.GetValue<int>(), Is.EqualTo(21));
        }

        [Test]
        public void Change_RemovesAttributes()
        {
            Apply("""{"c": {"sensor.jardin": {"+": {"lu": 2.0}, "-": {"a": ["friendly_name"]}}}}""");

            Assert.That(_store.Get("sensor.jardin")!.Attributes.ContainsKey("friendly_name"), Is.False);
        }

        [Test]
        public void Change_DoesNotMutatePublishedState()
        {
            var before = _store.Get("climate.salon")!;

            Apply("""{"c": {"climate.salon": {"+": {"a": {"current_temperature": 18.0}}}}}""");

            Assert.That(before.Attributes["current_temperature"]!.GetValue<double>(), Is.EqualTo(20.5));
        }

        [Test]
        public void Change_OfUnknownEntity_IsIgnored()
        {
            Apply("""{"c": {"sensor.inconnu": {"+": {"s": "1"}}}}""");

            Assert.That(_store.Get("sensor.inconnu"), Is.Null);
        }

        [Test]
        public void Remove_DropsTheEntity()
        {
            Apply("""{"r": ["sensor.jardin"]}""");

            Assert.That(_store.Get("sensor.jardin"), Is.Null);
            Assert.That(_store.Get("climate.salon"), Is.Not.Null);
        }

        [Test]
        public void Disconnect_ForgetsEverything()
        {
            _store.SetConnected(false);

            Assert.That(_store.Connected, Is.False);
            Assert.That(_store.Get("sensor.jardin"), Is.Null);
        }
    }
}
