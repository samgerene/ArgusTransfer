// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusQueryStringHelper.cs">
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

namespace ArgusTransfer.Protocol
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Provides helper methods for building and parsing query strings
    /// in the ARGUS/1.0 wire format
    /// </summary>
    internal static class ArgusQueryStringHelper
    {
        /// <summary>
        /// Builds a query string from the specified dictionary of parameters
        /// </summary>
        /// <param name="queryParameters">
        /// The dictionary of query parameter key-value pairs
        /// </param>
        /// <returns>
        /// An empty string when the dictionary is empty, or a query string
        /// prefixed with '?' (e.g. "?key1=val1&amp;key2=val2") with percent-encoded keys and values
        /// </returns>
        internal static string BuildQueryString(Dictionary<string, string> queryParameters)
        {
            if (queryParameters == null || queryParameters.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append('?');

            var first = true;

            foreach (var kvp in queryParameters)
            {
                if (!first)
                {
                    sb.Append('&');
                }

                sb.Append(Uri.EscapeDataString(kvp.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kvp.Value));

                first = false;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses a query string and populates the target dictionary.
        /// Duplicate keys are resolved by last-value-wins semantics
        /// </summary>
        /// <param name="queryString">
        /// The query string to parse (without the leading '?')
        /// </param>
        /// <param name="target">
        /// The dictionary to populate with the parsed key-value pairs
        /// </param>
        internal static void ParseQueryString(string queryString, Dictionary<string, string> target)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                return;
            }

            var pairs = queryString.Split('&');

            foreach (var pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                var equalsIndex = pair.IndexOf('=');

                if (equalsIndex < 0)
                {
                    target[Uri.UnescapeDataString(pair)] = string.Empty;
                }
                else
                {
                    var key = Uri.UnescapeDataString(pair.Substring(0, equalsIndex));
                    var value = Uri.UnescapeDataString(pair.Substring(equalsIndex + 1));
                    target[key] = value;
                }
            }
        }
    }
}
