﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Fluxor.UnitTests")]

namespace Fluxor
{
	/// <see cref="IStore"/>
	public class Store : IStore, IActionSubscriber, IDisposable
	{
		/// <see cref="IStore.Features"/>
		public IReadOnlyDictionary<string, IFeature> Features => FeaturesByName;
		/// <see cref="IStore.Initialized"/>
		public Task Initialized => InitializedCompletionSource.Task;

		private object SyncRoot = new object();
		private bool Disposed;
		private readonly Dictionary<string, IFeature> FeaturesByName = new(StringComparer.InvariantCultureIgnoreCase);
		private readonly List<IEffect> Effects = new();
		private readonly List<IMiddleware> Middlewares = new();
		private readonly List<IMiddleware> ReversedMiddlewares = new();
		private readonly Queue<object> QueuedActions = new();
		private readonly TaskCompletionSource<bool> InitializedCompletionSource = new();
		private readonly ActionSubscriber ActionSubscriber;

		private volatile bool IsDispatching;
		private volatile int BeginMiddlewareChangeCount;
		private volatile bool HasActivatedStore;
		private bool IsInsideMiddlewareChange => BeginMiddlewareChangeCount > 0;

		/// <summary>
		/// Creates an instance of the store
		/// </summary>
		public Store()
		{
			ActionSubscriber = new ActionSubscriber();
			Dispatch(new StoreInitializedAction());
		}

		/// <see cref="IStore.GetMiddlewares"/>
		public IEnumerable<IMiddleware> GetMiddlewares() => Middlewares;

		/// <see cref="IStore.AddFeature(IFeature)"/>
		public void AddFeature(IFeature feature)
		{
			if (feature is null)
				throw new ArgumentNullException(nameof(feature));

			lock (SyncRoot)
			{
				FeaturesByName.Add(feature.GetName(), feature);
			}
		}

		public void Dispatch(object action)
		{
			if (action is null)
				throw new ArgumentNullException(nameof(action));

			lock (SyncRoot)
			{
				// Do not allow task dispatching inside a middleware-change.
				// These change cycles are for things like "jump to state" in Redux Dev Tools
				// and should be short lived.
				// We avoid dispatching inside a middleware change because we don't want UI events (like component Init)
				// that trigger actions (such as fetching data from a server) to execute
				if (IsInsideMiddlewareChange)
					return;

				// If a dequeue is already in progress, we will just
				// let this new action be added to the queue and then exit
				// Note: This is to cater for the following scenario
				//	1: An action is dispatched
				//	2: An effect is triggered
				//	3: The effect immediately dispatches a new action
				// The Queue ensures it is processed after its triggering action has completed rather than immediately
				QueuedActions.Enqueue(action);

				// HasActivatedStore is set to true when the page finishes loading
				// At which point DequeueActions will be called
				if (!HasActivatedStore)
					return;

				DequeueActions();
			};
		}

		public int GetQueuedActionsCount()
		{
			return QueuedActions.Count;
		}

		/// <see cref="IStore.AddEffect(IEffect)"/>
		public void AddEffect(IEffect effect)
		{
			if (effect is null)
				throw new ArgumentNullException(nameof(effect));

			lock (SyncRoot)
			{
				Effects.Add(effect);
			}
		}

		/// <see cref="IStore.AddMiddleware(IMiddleware)"/>
		public void AddMiddleware(IMiddleware middleware)
		{
			lock (SyncRoot)
			{
				Middlewares.Add(middleware);
				ReversedMiddlewares.Insert(0, middleware);
				// Initialize the middleware immediately if the store has already been initialized, otherwise this will be
				// done the first time Dispatch is called
				if (HasActivatedStore)
				{
					middleware
						.InitializeAsync(this)
						.ContinueWith(t =>
						{
							if (!t.IsFaulted)
								middleware.AfterInitializeAllMiddlewares();
						});
				}
			}
		}

		/// <see cref="IStore.BeginInternalMiddlewareChange"/>
		public IDisposable BeginInternalMiddlewareChange()
		{
			IDisposable[] disposables = null;
			lock (SyncRoot)
			{
				BeginMiddlewareChangeCount++;
				disposables = Middlewares
					.Select(x => x.BeginInternalMiddlewareChange())
					.ToArray();
			}

			return new DisposableCallback(
				id: $"{nameof(Store)}.{nameof(BeginInternalMiddlewareChange)}",
				() => EndMiddlewareChange(disposables));
		}

		/// <see cref="IStore.InitializeAsync"/>
		public async Task InitializeAsync()
		{
			if (HasActivatedStore)
				return;

			await ActivateStoreAsync();
		}

		public event EventHandler<Exceptions.UnhandledExceptionEventArgs> UnhandledException;

		/// <see cref="IActionSubscriber.SubscribeToAction{TAction}(object, Action{TAction})"/>
		public void SubscribeToAction<TAction>(object subscriber, Action<TAction> callback)
		{
			ActionSubscriber.SubscribeToAction(subscriber, callback);
		}

		/// <see cref="IActionSubscriber.UnsubscribeFromAllActions(object)"/>
		public void UnsubscribeFromAllActions(object subscriber)
		{
			ActionSubscriber.UnsubscribeFromAllActions(subscriber);
		}

		/// <see cref="IActionSubscriber.GetActionUnsubscriberAsIDisposable(object)"/>
		public IDisposable GetActionUnsubscriberAsIDisposable(object subscriber) =>
			ActionSubscriber.GetActionUnsubscriberAsIDisposable(subscriber);

		void IDisposable.Dispose()
		{
			if (!Disposed)
			{
				Disposed = true;
			}
		}
		 
		private void DequeueActions()
		{
			if (IsDispatching)
				return;

			var dispatchedActions = new List<object>();
			IsDispatching = true;
			try
			{
				while (QueuedActions.Count > 0)
				{
					object nextActionToProcess = QueuedActions.Dequeue();

					// Only process the action if no middleware vetos it
					if (Middlewares.All(x => x.MayDispatchAction(nextActionToProcess)))
					{
						ExecuteMiddlewareBeforeDispatch(nextActionToProcess);

						// Notify all features of this action
						// Commented
						// foreach (var featureInstance in FeaturesByName.Values)
						// featureInstance.ReceiveDispatchNotificationFromStore(nextActionToProcess);

						ActionSubscriber?.Notify(nextActionToProcess);
						ExecuteMiddlewareAfterDispatch(nextActionToProcess);
						dispatchedActions.Add(nextActionToProcess);
					}
				}
			}
			finally
			{
				IsDispatching = false;
			}
			foreach (var dispatchedAction in dispatchedActions)
				TriggerEffects(dispatchedAction);
		}

		private void EndMiddlewareChange(IDisposable[] disposables)
		{
			lock (SyncRoot)
			{
				BeginMiddlewareChangeCount--;
				if (BeginMiddlewareChangeCount == 0)
					disposables.ToList().ForEach(x => x.Dispose());
			}
		}

		private void TriggerEffects(object action)
		{
			var recordedExceptions = new List<Exception>();
			var effectsToExecute = Effects
				.Where(x => x.ShouldReactToAction(action))
				.ToArray();
			var executedEffects = new List<Task>();

			Action<Exception> collectExceptions = e =>
			{
				if (e is AggregateException aggregateException)
					recordedExceptions.AddRange(aggregateException.Flatten().InnerExceptions);
				else
					recordedExceptions.Add(e);
			};

			// Execute all tasks. Some will execute synchronously and complete immediately,
			// so we need to catch their exceptions in the loop so they don't prevent
			// other effects from executing.
			// It's then up to the UI to decide if any of those exceptions should cause
			// the app to terminate or not.
			foreach (IEffect effect in effectsToExecute)
			{
				try
				{
					executedEffects.Add(effect.HandleAsync(action, this));
				}
				catch (Exception e)
				{
					collectExceptions(e);
				}
			}

			if (executedEffects.Count > 0)
			{
				Task.Run(async () =>
				{
					try
					{
						await Task.WhenAll(executedEffects);
					}
					catch (Exception e)
					{
						collectExceptions(e);
					}

					// Let the UI decide if it wishes to deal with any unhandled exceptions.
					// By default it should throw the exception if it is not handled.
					foreach (Exception exception in recordedExceptions)
						UnhandledException?.Invoke(this, new Exceptions.UnhandledExceptionEventArgs(exception));
				});
			}
		}

		private async Task InitializeMiddlewaresAsync()
		{
			foreach (IMiddleware middleware in Middlewares)
			{
				await middleware.InitializeAsync(this);
			}
			Middlewares.ForEach(x => x.AfterInitializeAllMiddlewares());
		}

		private void ExecuteMiddlewareBeforeDispatch(object actionAboutToBeDispatched)
		{
			foreach (IMiddleware middleWare in Middlewares)
				middleWare.BeforeDispatch(actionAboutToBeDispatched);
		}

		private void ExecuteMiddlewareAfterDispatch(object actionJustDispatched)
		{
			Middlewares.ForEach(x => x.AfterDispatch(actionJustDispatched));
		}

		private async Task ActivateStoreAsync()
		{
			if (HasActivatedStore)
				return;

			await InitializeMiddlewaresAsync();

			lock (SyncRoot)
			{
				HasActivatedStore = true;
				DequeueActions();
				InitializedCompletionSource.SetResult(true);
			}
		}
	}
}
