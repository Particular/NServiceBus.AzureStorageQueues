namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;

    class HeadersEncoder
    {
        const int IntSize = 4;
        static readonly Encoding Encoding = new UTF8Encoding(false);

        internal static byte[] Serialize(Dictionary<string, string> headers)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var header in headers)
                {
                    WriteWithPositionPrefix(ms, header.Key);
                    WriteWithPositionPrefix(ms, header.Value);
                }

                return ms.ToArray();
            }
        }

        static void WriteWithPositionPrefix(MemoryStream ms, string value)
        {
            var positionStart = ms.Position;

            // skip 4 for end position
            ms.Seek(IntSize, SeekOrigin.Current);

            // write
            var encoded = Encoding.GetBytes(value);
            WriteAllBytes(ms, encoded);
            var positionEnd = ms.Position;
            var length = (int) (positionEnd - positionStart - IntSize);

            // seek to start
            ms.Seek(positionStart, SeekOrigin.Begin);
            
            // write end position
            WriteAllBytes(ms, BitConverter.GetBytes(length));

            // seek to end
            ms.Seek(positionEnd, SeekOrigin.Begin);
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        static void WriteAllBytes(MemoryStream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        internal static Dictionary<string, string> Deserialize(byte[] bytes)
        {
            var result = new Dictionary<string, string>();
            using (var ms = new MemoryStream(bytes))
            {
                var buffer = new byte[64];
                while (ms.Position < ms.Length)
                {
                    var key = ReadString(ms, ref buffer);
                    var value = ReadString(ms, ref buffer);

                    result.Add(key,value);
                }
            }

            return result;
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        static string ReadString(MemoryStream ms, ref byte[] buffer)
        {
            if (ms.Read(buffer, 0, IntSize) != IntSize)
            {
                throw new SerializationException();
            }

            var length = BitConverter.ToInt32(buffer, 0);
            if (length > buffer.Length)
            {
                Array.Resize(ref buffer, length);
            }

            if (ms.Read(buffer, 0, length) != length)
            {
                throw new SerializationException();
            }

            return Encoding.GetString(buffer, 0, length);
        }
    }
}