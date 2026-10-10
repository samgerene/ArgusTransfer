// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTextRequestSerializer.cs">
//
//     Copyright (c) 2025-2026 Gerené
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
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Serializes and deserializes <see cref="ArgusRequest"/> messages using the ARGUS/1.0 text wire format
    /// </summary>
    public class ArgusRequestSerializer
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
        /// Initializes a new instance of the <see cref="ArgusRequestSerializer"/> class
        /// with the default <see cref="PlainTextArgusBodySerializer"/>
        /// </summary>
        public ArgusRequestSerializer()
            : this(new PlainTextArgusBodySerializer())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequestSerializer"/> class
        /// </summary>
        /// <param name="bodySerializer">
        /// The <see cref="IArgusBodySerializer"/> used to serialize and deserialize the message body
        /// </param>
        public ArgusRequestSerializer(IArgusBodySerializer bodySerializer)
        {
            this.bodySerializer = bodySerializer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequestSerializer"/> class
        /// with an <see cref="IArgusBodySerializerRegistry"/> for content-type based serializer resolution
        /// </summary>
        /// <param name="bodySerializerRegistry">
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </param>
        public ArgusRequestSerializer(IArgusBodySerializerRegistry bodySerializerRegistry)
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
        /// Gets or sets the maximum size in bytes of the header block -- the request line plus all header lines -- accepted by
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
        /// Serializes an <see cref="ArgusRequest"/> to its text wire format representation
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        /// <returns>
        /// A string containing the serialized request in ARGUS/1.0 wire format
        /// </returns>
        public string Write(ArgusRequest request)
        {
            var sb = new StringBuilder();

            AppendRequestLine(sb, request);
            ArgusWireFormat.AppendStandardHeaders(sb, request);
            ArgusWireFormat.AppendBody(sb, request, this.ResolveSerializer(ArgusWireFormat.GetContentType(request)));

            return sb.ToString();
        }

        /// <summary>
        /// Writes an <see cref="ArgusRequest"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        public void Write(StreamWriter writer, ArgusRequest request)
        {
            writer.Write(this.Write(request));
            writer.Flush();
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusRequest"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>.
        /// This method supports streaming bodies via chunked transfer encoding. The writer is flushed and the message is
        /// written as bytes to its <see cref="StreamWriter.BaseStream"/>, so binary streamed bodies are preserved.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task WriteAsync(StreamWriter writer, ArgusRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            await writer.FlushAsync(cancellationToken);
            await this.WriteAsync(writer.BaseStream, request, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusRequest"/> in ARGUS/1.0 wire format to a <see cref="Stream"/>.
        /// The header block and a string body are encoded as UTF-8; a streamed body is copied as raw bytes using
        /// chunked transfer encoding.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
        /// </param>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="request"/> is <c>null</c>
        /// </exception>
        public Task WriteAsync(Stream stream, ArgusRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(request);

            var serializer = this.ResolveSerializer(ArgusWireFormat.GetContentType(request));

            return ArgusWireFormat.WriteAsync(stream, request, sb => AppendRequestLine(sb, request), serializer, this.contentEncodings, cancellationToken);
        }

        /// <summary>
        /// Deserializes an <see cref="ArgusRequest"/> from its text wire format representation
        /// </summary>
        /// <param name="text">
        /// The text containing the serialized request in ARGUS/1.0 wire format
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. Pass 0 for no limit.
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        public ArgusRequest Read(string text, long maxBodySize = 0)
        {
            using var reader = new StringReader(text);

            var request = ParseRequestLine(reader.ReadLine());
            ArgusWireFormat.ReadHeadersAndBody(reader, request, maxBodySize, this.ResolveSerializer);

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
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. Pass 0 for no limit.
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        public Task<ArgusRequest> ReadAsync(StreamReader reader, CancellationToken cancellationToken, long maxBodySize = 0)
        {
            return this.ReadCoreAsync(new ArgusTextMessageSource(reader), maxBodySize, cancellationToken);
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusRequest"/> from a <see cref="Stream"/>. <c>Content-Length</c>
        /// and chunk sizes are honored as byte counts and a streamed body is read as raw bytes, so binary bodies are preserved.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to read from. The method reads one message and may buffer bytes that belong to it only
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. Pass 0 for no limit.
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the request line is missing or malformed, or a chunk is malformed
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body exceeds <paramref name="maxBodySize"/>
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before the complete body was received
        /// </exception>
        public Task<ArgusRequest> ReadAsync(Stream stream, CancellationToken cancellationToken, long maxBodySize = 0)
        {
            return this.ReadCoreAsync(new ArgusWireReader(stream), maxBodySize, cancellationToken);
        }

        /// <summary>
        /// Appends the request line (e.g. "POST /healthendpoint ARGUS/1.0")
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> whose verb, route and query parameters are written
        /// </param>
        private static void AppendRequestLine(StringBuilder sb, ArgusRequest request)
        {
            sb.Append(request.Verb.ToString());
            sb.Append(' ');
            sb.Append(ArgusRouteEncoding.EncodeRoute(request.Route));
            sb.Append(ArgusQueryStringHelper.BuildQueryString(request.QueryParameters));
            sb.Append(' ');
            sb.Append(ArgusWireFormat.ProtocolVersion);
            sb.Append("\r\n");
        }

        /// <summary>
        /// Parses the request line (e.g. "POST /healthendpoint ARGUS/1.0") into an <see cref="ArgusRequest"/>
        /// </summary>
        /// <param name="requestLine">
        /// The request line, or <c>null</c> when the input ended before it
        /// </param>
        /// <returns>
        /// A new <see cref="ArgusRequest"/> with the verb, route and query parameters set
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when the request line is missing or malformed, the protocol version is not ARGUS/1.0, or the verb is unknown
        /// </exception>
        private static ArgusRequest ParseRequestLine(string requestLine)
        {
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                throw new FormatException("Missing request line.");
            }

            var parts = requestLine.Split(' ');

            if (parts.Length != 3)
            {
                throw new FormatException($"Invalid request line: {requestLine}");
            }

            ArgusWireFormat.EnsureProtocolVersion(parts[2]);

            if (!Enum.TryParse<ArgusVerb>(parts[0], true, out var verb))
            {
                throw new FormatException($"Unknown verb: {parts[0]}");
            }

            var routeAndQuery = parts[1];
            var questionMarkIndex = routeAndQuery.IndexOf('?');

            var request = new ArgusRequest
            {
                Verb = verb,
                Route = ArgusRouteEncoding.DecodePath(questionMarkIndex >= 0
                    ? routeAndQuery.Substring(0, questionMarkIndex)
                    : routeAndQuery)
            };

            if (questionMarkIndex >= 0 && questionMarkIndex < routeAndQuery.Length - 1)
            {
                ArgusQueryStringHelper.ParseQueryString(
                    routeAndQuery.Substring(questionMarkIndex + 1),
                    request.QueryParameters);
            }

            return request;
        }

        /// <summary>
        /// Reads a complete request from an <see cref="IArgusMessageSource"/>
        /// </summary>
        /// <param name="source">
        /// The <see cref="IArgusMessageSource"/> to read from
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusRequest"/>
        /// </returns>
        private async Task<ArgusRequest> ReadCoreAsync(IArgusMessageSource source, long maxBodySize, CancellationToken cancellationToken)
        {
            var headerBudget = new ArgusHeaderBudget(this.maxHeaderSize);
            var request = ParseRequestLine(await headerBudget.ReadLineAsync(source, cancellationToken));
            await ArgusWireFormat.ReadHeadersAndBodyAsync(source, headerBudget, request, maxBodySize, this.ResolveSerializer, this.contentEncodings, cancellationToken);

            return request;
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
    }
}
