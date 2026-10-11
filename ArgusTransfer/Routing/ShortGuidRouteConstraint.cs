// ------------------------------------------------------------------------------------------------
// <copyright file="ShortGuidRouteConstraint.cs">
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
    /// A route constraint that validates the segment is a valid ShortGuid
    /// (a 22-character URL-safe base64-encoded <see cref="Guid"/>)
    /// </summary>
    public class ShortGuidRouteConstraint : IArgusRouteConstraint
    {
        /// <summary>
        /// Determines whether the route segment is a valid ShortGuid
        /// </summary>
        /// <param name="routeSegment">
        /// The route segment value to validate
        /// </param>
        /// <returns>
        /// <c>true</c> if the segment is a valid 22-character URL-safe base64-encoded <see cref="Guid"/>;
        /// otherwise, <c>false</c>
        /// </returns>
        public bool Match(string routeSegment)
        {
            if (routeSegment.Length != 22)
            {
                return false;
            }

            try
            {
                var base64 = routeSegment.Replace("_", "/").Replace("-", "+") + "==";
                _ = Convert.FromBase64String(base64);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
