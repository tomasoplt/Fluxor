using Fluxor;
using System.Threading.Tasks;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{ 
	public class CounterStateEffect : Effect<IncrementCounterAction>
	{
		public CounterStateEffect()
		{
		}

		public override async Task HandleAsync(IncrementCounterAction action, IStore store)
		{
			await Task.CompletedTask;
		}
	}
}