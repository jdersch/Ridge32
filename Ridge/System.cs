using System;
using System.IO;

using Ridge.CPU;
using Ridge.IO;
using Ridge.Memory;

namespace Ridge
{
    public class RidgeSystem
    {
        public RidgeSystem()
        {
            _scheduler = new Scheduler();

            _mem = new Memory.MemoryController(8192);
            _io = new IOBus();
            _cpu = new Processor(_mem, _io);
            _mem.AttachCPU(_cpu);

            // Attach IO devices
            _fdlp = new FDLP(0x1, this);
            _io.RegisterDevice(0x1, _fdlp);

            _priamController = new PriamDiskController(0x2, this);
            _io.RegisterDevice(0x2, _priamController);

            //_display = new Display(0x4, this);
            //_io.RegisterDevice(0x4, _display);
            //_io.RegisterDevice(0x5, _display);
        }

        public void Reset()
        {
            // Read in bootstrap from FDLP (TODO: base this on LOAD switch, etc.)
            FDLP.Boot();

            _cpu.Reset();
        }

        public void Clock()
        {            
            _scheduler.Clock();

            _cpu.Execute();
            _fdlp.Clock();
            //_display.Clock();
        }

        public Processor CPU
        {
            get { return _cpu; }
        }

        public IOBus IO
        {
            get { return _io; }
        }

        public MemoryController Memory
        {
            get { return _mem; }
        }

        public FDLP FDLP
        {
            get { return _fdlp; }
        }

        public PriamDiskController PriamController
        {
            get { return _priamController; }
        }

        public Scheduler Scheduler
        {
            get { return _scheduler; }
        }

        private Processor _cpu;
        private IOBus _io;
        private Memory.MemoryController _mem;        

        // IO devices
        private FDLP _fdlp;
        private Display _display;
        private PriamDiskController _priamController;

        // System scheduler
        private Scheduler _scheduler;
    }
}
