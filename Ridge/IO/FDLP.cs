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
        public FDLP(byte deviceId, RidgeSystem sys)
        {
            _sys = sys;
            _mem = sys.Memory;
            _deviceId = deviceId;

            // Character send callback used for handshaking between ridge/fdlp when sending
            // single characters.  appx. 833333ns per character at 9600 baud.
            _characterOutEvent = new Event(_characterOutTimeNsec, null, CharacterOutCallback);
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

                    _lastUnit = 8;
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

            Console.WriteLine("command {0:x}", command);

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
            }

            return 0;
        }

        private void CharacterOutCallback(ulong timeNsec, ulong skewNsec, object context)
        {
            // Reset handshake.
            _handshake = 0;
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
        private ulong _characterOutTimeNsec = 833333;

        private const uint _dcbAddress = 0x3c000;
    }
}
