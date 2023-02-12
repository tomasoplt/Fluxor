using Fluxor;
using System.Net.Http;
using System.Threading.Tasks;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{
	public class Effects
	{
		public Effects()
		{
		}

		[EffectMethod]
		public async Task HandleFetchDataAction(FetchDataAction action, IDispatcher dispatcher)
		{
			await Task.CompletedTask;
		}
	}
}
