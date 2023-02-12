using Fluxor;
using System.Threading.Tasks;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{
	public class Effects
	{
		public Effects()
		{
		}

		[EffectMethod]
		public async Task HandleFetchDataAction(FetchDataAction action, IStore dispatcher)
		{
			await Task.CompletedTask;
		}
	}
}
