﻿namespace Microsoft.ApplicationInsights.Channel
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    internal class StorageTransmission : Transmission, IDisposable
    {   
        internal Action<StorageTransmission> Disposing;

        internal const string ContentTypeHeader = "Content-Type";
        internal const string ContentEncodingHeader = "Content-Encoding";

        protected StorageTransmission(string fullPath, Uri address, byte[] content, string contentType, string contentEncoding) 
            : base(address, content, contentType, contentEncoding)
        {
            this.FullFilePath = fullPath;
            this.FileName = Path.GetFileName(fullPath);
        }

        internal string FileName { get; private set; }

        internal string FullFilePath { get; private set; }

        /// <summary>
        /// Disposing the storage transmission.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new transmission from the specified <paramref name="stream"/>.
        /// </summary>
        /// <returns>Return transmission loaded from file; return null if the file is corrupted.</returns>
        internal static async Task<StorageTransmission> CreateFromStreamAsync(Stream stream, string fileName)
        {
            var reader = new StreamReader(stream);
            Uri address = await ReadAddressAsync(reader).ConfigureAwait(false);
            string contentType = await ReadHeaderAsync(reader, ContentTypeHeader).ConfigureAwait(false);
            string contentEncoding = await ReadHeaderAsync(reader, ContentEncodingHeader).ConfigureAwait(false);
            byte[] content = await ReadContentAsync(reader).ConfigureAwait(false);
            return new StorageTransmission(fileName, address, content, contentType, contentEncoding);
        }

        /// <summary>
        /// Saves the transmission to the specified <paramref name="stream"/>.
        /// </summary>
        internal static async Task SaveAsync(Transmission transmission, Stream stream)
        {
            var writer = new StreamWriter(stream);
            try
            {
                await writer.WriteLineAsync(transmission.EndpointAddress.ToString()).ConfigureAwait(false);
                await writer.WriteLineAsync(ContentTypeHeader + ":" + transmission.ContentType).ConfigureAwait(false);
                await writer.WriteLineAsync(ContentEncodingHeader + ":" + transmission.ContentEncoding).ConfigureAwait(false);
                await writer.WriteLineAsync(string.Empty).ConfigureAwait(false);
                await writer.WriteAsync(Convert.ToBase64String(transmission.Content)).ConfigureAwait(false);
            }
            finally
            {
                writer.Flush();
            }
        }

        private static async Task<string> ReadHeaderAsync(TextReader reader, string headerName)
        {
            string line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(line))
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, "{0} header is expected.", headerName));
            }

            string[] parts = line.Split(':');
            if (parts.Length != 2)
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Unexpected header format. {0} header is expected. Actual header: {1}", headerName, line));
            }

            if (parts[0] != headerName)
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, "{0} header is expected. Actual header: {1}", headerName, line));
            }

            return parts[1].Trim();
        }

        private static async Task<Uri> ReadAddressAsync(TextReader reader)
        {
            string addressLine = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(addressLine))
            {
                throw new FormatException("Transmission address is expected.");
            }

            var address = new Uri(addressLine);
            return address;
        }

        private static async Task<byte[]> ReadContentAsync(TextReader reader)
        {
            string content = await reader.ReadToEndAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(content) || content == Environment.NewLine)
            {
                throw new FormatException("Content is expected.");
            }

            return Convert.FromBase64String(content);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Action<StorageTransmission> disposingDelegate = this.Disposing;
                if (disposingDelegate != null)
                {
                    disposingDelegate(this);
                }
            }
        }
    }
}
