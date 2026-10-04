// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusContentNegotiation.cs">
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

namespace ArgusTransfer.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Selects the <see cref="IArgusBodySerializer"/> for a response from the media ranges of an <c>Accept</c> header
    /// </summary>
    /// <remarks>
    /// The header is a comma-separated list of media ranges (<c>type/subtype</c>, <c>type/*</c> or <c>*/*</c>), each
    /// with an optional quality value (<c>;q=0.5</c>, default 1). A serializer is acceptable when its content type
    /// matches a range; the most specific matching range determines its quality, and quality 0 excludes it. The
    /// acceptable serializer with the highest quality wins; ties go to the more specific range, then to the range listed
    /// first, then to the default serializer, then to registration order. Media range parameters other than <c>q</c> are
    /// ignored, and malformed ranges are skipped.
    /// </remarks>
    internal static class ArgusContentNegotiation
    {
        /// <summary>
        /// Selects the serializer for a response
        /// </summary>
        /// <param name="registry">
        /// The <see cref="IArgusBodySerializerRegistry"/> with the available serializers
        /// </param>
        /// <param name="accept">
        /// The value of the request's <c>Accept</c> header; <c>null</c> or empty means any media type
        /// </param>
        /// <returns>
        /// The selected <see cref="IArgusBodySerializer"/>; <see cref="IArgusBodySerializerRegistry.DefaultSerializer"/>
        /// when the header expresses no usable preference; or <c>null</c> when no registered serializer is acceptable
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="registry"/> is <c>null</c>
        /// </exception>
        public static IArgusBodySerializer SelectSerializer(IArgusBodySerializerRegistry registry, string accept)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var ranges = Parse(accept);

            if (ranges.Count == 0)
            {
                return registry.DefaultSerializer;
            }

            IArgusBodySerializer best = null;
            MediaRange bestRange = default;

            foreach (var candidate in GetCandidates(registry, ranges))
            {
                if (!TryGetMostSpecificRange(ranges, candidate.ContentType, out var range) || range.Quality <= 0)
                {
                    continue;
                }

                if (best == null || IsBetter(range, candidate, bestRange, best, registry.DefaultSerializer))
                {
                    best = candidate;
                    bestRange = range;
                }
            }

            return best;
        }

        /// <summary>
        /// Parses the media ranges of an <c>Accept</c> header, skipping malformed ones
        /// </summary>
        /// <param name="accept">
        /// The header value
        /// </param>
        /// <returns>
        /// The parsed media ranges in header order
        /// </returns>
        internal static List<MediaRange> Parse(string accept)
        {
            var ranges = new List<MediaRange>();

            if (string.IsNullOrWhiteSpace(accept))
            {
                return ranges;
            }

            var parts = accept.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var pieces = parts[i].Split(';');

                if (TrySplitMediaType(pieces[0], out var type, out var subtype)
                    && (type != "*" || subtype == "*")
                    && TryGetQuality(pieces, out var quality))
                {
                    ranges.Add(new MediaRange(type, subtype, quality, i));
                }
            }

            return ranges;
        }

        /// <summary>
        /// Gets the serializers to consider: all registered serializers, the default serializer, and the serializers
        /// registered for the exact media types in the header (for registries that do not enumerate all serializers)
        /// </summary>
        /// <param name="registry">
        /// The <see cref="IArgusBodySerializerRegistry"/>
        /// </param>
        /// <param name="ranges">
        /// The parsed media ranges
        /// </param>
        /// <returns>
        /// The distinct candidate serializers
        /// </returns>
        private static List<IArgusBodySerializer> GetCandidates(IArgusBodySerializerRegistry registry, List<MediaRange> ranges)
        {
            var candidates = new List<IArgusBodySerializer>();

            foreach (var serializer in registry.GetSerializers() ?? [])
            {
                AddCandidate(candidates, serializer);
            }

            AddCandidate(candidates, registry.DefaultSerializer);

            foreach (var range in ranges)
            {
                if (range.Specificity == 2 && registry.TryGetSerializer($"{range.Type}/{range.Subtype}", out var serializer))
                {
                    AddCandidate(candidates, serializer);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Adds a serializer to the candidates unless it is <c>null</c> or already present
        /// </summary>
        /// <param name="candidates">
        /// The candidate list
        /// </param>
        /// <param name="serializer">
        /// The serializer to add
        /// </param>
        private static void AddCandidate(List<IArgusBodySerializer> candidates, IArgusBodySerializer serializer)
        {
            if (serializer != null && !candidates.Contains(serializer))
            {
                candidates.Add(serializer);
            }
        }

        /// <summary>
        /// Finds the most specific media range that matches a content type; among equally specific ranges the first
        /// listed one is used
        /// </summary>
        /// <param name="ranges">
        /// The parsed media ranges
        /// </param>
        /// <param name="contentType">
        /// The content type of a serializer (parameters are ignored)
        /// </param>
        /// <param name="match">
        /// When the method returns <c>true</c>, the matching media range
        /// </param>
        /// <returns>
        /// <c>true</c> when a range matches; otherwise, <c>false</c>
        /// </returns>
        private static bool TryGetMostSpecificRange(List<MediaRange> ranges, string contentType, out MediaRange match)
        {
            match = default;

            if (!TrySplitMediaType(contentType?.Split(';')[0], out var type, out var subtype))
            {
                return false;
            }

            var found = false;

            foreach (var range in ranges)
            {
                if (range.Matches(type, subtype) && (!found || range.Specificity > match.Specificity))
                {
                    match = range;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Determines whether a candidate is preferred over the current best
        /// </summary>
        /// <param name="range">
        /// The range that determines the candidate's quality
        /// </param>
        /// <param name="candidate">
        /// The candidate serializer
        /// </param>
        /// <param name="bestRange">
        /// The range that determines the current best's quality
        /// </param>
        /// <param name="best">
        /// The current best serializer
        /// </param>
        /// <param name="defaultSerializer">
        /// The registry's default serializer
        /// </param>
        /// <returns>
        /// <c>true</c> when the candidate is preferred
        /// </returns>
        private static bool IsBetter(MediaRange range, IArgusBodySerializer candidate, MediaRange bestRange, IArgusBodySerializer best, IArgusBodySerializer defaultSerializer)
        {
            if (range.Quality.CompareTo(bestRange.Quality) != 0)
            {
                return range.Quality > bestRange.Quality;
            }

            if (range.Specificity != bestRange.Specificity)
            {
                return range.Specificity > bestRange.Specificity;
            }

            if (range.Index != bestRange.Index)
            {
                return range.Index < bestRange.Index;
            }

            // Same range (a wildcard matching several serializers): prefer the default; otherwise keep the earlier one
            return candidate == defaultSerializer && best != defaultSerializer;
        }

        /// <summary>
        /// Splits a media type into its type and subtype, lower-cased; <c>*</c> alone is read as <c>*/*</c>
        /// </summary>
        /// <param name="mediaType">
        /// The media type, e.g. <c>application/json</c>
        /// </param>
        /// <param name="type">
        /// The type, e.g. <c>application</c>
        /// </param>
        /// <param name="subtype">
        /// The subtype, e.g. <c>json</c>
        /// </param>
        /// <returns>
        /// <c>true</c> when the media type is well-formed; otherwise, <c>false</c>
        /// </returns>
        private static bool TrySplitMediaType(string mediaType, out string type, out string subtype)
        {
            type = null;
            subtype = null;

            var trimmed = mediaType?.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (trimmed == "*")
            {
                trimmed = "*/*";
            }

            var slash = trimmed.IndexOf('/');

            if (slash <= 0 || slash == trimmed.Length - 1 || trimmed.IndexOf('/', slash + 1) >= 0)
            {
                return false;
            }

            type = trimmed.Substring(0, slash).Trim().ToLowerInvariant();
            subtype = trimmed.Substring(slash + 1).Trim().ToLowerInvariant();

            return type.Length > 0 && subtype.Length > 0;
        }

        /// <summary>
        /// Reads the quality value from the parameters of a media range
        /// </summary>
        /// <param name="pieces">
        /// The media range split at <c>';'</c>; the first piece is the media type
        /// </param>
        /// <param name="quality">
        /// The quality value between 0 and 1; 1 when the range has no <c>q</c> parameter
        /// </param>
        /// <returns>
        /// <c>false</c> when the <c>q</c> parameter is not a number between 0 and 1; otherwise, <c>true</c>
        /// </returns>
        private static bool TryGetQuality(string[] pieces, out double quality)
        {
            quality = 1;

            for (var i = 1; i < pieces.Length; i++)
            {
                var parameter = pieces[i].Split('=', 2);

                if (parameter.Length == 2 && string.Equals(parameter[0].Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    return double.TryParse(parameter[1].Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quality)
                        && quality >= 0
                        && quality <= 1;
                }
            }

            return true;
        }

        /// <summary>
        /// A parsed media range of an <c>Accept</c> header
        /// </summary>
        /// <param name="Type">
        /// The lower-cased type, or <c>*</c>
        /// </param>
        /// <param name="Subtype">
        /// The lower-cased subtype, or <c>*</c>
        /// </param>
        /// <param name="Quality">
        /// The quality value between 0 and 1
        /// </param>
        /// <param name="Index">
        /// The position of the range in the header
        /// </param>
        internal readonly record struct MediaRange(string Type, string Subtype, double Quality, int Index)
        {
            /// <summary>
            /// Gets the specificity of the range: 2 for <c>type/subtype</c>, 1 for <c>type/*</c>, 0 for <c>*/*</c>
            /// </summary>
            public int Specificity
            {
                get
                {
                    if (this.Type == "*")
                    {
                        return 0;
                    }

                    return this.Subtype == "*" ? 1 : 2;
                }
            }

            /// <summary>
            /// Determines whether the range matches a media type
            /// </summary>
            /// <param name="type">
            /// The lower-cased type
            /// </param>
            /// <param name="subtype">
            /// The lower-cased subtype
            /// </param>
            /// <returns>
            /// <c>true</c> when the range matches
            /// </returns>
            public bool Matches(string type, string subtype)
            {
                return this.Specificity switch
                {
                    0 => true,
                    1 => this.Type == type,
                    _ => this.Type == type && this.Subtype == subtype
                };
            }
        }
    }
}
