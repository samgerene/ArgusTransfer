// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusModule.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    /// <summary>
    /// Contract for modules that register route handlers with the Argus IPC router
    /// </summary>
    public interface IArgusModule
    {
        /// <summary>
        /// Registers route handlers on the provided <see cref="IArgusRouteBuilder"/>
        /// </summary>
        /// <param name="app">
        /// The <see cref="IArgusRouteBuilder"/> to register routes on
        /// </param>
        void AddRoutes(IArgusRouteBuilder app);
    }
}
