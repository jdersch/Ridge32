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

            using (FileStream fs = new FileStream("Disks\\SUS.img", FileMode.Open, FileAccess.Read))
            {
                _floppyDisk = new FloppyDisk(fs);
            }
        }

        public bool Interrupt
        {
            get { return _interrupt; }
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
                _hack = 1000;

                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);
                    _ioir = (uint)(0x01880000 | (k.KeyChar << 8));
                    _interrupt = true;

                    _lastUnit = 0;
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

            return 0;
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

            uint ret = 0;
            // 00-7F: write one character on port 0 - handshake is by bit 30
            //        (special order only used by RBUG).
            if (command < 0x80)
            {
                //Console.WriteLine("{0} - {1}", command, (char)command);
                Console.Write((char)command);
                _handshake = 1;

                // Schedule handshake event
                _characterOutEvent.TimestampNsec = _characterOutTimeNsec;
                _sys.Scheduler.Schedule(_characterOutEvent);

                ret = 0;    // This should always succeed.
            }
            else
            {                
                switch(command)
                {
                    case 0x86:  // Start I/O to left floppy
                        ret = StartFloppy(0);
                        break;

                    case 0xff:  // Undocumented...
                        break;

                    default:
                        Console.WriteLine("Unimplemented FDLP command {0:x2}", command);
                        break;
                }
            }

            return ret;
        }

        private uint StartFloppy(int drive)
        {
            uint ret = 0;

            //
            // Read the floppy disc DCB -- this starts at offset 0x0c.
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

            Console.WriteLine("gOrder {0:x} sOrder {1:x} ridgeAddress {2:x8} byteCount {3:x4} necOrder {4:x}",
                gOrder, sOrder, ridgeAddress, byteCount, necOrder);

            Console.WriteLine("head/Unit {0:x} cyl {1:x} sector {2:x}", headUnit, cylinder, sector);

            switch(sOrder)
            {
                case 0:
                    DoRead(ridgeAddress, byteCount, headUnit, cylinder, sector);
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unhandled GORDER {0:x}", gOrder));
            }

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
                byte[] sectorData = _floppyDisk.ReadSector(cylinder, head, sector);

                for(uint i=0;i<sectorData.Length;i++)
                {
                    _mem.WriteByte(ridgeAddress++, sectorData[i]);
                }

                bytesRead += sectorData.Length;

                sector++;

                if (sector > 15)
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

            // Schedule completion interrupt.
            //_floppyActionEvent.TimestampNsec = _floppyActionTimeNsec;
            //_sys.Scheduler.Schedule(_floppyActionEvent);
            //_handshake = 0;
            //_lastUnit = 0;

            Console.WriteLine(_interrupt);
            _ioir = 0x01860000;
            _interrupt = true;
            _lastUnit = 6;

        }

        private void CharacterOutCallback(ulong timeNsec, ulong skewNsec, object context)
        {
            // Reset handshake.
            _handshake = 0;
        }

        private void FloppyActionCallback(ulong timeNsec, ulong skewNsec, object context)
        {
            _ioir = 0x01860000;
            _interrupt = true;

            _lastUnit = 0;
        }

        private bool _interrupt;
        private uint _ioir;

        private int _lastUnit;
        private int _handshake;

        private int _hack = 1000;

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
