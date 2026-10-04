// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusHeaderBudget.cs">
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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Tracks the remaining size of a message's header block -- the request or status line plus all header lines -- while
    /// it is read, so a peer cannot send an unbounded line or an unbounded number of headers
    /// </summary>
    internal sealed class ArgusHeaderBudget
    {
        /// <summary>
        /// The number of bytes counted for a line terminator
        /// </summary>
        private const int LineTerminatorSize = 2;

        /// <summary>
        /// The maximum size of the header block in bytes
        /// </summary>
        private readonly int maxHeaderSize;

        /// <summary>
        /// The number of header block bytes that may still be read
        /// </summary>
        private int remaining;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusHeaderBudget"/> class
        /// </summary>
        /// <param name="maxHeaderSize">
        /// The maximum size of the header block in bytes
        /// </param>
        public ArgusHeaderBudget(int maxHeaderSize)
        {
            this.maxHeaderSize = maxHeaderSize;
            this.remaining = maxHeaderSize;
        }

        /// <summary>
        /// Reads the next line of the header block and deducts it from the budget. The empty line that ends the header
        /// block is always allowed.
        /// </summary>
        /// <param name="source">
        /// The <see cref="IArgusMessageSource"/> to read from
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The line without its terminator, or <c>null</c> at the end of the input
        /// </returns>
        /// <exception cref="ArgusProtocolException">
        /// Thrown when the header block exceeds the maximum size
        /// </exception>
        public async ValueTask<string> ReadLineAsync(IArgusMessageSource source, CancellationToken cancellationToken)
        {
            string line;

            try
            {
                line = await source.ReadLineAsync(this.remaining, cancellationToken);
            }
            catch (ArgusLineTooLongException ex)
            {
                throw new ArgusProtocolException($"The header block exceeds the maximum allowed size of {this.maxHeaderSize} bytes.", ex);
            }

            if (line != null)
            {
                this.remaining = Math.Max(0, this.remaining - Encoding.UTF8.GetByteCount(line) - LineTerminatorSize);
            }

            return line;
        }
    }
}
