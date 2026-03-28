// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTextResponseSerializer.cs">
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
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Serializes and deserializes <see cref="ArgusResponse"/> messages using the ARGUS/1.0 text wire format
    /// </summary>
    public class ArgusResponseSerializer
    {
        /// <summary>
        /// The <see cref="IArgusBodySerializer"/> used to serialize and deserialize the message body
        /// </summary>
        private readonly IArgusBodySerializer bodySerializer;

        /// <summary>
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </summary>
        private readonly IArgusBodySerializerRegistry bodySerializerRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusResponseSerializer"/> class
        /// with the default <see cref="PlainTextArgusBodySerializer"/>
        /// </summary>
        public ArgusResponseSerializer() : this(new PlainTextArgusBodySerializer())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusResponseSerializer"/> class
        /// </summary>
        /// <param name="bodySerializer">
        /// The <see cref="IArgusBodySerializer"/> used to serialize and deserialize the message body
        /// </param>
        public ArgusResponseSerializer(IArgusBodySerializer bodySerializer)
        {
            this.bodySerializer = bodySerializer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusResponseSerializer"/> class
        /// with an <see cref="IArgusBodySerializerRegistry"/> for content-type based serializer resolution
        /// </summary>
        /// <param name="bodySerializerRegistry">
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </param>
        public ArgusResponseSerializer(IArgusBodySerializerRegistry bodySerializerRegistry)
            : this(bodySerializerRegistry.DefaultSerializer)
        {
            this.bodySerializerRegistry = bodySerializerRegistry;
        }

        /// <summary>
        /// Serializes an <see cref="ArgusResponse"/> to its text wire format representation
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <returns>
        /// A string containing the serialized response in ARGUS/1.0 wire format
        /// </returns>
        public string Write(ArgusResponse response)
        {
            var contentType = response.Headers.TryGetValue("Content-Type", out var ct) ? ct : null;
            var resolvedSerializer = this.ResolveSerializer(contentType);

            return this.WriteCore(response, resolvedSerializer);
        }

        /// <summary>
        /// Serializes an <see cref="ArgusResponse"/> to its text wire format representation
        /// using a serializer resolved from the specified accept content type
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        /// <returns>
        /// A string containing the serialized response in ARGUS/1.0 wire format
        /// </returns>
        public string Write(ArgusResponse response, string acceptContentType)
        {
            var resolvedSerializer = this.ResolveSerializer(acceptContentType);

            if (!response.Headers.ContainsKey("Content-Type"))
            {
                response.Headers["Content-Type"] = resolvedSerializer.ContentType;
            }

            return this.WriteCore(response, resolvedSerializer);
        }

        /// <summary>
        /// Writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        public void Write(StreamWriter writer, ArgusResponse response)
        {
            writer.Write(this.Write(response));
            writer.Flush();
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>.
        /// This method supports streaming bodies via chunked transfer encoding.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task WriteAsync(StreamWriter writer, ArgusResponse response, CancellationToken cancellationToken = default)
        {
            if (!response.IsStreamed)
            {
                await writer.WriteAsync(this.Write(response));
                await writer.FlushAsync(cancellationToken);
                return;
            }

            var sb = new StringBuilder();

            sb.Append("ARGUS/1.0 ");
            sb.Append(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(response.StatusCode.ToReasonPhrase());
            sb.Append("\r\n");

            sb.Append("X-Correlation-Token: ");
            sb.Append(response.CorrelationToken.ToString());
            sb.Append("\r\n");

            sb.Append("X-Timestamp: ");
            sb.Append(response.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append("\r\n");

            foreach (var header in response.Headers)
            {
                sb.Append(header.Key);
                sb.Append(": ");
                sb.Append(header.Value);
                sb.Append("\r\n");
            }

            if (!response.Headers.ContainsKey("Content-Type"))
            {
                sb.Append("Content-Type: application/octet-stream\r\n");
            }

            sb.Append("Transfer-Encoding: chunked\r\n");
            sb.Append("\r\n");

            await writer.WriteAsync(sb.ToString());
            await writer.FlushAsync(cancellationToken);

            await ArgusChunkedEncoding.WriteChunkedAsync(response.BodyStream, writer, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// using a serializer resolved from the specified accept content type
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        public void Write(StreamWriter writer, ArgusResponse response, string acceptContentType)
        {
            writer.Write(this.Write(response, acceptContentType));
            writer.Flush();
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// using a serializer resolved from the specified accept content type
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task WriteAsync(StreamWriter writer, ArgusResponse response, string acceptContentType, CancellationToken cancellationToken = default)
        {
            if (!response.IsStreamed)
            {
                await writer.WriteAsync(this.Write(response, acceptContentType));
                await writer.FlushAsync(cancellationToken);
                return;
            }

            await this.WriteAsync(writer, response, cancellationToken);
        }

        /// <summary>
        /// Deserializes an <see cref="ArgusResponse"/> from its text wire format representation
        /// </summary>
        /// <param name="text">
        /// The text containing the serialized response in ARGUS/1.0 wire format
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        public ArgusResponse Read(string text)
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

            var isChunked = response.Headers.TryGetValue("Transfer-Encoding", out var te)
                && string.Equals(te, "chunked", StringComparison.OrdinalIgnoreCase);

            if (isChunked)
            {
                response.BodyStream = ArgusChunkedEncoding.ReadChunked(reader);
            }
            else if (contentLength > 0)
            {
                var contentType = response.Headers.TryGetValue("Content-Type", out var ct) ? ct : null;
                var resolvedSerializer = this.ResolveSerializer(contentType);

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

                response.Body = resolvedSerializer.ReadBody(new string(bodyChars, 0, totalRead));
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
        public async Task<ArgusResponse> ReadAsync(StreamReader reader, CancellationToken cancellationToken)
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

            var isChunked = response.Headers.TryGetValue("Transfer-Encoding", out var teValue)
                && string.Equals(teValue, "chunked", StringComparison.OrdinalIgnoreCase);

            if (isChunked)
            {
                response.BodyStream = await ArgusChunkedEncoding.ReadChunkedAsync(reader, cancellationToken: cancellationToken);
            }
            else if (contentLength > 0)
            {
                var contentType = response.Headers.TryGetValue("Content-Type", out var ct) ? ct : null;
                var resolvedSerializer = this.ResolveSerializer(contentType);

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

                response.Body = resolvedSerializer.ReadBody(new string(bodyChars, 0, totalRead));
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
            else
            {
                response.Headers[name] = value;
            }

            return contentLength;
        }

        /// <summary>
        /// Core write logic that serializes an <see cref="ArgusResponse"/> using the specified serializer
        /// </summary>
        private string WriteCore(ArgusResponse response, IArgusBodySerializer serializer)
        {
            var sb = new StringBuilder();

            sb.Append("ARGUS/1.0 ");
            sb.Append(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(response.StatusCode.ToReasonPhrase());
            sb.Append("\r\n");

            sb.Append("X-Correlation-Token: ");
            sb.Append(response.CorrelationToken.ToString());
            sb.Append("\r\n");

            sb.Append("X-Timestamp: ");
            sb.Append(response.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append("\r\n");

            foreach (var header in response.Headers)
            {
                sb.Append(header.Key);
                sb.Append(": ");
                sb.Append(header.Value);
                sb.Append("\r\n");
            }

            string serializedBody = null;

            if (response.IsStreamed)
            {
                if (!response.Headers.ContainsKey("Content-Type"))
                {
                    sb.Append("Content-Type: application/octet-stream\r\n");
                }

                sb.Append("Transfer-Encoding: chunked\r\n");
                sb.Append("\r\n");

                ArgusChunkedEncoding.WriteChunked(response.BodyStream, sb);
            }
            else
            {
                if (!string.IsNullOrEmpty(response.Body))
                {
                    serializedBody = serializer.WriteBody(response.Body);
                    var bodyBytes = Encoding.UTF8.GetByteCount(serializedBody);

                    if (!response.Headers.ContainsKey("Content-Type"))
                    {
                        sb.Append("Content-Type: ");
                        sb.Append(serializer.ContentType);
                        sb.Append("\r\n");
                    }

                    sb.Append("Content-Length: ");
                    sb.Append(bodyBytes.ToString(CultureInfo.InvariantCulture));
                    sb.Append("\r\n");
                }

                sb.Append("\r\n");

                if (serializedBody != null)
                {
                    sb.Append(serializedBody);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resolves the appropriate <see cref="IArgusBodySerializer"/> for the specified content type
        /// </summary>
        private IArgusBodySerializer ResolveSerializer(string contentType)
        {
            if (this.bodySerializerRegistry != null && contentType != null
                && this.bodySerializerRegistry.TryGetSerializer(contentType, out var resolved))
            {
                return resolved;
            }

            return this.bodySerializer;
        }
    }
}
