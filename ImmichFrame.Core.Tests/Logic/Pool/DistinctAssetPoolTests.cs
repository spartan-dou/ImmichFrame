using ImmichFrame.Core.Api;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool
{
    [TestFixture]
    public class DistinctAssetPoolTests
    {
        private static Mock<IAssetPool> PoolOf(params string[] seeds)
        {
            var pool = new Mock<IAssetPool>();
            pool.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync((long)seeds.Length);
            pool.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(seeds.Select(s => new AssetResponseDto { Id = FixtureHelpers.GuidFor(s) }).ToList());
            return pool;
        }

        [Test]
        public async Task MultiAssetPool_DrawsWithReplacement_ButTheBatchHasEachPhotoOnce()
        {
            // The same photo in today's memories and in the album.
            var multi = new MultiAssetPool(new[] { PoolOf("photo").Object, PoolOf("photo").Object });

            Assert.That((await multi.GetAssets(10)).Select(a => a.Id).Distinct(), Has.Exactly(1).Items);
            Assert.That(await new DistinctAssetPool(multi).GetAssets(10), Has.Exactly(1).Items);
        }

        [Test]
        public async Task KeepsTheOrderOfFirstAppearance()
        {
            var pool = new DistinctAssetPool(PoolOf("a", "b", "a", "c", "b").Object);

            var ids = (await pool.GetAssets(5)).Select(a => a.Id);

            Assert.That(ids, Is.EqualTo(new[] { "a", "b", "c" }.Select(FixtureHelpers.GuidFor)));
        }
    }
}
