using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool
{
    [TestFixture]
    public class TodaysMemoriesTests
    {
        private static IAssetPool PoolWith(long count)
        {
            var pool = new Mock<IAssetPool>();
            pool.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(count);
            return pool.Object;
        }

        [Test]
        public async Task AnyAccountHasSome_WhenOneOfThemHas()
        {
            var todaysMemories = new TodaysMemories();
            todaysMemories.Register(PoolWith(0));
            todaysMemories.Register(PoolWith(3));

            Assert.That(await todaysMemories.AnyAccountHasSome(), Is.True);
        }

        [Test]
        public async Task ADisposedAccount_NoLongerCounts()
        {
            var todaysMemories = new TodaysMemories();
            todaysMemories.Register(PoolWith(0));
            var reloaded = todaysMemories.Register(PoolWith(3));

            reloaded.Dispose();

            Assert.That(await todaysMemories.AnyAccountHasSome(), Is.False);
        }
    }
}
