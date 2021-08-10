// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class ReadOnlyInteropStream : Stream
    {
        private readonly IStream _stream;

        public ReadOnlyInteropStream(IStream stream)
        {
            Debug.Assert(stream != null);
            _stream = stream;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        public unsafe override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            _stream.Read(buffer, count, (IntPtr)(&bytesRead));
            return bytesRead;
        }

        public override long Position
        {
            get
            {
                unsafe
                {
                    long position;
                    _stream.Seek(0, (int)SeekOrigin.Current, (IntPtr)(&position));
                    return position;
                }
            }

            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public unsafe override long Seek(long offset, SeekOrigin origin)
        {
            long position;
            _stream.Seek(0, (int)origin, (IntPtr)(&position));
            return position;
        }

        public override long Length
        {
            get
            {
                const int STATFLAG_NONAME = 1;

                STATSTG stats;
                _stream.Stat(out stats, STATFLAG_NONAME);
                return stats.cbSize;
            }
        }

        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
