using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ridge.CPU;
using Ridge.Memory;
using Ridge.IO.Disk;
using System.IO;

namespace Ridge.IO
{
    /// <summary>
    /// "The Floppy Disc/Line Printer (FD/LP) controller can control two floppy
    ///  disc drives, four RS-232 ports, a Centronics/DataProducts printer
    ///  port, and a Versatec printer/plotter port.  The FD/LP communicates
    ///  with the Ridge cpu via Device Control Blocks (DCB's) in Ridge main
    ///  memory.  Each DCB is 32 bytes long and the eight control blocks for
    ///  the FD/LP are laid out as follows:
    ///  
    ///    Main Memory                                  Unit Number
    ///    Address 3C000H   +-----------------+
    ///                    0|   Terminal 0    |             0
    ///                  20H|   Terminal 1    |             1
    ///                  40H|   Terminal 2    |             2
    ///                  60H|   Terminal 3    |             3
    ///                  80H|    Printer      |             4
    ///                  A0H|   Versatec      |             5
    ///                  C0H|  Floppy Drive 0 |             6
    ///                  E0H|    Reserved     |             7
    ///                     +-----------------+
    /// 
    /// By convention, the FD/LP is device 1.
    /// 
    /// </summary>
    public class FDLP : IIODevice
    {
        public FDLP(byte deviceId, RidgeSystem sys)
        {
            _sys = sys;
            _mem = sys.Memory;
            _deviceId = deviceId;

            // Character send callback used for handshaking between ridge/fdlp when sending
            // single characters.  appx. 833333ns per character at 9600 baud.
            _characterOutEvent = new Event(0, null, CharacterOutCallback);
            _floppyActionEvent = new Event(0, null, FloppyActionCallback);
            
            _floppyDisk = new FloppyDisk("F:\\software\\ridge\\ROS\\ROS35\\004-0335.imd");
            
        }

        public bool Interrupt
        {
            get { return _interrupt; }
        }

        public void Boot()
        {
            //
            // Read 8K of data starting at floppy track 2, head 0 into
            // Ridge main memory at 0x3e000.
            //
            DoRead(0x3e000, 8192, 0, 2, 1);

        }

        public uint AckInterrupt()
        {            
            _interrupt = false;
            return _ioir;
        }

        public void Clock()
        {
            _hack--;

            // Real dirty hack for RBUG console input for now
            if (_hack == 0)
            {
                _handshake = 0;

                _hack = 256;

                if (/* _rbugReadChar && */ Console.KeyAvailable)
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);

                    // This is always unit 0 for now...
                    PostInterrupt(0x00880000 | (uint)(k.KeyChar << 8), 0);

                    _rbugReadChar = false;
                }                
            }
        }

        public uint Read(uint addressWord, out uint data)
        {
            //
            // "The FDLP returns its device number and last interrupting unit in the
            // following format:
            //  0         7 8         15       30    31
            //  | device # | last unit |      | H/S | |
            //
            data = (uint)((_deviceId << 24) | (_lastUnit << 16) | (_handshake << 1));            
            return _handshake;
        }

        public uint Write(uint addressWord, uint data)
        {
            //
            // The FDLP uses no information from the WRITE address word other
            // than the device number.
            //
            // The most significant byte of the I/O Write Data Word ("data")
            // contains the command byte:
            //            
            uint command = (data >> 24);

            // Console.WriteLine("addr {0:x8} data {1:x8}", addressWord, data);            

            uint ret = 1;
            // 00-7F: write one character on port 0 - handshake is by bit 30
            //        (special order only used by RBUG).
            if (command < 0x80)
            {
                Console.Write((char)command);
                ret = 0;    // This should always succeed.
            }
            else
            {                
                switch(command)
                {
                    case 0x80:
                        ret = StartPortWrite(0);
                        break;

                    case 0x86:  // Start I/O to left floppy
                        ret = StartFloppy(0);
                        break;

                    case 0xff: 
                        //
                        // This command is undocumented, but it appears to be sent when RBUG (and other
                        // code that uses primitive port I/O) wants to read a single character
                        // from Port 0... IOIR is then set and the interrupt flag is raised...
                        // This despite command C2 apparently being defined to do just this.
                        //
                        _rbugReadChar = true;
                        break;

                    default:
                        Console.WriteLine("Unimplemented FDLP command {0:x2}", command);
                        break;
                }
            }

            _handshake = 1;

            // Schedule handshake event
            //_characterOutEvent.TimestampNsec = _characterOutTimeNsec;
            //_sys.Scheduler.Schedule(_characterOutEvent);

            return ret;
        }

        private uint StartPortWrite(uint port)
        {
            uint ret = 0;

            //
            // Read the port write DCB -- this starts at offset port * 0x20.
            //
            uint dcbOffset = _dcbAddress + 0x20 * (uint)port;

            byte gOrder = _mem.ReadByte(dcbOffset);
            byte sOrder = _mem.ReadByte(dcbOffset + 1);
            byte gStat = _mem.ReadByte(dcbOffset + 2);
            byte sStat = _mem.ReadByte(dcbOffset + 3);
            byte retries = _mem.ReadByte(dcbOffset + 4);
            uint ridgeAddress = _mem.ReadWord(dcbOffset + 5) >> 8;
            ushort byteCount = (ushort)(_mem.ReadHalfWord(dcbOffset + 8));

            Console.WriteLine("port {0} gOrder {1:x} sOrder {2:x} gStat {3:x} sStat {4:x} retries {5:x} ridgeAddress {6:x8} byteCount {7:x4}",
                port, gOrder, sOrder, gStat, sStat, retries, ridgeAddress, byteCount);

            switch(gOrder)
            {
                case 1: // block write
                    DoBlockPortWrite(ridgeAddress, byteCount, port);
                    break;

                case 3: // single character write
                    Console.Write((char)sOrder);
                    PostInterrupt(0x00800000, port);
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unimplemented port gOrder {0}", gOrder));
            }

            return ret;
        }

        private void DoBlockPortWrite(uint ridgeAddress, uint byteCount, uint port)
        {
            for(uint i=0;i<byteCount;i++)
            {
                byte b = _mem.ReadByte(ridgeAddress + i);
                Console.Write((char)b);
            }

            uint dcbOffset = _dcbAddress + 0x20 * (uint)port;
            _mem.WriteHalfWord(dcbOffset + 0xa, (ushort)byteCount);

            uint ioir = ((0x80 + port) << 16) | 0x8000;   // "completion of block requests"
            PostInterrupt(ioir, port);
        }

        private uint StartFloppy(int drive)
        {
            uint ret = 0;

            //
            // Read the floppy disc DCB -- this starts at offset 0xc0.
            //
            uint dcbOffset = _dcbAddress + 0xc0;
            byte gOrder = _mem.ReadByte(dcbOffset);
            byte sOrder = _mem.ReadByte(dcbOffset + 1);

            uint ridgeAddress = _mem.ReadWord(dcbOffset + 5) >> 8;
            ushort byteCount = _mem.ReadHalfWord(dcbOffset + 8);
            byte necOrder = _mem.ReadByte(dcbOffset + 0xc);

            byte headUnit = _mem.ReadByte(dcbOffset + 0xd);
            byte cylinder = _mem.ReadByte(dcbOffset + 0xe);
            byte sector = _mem.ReadByte(dcbOffset + 0xf);

            
            Console.WriteLine("floppy gOrder {0:x} sOrder {1:x} ridgeAddress {2:x8} byteCount {3:x4} necOrder {4:x}",
                gOrder, sOrder, ridgeAddress, byteCount, necOrder);

            Console.WriteLine("       head/Unit {0:x} cyl {1:x} sector {2:x}", headUnit, cylinder, sector);
            

            switch(gOrder)
            {
                case 0:
                    DoRead(ridgeAddress, byteCount, headUnit, cylinder, sector);
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unhandled GORDER {0:x}", sOrder));
            }

            // Set GSTAT (general status)
            _mem.WriteByte(dcbOffset + 2, 0);       // always OK for now; should eventually actually return errors

            // Set SSTAT (Special Status)
            _mem.WriteByte(dcbOffset + 3, (byte)(headUnit | 0x28));        // selected head/unit + two-sided and ready bits.

            // Set BYTE COUNT TRANSFERRED (always the amount requested)
            _mem.WriteHalfWord(dcbOffset + 0xa, byteCount);



            return ret;
        }

        private void DoRead(uint ridgeAddress, ushort byteCount, byte headUnit, byte cylinder, byte sector)
        {
            int unit = headUnit & 0x3;
            int head = (headUnit & 0x4) >> 2;

            sector--;       // sector is 1-indexed

            int bytesRead = 0;

            while(bytesRead < byteCount)
            {
                byte[] sectorData = _floppyDisk.GetSector(cylinder, head, sector).Data;

                for(uint i=0;i<sectorData.Length;i++)
                {
                    _mem.WriteByte(ridgeAddress++, sectorData[i]);
                }

                bytesRead += sectorData.Length;

                sector++;

                if (sector > _floppyDisk.GetTrack(cylinder, head).SectorCount - 1)
                {
                    sector = 0;

                    head++;

                    if (head > 1)
                    {
                        head = 0;
                        cylinder++;
                    }
                }
            }

            PostInterrupt(0x00800000, 6);
        }

        private void CharacterOutCallback(ulong timeNsec, ulong skewNsec, object context)
        {
            // Reset handshake.
            _handshake = 0;
        }

        private void FloppyActionCallback(ulong timeNsec, ulong skewNsec, object context)
        {
            PostInterrupt(0x00800000, 6);
        }

        private void PostInterrupt(uint ioir, uint unit)
        {
            _ioir = (uint)(ioir | ((uint)_deviceId << 24) | ((unit & 0xf) << 16));
            _lastUnit = unit;
            _interrupt = true;
        }

        private bool _interrupt;
        private uint _ioir;

        private uint _lastUnit;
        private uint _handshake;
        private bool _rbugReadChar;

        private int _hack = 1000;
        private bool _hackInterrupt;
        private uint _hackValue = 0;

        private RidgeSystem _sys;
        private IPhysicalMemory _mem;        
        private byte _deviceId;

        //
        // Device events
        //
        private Event _characterOutEvent;
        private ulong _characterOutTimeNsec = 833333 / 100;

        private Event _floppyActionEvent;
        private ulong _floppyActionTimeNsec = 833333;

        //
        // Floppy data
        //
        private FloppyDisk _floppyDisk;

        private const uint _dcbAddress = 0x3c000;
    }
}
