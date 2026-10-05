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

        private IEnumerable<string> Messages() => _store.Current.Select(n => n.Message);

        [Test]
        public void Add_WithoutDuration_StaysUntilPushedOut()
        {
            _store.Add("Colis", "/lovelace/cameras", null);

            _time.Now = _time.Now.AddDays(3);

            var shown = _store.Current.Single();
            Assert.That(shown.Message, Is.EqualTo("Colis"));
            Assert.That(shown.Link, Is.EqualTo("/lovelace/cameras"));
            Assert.That(shown.Until, Is.Null);
        }

        [Test]
        public void Add_WithDuration_ExpiresOnItsOwn()
        {
            _store.Add("Colis", null, 30);

            _time.Now = _time.Now.AddMinutes(29);
            Assert.That(_store.Current, Is.Not.Empty);

            _time.Now = _time.Now.AddMinutes(2);
            Assert.That(_store.Current, Is.Empty);
        }

        [Test]
        public void Add_KeepsTheLatestOnes_NewestFirst()
        {
            _store.Add("Un", null, null);
            _store.Add("Deux", null, null);
            _store.Add("Trois", null, null);
            _store.Add("Quatre", null, null);

            Assert.That(Messages(), Is.EqualTo(new[] { "Quatre", "Trois", "Deux" }));
        }

        [Test]
        public void Add_AnExpiredOne_NoLongerTakesASlot()
        {
            _store.Add("Un", null, 10);
            _store.Add("Deux", null, null);
            _store.Add("Trois", null, null);

            _time.Now = _time.Now.AddMinutes(11);
            _store.Add("Quatre", null, null);
            _store.Add("Cinq", null, null);

            Assert.That(Messages(), Is.EqualTo(new[] { "Cinq", "Quatre", "Trois" }));
        }

        [Test]
        public void Add_AMessageAlreadyShown_MovesUpWithItsNewLinkAndEnd()
        {
            _store.Add("Lave-linge terminé", null, null);
            _store.Add("Colis", null, null);

            _store.Add("Lave-linge terminé", "/lovelace/buanderie", 5);

            Assert.That(Messages(), Is.EqualTo(new[] { "Lave-linge terminé", "Colis" }));
            Assert.That(_store.Current[0].Link, Is.EqualTo("/lovelace/buanderie"));
            Assert.That(_store.Current[0].Until, Is.Not.Null);
        }

        [Test]
        public void Add_SameTag_ReplacesItsOwnWhateverTheText()
        {
            _store.Add("⚡ Coupure de courant · 30 min", null, 360, "onduleur");
            _store.Add("Colis", null, null);

            _store.Add("🔌 Courant rétabli après 12 min", null, 30, "onduleur");

            Assert.That(Messages(), Is.EqualTo(new[] { "🔌 Courant rétabli après 12 min", "Colis" }));
        }

        [Test]
        public void Add_EmptyMessageWithTag_ClearsThatOneAlone()
        {
            _store.Add("Aérer la chambre", null, 20, "volet_chambre");
            _store.Add("Colis", null, null);

            _store.Add("", null, null, "volet_chambre");
            _store.Add("", null, null, "rien_de_tel");

            Assert.That(Messages(), Is.EqualTo(new[] { "Colis" }));
        }

        [Test]
        public void Add_GivesEachNotificationItsOwnId()
        {
            _store.Add("Un", null, null);
            _store.Add("Deux", null, null);
            _store.Add("Un", null, null);

            var ids = _store.Current.Select(n => n.Id).ToList();
            Assert.That(ids, Is.Unique);
            Assert.That(ids[0], Is.GreaterThan(ids[1]));
        }

        [Test]
        public void Add_EmptyMessage_ClearsThemAll()
        {
            _store.Add("Un", null, null);
            _store.Add("Deux", null, null);

            _store.Add("  ", null, null);

            Assert.That(_store.Current, Is.Empty);
        }

        [Test]
        public void Add_TruncatesLongMessages()
        {
            _store.Add(new string('x', NotificationStore.MaxLength + 50), null, null);

            Assert.That(_store.Current.Single().Message, Has.Length.EqualTo(NotificationStore.MaxLength));
        }
    }
}
