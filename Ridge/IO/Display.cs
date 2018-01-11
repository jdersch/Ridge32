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
    /// "The monochrome graphics display interface board supports the graphics display
    ///  and keyboard.  A 128K-byte framebuffer on the board handles display refresh
    ///  without utilizing Ridge32 main memory.  The refresh buffer uses dynamic RAM
    ///  chips that are themselves refreshed by the video sweep.  The display interface
    ///  can perform four memory transfer operations:
    ///  
    ///    Write Buffer     Move Data from main memory to refresh buffer.
    ///    Read Buffer      Move data from refresh buffer to main memory.
    ///    Scroll Up        Move data from one place to another in refresh buffer, with
    ///                     increasing addresses.
    ///    Scroll Down      Similar to scroll up, except the data is moved with decreasing
    ///                     addresses.
    ///                     
    ///  All data operations are multiples of 32 bits, aligned on word boundaries.  The
    ///  video sweep accesses memory in increasing sequential order from 0 to the highest
    ///  displayable location.
    ///  
    /// The display interface has four registers to control memory transfers:
    /// 
    ///     1.  The memory address register (MAR) is a 24-bit register with two functions;
    ///         for write/read it contains the main memory source or destination address,
    ///         for scrolling it contains the destination address in the refresh buffer.
    ///         
    ///     2.  The display address register (DAR) is a 16-bit register that contains the
    ///         refresh buffer source or destination address.  For scrolling it contains
    ///         the buffer source address.
    ///         
    ///     3.  The count register controls the length of a transfer.  A count of 0
    ///         results in no operation.
    ///         
    ///     4.  The status register is used to control display attributes and interrupts,
    ///         and return information on the display interface's state.
    /// 
    /// </summary>
    public class Display : IIODevice
    {
        public Display(byte deviceId, RidgeSystem sys)
        {
            _sys = sys;
            _mem = sys.Memory;
            _deviceId = deviceId;

            // 128kb of framebuffer.
            _framebuffer = new uint[32768];           

            _display = new DisplayWindow();

            _hack = 1000000;

            _frameEvent = new Event(_frameTimeNsec, null, FrameCompleteCallback);
            _sys.Scheduler.Schedule(_frameEvent);
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
            // Real dirty hack for RBUG console input for now
            if (_hack == 0)
            {
                _hack = 250;

                if (_display.KeyAvailable &&
                   (_status & 0x10) == 0)
                {
                    _key = _display.GetKey();  

                    // keyboard device + key char
                    _ioir = (((uint)_deviceId & 0xfe) << 24) | (_key << 16);
                    _interrupt = true;
                }
            }

            _hack--;
        }

        public uint Read(uint addressWord, out uint data)
        {
            //
            // Display I/O Read Address Word format:
            // 0      7 8              27 28        31
            // | dev # |                 | register # |
            //
            uint register = addressWord & 0xf;

            data = 0;

            switch (register)
            {
                case 0x0:
                    break;

                case 0x1:
                    data = _dar;
                    break;

                case 0x2:
                    data = _mar << 2;
                    break;

                case 0x4:
                    data = _count << 16;
                    break;

                case 0x5:
                    data = _dar | (_count << 16);
                    break;

                case 0x8:
                    // For now: row addr always 0, command always 0 (idle)
                    // and we return the status bits as written.
                    data = _status;
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unhandled display register {0}.", register));
            }

            Console.WriteLine("reg {0:x}", register);

            return 0;
        }

        public uint Write(uint addressWord, uint data)
        {            
            //
            // Display I/O Write Address Word format:
            // 0      7 8    22 23     27 28        31
            // | dev # |       | command | register # |
            //
            uint register = addressWord & 0xf;
            uint command = (addressWord >> 4) & 0x1f;

            switch(register)
            {
                case 0x0:
                    break;

                case 0x1:
                    _dar = data & 0xffff;
                    break;

                case 0x2:
                    _mar = data >> 6;
                    break;

                case 0x4:                    
                    _count = data >> 16;
                    break;

                case 0x5:
                    _dar = data & 0xffff;
                    _count = data >> 16;
                    break;

                case 0x8:
                    // TODO: i think the sense may be inverted from what the manual says
                    // on these bits...
                    _status = data;
                    Console.WriteLine("status {0:x}", _status);
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unhandled display register {0}.", register));
            }

            switch(command)
            {
                case 0x0:
                case 0x10:
                    break;

                case 0xe:
                    WriteBuffer();
                    break;

                case 0xd:
                    ReadBuffer();
                    break;

                case 0xf:
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unhandled display command {0:x}.", command));

            }

            if ((_status & 0x1) == 0)
            {
                _interrupt = true;

                // display device id + 768x1024 display + command completion.
                _ioir = (uint)(0x000000001 | (_deviceId << 24));
            }

            //Console.WriteLine("command {0:x} reg {1:x} val {2:x}", command, register, data);

            return 0;
        }

        private void WriteBuffer()
        {
            //
            // For now, we just do this instantaneously.  We should do this cycle-accurately
            // at some point.
            //
            for(uint i=0;i<_count;i++)
            {
                uint word = _mem.ReadWord(_mar + i * 4);
                _framebuffer[_dar + i] = ~word;
            }
        }

        private void ReadBuffer()
        {
            //
            // For now, we just do this instantaneously.  We should do this cycle-accurately
            // at some point.
            //
            for (uint i = 0; i < _count; i++)
            {
                uint word = _framebuffer[_dar + i];
                _mem.WriteWord(i + _mar, word);
            }
        }

        private void FrameCompleteCallback(ulong timeNsec, ulong skewNsec, object context)
        {
            //
            // Interrupt, if enabled.
            //
            if ((_status & 0x8) == 0)
            {
                _interrupt = true;

                // display device id + beam at top of screen, 768x1024 display.
                _ioir = (uint)(0x000000008 | (_deviceId << 24));
            }

            _display.Render(_framebuffer);

            //
            // Schedule next frame unless the display has been turned off.
            //
            //if ((_status & 0x2) != 0)
            {
                _frameEvent.TimestampNsec = _frameTimeNsec;
                _sys.Scheduler.Schedule(_frameEvent);
            }
        }

        //
        // Framebuffer memory
        //
        private uint[] _framebuffer;

        //
        // The display
        //
        DisplayWindow _display;

        //
        // Registers
        //
        private uint _dar;
        private uint _mar;
        private uint _count;
        private uint _status;

        private bool _interrupt;
        private uint _ioir;

        private RidgeSystem _sys;
        private IPhysicalMemory _mem;        
        private byte _deviceId;

        private int _hack;
        private uint _key;

        //
        // Device events
        //
        private Event _frameEvent;
        private ulong _frameTimeNsec = 16666000;
    }
}
