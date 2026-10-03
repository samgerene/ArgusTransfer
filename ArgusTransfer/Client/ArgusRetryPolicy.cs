// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRetryPolicy.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, softwareUseCases
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Client
{
    using System;
    using System.IO;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Configures how <see cref="ArgusClient"/> retries requests that fail because of transient named pipe errors,
    /// such as the pipe breaking while the server restarts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A failure while connecting, before any part of the request was written, is always safe to retry.
    /// A failure after the request was written may mean the server already processed it, so by default it is
    /// only retried for idempotent verbs (<see cref="ArgusVerb.GET"/>, <see cref="ArgusVerb.HEAD"/>,
    /// <see cref="ArgusVerb.PUT"/> and <see cref="ArgusVerb.DELETE"/>); see <see cref="RetryNonIdempotentRequests"/>.
    /// A request with a <see cref="ArgusMessage.BodyStream"/> is only retried after it was written when the
    /// stream is seekable, in which case it is rewound to its original position.
    /// </para>
    /// <para>
    /// The request timeout is a budget for the whole call, including all retries and the delays between them.
    /// </para>
    /// </remarks>
    public class ArgusRetryPolicy
    {
        /// <summary>
        /// Backing field for <see cref="MaxRetries"/>
        /// </summary>
        private int maxRetries = 3;

        /// <summary>
        /// Backing field for <see cref="InitialDelay"/>
        /// </summary>
        private TimeSpan initialDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Backing field for <see cref="MaxDelay"/>
        /// </summary>
        private TimeSpan maxDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Backing field for <see cref="Strategy"/>
        /// </summary>
        private RetryBackoffStrategy strategy = RetryBackoffStrategy.Exponential;

        /// <summary>
        /// Gets or sets the maximum number of retries after the first attempt. Defaults to 3.
        /// A value of 0 disables retries.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is negative
        /// </exception>
        public int MaxRetries
        {
            get => this.maxRetries;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                this.maxRetries = value;
            }
        }

        /// <summary>
        /// Gets or sets the delay before the first retry, from which later delays are derived
        /// according to <see cref="Strategy"/>. Defaults to 100 milliseconds.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is negative
        /// </exception>
        public TimeSpan InitialDelay
        {
            get => this.initialDelay;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
                this.initialDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the upper bound for the delay between two attempts. Defaults to 10 seconds.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is negative
        /// </exception>
        public TimeSpan MaxDelay
        {
            get => this.maxDelay;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
                this.maxDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="RetryBackoffStrategy"/> that determines how the delay grows between retries.
        /// Defaults to <see cref="RetryBackoffStrategy.Exponential"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is not a defined <see cref="RetryBackoffStrategy"/>
        /// </exception>
        public RetryBackoffStrategy Strategy
        {
            get => this.strategy;
            set
            {
                if (!Enum.IsDefined(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"Undefined {nameof(RetryBackoffStrategy)}.");
                }

                this.strategy = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether non-idempotent requests (<see cref="ArgusVerb.POST"/> and
        /// <see cref="ArgusVerb.PATCH"/>) are retried when they fail after the request was written.
        /// Defaults to <c>false</c>, because the server may already have processed the request.
        /// </summary>
        public bool RetryNonIdempotentRequests { get; set; }

        /// <summary>
        /// Gets or sets the predicate that decides whether an exception is transient and the request may be retried.
        /// Defaults to <see cref="IsTransient"/>, which retries on <see cref="IOException"/> (including a broken pipe
        /// or a connection closed before a response was received). A <see cref="TimeoutException"/> or
        /// <see cref="OperationCanceledException"/> is never retried, regardless of this predicate.
        /// </summary>
        public Func<Exception, bool> ShouldRetry { get; set; } = IsTransient;

        /// <summary>
        /// The default <see cref="ShouldRetry"/> predicate: returns <c>true</c> for an <see cref="IOException"/>
        /// </summary>
        /// <param name="exception">
        /// The <see cref="Exception"/> raised by the failed attempt
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="exception"/> is an <see cref="IOException"/>; otherwise <c>false</c>
        /// </returns>
        public static bool IsTransient(Exception exception)
        {
            return exception is IOException;
        }

        /// <summary>
        /// Calculates the delay before the specified retry according to <see cref="Strategy"/>,
        /// <see cref="InitialDelay"/> and <see cref="MaxDelay"/>
        /// </summary>
        /// <param name="retryAttempt">
        /// The 1-based number of the retry
        /// </param>
        /// <returns>
        /// The delay before the retry, never more than <see cref="MaxDelay"/>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="retryAttempt"/> is less than 1
        /// </exception>
        public TimeSpan GetDelay(int retryAttempt)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(retryAttempt, 1);

            var factor = this.Strategy switch
            {
                RetryBackoffStrategy.Fixed => 1d,
                RetryBackoffStrategy.Linear => retryAttempt,
                _ => Math.Pow(2, retryAttempt - 1)
            };

            var ticks = this.InitialDelay.Ticks * factor;

            return ticks >= this.MaxDelay.Ticks ? this.MaxDelay : TimeSpan.FromTicks((long)ticks);
        }

        /// <summary>
        /// Determines whether the specified verb is idempotent, meaning that sending the request
        /// more than once has the same effect as sending it once
        /// </summary>
        /// <param name="verb">
        /// The <see cref="ArgusVerb"/> to check
        /// </param>
        /// <returns>
        /// <c>true</c> for <see cref="ArgusVerb.GET"/>, <see cref="ArgusVerb.HEAD"/>, <see cref="ArgusVerb.PUT"/>
        /// and <see cref="ArgusVerb.DELETE"/>; otherwise <c>false</c>
        /// </returns>
        public static bool IsIdempotent(ArgusVerb verb)
        {
            return verb is ArgusVerb.GET or ArgusVerb.HEAD or ArgusVerb.PUT or ArgusVerb.DELETE;
        }
    }
}
