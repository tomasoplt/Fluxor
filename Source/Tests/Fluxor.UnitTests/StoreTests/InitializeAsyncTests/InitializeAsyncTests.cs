using Moq;
using System.Threading.Tasks;
using Xunit;

namespace Fluxor.UnitTests.StoreTests.InitializeAsyncTests
{
	public class InitializeAsyncTests
	{
		private readonly IStore Subject;

		[Fact]
		public async Task WhenCalled_ThenCallsInitializeAsyncOnRegisteredMiddlewares()
		{
			await Subject.InitializeAsync();
			var mockMiddleware = new Mock<IMiddleware>();
			Subject.AddMiddleware(mockMiddleware.Object);

			mockMiddleware
				.Verify(x => x.InitializeAsync(Subject));
		}

		[Fact]
		public async Task WhenCalled_ThenCallsAfterInitializeAllMiddlewaresOnRegisteredMiddlewares()
		{
			var mockMiddleware = new Mock<IMiddleware>();
			Subject.AddMiddleware(mockMiddleware.Object);

			await Subject.InitializeAsync();

			mockMiddleware
				.Verify(x => x.AfterInitializeAllMiddlewares());
		}

		[Fact]
		public async Task WhenStoreIsInitialized_ThenCallsInitializeAsyncOnAllRegisteredMiddlewares()
		{
			await Subject.InitializeAsync();

			var mockMiddleware = new Mock<IMiddleware>();
			Subject.AddMiddleware(mockMiddleware.Object);

			mockMiddleware
				.Verify(x => x.InitializeAsync(Subject));
		}

		public InitializeAsyncTests()
		{
			Subject = new Store();
		}
	}
}