using ImmichFrame.Core.Api;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool
{
    [TestFixture]
    public class MemoriesOnlyAssetPoolTests
    {
        private List<AssetResponseDto> _todaysMemories;
        private bool _only;
        private bool _anotherAccountHasMemories;
        private MemoriesOnlyAssetPool _pool;

        [SetUp]
        public void Setup()
        {
            _todaysMemories = new List<AssetResponseDto> { new() { Id = FixtureHelpers.GuidFor("memory") } };
            var memories = new Mock<IAssetPool>();
            memories.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)_todaysMemories.Count);
            memories.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _todaysMemories);

            var usual = new Mock<IAssetPool>();
            usual.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(500L);
            usual.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AssetResponseDto> { new() { Id = FixtureHelpers.GuidFor("album") } });

            _only = false;
            _anotherAccountHasMemories = false;
            _pool = new MemoriesOnlyAssetPool(memories.Object, usual.Object, () => _only,
                async ct => _anotherAccountHasMemories || await memories.Object.GetAssetCount(ct) > 0);
        }

        private async Task<Guid> NextId() => (await _pool.GetAssets(1)).Single().Id;

        [Test]
        public async Task On_ThisAccountHasNoneButAnotherHas_ShowsNothingFromThisOne()
        {
            _only = true;
            _todaysMemories.Clear();
            _anotherAccountHasMemories = true;

            Assert.That(await _pool.GetAssets(5), Is.Empty);
            Assert.That(await _pool.GetAssetCount(), Is.Zero);
        }

        [Test]
        public async Task Off_ShowsTheUsualAssets()
        {
            Assert.That(await NextId(), Is.EqualTo(FixtureHelpers.GuidFor("album")));
            Assert.That(await _pool.GetAssetCount(), Is.EqualTo(500L));
        }

        [Test]
        public async Task On_ShowsTodaysMemoriesAlone()
        {
            _only = true;

            Assert.That(await NextId(), Is.EqualTo(FixtureHelpers.GuidFor("memory")));
            Assert.That(await _pool.GetAssetCount(), Is.EqualTo(1L));
        }

        [Test]
        public async Task On_ADayWithoutMemories_FallsBackToTheUsualAssets()
        {
            _only = true;
            _todaysMemories.Clear();

            Assert.That(await NextId(), Is.EqualTo(FixtureHelpers.GuidFor("album")));
        }
    }
}
