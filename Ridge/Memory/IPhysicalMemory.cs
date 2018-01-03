using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.Memory
{
    /// <summary>
    /// Interface to physical memory.  
    /// </summary>
    public interface IPhysicalMemory
    {
        byte ReadByte(uint address);
        ushort ReadHalfWord(uint address);
        uint ReadWord(uint address);
        ulong ReadDoubleWord(uint address);

        void WriteByte(uint address, byte b);
        void WriteHalfWord(uint address, ushort h);
        void WriteWord(uint address, uint w);
        void WriteDoubleWord(uint address, ulong d);
    }
}
