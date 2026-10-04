// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteTemplate.cs">
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
    using System.Collections.Generic;

    /// <summary>
    /// A parsed and validated route template: the template is split into segments and its constraints are resolved
    /// once, when the endpoint is registered, so matching a request does not parse the template again
    /// </summary>
    internal sealed class ArgusRouteTemplate
    {
        /// <summary>
        /// The parsed segments of the template
        /// </summary>
        private readonly Segment[] segments;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRouteTemplate"/> class
        /// </summary>
        /// <param name="text">
        /// The route template text
        /// </param>
        /// <param name="segments">
        /// The parsed segments of the template
        /// </param>
        private ArgusRouteTemplate(string text, Segment[] segments)
        {
            this.Text = text;
            this.segments = segments;
        }

        /// <summary>
        /// Gets the route template text (e.g. "/healthendpoint/{identifier:Guid}")
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Parses and validates a route template
        /// </summary>
        /// <param name="routeTemplate">
        /// The route template (e.g. "/healthendpoint/{identifier:ShortGuid}")
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
            ArgumentNullException.ThrowIfNull(routeTemplate);

            var texts = routeTemplate.Split('/');
            var segments = new Segment[texts.Length];

            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];

                if (!text.StartsWith('{') || !text.EndsWith('}'))
                {
                    segments[i] = new Segment(text, null, null);
                    continue;
                }

                var paramContent = text.Substring(1, text.Length - 2);
                var colonIndex = paramContent.IndexOf(':');
                var paramName = colonIndex >= 0 ? paramContent.Substring(0, colonIndex) : paramContent;

                if (string.IsNullOrWhiteSpace(paramName))
                {
                    throw new ArgumentException($"The route template '{routeTemplate}' contains a parameter without a name: '{text}'.", nameof(routeTemplate));
                }

                IArgusRouteConstraint constraint = null;

                if (colonIndex >= 0)
                {
                    var constraintName = paramContent.Substring(colonIndex + 1);
                    if (!ArgusRouteTemplateParser.TryGetConstraint(constraintName, out constraint))
                    {
                        throw new ArgumentException($"The route template '{routeTemplate}' references an unknown route constraint: '{constraintName}'. Known constraints: {ArgusRouteTemplateParser.GetConstraintNames()}.", nameof(routeTemplate));
                    }
                }

                segments[i] = new Segment(null, paramName, constraint);
            }

            return new ArgusRouteTemplate(routeTemplate, segments);
        }

        /// <summary>
        /// Attempts to match a route, already split at <c>'/'</c>, against this template, extracting parameter values
        /// </summary>
        /// <param name="routeSegments">
        /// The segments of the route to match (e.g. the result of <c>"/healthendpoint/kOWyz4q5vE2OVvXTiaw6jg".Split('/')</c>)
        /// </param>
        /// <param name="routeValues">
        /// When the method returns <c>true</c>, contains the extracted parameter values keyed by parameter name
        /// (case-insensitive); otherwise, <c>null</c>
        /// </param>
        /// <returns>
        /// <c>true</c> if the route matches the template; otherwise, <c>false</c>
        /// </returns>
        public bool TryMatch(string[] routeSegments, out IReadOnlyDictionary<string, string> routeValues)
        {
            routeValues = null;

            if (routeSegments.Length != this.segments.Length)
            {
                return false;
            }

            for (var i = 0; i < this.segments.Length; i++)
            {
                var segment = this.segments[i];

                if (segment.ParameterName == null)
                {
                    if (!string.Equals(segment.Literal, routeSegments[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                else if (segment.Constraint != null && !segment.Constraint.Match(routeSegments[i]))
                {
                    return false;
                }
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < this.segments.Length; i++)
            {
                if (this.segments[i].ParameterName != null)
                {
                    values[this.segments[i].ParameterName] = routeSegments[i];
                }
            }

            routeValues = values;
            return true;
        }

        /// <summary>
        /// A parsed template segment: either a literal or a parameter with an optional constraint
        /// </summary>
        /// <param name="Literal">
        /// The literal text, or <c>null</c> for a parameter segment
        /// </param>
        /// <param name="ParameterName">
        /// The parameter name, or <c>null</c> for a literal segment
        /// </param>
        /// <param name="Constraint">
        /// The resolved constraint of a parameter segment, or <c>null</c> when it has none
        /// </param>
        private readonly record struct Segment(string Literal, string ParameterName, IArgusRouteConstraint Constraint);
    }
}
