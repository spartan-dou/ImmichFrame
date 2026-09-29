using ImmichFrame.WebApi.HomeAssistant;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.HomeAssistant
{
    [TestFixture]
    public class NotificationStoreTests
    {
        private class ManualTimeProvider : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private ManualTimeProvider _time;
        private NotificationStore _store;

        [SetUp]
        public void Setup()
        {
            _time = new ManualTimeProvider();
            _store = new NotificationStore(_time);
        }

        [Test]
        public void Set_WithoutDuration_StaysUntilTheNextOne()
        {
            Assert.That(_store.Set("Colis", "/lovelace/cameras", null, true), Is.True);

            _time.Now = _time.Now.AddDays(3);

            Assert.That(_store.Current!.Message, Is.EqualTo("Colis"));
            Assert.That(_store.Current!.Link, Is.EqualTo("/lovelace/cameras"));
            Assert.That(_store.Current!.Until, Is.Null);
        }

        [Test]
        public void Set_WithDuration_ExpiresOnItsOwn()
        {
            _store.Set("Colis", null, 30, true);

            _time.Now = _time.Now.AddMinutes(29);
            Assert.That(_store.Current, Is.Not.Null);

            _time.Now = _time.Now.AddMinutes(2);
            Assert.That(_store.Current, Is.Null);
        }

        [Test]
        public void Set_WithoutReplace_IsIgnoredWhileAnotherIsShown()
        {
            _store.Set("Premier", null, 10, true);

            Assert.That(_store.Set("Second", null, null, false), Is.False);
            Assert.That(_store.Current!.Message, Is.EqualTo("Premier"));

            _time.Now = _time.Now.AddMinutes(11);
            Assert.That(_store.Set("Second", null, null, false), Is.True);
            Assert.That(_store.Current!.Message, Is.EqualTo("Second"));
        }

        [Test]
        public void Set_EmptyMessage_AlwaysClears()
        {
            _store.Set("Premier", null, null, true);

            Assert.That(_store.Set("  ", null, null, false), Is.True);
            Assert.That(_store.Current, Is.Null);
        }

        [Test]
        public void Set_TruncatesLongMessages()
        {
            _store.Set(new string('x', NotificationStore.MaxLength + 50), null, null, true);

            Assert.That(_store.Current!.Message, Has.Length.EqualTo(NotificationStore.MaxLength));
        }
    }
}
