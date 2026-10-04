// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteEncoding.cs">
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
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Percent-encodes a route for the request line and decodes it again, so that a route containing spaces, <c>%</c> or
    /// control characters (including CR and LF) survives the wire unchanged and cannot split the request line or inject headers
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the path (before the first <c>?</c>), <c>%</c>, spaces and control characters are encoded; decoding restores
    /// them, so the server sees exactly the route the client set. An encoded slash (<c>%2F</c>) is never decoded, so it
    /// cannot become a path separator.
    /// </para>
    /// <para>
    /// A query embedded in the route (after the first <c>?</c>, e.g. <c>/items?page=1</c>) is assumed to be URL-encoded
    /// already: only spaces and control characters are encoded there, and the query is decoded by
    /// <see cref="ArgusQueryStringHelper.ParseQueryString"/>.
    /// </para>
    /// </remarks>
    internal static class ArgusRouteEncoding
    {
        /// <summary>
        /// Encodes a route, optionally followed by a query, for the request line
        /// </summary>
        /// <param name="route">
        /// The route, e.g. <c>/items/a b</c> or <c>/items?page=1</c>; <c>null</c> is treated as empty
        /// </param>
        /// <returns>
        /// The encoded route, containing no spaces or control characters
        /// </returns>
        public static string EncodeRoute(string route)
        {
            if (string.IsNullOrEmpty(route))
            {
                return string.Empty;
            }

            var queryStart = route.IndexOf('?');
            var path = queryStart < 0 ? route : route.Substring(0, queryStart);

            var sb = new StringBuilder(route.Length);
            Encode(sb, path, encodePercent: true);

            if (queryStart >= 0)
            {
                Encode(sb, route.Substring(queryStart), encodePercent: false);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Decodes the path of a route read from the request line. Percent-encoded sequences are decoded as UTF-8,
        /// except <c>%2F</c>, which is kept as is; invalid sequences are kept as is.
        /// </summary>
        /// <param name="path">
        /// The encoded path, without the query
        /// </param>
        /// <returns>
        /// The decoded path
        /// </returns>
        public static string DecodePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('%') < 0)
            {
                return path;
            }

            using var bytes = new MemoryStream(path.Length);
            var utf8 = new byte[4];

            var position = 0;

            while (position < path.Length)
            {
                if (path[position] == '%'
                    && position + 2 < path.Length
                    && byte.TryParse(path.AsSpan(position + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
                    && value != (byte)'/')
                {
                    bytes.WriteByte(value);
                    position += 3;
                    continue;
                }

                // A surrogate pair is encoded together (4 bytes); any other character, including a lone surrogate, on its own
                var charCount = position + 1 < path.Length && char.IsSurrogatePair(path[position], path[position + 1]) ? 2 : 1;
                var count = Encoding.UTF8.GetBytes(path.AsSpan(position, charCount), utf8);
                bytes.Write(utf8, 0, count);
                position += charCount;
            }

            return Encoding.UTF8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
        }

        /// <summary>
        /// Appends a part of a route, percent-encoding the characters that are not allowed on the request line
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="part">
        /// The part of the route to encode
        /// </param>
        /// <param name="encodePercent">
        /// Whether <c>%</c> is encoded too (in the path), or treated as an existing escape (in a query)
        /// </param>
        private static void Encode(StringBuilder sb, string part, bool encodePercent)
        {
            foreach (var c in part)
            {
                if (c == ' ' || char.IsControl(c) || (encodePercent && c == '%'))
                {
                    foreach (var b in Encoding.UTF8.GetBytes(c.ToString()))
                    {
                        sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
    }
}
