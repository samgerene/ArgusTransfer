// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusWireFormat.cs">
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
    /// The parts of the ARGUS/1.0 wire format that requests and responses share: everything after the
    /// request or status line, header parsing, body limits, serializer resolution and writing to a <see cref="Stream"/>
    /// </summary>
    internal static class ArgusWireFormat
    {
        /// <summary>
        /// The name of the header that signals chunked transfer encoding
        /// </summary>
        private const string TransferEncodingHeader = "Transfer-Encoding";

        /// <summary>
        /// Appends the correlation token, timestamp and custom headers of a message
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> whose headers are written
        /// </param>
        public static void AppendStandardHeaders(StringBuilder sb, ArgusMessage message)
        {
            sb.Append(ArgusHeaderNames.CorrelationToken + ": ");
            sb.Append(message.CorrelationToken.ToString());
            sb.Append("\r\n");

            sb.Append(ArgusHeaderNames.Timestamp + ": ");
            sb.Append(message.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append("\r\n");

            foreach (var header in message.Headers)
            {
                sb.Append(header.Key);
                sb.Append(": ");
                sb.Append(header.Value);
                sb.Append("\r\n");
            }
        }

        /// <summary>
        /// Appends the headers that end the header block of a message with a streamed body: a default
        /// <c>Content-Type</c> when none is set, <c>Transfer-Encoding: chunked</c> and the empty line
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> with a streamed body
        /// </param>
        public static void AppendStreamedHeaderTail(StringBuilder sb, ArgusMessage message)
        {
            if (!message.Headers.ContainsKey(ArgusHeaderNames.ContentType))
            {
                sb.Append(ArgusHeaderNames.ContentType + ": application/octet-stream\r\n");
            }

            sb.Append(TransferEncodingHeader + ": chunked\r\n");
            sb.Append("\r\n");
        }

        /// <summary>
        /// Appends the body-related headers, the empty line that ends the header block, and the body. A string body is
        /// serialized with <paramref name="serializer"/> and announced with <c>Content-Length</c> in UTF-8 bytes; a streamed
        /// body is written in text-based chunked transfer encoding.
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> whose body is written
        /// </param>
        /// <param name="serializer">
        /// The <see cref="IArgusBodySerializer"/> used for a string body
        /// </param>
        public static void AppendBody(StringBuilder sb, ArgusMessage message, IArgusBodySerializer serializer)
        {
            if (message.IsStreamed)
            {
                AppendStreamedHeaderTail(sb, message);
                ArgusChunkedEncoding.WriteChunked(message.BodyStream, sb);
                return;
            }

            string serializedBody = null;

            if (!string.IsNullOrEmpty(message.Body))
            {
                serializedBody = serializer.WriteBody(message.Body);

                if (!message.Headers.ContainsKey(ArgusHeaderNames.ContentType))
                {
                    sb.Append(ArgusHeaderNames.ContentType + ": ");
                    sb.Append(serializer.ContentType);
                    sb.Append("\r\n");
                }

                sb.Append(ArgusHeaderNames.ContentLength + ": ");
                sb.Append(Encoding.UTF8.GetByteCount(serializedBody).ToString(CultureInfo.InvariantCulture));
                sb.Append("\r\n");
            }

            sb.Append("\r\n");

            if (serializedBody != null)
            {
                sb.Append(serializedBody);
            }
        }

        /// <summary>
        /// Writes a message to a <see cref="Stream"/>. A message with a streamed body is written as its header block followed
        /// by the body as raw bytes in chunked transfer encoding; any other message is written as its UTF-8 encoded text form.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to write
        /// </param>
        /// <param name="serialize">
        /// Produces the complete text form of a message without a streamed body
        /// </param>
        /// <param name="buildStreamedHead">
        /// Produces the header block of a message with a streamed body
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public static async Task WriteAsync(Stream stream, ArgusMessage message, Func<string> serialize, Func<string> buildStreamedHead, CancellationToken cancellationToken)
        {
            if (message.IsStreamed)
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(buildStreamedHead()).AsMemory(), cancellationToken);
                await ArgusChunkedEncoding.WriteChunkedAsync(message.BodyStream, stream, cancellationToken: cancellationToken);
                return;
            }

            await stream.WriteAsync(Encoding.UTF8.GetBytes(serialize()).AsMemory(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Reads the header block and the body of a message from an <see cref="IArgusMessageSource"/>
        /// </summary>
        /// <param name="source">
        /// The <see cref="IArgusMessageSource"/> positioned after the request or status line
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to populate
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="resolveSerializer">
        /// Resolves the <see cref="IArgusBodySerializer"/> for a content type
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body exceeds <paramref name="maxBodySize"/>
        /// </exception>
        public static async Task ReadHeadersAndBodyAsync(IArgusMessageSource source, ArgusMessage message, long maxBodySize, Func<string, IArgusBodySerializer> resolveSerializer, CancellationToken cancellationToken)
        {
            var contentLength = -1;
            string line;

            while (!string.IsNullOrEmpty(line = await source.ReadLineAsync(cancellationToken)))
            {
                contentLength = ParseHeader(line, message, contentLength);
            }

            if (IsChunked(message))
            {
                message.BodyStream = await source.ReadChunkedAsync(maxBodySize, cancellationToken);
                return;
            }

            EnsureWithinLimit(contentLength, maxBodySize);

            if (contentLength > 0)
            {
                message.Body = resolveSerializer(GetContentType(message)).ReadBody(await source.ReadBodyAsync(contentLength, cancellationToken));
            }
        }

        /// <summary>
        /// Reads the header block and the body of a message from a <see cref="StringReader"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StringReader"/> positioned after the request or status line
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to populate
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="resolveSerializer">
        /// Resolves the <see cref="IArgusBodySerializer"/> for a content type
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body exceeds <paramref name="maxBodySize"/>
        /// </exception>
        public static void ReadHeadersAndBody(StringReader reader, ArgusMessage message, long maxBodySize, Func<string, IArgusBodySerializer> resolveSerializer)
        {
            var contentLength = -1;
            string line;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                contentLength = ParseHeader(line, message, contentLength);
            }

            if (IsChunked(message))
            {
                message.BodyStream = ArgusChunkedEncoding.ReadChunked(reader, maxBodySize);
                return;
            }

            EnsureWithinLimit(contentLength, maxBodySize);

            if (contentLength > 0)
            {
                message.Body = resolveSerializer(GetContentType(message)).ReadBody(ArgusTextBodyReader.Read(reader, contentLength));
            }
        }

        /// <summary>
        /// Resolves the <see cref="IArgusBodySerializer"/> for a content type
        /// </summary>
        /// <param name="registry">
        /// The optional <see cref="IArgusBodySerializerRegistry"/> to consult
        /// </param>
        /// <param name="fallback">
        /// The <see cref="IArgusBodySerializer"/> used when the content type is <c>null</c> or not registered
        /// </param>
        /// <param name="contentType">
        /// The content type to resolve
        /// </param>
        /// <returns>
        /// The resolved <see cref="IArgusBodySerializer"/>
        /// </returns>
        public static IArgusBodySerializer ResolveSerializer(IArgusBodySerializerRegistry registry, IArgusBodySerializer fallback, string contentType)
        {
            if (registry != null && contentType != null && registry.TryGetSerializer(contentType, out var resolved))
            {
                return resolved;
            }

            return fallback;
        }

        /// <summary>
        /// Gets the <c>Content-Type</c> header of a message
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <returns>
        /// The content type, or <c>null</c> when the header is not set
        /// </returns>
        public static string GetContentType(ArgusMessage message)
        {
            return message.Headers.TryGetValue(ArgusHeaderNames.ContentType, out var contentType) ? contentType : null;
        }

        /// <summary>
        /// Parses a single header line and applies it to a message. The correlation token and timestamp set the
        /// corresponding properties, <c>Content-Length</c> is returned, and any other header is added to
        /// <see cref="ArgusMessage.Headers"/>. Lines without a colon are ignored.
        /// </summary>
        /// <param name="line">
        /// The header line
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to update
        /// </param>
        /// <param name="contentLength">
        /// The content length parsed so far, or -1
        /// </param>
        /// <returns>
        /// The content length after applying this line
        /// </returns>
        private static int ParseHeader(string line, ArgusMessage message, int contentLength)
        {
            var colonIndex = line.IndexOf(':');

            if (colonIndex < 0)
            {
                return contentLength;
            }

            var name = line.AsSpan(0, colonIndex).Trim().ToString();
            var value = line.AsSpan(colonIndex + 1).Trim().ToString();

            if (string.Equals(name, ArgusHeaderNames.CorrelationToken, StringComparison.OrdinalIgnoreCase))
            {
                if (Guid.TryParse(value, out var guid))
                {
                    message.CorrelationToken = guid;
                }
            }
            else if (string.Equals(name, ArgusHeaderNames.Timestamp, StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    message.Timestamp = timestamp;
                }
            }
            else if (string.Equals(name, ArgusHeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) && length >= 0)
                {
                    contentLength = length;
                }
            }
            else
            {
                message.Headers[name] = value;
            }

            return contentLength;
        }

        /// <summary>
        /// Determines whether a message announces chunked transfer encoding
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <returns>
        /// <c>true</c> if the <c>Transfer-Encoding</c> header is <c>chunked</c>
        /// </returns>
        private static bool IsChunked(ArgusMessage message)
        {
            return message.Headers.TryGetValue(TransferEncodingHeader, out var transferEncoding)
                && string.Equals(transferEncoding, "chunked", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Throws when a <c>Content-Length</c> exceeds the maximum allowed body size
        /// </summary>
        /// <param name="contentLength">
        /// The announced content length, or -1
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="contentLength"/> exceeds <paramref name="maxBodySize"/>
        /// </exception>
        private static void EnsureWithinLimit(int contentLength, long maxBodySize)
        {
            if (maxBodySize > 0 && contentLength > maxBodySize)
            {
                throw new InvalidOperationException(
                    $"Request body size {contentLength} bytes exceeds the maximum allowed size of {maxBodySize} bytes.");
            }
        }
    }
}
