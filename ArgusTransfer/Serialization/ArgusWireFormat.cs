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
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
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
        /// The default maximum size in bytes of a header block: the request or status line plus all header lines
        /// </summary>
        public const int DefaultMaxHeaderSize = 32 * 1024;

        /// <summary>
        /// The error message for a compressed body on a text-based read or write path
        /// </summary>
        private const string TextFormContentEncodingMessage =
            "A body with a Content-Encoding is binary and can only be written and read with the Stream overloads.";

        /// <summary>
        /// Appends the correlation token, timestamp and custom headers of a message
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> whose headers are written
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a header name is not a valid token or a header value contains a carriage return, line feed or NUL,
        /// which would corrupt the message or inject additional headers
        /// </exception>
        public static void AppendStandardHeaders(StringBuilder sb, ArgusMessage message)
        {
            var headerError = GetHeaderError(message);

            if (headerError != null)
            {
                throw new InvalidOperationException(headerError);
            }

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
        /// Checks the custom headers of a message: a name must be a non-empty token of visible ASCII characters without
        /// <c>:</c>, and a value must not contain a carriage return, line feed or NUL character
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to check
        /// </param>
        /// <returns>
        /// A description of the first invalid header, or <c>null</c> when all headers are valid
        /// </returns>
        public static string GetHeaderError(ArgusMessage message)
        {
            foreach (var header in message.Headers)
            {
                if (string.IsNullOrEmpty(header.Key) || header.Key.Any(c => c <= ' ' || c >= '\u007f' || c == ':'))
                {
                    return $"The header name '{EscapeForMessage(header.Key)}' is invalid: it must be a non-empty token of visible ASCII characters without ':'.";
                }

                if (header.Value != null && header.Value.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
                {
                    return $"The value of header '{header.Key}' contains a carriage return, line feed or NUL character.";
                }
            }

            return null;
        }

        /// <summary>
        /// Makes control characters in a header name visible in an error message
        /// </summary>
        /// <param name="value">
        /// The value to escape
        /// </param>
        /// <returns>
        /// The value with control characters written as <c>\uXXXX</c>
        /// </returns>
        private static string EscapeForMessage(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);

            foreach (var c in value)
            {
                if (char.IsControl(c))
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
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
        /// <exception cref="InvalidOperationException">
        /// Thrown when the message has a <c>Content-Encoding</c>, which the text form cannot carry
        /// </exception>
        public static void AppendBody(StringBuilder sb, ArgusMessage message, IArgusBodySerializer serializer)
        {
            EnsureNoContentEncoding(message);

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
                AppendContentHeaders(sb, message, serializer, Encoding.UTF8.GetByteCount(serializedBody));
            }

            sb.Append("\r\n");

            if (serializedBody != null)
            {
                sb.Append(serializedBody);
            }
        }

        /// <summary>
        /// Writes a message to a <see cref="Stream"/>. The header block is encoded as UTF-8. A string body is serialized,
        /// encoded as UTF-8 and announced with <c>Content-Length</c> in bytes; a streamed body is copied as raw bytes in
        /// chunked transfer encoding. When the message has a <c>Content-Encoding</c> header, the body is compressed with
        /// that encoding first.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to write
        /// </param>
        /// <param name="appendStartLine">
        /// Appends the request or status line
        /// </param>
        /// <param name="serializer">
        /// The <see cref="IArgusBodySerializer"/> used for a string body
        /// </param>
        /// <param name="encodings">
        /// The supported content encodings
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the <c>Content-Encoding</c> header names an unsupported encoding
        /// </exception>
        public static async Task WriteAsync(Stream stream, ArgusMessage message, Action<StringBuilder> appendStartLine, IArgusBodySerializer serializer, IEnumerable<IArgusContentEncoding> encodings, CancellationToken cancellationToken)
        {
            var encoding = ArgusCompression.ResolveContentEncoding(message, encodings);
            var head = new StringBuilder();

            appendStartLine(head);
            AppendStandardHeaders(head, message);

            if (message.IsStreamed)
            {
                AppendStreamedHeaderTail(head, message);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(head.ToString()).AsMemory(), cancellationToken);
                await ArgusChunkedEncoding.WriteChunkedAsync(message.BodyStream, stream, encoding, cancellationToken);
                return;
            }

            byte[] body = null;

            if (!string.IsNullOrEmpty(message.Body))
            {
                body = Encoding.UTF8.GetBytes(serializer.WriteBody(message.Body));

                if (encoding != null)
                {
                    body = ArgusCompression.Compress(encoding, body);
                }

                AppendContentHeaders(head, message, serializer, body.Length);
            }

            head.Append("\r\n");

            // Send the header block and the body in a single write: on an unbuffered pipe each write blocks until the
            // peer reads it, and a server that rejects the request after the headers never reads a separately written body
            var headBytes = Encoding.UTF8.GetBytes(head.ToString());
            var wireBytes = new byte[headBytes.Length + (body?.Length ?? 0)];
            headBytes.CopyTo(wireBytes, 0);
            body?.CopyTo(wireBytes, headBytes.Length);

            await stream.WriteAsync(wireBytes.AsMemory(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Reads the header block and the body of a message from an <see cref="IArgusMessageSource"/>. A body with a
        /// <c>Content-Encoding</c> is decompressed and the header is removed, so the message describes the decoded body.
        /// </summary>
        /// <param name="source">
        /// The <see cref="IArgusMessageSource"/> positioned after the request or status line
        /// </param>
        /// <param name="headerBudget">
        /// The <see cref="ArgusHeaderBudget"/> that limits the header block, already charged for the request or status line
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to populate
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes, applied to both the wire size and the decompressed size.
        /// A value of 0 disables the limit.
        /// </param>
        /// <param name="resolveSerializer">
        /// Resolves the <see cref="IArgusBodySerializer"/> for a content type
        /// </param>
        /// <param name="encodings">
        /// The supported content encodings
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="ArgusProtocolException">
        /// Thrown when the header block or the body exceeds its maximum size, or the <c>Content-Encoding</c> is unsupported
        /// or the compressed body is invalid
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a compressed body is read from a text source
        /// </exception>
        public static async Task ReadHeadersAndBodyAsync(IArgusMessageSource source, ArgusHeaderBudget headerBudget, ArgusMessage message, long maxBodySize, Func<string, IArgusBodySerializer> resolveSerializer, IEnumerable<IArgusContentEncoding> encodings, CancellationToken cancellationToken)
        {
            var contentLength = -1;
            var line = await headerBudget.ReadLineAsync(source, cancellationToken);

            while (!string.IsNullOrEmpty(line))
            {
                contentLength = ParseHeader(line, message, contentLength);
                line = await headerBudget.ReadLineAsync(source, cancellationToken);
            }

            var encoding = ArgusCompression.ResolveReceivedContentEncoding(message, encodings);

            if (encoding != null)
            {
                if (source is not ArgusWireReader wireReader)
                {
                    throw new InvalidOperationException(TextFormContentEncodingMessage);
                }

                await ReadEncodedBodyAsync(wireReader, message, contentLength, maxBodySize, resolveSerializer, encoding, cancellationToken);
                message.Headers.Remove(ArgusHeaderNames.ContentEncoding);
                return;
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
        /// Reads a compressed body and stores the decompressed result on the message. The wire size and the decompressed
        /// size are both limited by <paramref name="maxBodySize"/>.
        /// </summary>
        /// <param name="reader">
        /// The <see cref="ArgusWireReader"/> positioned at the start of the body
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to populate
        /// </param>
        /// <param name="contentLength">
        /// The announced content length, or -1
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="resolveSerializer">
        /// Resolves the <see cref="IArgusBodySerializer"/> for a content type
        /// </param>
        /// <param name="encoding">
        /// The <see cref="IArgusContentEncoding"/> the body is compressed with
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        private static async Task ReadEncodedBodyAsync(ArgusWireReader reader, ArgusMessage message, int contentLength, long maxBodySize, Func<string, IArgusBodySerializer> resolveSerializer, IArgusContentEncoding encoding, CancellationToken cancellationToken)
        {
            if (IsChunked(message))
            {
                await using var compressed = await reader.ReadChunkedAsync(maxBodySize, cancellationToken);
                message.BodyStream = await ArgusCompression.DecompressAsync(encoding, compressed, maxBodySize, cancellationToken);
                return;
            }

            EnsureWithinLimit(contentLength, maxBodySize);

            if (contentLength > 0)
            {
                using var compressed = new MemoryStream(await reader.ReadBytesAsync(contentLength, cancellationToken));
                await using var decompressed = await ArgusCompression.DecompressAsync(encoding, compressed, maxBodySize, cancellationToken);

                message.Body = resolveSerializer(GetContentType(message)).ReadBody(Encoding.UTF8.GetString(decompressed.ToArray()));
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
        /// Thrown when the body exceeds <paramref name="maxBodySize"/>, or the message has a <c>Content-Encoding</c>,
        /// which the text form cannot carry
        /// </exception>
        public static void ReadHeadersAndBody(StringReader reader, ArgusMessage message, long maxBodySize, Func<string, IArgusBodySerializer> resolveSerializer)
        {
            var contentLength = -1;
            var line = reader.ReadLine();

            while (!string.IsNullOrEmpty(line))
            {
                contentLength = ParseHeader(line, message, contentLength);
                line = reader.ReadLine();
            }

            EnsureNoContentEncoding(message);

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
        /// <exception cref="ArgusProtocolException">
        /// Thrown when <paramref name="contentLength"/> exceeds <paramref name="maxBodySize"/>
        /// </exception>
        private static void EnsureWithinLimit(int contentLength, long maxBodySize)
        {
            if (maxBodySize > 0 && contentLength > maxBodySize)
            {
                throw new ArgusProtocolException(
                    $"Request body size {contentLength} bytes exceeds the maximum allowed size of {maxBodySize} bytes.");
            }
        }

        /// <summary>
        /// Throws when a message has a <c>Content-Encoding</c> other than <c>identity</c>, because the text-based
        /// read and write paths cannot carry the binary compressed body
        /// </summary>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> to inspect
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the message has a <c>Content-Encoding</c>
        /// </exception>
        private static void EnsureNoContentEncoding(ArgusMessage message)
        {
            if (ArgusCompression.HasContentEncoding(message))
            {
                throw new InvalidOperationException(TextFormContentEncodingMessage);
            }
        }

        /// <summary>
        /// Appends the <c>Content-Type</c> header (when the message has none) and the <c>Content-Length</c> header of a string body
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="message">
        /// The <see cref="ArgusMessage"/> whose body is written
        /// </param>
        /// <param name="serializer">
        /// The <see cref="IArgusBodySerializer"/> whose content type is used as the default
        /// </param>
        /// <param name="byteCount">
        /// The body length in bytes as written on the wire
        /// </param>
        private static void AppendContentHeaders(StringBuilder sb, ArgusMessage message, IArgusBodySerializer serializer, int byteCount)
        {
            if (!message.Headers.ContainsKey(ArgusHeaderNames.ContentType))
            {
                sb.Append(ArgusHeaderNames.ContentType + ": ");
                sb.Append(serializer.ContentType);
                sb.Append("\r\n");
            }

            sb.Append(ArgusHeaderNames.ContentLength + ": ");
            sb.Append(byteCount.ToString(CultureInfo.InvariantCulture));
            sb.Append("\r\n");
        }
    }
}
