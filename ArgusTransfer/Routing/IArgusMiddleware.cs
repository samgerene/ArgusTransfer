// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusMiddleware.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a middleware component that can inspect, modify, or short-circuit
    /// an Argus request as it flows through the pipeline
    /// </summary>
    public interface IArgusMiddleware
    {
        /// <summary>
        /// Processes an Argus request. Implementations should call <paramref name="next"/>
        /// to pass control to the next middleware in the pipeline, or skip it to short-circuit
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> for the current request
        /// </param>
        /// <param name="next">
        /// The <see cref="ArgusRequestDelegate"/> representing the next middleware in the pipeline
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next);
    }
}
