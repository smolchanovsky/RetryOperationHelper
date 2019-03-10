using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Samples
{
    /// <summary>
    /// Provides functionality to automatically try the given piece of logic some number of times before re-throwing the exception. 
    /// This is useful for any piece of code which may experience transient failures. Be cautious of passing code with two distinct 
    /// actions given that if the second or subsequent piece of logic fails, the first will also be retried upon each retry. 
    /// </summary>
    public static class RetryOperationHelper
    {
		private static string AttemptOutOfRangeExceptionMsg => "The maximum number of attempts must not be less than 1.";

		/// <summary>Executes asynchronous function with retry logic.</summary>
		/// <param name="func">The asynchronous function to be executed.</param>
		/// <param name="maxAttempts">The maximum number of attempts.</param>
		/// <param name="retryInterval">Timespan to wait between attempts of the operation</param>
		/// <param name="onAttemptFailed">The callback executed when an attempt is failed.</param>
		/// <param name="onAttemptsOver">The callback executed when an attempt is over.</param>
		/// <typeparam name="T">The result type.</typeparam>
		/// <returns>The <see cref="Task"/> producing the result.</returns>
		public static async Task<T> ExecuteWithRetryAsync<T>(
			Func<Task<T>> func,
			int maxAttempts,
			TimeSpan? retryInterval = null,
			Action<int, Exception> onAttemptFailed = null,
			Action<AggregateException> onAttemptsOver = null)
		{
			if (func == null)
				throw new ArgumentNullException(nameof(func));

			if (maxAttempts < 1)
				throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, AttemptOutOfRangeExceptionMsg);

			var attempt = 0;
			var exceptions = new List<Exception>();
			while (true)
			{
				if (attempt > 0 && retryInterval != null)
					await Task.Delay(retryInterval.Value);

				try
				{
					return await func().ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					++attempt;
					HandleException(attempt, maxAttempts, exception, exceptions, onAttemptFailed, onAttemptsOver);
				}
			}
		}

		/// <summary>Executes asynchronous function with retry logic.</summary>
		/// <param name="func">The asynchronous function to be executed.</param>
		/// <param name="maxAttempts">The maximum number of attempts.</param>
		/// <param name="retryInterval">Timespan to wait between attempts of the operation</param>
		/// <param name="onAttemptFailed">The retry handler.</param>
		/// <param name="onAttemptsOver">The callback executed when an attempt is over.</param>
		/// <returns>The <see cref="Task"/> producing the result.</returns>
		public static async Task ExecuteWithRetryAsync(
			Func<Task> func,
			int maxAttempts,
			TimeSpan? retryInterval = null,
			Action<int, Exception> onAttemptFailed = null,
			Action<AggregateException> onAttemptsOver = null)
		{
			if (func == null)
				throw new ArgumentNullException(nameof(func));

			async Task<bool> Wrapper()
			{
				await func().ConfigureAwait(false);
				return true;
			}

			await ExecuteWithRetry(Wrapper, maxAttempts, retryInterval, onAttemptFailed, onAttemptsOver);
		}

		/// <summary>Executes asynchronous function with retry logic.</summary>
		/// <param name="action">The action to be executed.</param>
		/// <param name="maxAttempts">The maximum number of attempts.</param>
		/// <param name="retryInterval">Timespan to wait between attempts of the operation</param>
		/// <param name="onAttemptFailed">The retry handler.</param>
		/// <param name="onAttemptsOver">The callback executed when an attempt is over.</param>
		public static void ExecuteWithRetry(
			Action action,
			int maxAttempts,
			TimeSpan? retryInterval = null,
			Action<int, Exception> onAttemptFailed = null,
			Action<AggregateException> onAttemptsOver = null)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));

			bool Wrapper()
			{
				action();
				return true;
			}

			ExecuteWithRetry(Wrapper, maxAttempts, retryInterval, onAttemptFailed, onAttemptsOver);
		}

		/// <summary>Executes asynchronous function with retry logic.</summary>
		/// <param name="func">The function to be executed.</param>
		/// <param name="maxAttempts">The maximum number of attempts.</param>
		/// <param name="retryInterval">Timespan to wait between attempts of the operation</param>
		/// <param name="onAttemptFailed">The retry handler.</param>
		/// <param name="onAttemptsOver">The callback executed when an attempt is over.</param>
		/// <returns>The producing the result.</returns>
		public static T ExecuteWithRetry<T>(
			Func<T> func,
			int maxAttempts,
			TimeSpan? retryInterval = null,
			Action<int, Exception> onAttemptFailed = null,
			Action<AggregateException> onAttemptsOver = null)
		{
			if (func == null)
				throw new ArgumentNullException(nameof(func));

			if (maxAttempts < 1)
				throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, AttemptOutOfRangeExceptionMsg);

			var attempt = 0;
			var pastAttemptsExceptions = new List<Exception>();
			while (true)
			{
				if (attempt > 0 && retryInterval != null)
					Thread.Sleep((TimeSpan)retryInterval);

				try
				{
					return func();
				}
				catch (Exception exception)
				{
					++attempt;
					HandleException(attempt, maxAttempts, exception, pastAttemptsExceptions, onAttemptFailed, onAttemptsOver);
				}
			}
		}

		private static void HandleException(
			int attempt,
			int maxAttempts,
			Exception exception,
			ICollection<Exception> pastAttemptsExceptions,
			Action<int, Exception> onAttemptFailed = null,
			Action<AggregateException> onAttemptsOver = null)
		{
			onAttemptFailed?.Invoke(attempt, exception);
			pastAttemptsExceptions.Add(exception);

			if (attempt < maxAttempts)
				return;

			if (onAttemptsOver == null)
				throw new AggregateException(pastAttemptsExceptions);
			onAttemptsOver(new AggregateException(pastAttemptsExceptions));
		}
	}
}