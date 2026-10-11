// ------------------------------------------------------------------------------------------------
// <copyright file="RetryBackoffStrategy.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Client
{
    /// <summary>
    /// Specifies how the delay between retry attempts grows
    /// </summary>
    public enum RetryBackoffStrategy
    {
        /// <summary>
        /// Every retry waits <see cref="ArgusRetryPolicy.InitialDelay"/>
        /// </summary>
        Fixed,

        /// <summary>
        /// Retry n waits n × <see cref="ArgusRetryPolicy.InitialDelay"/>
        /// </summary>
        Linear,

        /// <summary>
        /// Retry n waits 2^(n-1) × <see cref="ArgusRetryPolicy.InitialDelay"/>
        /// </summary>
        Exponential
    }
}
