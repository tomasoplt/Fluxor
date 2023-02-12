using Fluxor;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace BasicConcepts.StateActionsReducersTutorial.Store.CounterUseCase
{
	public class WeatherState
	{
		public bool IsLoading { get; }
		public IEnumerable<WeatherForecast> Forecasts { get; }

		private WeatherState() { }
		public WeatherState(bool isLoading, IEnumerable<WeatherForecast> forecasts)
		{
			IsLoading = isLoading;
			Forecasts = forecasts ?? Array.Empty<WeatherForecast>();
		}

		public static WeatherState Empty => new();
	}
}
