// ------------------------------------------------------------------------------------------------
// <copyright file="GuidRouteConstraint.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Routing
{
    using System;

    /// <summary>
    /// A route constraint that validates the segment is a valid <see cref="Guid"/>
    /// </summary>
    public class GuidRouteConstraint : IArgusRouteConstraint
    {
        /// <summary>
        /// Determines whether the route segment is a valid <see cref="Guid"/>
        /// </summary>
        /// <param name="routeSegment">
        /// The route segment value to validate
        /// </param>
        /// <returns>
        /// <c>true</c> if the segment is a valid <see cref="Guid"/>; otherwise, <c>false</c>
        /// </returns>
        public bool Match(string routeSegment)
        {
            return Guid.TryParse(routeSegment, out _);
        }
    }
}
