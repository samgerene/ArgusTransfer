// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusHandlerDelegate.cs">
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
    /// Delegate that handles an Argus request by populating the response
    /// on the provided <see cref="ArgusContext"/>
    /// </summary>
    /// <param name="context">
    /// The <see cref="ArgusContext"/> for the current request
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>
    /// </returns>
    public delegate Task ArgusHandlerDelegate(ArgusContext context);
}
