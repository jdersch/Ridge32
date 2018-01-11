using Ridge.CPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.Memory
{
    //
    // VRT information
    //
    public enum SegmentType
    {
        Code,           // Use the code segment (in SR8)
        Data            // Use the data segment (in SR9)
    }

    public interface IVirtualMemory
    {
        void AttachCPU(Processor p);

        byte ReadByteV(uint address, SegmentType segment, out bool pageFault);
        ushort ReadHalfWordV(uint address,SegmentType segment, out bool pageFault);
        uint ReadWordV(uint address, SegmentType segment, out bool pageFault);
        ulong ReadDoubleWordV(uint address, SegmentType segment, out bool pageFault);

        bool WriteByteV(uint address, byte b);
        bool WriteHalfWordV(uint address, ushort h);
        bool WriteWordV(uint address, uint w);
        bool WriteDoubleWordV(uint address, ulong d);

        uint TranslateVirtualToReal(uint segmentNumber, uint virtualAddress, bool modified, bool referenced, out bool pageFault);
    }
}
