using Fluxor;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{
	public static class WeatherReducers
	{
		[ReducerMethod]
		public static WeatherState ReduceFetchDataAction(WeatherState state, FetchDataAction action) =>
			new(isLoading: true, forecasts: null);

	}
}
