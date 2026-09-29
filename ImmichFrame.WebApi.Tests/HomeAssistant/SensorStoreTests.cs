using ImmichFrame.WebApi.HomeAssistant;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.HomeAssistant
{
    [TestFixture]
    public class SensorStoreTests
    {
        private class ManualTimeProvider : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private ManualTimeProvider _time;
        private SensorStore _store;

        [SetUp]
        public void Setup()
        {
            _time = new ManualTimeProvider();
            _store = new SensorStore(_time);
            _store.Set(new[] { new SensorValue("🌳", "12.5", "°C") });
        }

        [Test]
        public void FreshValues_AreShown()
        {
            _time.Now = _time.Now.Add(SensorStore.StaleAfter).AddSeconds(-1);

            Assert.That(_store.Current[0].Value, Is.EqualTo("12.5"));
        }

        [Test]
        public void StaleValues_AreBlankedButKeepTheirPlace()
        {
            _time.Now = _time.Now.Add(SensorStore.StaleAfter);

            Assert.That(_store.Current, Has.Count.EqualTo(1));
            Assert.That(_store.Current[0].Icon, Is.EqualTo("🌳"));
            Assert.That(_store.Current[0].Value, Is.Null);
        }

        [Test]
        public void NewPush_RefreshesThem()
        {
            _time.Now = _time.Now.Add(SensorStore.StaleAfter);

            _store.Set(new[] { new SensorValue("🌳", "13.0", "°C") });

            Assert.That(_store.Current[0].Value, Is.EqualTo("13.0"));
        }
    }
}
