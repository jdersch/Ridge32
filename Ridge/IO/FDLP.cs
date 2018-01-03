using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ridge.CPU;
using Ridge.Memory;

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
        public FDLP(IPhysicalMemory mem, Processor cpu)
        {            
            _mem = mem;
            _cpu = cpu;            
        }

        public void Clock()
        {
            // TODO: all of this is 100% bogus.
            if (_handshakeCounter > 0)
            {
                _handshakeCounter--;

                if (_handshakeCounter == 0)
                {
                    _handshake = true;
                }
            }


            _hack--;

            // Real dirty hack for RBUG console input
            if (_hack == 0)
            {
                _hack = 1000;

                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);
                    uint ioir = (uint)(0x01880000 | (k.KeyChar << 8));
                    _cpu.Interrupt(ioir);
                }
            }
        }

        public uint Read(uint deviceData, out uint data)
        {
            if (_handshake)
            {
                _handshake = false;
                data = 0x0;
            }
            else
            {
                data = 0x2;
            }

            return 0;
        }

        public uint Write(uint deviceData, uint data)
        {
            //
            // The FDLP uses no information from the WRITE address word other
            // than the device number.
            //
            // The most significant byte of the I/O Write Data Word ("data")
            // contains the command byte:
            //
            //

            
            uint command = (data >> 24);

            // 00-7F: write one character on port 0 - handshake is by bit 30
            //        (special order only used by RBUG).
            if (command < 0x80)
            {
                //Console.WriteLine("{0} - {1}", command, (char)command);
                Console.Write((char)command);
                _handshake = false;
                _handshakeCounter = 1000;
            }

            return 0;
        }

        private bool _handshake;
        private int _handshakeCounter;

        private int _hack = 1000;

        private IPhysicalMemory _mem;
        private Processor _cpu;

        private const uint _dcbAddress = 0x3c000;
    }
}
