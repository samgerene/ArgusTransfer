// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusRequestDelegate.cs">
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
    /// A function that can process an Argus request within the middleware pipeline
    /// </summary>
    /// <param name="context">
    /// The <see cref="ArgusContext"/> for the current request
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> representing the asynchronous operation
    /// </returns>
    public delegate Task ArgusRequestDelegate(ArgusContext context);
}
