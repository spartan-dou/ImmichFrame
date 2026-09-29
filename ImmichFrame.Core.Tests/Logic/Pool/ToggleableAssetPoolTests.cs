using ImmichFrame.Core.Api;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool
{
    [TestFixture]
    public class ToggleableAssetPoolTests
    {
        private Mock<IAssetPool> _inner;
        private bool _enabled;
        private ToggleableAssetPool _pool;

        [SetUp]
        public void Setup()
        {
            _inner = new Mock<IAssetPool>();
            _inner.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(7L);
            _inner.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AssetResponseDto> { new AssetResponseDto { Id = FixtureHelpers.GuidFor("memory") } });
            _enabled = true;
            _pool = new ToggleableAssetPool(_inner.Object, () => _enabled);
        }

        [Test]
        public async Task Enabled_DelegatesToInnerPool()
        {
            Assert.That(await _pool.GetAssetCount(), Is.EqualTo(7L));
            Assert.That(await _pool.GetAssets(1), Has.Exactly(1).Items);
        }

        [Test]
        public async Task Disabled_IsEmptyAndLeavesInnerPoolAlone()
        {
            _enabled = false;

            Assert.That(await _pool.GetAssetCount(), Is.EqualTo(0L));
            Assert.That(await _pool.GetAssets(5), Is.Empty);
            _inner.VerifyNoOtherCalls();
        }

        [Test]
        public async Task Disabled_IsNeverChosenByMultiAssetPool()
        {
            var album = new Mock<IAssetPool>();
            album.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(3L);
            album.Setup(p => p.GetAssets(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AssetResponseDto> { new AssetResponseDto { Id = FixtureHelpers.GuidFor("album") } });
            var multi = new MultiAssetPool(new IAssetPool[] { _pool, album.Object });
            _enabled = false;

            var assets = (await multi.GetAssets(20)).ToList();

            Assert.That(assets, Has.Count.EqualTo(20));
            Assert.That(assets.Select(a => a.Id), Is.All.EqualTo(FixtureHelpers.GuidFor("album")));
        }
    }
}
