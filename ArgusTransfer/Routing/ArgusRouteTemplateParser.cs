// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteTemplateParser.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides route template matching with parameter extraction
    /// </summary>
    /// <remarks>
    /// Supports literal segments, <c>{param}</c> parameter segments,
    /// and <c>{param:constraint}</c> constrained parameter segments.
    /// Matching is case-insensitive for literal segments.
    /// Built-in constraints include <c>Guid</c> and <c>ShortGuid</c>.
    /// Custom constraints can be registered via <see cref="RegisterConstraint"/>.
    /// </remarks>
    internal static class ArgusRouteTemplateParser
    {
        /// <summary>
        /// Registry of named route constraints
        /// </summary>
        private static readonly ConcurrentDictionary<string, IArgusRouteConstraint> Constraints =
            new ConcurrentDictionary<string, IArgusRouteConstraint>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the <see cref="ArgusRouteTemplateParser"/> class with built-in constraints
        /// </summary>
        static ArgusRouteTemplateParser()
        {
            Constraints["Guid"] = new GuidRouteConstraint();
            Constraints["ShortGuid"] = new ShortGuidRouteConstraint();
        }

        /// <summary>
        /// Registers a custom route constraint
        /// </summary>
        /// <param name="name">
        /// The constraint name used in route templates (e.g. "Guid" for <c>{param:Guid}</c>)
        /// </param>
        /// <param name="constraint">
        /// The <see cref="IArgusRouteConstraint"/> implementation
        /// </param>
        public static void RegisterConstraint(string name, IArgusRouteConstraint constraint)
        {
            Constraints[name] = constraint;
        }

        /// <summary>
        /// Gets the constraint registered under the specified name
        /// </summary>
        /// <param name="name">
        /// The constraint name (case-insensitive)
        /// </param>
        /// <param name="constraint">
        /// When the method returns <c>true</c>, the registered <see cref="IArgusRouteConstraint"/>
        /// </param>
        /// <returns>
        /// <c>true</c> if a constraint is registered under <paramref name="name"/>; otherwise, <c>false</c>
        /// </returns>
        public static bool TryGetConstraint(string name, out IArgusRouteConstraint constraint)
        {
            return Constraints.TryGetValue(name, out constraint);
        }

        /// <summary>
        /// Gets the names of the registered constraints, sorted and comma-separated, for use in error messages
        /// </summary>
        /// <returns>
        /// The registered constraint names (e.g. "Guid, ShortGuid")
        /// </returns>
        public static string GetConstraintNames()
        {
            return string.Join(", ", Constraints.Keys.Order(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Parses and validates a route template, so that errors surface when the route is registered instead of when
        /// a request is matched against it
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to parse (e.g. "/healthendpoint/{identifier:ShortGuid}")
        /// </param>
        /// <returns>
        /// The parsed <see cref="ArgusRouteTemplate"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> is <c>null</c>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a parameter segment has an empty name or references an empty or unknown constraint name
        /// </exception>
        public static ArgusRouteTemplate Parse(string routeTemplate)
        {
            return ArgusRouteTemplate.Parse(routeTemplate);
        }

        /// <summary>
        /// Attempts to match a route against a route template, extracting parameter values. The template is parsed
        /// on every call; the router parses each template once with <see cref="Parse"/> instead
        /// </summary>
        /// <param name="template">
        /// The route template (e.g. "/healthendpoint/{identifier:ShortGuid}")
        /// </param>
        /// <param name="route">
        /// The actual route to match (e.g. "/healthendpoint/kOWyz4q5vE2OVvXTiaw6jg")
        /// </param>
        /// <param name="routeValues">
        /// When the method returns <c>true</c>, contains the extracted parameter values keyed by parameter name;
        /// otherwise, <c>null</c>
        /// </param>
        /// <returns>
        /// <c>true</c> if the route matches the template; otherwise, <c>false</c>
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the route template is invalid, for example when it references an unknown constraint name
        /// </exception>
        public static bool TryMatch(string template, string route, out IReadOnlyDictionary<string, string> routeValues)
        {
            return Parse(template).TryMatch(route.Split('/'), out routeValues);
        }
    }
}
