using Fluxor;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{
    public class WeatherStateFeature : Feature<WeatherState>
    {
        public override string GetName() => nameof(WeatherStateFeature);
        protected override WeatherState GetInitialState() => WeatherState.Empty;
    }
}