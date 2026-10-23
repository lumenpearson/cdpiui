using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Pipe
{
    public class StreamString(Stream ioStream)
    {
        private readonly Stream ioStream = 
            ioStream ?? throw new ArgumentNullException(nameof(ioStream));
        private readonly Encoding streamEncoding = Encoding.Unicode;

        // Messages shorter than 0xFFFF bytes keep the original two-byte length. Longer ones, which were
        // cut off in the middle of a character before, send 0xFFFF followed by a four-byte length.
        private const int ExtendedLengthMarker = ushort.MaxValue;
        private const int MaxMessageLength = 16 * 1024 * 1024;

        public async Task<string> ReadStringAsync(CancellationToken token = default)
        {
            byte[] lenBuffer = new byte[2];
            await ReadExactlyAsync(lenBuffer, token).ConfigureAwait(false);

            int len = lenBuffer[0] * 256 + lenBuffer[1];
            if (len == ExtendedLengthMarker)
            {
                byte[] extendedBuffer = new byte[4];
                await ReadExactlyAsync(extendedBuffer, token).ConfigureAwait(false);
                len = (extendedBuffer[0] << 24) | (extendedBuffer[1] << 16) | (extendedBuffer[2] << 8) | extendedBuffer[3];
                if (len < 0 || len > MaxMessageLength)
                    throw new InvalidDataException("The pipe message is too long.");
            }

            byte[] inBuffer = new byte[len];
            await ReadExactlyAsync(inBuffer, token).ConfigureAwait(false);

            return streamEncoding.GetString(inBuffer);
        }

        private async Task ReadExactlyAsync(byte[] buffer, CancellationToken token)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int bytesRead = await ioStream
                    .ReadAsync(buffer, offset, buffer.Length - offset, token)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += bytesRead;
            }
        }

        public async Task<int> WriteStringAsync(string outString, CancellationToken token = default)
        {
            byte[] outBuffer = streamEncoding.GetBytes(outString ?? string.Empty);
            int len = outBuffer.Length;
            if (len > MaxMessageLength)
                throw new InvalidDataException("The pipe message is too long.");

            byte[] header = len < ExtendedLengthMarker
                ? new byte[] { (byte)(len / 256), (byte)(len & 255) }
                : new byte[] { 0xFF, 0xFF, (byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len };

            await ioStream.WriteAsync(header, 0, header.Length, token).ConfigureAwait(false);
            await ioStream.WriteAsync(outBuffer, 0, len, token).ConfigureAwait(false);
            await ioStream.FlushAsync(token).ConfigureAwait(false);

            return len + header.Length;
        }
    }
}
