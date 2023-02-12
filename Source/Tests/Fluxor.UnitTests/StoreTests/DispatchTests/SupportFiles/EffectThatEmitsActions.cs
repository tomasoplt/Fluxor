using System;
using System.Threading.Tasks;

namespace Fluxor.UnitTests.StoreTests.DispatchTests.SupportFiles
{
	public class EffectThatEmitsActions : Effect<TestAction>
	{
		public readonly object[] ActionsToEmit;

		public EffectThatEmitsActions(object[] actionsToEmit)
		{
			ActionsToEmit = actionsToEmit ?? Array.Empty<object>();
		}
		public override Task HandleAsync(TestAction action, IStore store)
		{
			foreach (object actionToEmit in ActionsToEmit)
				store.Dispatch(actionToEmit);
			return Task.CompletedTask;
		}
	}
}
