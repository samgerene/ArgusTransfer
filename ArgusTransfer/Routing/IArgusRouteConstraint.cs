// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusRouteConstraint.cs">
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
