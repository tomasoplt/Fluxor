using Fluxor;
using System.Threading.Tasks;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{ 
	public class WeatherEffect : Effect<FetchDataAction>
	{
		public WeatherEffect()
		{
		}

		public override async Task HandleAsync(FetchDataAction action, IStore store)
		{
			await Task.CompletedTask;
		}
	}
}