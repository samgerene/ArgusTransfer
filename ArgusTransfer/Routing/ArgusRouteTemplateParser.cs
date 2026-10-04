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
        /// Validates a route template so that errors surface when the route is registered instead of when a request
        /// is matched against it
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template to validate (e.g. "/healthendpoint/{identifier:ShortGuid}")
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="routeTemplate"/> is <c>null</c>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a parameter segment has an empty name or references an empty or unknown constraint name
        /// </exception>
        public static void Validate(string routeTemplate)
        {
            ArgumentNullException.ThrowIfNull(routeTemplate);

            foreach (var segment in routeTemplate.Split('/'))
            {
                if (!segment.StartsWith('{') || !segment.EndsWith('}'))
                {
                    continue;
                }

                var paramContent = segment.Substring(1, segment.Length - 2);
                var colonIndex = paramContent.IndexOf(':');
                var paramName = colonIndex >= 0 ? paramContent.Substring(0, colonIndex) : paramContent;

                if (string.IsNullOrWhiteSpace(paramName))
                {
                    throw new ArgumentException($"The route template '{routeTemplate}' contains a parameter without a name: '{segment}'.", nameof(routeTemplate));
                }

                if (colonIndex < 0)
                {
                    continue;
                }

                var constraint = paramContent.Substring(colonIndex + 1);

                if (!Constraints.ContainsKey(constraint))
                {
                    throw new ArgumentException($"The route template '{routeTemplate}' references an unknown route constraint: '{constraint}'. Known constraints: {string.Join(", ", Constraints.Keys.Order(StringComparer.OrdinalIgnoreCase))}.", nameof(routeTemplate));
                }
            }
        }

        /// <summary>
        /// Attempts to match a route against a route template, extracting parameter values
        /// </summary>
        /// <param name="template">
        /// The route template (e.g. "/healthendpoint/{identifier:ShortGuid}")
        /// </param>
        /// <param name="route">
        /// The actual route to match (e.g. "/healthendpoint/kOWyz4q5vE2OVvXTiaw6jg")
        /// </param>
        /// <param name="routeValues">
        /// When the method returns <c>true</c>, contains the extracted parameter values keyed by parameter name
        /// </param>
        /// <returns>
        /// <c>true</c> if the route matches the template; otherwise, <c>false</c>
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the route template references an unknown constraint name; cannot happen for templates
        /// registered through <see cref="ArgusRouter"/>, which validates them with <see cref="Validate"/>
        /// </exception>
        public static bool TryMatch(string template, string route, out IReadOnlyDictionary<string, string> routeValues)
        {
            routeValues = new Dictionary<string, string>();

            var templateSegments = template.Split('/');
            var routeSegments = route.Split('/');

            if (templateSegments.Length != routeSegments.Length)
            {
                return false;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < templateSegments.Length; i++)
            {
                var templateSegment = templateSegments[i];
                var routeSegment = routeSegments[i];

                if (templateSegment.StartsWith('{') && templateSegment.EndsWith('}'))
                {
                    var paramContent = templateSegment.Substring(1, templateSegment.Length - 2);

                    var colonIndex = paramContent.IndexOf(':');

                    if (colonIndex >= 0)
                    {
                        var paramName = paramContent.Substring(0, colonIndex);
                        var constraint = paramContent.Substring(colonIndex + 1);

                        if (Constraints.TryGetValue(constraint, out var routeConstraint))
                        {
                            if (!routeConstraint.Match(routeSegment))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unknown route constraint: '{constraint}'");
                        }

                        values[paramName] = routeSegment;
                    }
                    else
                    {
                        values[paramContent] = routeSegment;
                    }
                }
                else
                {
                    if (!string.Equals(templateSegment, routeSegment, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            routeValues = values;
            return true;
        }
    }
}
