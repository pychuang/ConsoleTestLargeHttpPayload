using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTestLargeHttpPayload
{
    public class RandomReadOnlyStream : Stream
    {
        private const int lineWidth = 64;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position { get; set; } = 0;

        private static Random random = new Random();

        public RandomReadOnlyStream(long length)
        {
            this.Length = length;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            //const string chars = "0123456789";

            int i;
            for (i = 0; i < count; i++)
            {
                if (this.Position >= this.Length)
                {
                    break;
                }

                char c;
                if (this.Position % lineWidth == lineWidth - 1)
                {
                    c = '\n';
                }
                else
                {
                    c = chars[random.Next(chars.Length)];
                    //c = chars[(int)(this.Position % 10)];
                }

                buffer[offset + i] = Convert.ToByte(c);
                this.Position++;

                if (this.Position != 0 && this.Position % 1000 == 0)
                {
                    Console.WriteLine($"position {this.Position}");
                }
            }

            return i;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
