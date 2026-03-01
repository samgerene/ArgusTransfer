// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusResponseReader.cs" company="Sam Gerené">
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
    /// Deserializes an <see cref="ArgusResponse"/> from the ARGUS/1.0 text wire format
    /// </summary>
    public static class ArgusResponseReader
    {
        /// <summary>
        /// Deserializes an <see cref="ArgusResponse"/> from its text wire format representation
        /// </summary>
        /// <param name="text">
        /// The text containing the serialized response in ARGUS/1.0 wire format
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse Read(string text)
        {
            using var reader = new StringReader(text);

            var statusLine = reader.ReadLine();

            if (string.IsNullOrWhiteSpace(statusLine))
            {
                throw new FormatException("Missing status line.");
            }

            var response = ParseStatusLine(statusLine);
            var contentLength = -1;

            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0)
                {
                    break;
                }

                contentLength = ParseHeader(line, response, contentLength);
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

                response.Body = new string(bodyChars, 0, totalRead);
            }

            return response;
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusResponse"/> from a <see cref="StreamReader"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read from
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        public static async Task<ArgusResponse> ReadAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var statusLine = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(statusLine))
            {
                throw new FormatException("Missing status line.");
            }

            var response = ParseStatusLine(statusLine);
            var contentLength = -1;

            string line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (line.Length == 0)
                {
                    break;
                }

                contentLength = ParseHeader(line, response, contentLength);
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

                response.Body = new string(bodyChars, 0, totalRead);
            }

            return response;
        }

        /// <summary>
        /// Parses the status line (e.g. "ARGUS/1.0 200 OK") into an <see cref="ArgusResponse"/>
        /// </summary>
        private static ArgusResponse ParseStatusLine(string statusLine)
        {
            var firstSpace = statusLine.IndexOf(' ');

            if (firstSpace < 0)
            {
                throw new FormatException($"Invalid status line: {statusLine}");
            }

            var afterVersion = statusLine.Substring(firstSpace + 1);
            var secondSpace = afterVersion.IndexOf(' ');

            if (secondSpace < 0)
            {
                throw new FormatException($"Invalid status line: {statusLine}");
            }

            var codeStr = afterVersion.Substring(0, secondSpace);

            if (!int.TryParse(codeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                throw new FormatException($"Invalid status code: {codeStr}");
            }

            if (!ArgusStatusCodeExtensions.TryParse(code, out var statusCode))
            {
                throw new FormatException($"Unknown status code: {code}");
            }

            return new ArgusResponse
            {
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// Parses a single header line and applies it to the response
        /// </summary>
        private static int ParseHeader(string line, ArgusResponse response, int contentLength)
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
                    response.CorrelationToken = guid;
                }
            }
            else if (string.Equals(name, "X-Timestamp", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    response.Timestamp = timestamp;
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
                response.Headers[name] = value;
            }

            return contentLength;
        }
    }
}
