// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusRouteConstraint.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    /// <summary>
    /// Defines a constraint that a route segment must satisfy for a route template to match
    /// </summary>
    public interface IArgusRouteConstraint
    {
        /// <summary>
        /// Determines whether the route segment satisfies the constraint
        /// </summary>
        /// <param name="routeSegment">
        /// The route segment value to validate
        /// </param>
        /// <returns>
        /// <c>true</c> if the segment satisfies the constraint; otherwise, <c>false</c>
        /// </returns>
        bool Match(string routeSegment);
    }
}
