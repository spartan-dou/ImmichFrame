using System.Net;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic
{
    [TestFixture]
    public class PooledImmichFrameLogicTests
    {
        private class RecordingHandler : HttpMessageHandler
        {
            public int Calls;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        [Test]
        public async Task AnAccountWithoutAnySource_BringsItsMemoriesNotItsWholeLibrary()
        {
            // A second account added only for its memories: no album, person, tag nor favorites.
            var account = new Mock<IAccountSettings>();
            account.Setup(a => a.ImmichServerUrl).Returns("http://immich.test");
            account.Setup(a => a.ApiKey).Returns("key");
            var handler = new RecordingHandler();
            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

            using var logic = new PooledImmichFrameLogic(account.Object, new Mock<IGeneralSettings>().Object,
                httpClientFactory.Object, new MemoriesSwitch(), new TodaysMemories());

            // Memories switched off: nothing to show, and the library is never listed.
            Assert.That(await logic.GetTotalAssets(), Is.Zero);
            Assert.That(await logic.GetAssets(), Is.Empty);
            Assert.That(handler.Calls, Is.Zero);
        }
    }
}
