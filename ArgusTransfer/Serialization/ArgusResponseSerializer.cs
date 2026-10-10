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
    using System.Collections.Generic;
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
        /// Backing field for <see cref="ContentEncodings"/>
        /// </summary>
        private IList<IArgusContentEncoding> contentEncodings = new List<IArgusContentEncoding> { new GZipArgusContentEncoding() };

        /// <summary>
        /// Backing field for <see cref="MaxHeaderSize"/>
        /// </summary>
        private int maxHeaderSize = ArgusWireFormat.DefaultMaxHeaderSize;

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
        /// Gets or sets the content encodings used by the <see cref="Stream"/> overloads: a body whose <c>Content-Encoding</c>
        /// header names one of them is compressed when written and decompressed when read (the header is then removed).
        /// Contains a <see cref="GZipArgusContentEncoding"/> by default. The text-based overloads do not support content
        /// encodings and throw <see cref="InvalidOperationException"/> for a message with a <c>Content-Encoding</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the value is <c>null</c>
        /// </exception>
        public IList<IArgusContentEncoding> ContentEncodings
        {
            get => this.contentEncodings;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                this.contentEncodings = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum size in bytes of the header block -- the status line plus all header lines -- accepted by
        /// the <see cref="Stream"/> and <see cref="StreamReader"/> readers. Defaults to 32 KB. A larger header block raises an
        /// <see cref="ArgusProtocolException"/>; the <see cref="Stream"/> readers stop reading as soon as the limit is exceeded.
        /// The string reader is not limited, because the whole message is already in memory.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is zero or negative
        /// </exception>
        public int MaxHeaderSize
        {
            get => this.maxHeaderSize;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                this.maxHeaderSize = value;
            }
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
            return WriteCore(response, this.ResolveSerializer(ArgusWireFormat.GetContentType(response)));
        }

        /// <summary>
        /// Serializes an <see cref="ArgusResponse"/> to its text wire format representation
        /// using a serializer resolved from the specified accept content type
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The request's <c>Accept</c> header value (a single media type, or a list of media ranges with quality values)
        /// used to select the <see cref="IArgusBodySerializer"/> when the response has no <c>Content-Type</c>; the selected
        /// serializer's content type is then set as <c>Content-Type</c>. A response that already has a <c>Content-Type</c> is
        /// serialized with the serializer for that content type. Without a registry, or when nothing acceptable is registered,
        /// the default serializer is used
        /// </param>
        /// <returns>
        /// A string containing the serialized response in ARGUS/1.0 wire format
        /// </returns>
        public string Write(ArgusResponse response, string acceptContentType)
        {
            return WriteCore(response, this.ResolveResponseSerializer(response, acceptContentType));
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
        /// The request's <c>Accept</c> header value (a single media type, or a list of media ranges with quality values)
        /// used to select the <see cref="IArgusBodySerializer"/> when the response has no <c>Content-Type</c>; the selected
        /// serializer's content type is then set as <c>Content-Type</c>. A response that already has a <c>Content-Type</c> is
        /// serialized with the serializer for that content type. Without a registry, or when nothing acceptable is registered,
        /// the default serializer is used
        /// </param>
        public void Write(StreamWriter writer, ArgusResponse response, string acceptContentType)
        {
            writer.Write(this.Write(response, acceptContentType));
            writer.Flush();
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>.
        /// This method supports streaming bodies via chunked transfer encoding. The writer is flushed and the message is
        /// written as bytes to its <see cref="StreamWriter.BaseStream"/>, so binary streamed bodies are preserved.
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
            ArgumentNullException.ThrowIfNull(writer);

            await writer.FlushAsync(cancellationToken);
            await this.WriteAsync(writer.BaseStream, response, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// using a serializer resolved from the specified accept content type. The writer is flushed and the message is
        /// written as bytes to its <see cref="StreamWriter.BaseStream"/>, so binary streamed bodies are preserved.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The request's <c>Accept</c> header value (a single media type, or a list of media ranges with quality values)
        /// used to select the <see cref="IArgusBodySerializer"/> when the response has no <c>Content-Type</c>; the selected
        /// serializer's content type is then set as <c>Content-Type</c>. A response that already has a <c>Content-Type</c> is
        /// serialized with the serializer for that content type. Without a registry, or when nothing acceptable is registered,
        /// the default serializer is used
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task WriteAsync(StreamWriter writer, ArgusResponse response, string acceptContentType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            await writer.FlushAsync(cancellationToken);
            await this.WriteAsync(writer.BaseStream, response, acceptContentType, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="Stream"/>.
        /// The header block and a string body are encoded as UTF-8; a streamed body is copied as raw bytes using
        /// chunked transfer encoding.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
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
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="response"/> is <c>null</c>
        /// </exception>
        public Task WriteAsync(Stream stream, ArgusResponse response, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(response);

            var serializer = this.ResolveSerializer(ArgusWireFormat.GetContentType(response));

            return ArgusWireFormat.WriteAsync(stream, response, sb => AppendStatusLine(sb, response), serializer, this.contentEncodings, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="Stream"/>
        /// using a serializer resolved from the specified accept content type. The header block and a string body are
        /// encoded as UTF-8; a streamed body is copied as raw bytes using chunked transfer encoding.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The request's <c>Accept</c> header value (a single media type, or a list of media ranges with quality values)
        /// used to select the <see cref="IArgusBodySerializer"/> when the response has no <c>Content-Type</c>; the selected
        /// serializer's content type is then set as <c>Content-Type</c>. A response that already has a <c>Content-Type</c> is
        /// serialized with the serializer for that content type. Without a registry, or when nothing acceptable is registered,
        /// the default serializer is used
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="response"/> is <c>null</c>
        /// </exception>
        public Task WriteAsync(Stream stream, ArgusResponse response, string acceptContentType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(response);

            var serializer = this.ResolveResponseSerializer(response, acceptContentType);

            return ArgusWireFormat.WriteAsync(stream, response, sb => AppendStatusLine(sb, response), serializer, this.contentEncodings, cancellationToken);
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

            var response = ParseStatusLine(reader.ReadLine());
            ArgusWireFormat.ReadHeadersAndBody(reader, response, 0, this.ResolveSerializer);

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
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before a status line is received, for example because the connection was closed
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the status line is empty or malformed
        /// </exception>
        public Task<ArgusResponse> ReadAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            return this.ReadAsync(reader, 0, cancellationToken);
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusResponse"/> from a <see cref="StreamReader"/>, limiting the body size
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read from
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes, applied to the <c>Content-Length</c> and to chunked bodies. Pass 0 for no limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before a status line is received, for example because the connection was closed
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the status line is empty or malformed
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body exceeds <paramref name="maxBodySize"/>
        /// </exception>
        public Task<ArgusResponse> ReadAsync(StreamReader reader, long maxBodySize, CancellationToken cancellationToken)
        {
            return this.ReadCoreAsync(new ArgusTextMessageSource(reader), maxBodySize, cancellationToken);
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusResponse"/> from a <see cref="Stream"/>. <c>Content-Length</c>
        /// and chunk sizes are honored as byte counts and a streamed body is read as raw bytes, so binary bodies are preserved.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to read from. The method reads one message and may buffer bytes that belong to it only
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before a status line or the complete body was received, for example because the connection was closed
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the status line is empty or malformed, or a chunk is malformed
        /// </exception>
        public Task<ArgusResponse> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            return this.ReadAsync(stream, 0, cancellationToken);
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusResponse"/> from a <see cref="Stream"/>, limiting the body size.
        /// <c>Content-Length</c> and chunk sizes are honored as byte counts and a streamed body is read as raw bytes, so
        /// binary bodies are preserved.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to read from. The method reads one message and may buffer bytes that belong to it only
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes, applied to the <c>Content-Length</c>, to chunked bodies, and to the
        /// decompressed size of a compressed body. A <c>Content-Length</c> above the limit is rejected before the body is
        /// read. Pass 0 for no limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before a status line or the complete body was received, for example because the connection was closed
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the status line is empty or malformed, or a chunk is malformed
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body or its decompressed size exceeds <paramref name="maxBodySize"/>
        /// </exception>
        public Task<ArgusResponse> ReadAsync(Stream stream, long maxBodySize, CancellationToken cancellationToken)
        {
            return this.ReadCoreAsync(new ArgusWireReader(stream), maxBodySize, cancellationToken);
        }

        /// <summary>
        /// Appends the status line (e.g. "ARGUS/1.0 200 OK")
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> whose status code is written
        /// </param>
        private static void AppendStatusLine(StringBuilder sb, ArgusResponse response)
        {
            sb.Append(ArgusWireFormat.ProtocolVersion);
            sb.Append(' ');
            sb.Append(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(response.StatusCode.ToReasonPhrase());
            sb.Append("\r\n");
        }

        /// <summary>
        /// Core write logic that serializes an <see cref="ArgusResponse"/> using the specified serializer
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="serializer">
        /// The <see cref="IArgusBodySerializer"/> used for a string body
        /// </param>
        /// <returns>
        /// The response in ARGUS/1.0 wire format
        /// </returns>
        private static string WriteCore(ArgusResponse response, IArgusBodySerializer serializer)
        {
            var sb = new StringBuilder();

            AppendStatusLine(sb, response);
            ArgusWireFormat.AppendStandardHeaders(sb, response);
            ArgusWireFormat.AppendBody(sb, response, serializer);

            return sb.ToString();
        }

        /// <summary>
        /// Parses the status line (e.g. "ARGUS/1.0 200 OK") into an <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="statusLine">
        /// The status line, or <c>null</c> when the input ended before it
        /// </param>
        /// <returns>
        /// A new <see cref="ArgusResponse"/> with the status code set
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when the status line is missing or malformed, the protocol version is not ARGUS/1.0, or the status code is unknown
        /// </exception>
        private static ArgusResponse ParseStatusLine(string statusLine)
        {
            if (string.IsNullOrWhiteSpace(statusLine))
            {
                throw new FormatException("Missing status line.");
            }

            var firstSpace = statusLine.IndexOf(' ');

            if (firstSpace < 0)
            {
                throw new FormatException($"Invalid status line: {statusLine}");
            }

            ArgusWireFormat.EnsureProtocolVersion(statusLine.Substring(0, firstSpace));

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
        /// Reads a complete response from an <see cref="IArgusMessageSource"/>
        /// </summary>
        /// <param name="source">
        /// The <see cref="IArgusMessageSource"/> to read from
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. Pass 0 for no limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the input ends before a status line is received
        /// </exception>
        private async Task<ArgusResponse> ReadCoreAsync(IArgusMessageSource source, long maxBodySize, CancellationToken cancellationToken)
        {
            var headerBudget = new ArgusHeaderBudget(this.maxHeaderSize);
            var statusLine = await headerBudget.ReadLineAsync(source, cancellationToken)
                ?? throw new EndOfStreamException("The stream ended before a status line was received.");

            var response = ParseStatusLine(statusLine);
            await ArgusWireFormat.ReadHeadersAndBodyAsync(source, headerBudget, response, maxBodySize, this.ResolveSerializer, this.contentEncodings, cancellationToken);

            return response;
        }

        /// <summary>
        /// Resolves the appropriate <see cref="IArgusBodySerializer"/> for the specified content type
        /// </summary>
        /// <param name="contentType">
        /// The content type to resolve, or <c>null</c>
        /// </param>
        /// <returns>
        /// The serializer registered for <paramref name="contentType"/>, or the default serializer
        /// </returns>
        private IArgusBodySerializer ResolveSerializer(string contentType)
        {
            return ArgusWireFormat.ResolveSerializer(this.bodySerializerRegistry, this.bodySerializer, contentType);
        }

        /// <summary>
        /// Resolves the serializer for writing a response: the serializer for the response's <c>Content-Type</c> when it
        /// has one; otherwise the serializer negotiated from <paramref name="accept"/> (falling back to the default
        /// serializer), whose content type is then set as the response's <c>Content-Type</c>
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to write
        /// </param>
        /// <param name="accept">
        /// The request's <c>Accept</c> header value
        /// </param>
        /// <returns>
        /// The <see cref="IArgusBodySerializer"/> to serialize the body with
        /// </returns>
        private IArgusBodySerializer ResolveResponseSerializer(ArgusResponse response, string accept)
        {
            var contentType = ArgusWireFormat.GetContentType(response);

            if (contentType != null)
            {
                return this.ResolveSerializer(contentType);
            }

            var serializer = (this.bodySerializerRegistry == null ? null : ArgusContentNegotiation.SelectSerializer(this.bodySerializerRegistry, accept))
                ?? this.bodySerializer;

            response.Headers[ArgusHeaderNames.ContentType] = serializer.ContentType;

            return serializer;
        }
    }
}
