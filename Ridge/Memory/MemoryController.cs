using Ridge.CPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.Memory
{
    public class MemoryController : IPhysicalMemory, IVirtualMemory
    {
        /// <summary>
        /// Initializes system memory.
        /// </summary>
        /// <param name="size">Size of memory in kilobytes</param>
        public MemoryController(uint size)
        {
            _mem = new byte[size * 1024];
        }

        public uint Size
        {
            get { return (uint)_mem.Length; }
        }

        //
        // Physical ("real") memory routines
        //
        public byte ReadByte(uint address)
        {
            if (address < _mem.Length)
            {
                return _mem[address];
            }
            else
            {        
                return 0x00;
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
        {            WriteWord(address, (uint)(d >> 32));
            WriteWord(address + 4, (uint)d);
        }

        //
        // Virtual memory routines.
        // Note that the Read/Write functions rely on correct data alignment
        // being checked before the call, otherwise reads or writes may occur
        // across page boundaries leading to incorrect translations.
        //
        public void AttachCPU(Processor p)
        {
            _cpu = p;
        }

        public uint TranslateVirtualToReal(uint segmentNumber, uint virtualAddress, bool modified, bool referenced, out bool pageFault)
        {
            uint translatedAddress = virtualAddress;

            //
            // Search the VRT to find the translation entry.
            //
            // From the Ridge Processor Reference, 20 jan 1983:
            // "When the processor needs to search the VRT, it proceeds as follows...
            //  1. The segment number of the code [SR8] or data [SR9] segment to be
            //     referenced is added to bits 0...19 of the virtual address.
            //  2. This sum is logically ANDed with the contents of VRMASK
            //     which is kept in special register SR13.
            //  3. The result is shifted left 3 bits and added to the VRT
            //     table base address which is stored in SR12.
            //  4. The VRT entry is fetched and the tag and segment number
            //     parts are compared with virtual address and segment number
            //     desired.
            //  5. If they match, the real page number, virtual address, 
            //     and modify bits are loaded into the TMT and the
            //     referenced bit is set.
            //  6. If not, the link pointer is followed (added to SR12) to
            //     the next VRT entry.  If a link pointer of zero is found
            //     the end of the chain has been reached and a page fault
            //     interrupt is generated."
            //
            // Whew.  A 32-bit virtual address looks like:
            //
            //  0            19 20            31
            //  | Virt. Page # | Byte in Page |
            //
            // Yielding a 20 bit page # and 12-bit offsets in said page.
            //
            pageFault = false;

            // add SR8/SR9 to bits 0..19 of the virtual address
            uint vrtAddress = (virtualAddress >> 12) + segmentNumber;

            // AND with contents of VRMASK (SR13)
            vrtAddress &= _cpu.SR[13];

            // Shift left 3 bits, add to VRT table base address in SR12
            vrtAddress = (vrtAddress << 3) + _cpu.SR[12];

            // Grab the VRT entry from the computed address.
            UInt32 vrtEntry0 = ReadWord(vrtAddress);
            UInt32 vrtEntry1 = ReadWord(vrtAddress + 4);

            // 
            // The first word of the VRT table entry looks like:
            // 0         15 16            31
            // |  Seg #    |     Tag      |
            //
            // Where the Seg # is the actual segment number (as in SR8 or SR9) and Tag
            // is the high order bits (0..15) of the virtual address.
            //
            // Follow the chain until we find a match or hit the end...
            while (true)
            {
                if (segmentNumber == (vrtEntry0 >> 16) &&
                    (virtualAddress >> 16) == (vrtEntry0 & 0xffff))
                {
                    //
                    // We have a match!  Check the validity bits.
                    // If 0, this is invalid and we take a page-fault
                    //
                    if ((vrtEntry1 & 0x7000) == 0)
                    {
                        pageFault = true;
                        break;
                    }
                    else
                    {
                        // Build an address from the page number in the VRT
                        // and the offset in the virtual address
                        //
                        translatedAddress = ((vrtEntry1 & 0x7ff) << 12) | (virtualAddress & 0xfff);

                        // Set the modified/referenced bits as appropriate.
                        if (modified)
                        {
                            vrtEntry1 |= 0x800;
                        }

                        if (referenced)
                        {
                            vrtEntry1 |= 0x8000;
                        }

                        WriteWord(vrtAddress + 4, vrtEntry1);

                        break;
                    }
                }
                else
                {
                    //
                    // This VRT entry doesn't match.  Follow the link and try again.
                    // If this is the last link, take a page-fault.
                    //
                    uint link = vrtEntry1 >> 16;

                    if (link == 0)
                    {
                        pageFault = true;
                        break;
                    }
                    else
                    {
                        vrtAddress = link + _cpu.SR[12];
                        vrtEntry0 = ReadWord(vrtAddress);
                        vrtEntry1 = ReadWord(vrtAddress + 4);
                    }
                }
            }

            return translatedAddress;
        }

        public byte ReadByteV(uint address, SegmentType segment, out bool pageFault)
        {
            pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    segment == SegmentType.Code ? _cpu.SR[8] : _cpu.SR[9],
                    address,
                    false,      // not modified
                    true,
                    out pageFault);
            }

            return ReadByte(address);
        }

        public ushort ReadHalfWordV(uint address, SegmentType segment, out bool pageFault)
        {            
            pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    segment == SegmentType.Code ? _cpu.SR[8] : _cpu.SR[9],
                    address,
                    false,      // not modified
                    true,
                    out pageFault);
            }

            return ReadHalfWord(address);
        }

        public uint ReadWordV(uint address, SegmentType segment, out bool pageFault)
        {
            pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    segment == SegmentType.Code ? _cpu.SR[8] : _cpu.SR[9],
                    address,
                    false,      // not modified
                    true,
                    out pageFault);
            }

            return ReadWord(address);
        }

        public ulong ReadDoubleWordV(uint address, SegmentType segment, out bool pageFault)
        {
            pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    segment == SegmentType.Code ? _cpu.SR[8] : _cpu.SR[9],
                    address,
                    false,      // not modified
                    true,
                    out pageFault);
            }

            return ReadDoubleWord(address);
        }

        public bool WriteByteV(uint address, byte value)
        {
            bool pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    _cpu.SR[9],
                    address,
                    true,
                    true,
                    out pageFault);
            }

            if (!pageFault)
            {
                WriteByte(address, value);
            }

            return pageFault;
        }

        public bool WriteHalfWordV(uint address, ushort value)
        {
            bool pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    _cpu.SR[9],
                    address,
                    true,
                    true,
                    out pageFault);
            }

            if (!pageFault)
            {
                WriteHalfWord(address, value);
            }

            return pageFault;
        }

        public bool WriteWordV(uint address, uint value)
        {            
            bool pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    _cpu.SR[9],
                    address,
                    true,
                    true,
                    out pageFault);                
            }

            if (!pageFault)
            {
                WriteWord(address, value);
            }

            return pageFault;
        }

        public bool WriteDoubleWordV(uint address, ulong value)
        {            
            bool pageFault = false;
            if (_cpu.Mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    _cpu.SR[9],
                    address,
                    true,
                    true,
                    out pageFault);
            }

            if (!pageFault)
            {
                WriteDoubleWord(address, value);
            }

            return pageFault;
        }

        private byte[] _mem;

        // Reference to CPU (for virtual memory registers)
        private Processor _cpu;
    }
}
