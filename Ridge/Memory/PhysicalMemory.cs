using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.Memory
{
    public class PhysicalMemory : IPhysicalMemory
    {
        /// <summary>
        /// Initializes system memory.
        /// </summary>
        /// <param name="size">Size of memory in kilobytes</param>
        public PhysicalMemory(uint size)
        {
            _mem = new byte[size * 1024];
        }

        public byte ReadByte(uint address)
        {
            if (address < _mem.Length)
            {
                return _mem[address];
            }
            else
            {
                return 0xff;
            }
        }

        public ushort ReadHalfWord(uint address)
        {
            return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
        }

        public uint ReadWord(uint address)
        {
            return (uint)((ReadByte(address) << 24) |
                          (ReadByte(address + 1) << 16) |
                          (ReadByte(address + 2) << 8) |
                           ReadByte(address + 3));
        }

        public ulong ReadDoubleWord(uint address)
        {
            return (ulong)((ulong)ReadWord(address) << 32) | ReadWord(address + 4);
        }

        public void WriteByte(uint address, byte b)
        {
            if(address < _mem.Length)
            {
                _mem[address] = b;
            }
        }

        public void WriteHalfWord(uint address, ushort h)
        {
            WriteByte(address, (byte)(h >> 8));
            WriteByte(address + 1, (byte)h);
        }

        public void WriteWord(uint address, uint w)
        {
            WriteByte(address, (byte)(w >> 24));
            WriteByte(address + 1, (byte)(w >> 16));
            WriteByte(address + 2, (byte)(w >> 8));
            WriteByte(address + 3, (byte)(w));
        }

        public void WriteDoubleWord(uint address, ulong d)
        {
            WriteWord(address, (uint)(d >> 32));
            WriteWord(address + 4, (uint)d);
        }

        private byte[] _mem;
    }
}
