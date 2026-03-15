// -------------------------------------------------------------------------------------------------
//   <copyright file="ShortGuidRouteConstraint.cs">
//
//     Copyright (c) 2026 Sam Gerené
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
                var bytes = Convert.FromBase64String(base64);
                return bytes.Length == 16;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
