using ImmichFrame.Core.Logic;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic
{
    [TestFixture]
    public class MemoriesSwitchTests
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "immichframe-tests", Guid.NewGuid().ToString());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        // A sub-directory that does not exist yet, like a freshly mounted volume path.
        private string StateFile => Path.Combine(_dir, "state", "memories.json");

        [Test]
        public void WithoutFile_StartsDisabled()
        {
            Assert.That(new MemoriesSwitch(StateFile).Enabled, Is.False);
        }

        [Test]
        public void Set_SurvivesARestart()
        {
            new MemoriesSwitch(StateFile) { Enabled = true };
            Assert.That(new MemoriesSwitch(StateFile).Enabled, Is.True);

            new MemoriesSwitch(StateFile) { Enabled = false };
            Assert.That(new MemoriesSwitch(StateFile).Enabled, Is.False);
        }

        [Test]
        public void Only_SurvivesARestart_AlongsideEnabled()
        {
            new MemoriesSwitch(StateFile) { Enabled = true, Only = true };

            var restarted = new MemoriesSwitch(StateFile);
            Assert.That(restarted.Enabled, Is.True);
            Assert.That(restarted.Only, Is.True);
        }

        [Test]
        public void FileFromBeforeOnly_KeepsEnabled_AndStartsWithOnlyOff()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
            File.WriteAllText(StateFile, """{"Enabled":true}""");

            var memoriesSwitch = new MemoriesSwitch(StateFile);
            Assert.That(memoriesSwitch.Enabled, Is.True);
            Assert.That(memoriesSwitch.Only, Is.False);
        }

        [Test]
        public void UnreadableFile_StartsDisabled()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
            File.WriteAllText(StateFile, "{not json");

            Assert.That(new MemoriesSwitch(StateFile).Enabled, Is.False);
        }

        [Test]
        public void WithoutStateFile_LivesInMemory()
        {
            var memoriesSwitch = new MemoriesSwitch { Enabled = true };

            Assert.That(memoriesSwitch.Enabled, Is.True);
        }
    }
}
