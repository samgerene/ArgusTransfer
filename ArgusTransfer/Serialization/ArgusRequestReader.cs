// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestReader.cs" company="Sam Gerené">
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

namespace ArgusTransfer.Serialization
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Deserializes an <see cref="ArgusRequest"/> from the ARGUS/1.0 text wire format
    /// </summary>
    public static class ArgusRequestReader
    {
        /// <summary>
        /// Deserializes an <see cref="ArgusRequest"/> from its text wire format representation
        /// </summary>
        /// <param name="text">
        /// The text containing the serialized request in ARGUS/1.0 wire format
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        public static ArgusRequest Read(string text)
        {
            using var reader = new StringReader(text);

            var requestLine = reader.ReadLine();

            if (string.IsNullOrWhiteSpace(requestLine))
            {
                throw new FormatException("Missing request line.");
            }

            var request = ParseRequestLine(requestLine);
            var contentLength = -1;

            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0)
                {
                    break;
                }

                contentLength = ParseHeader(line, request, contentLength);
            }

            if (contentLength > 0)
            {
                var bodyChars = new char[contentLength];
                var totalRead = 0;

                while (totalRead < contentLength)
                {
                    var read = reader.Read(bodyChars, totalRead, contentLength - totalRead);

                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                request.Body = new string(bodyChars, 0, totalRead);
            }

            return request;
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusRequest"/> from a <see cref="StreamReader"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read from
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        public static async Task<ArgusRequest> ReadAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var requestLine = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(requestLine))
            {
                throw new FormatException("Missing request line.");
            }

            var request = ParseRequestLine(requestLine);
            var contentLength = -1;

            string line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (line.Length == 0)
                {
                    break;
                }

                contentLength = ParseHeader(line, request, contentLength);
            }

            if (contentLength > 0)
            {
                var bodyChars = new char[contentLength];
                var totalRead = 0;

                while (totalRead < contentLength)
                {
                    var read = await reader.ReadAsync(bodyChars, totalRead, contentLength - totalRead);

                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                request.Body = new string(bodyChars, 0, totalRead);
            }

            return request;
        }

        /// <summary>
        /// Parses the request line (e.g. "POST /healthendpoint ARGUS/1.0") into an <see cref="ArgusRequest"/>
        /// </summary>
        private static ArgusRequest ParseRequestLine(string requestLine)
        {
            var parts = requestLine.Split(' ');

            if (parts.Length < 3)
            {
                throw new FormatException($"Invalid request line: {requestLine}");
            }

            if (!Enum.TryParse<ArgusVerb>(parts[0], true, out var verb))
            {
                throw new FormatException($"Unknown verb: {parts[0]}");
            }

            return new ArgusRequest
            {
                Verb = verb,
                Route = parts[1]
            };
        }

        /// <summary>
        /// Parses a single header line and applies it to the request
        /// </summary>
        private static int ParseHeader(string line, ArgusRequest request, int contentLength)
        {
            var colonIndex = line.IndexOf(':');

            if (colonIndex < 0)
            {
                return contentLength;
            }

            var name = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();

            if (string.Equals(name, "X-Correlation-Token", StringComparison.OrdinalIgnoreCase))
            {
                if (Guid.TryParse(value, out var guid))
                {
                    request.CorrelationToken = guid;
                }
            }
            else if (string.Equals(name, "X-Timestamp", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    request.Timestamp = timestamp;
                }
            }
            else if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
                {
                    contentLength = length;
                }
            }
            else if (!string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers[name] = value;
            }

            return contentLength;
        }
    }
}
