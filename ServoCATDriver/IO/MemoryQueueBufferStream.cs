#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.Joko.ServoCAT.IO {
    /// <summary>
    /// A stream that purges data as it is read. Copied from https://codereview.stackexchange.com/questions/93154/memoryqueuebufferstream
    /// </summary>
    public class MemoryQueueBufferStream : Stream {
        private class Chunk {
            public int ChunkReadStartIndex { get; set; }
            public byte[] Data { get; set; }
        }

        private readonly Queue<Chunk> lstBuffers_m;

        public MemoryQueueBufferStream() {
            this.lstBuffers_m = new Queue<Chunk>();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            this.ValidateBufferArgs(buffer, offset, count);
            int iRemainingBytesToRead = count;
            int iTotalBytesRead = 0;
            while (iTotalBytesRead <= count && lstBuffers_m.Count > 0) {
                Chunk chunk = this.lstBuffers_m.Peek();
                int iUnreadChunkLength = chunk.Data.Length - chunk.ChunkReadStartIndex;
                int iBytesToRead = Math.Min(iUnreadChunkLength, iRemainingBytesToRead);

                if (iBytesToRead > 0) {
                    Buffer.BlockCopy(chunk.Data, chunk.ChunkReadStartIndex, buffer, offset + iTotalBytesRead, iBytesToRead);

                    iTotalBytesRead += iBytesToRead;
                    iRemainingBytesToRead -= iBytesToRead;

                    if (chunk.ChunkReadStartIndex + iBytesToRead >= chunk.Data.Length) {
                        this.lstBuffers_m.Dequeue();
                    } else {
                        chunk.ChunkReadStartIndex = chunk.ChunkReadStartIndex + iBytesToRead;
                    }
                } else {
                    break;
                }
            }

            return iTotalBytesRead;
        }

        private void ValidateBufferArgs(byte[] buffer, int offset, int count) {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "offset must be non-negative");
            }
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "count must be non-negative");
            }
            if ((buffer.Length - offset) < count) {
                throw new ArgumentException("requested count exceeds available size");
            }
        }

        public override void Write(byte[] buffer, int offset, int count) {
            this.ValidateBufferArgs(buffer, offset, count);
            byte[] bufSave = new byte[count];
            Buffer.BlockCopy(buffer, offset, bufSave, 0, count);
            this.lstBuffers_m.Enqueue(new Chunk() { ChunkReadStartIndex = 0, Data = bufSave });
        }

        public override bool CanSeek {
            get { return false; }
        }

        public override long Position {
            get {
                return 0;
            }
            set {
                throw new NotSupportedException(this.GetType().Name + " is not seekable");
            }
        }

        public override bool CanWrite {
            get { return true; }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException(this.GetType().Name + " is not seekable");
        }

        public override void SetLength(long value) {
            throw new NotSupportedException(this.GetType().Name + " length can not be changed");
        }

        public override bool CanRead {
            get { return true; }
        }

        public override long Length {
            get {
                if (this.lstBuffers_m == null) {
                    return 0;
                }

                if (this.lstBuffers_m.Count == 0) {
                    return 0;
                }

                return this.lstBuffers_m.Sum(b => b.Data.Length - b.ChunkReadStartIndex);
            }
        }

        public override void Flush() {
        }
    }
}