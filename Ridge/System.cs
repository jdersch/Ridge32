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

            _mem = new PhysicalMemory(8192);
            _io = new IOBus();
            _cpu = new Processor(_mem, _io);

            // Attach IO devices
            _fdlp = new FDLP(0x1, this);
            _io.RegisterDevice(0x1, _fdlp);

            //_display = new Display(0x5, this);
            //_io.RegisterDevice(0x5, _display);
        }

        public void Reset()
        {
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

        public IPhysicalMemory Memory
        {
            get { return _mem; }
        }

        public Scheduler Scheduler
        {
            get { return _scheduler; }
        }

        private Processor _cpu;
        private IOBus _io;
        private IPhysicalMemory _mem;

        // IO devices
        private FDLP _fdlp;
        private Display _display;

        // System scheduler
        private Scheduler _scheduler;
    }
}
