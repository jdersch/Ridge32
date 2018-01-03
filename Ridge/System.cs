using System;
using System.IO;

using Ridge.CPU;
using Ridge.IO;
using Ridge.Memory;

namespace Ridge
{
    public class System
    {
        public System()
        {
            _mem = new PhysicalMemory(1024);
            _io = new IOBus();
            _cpu = new Processor(_mem, _io);

            // Attach IO devices
            _fdlp = new FDLP(_mem, _cpu);
            _io.RegisterDevice(1, _fdlp);
        }

        public void Reset()
        {
            _cpu.Reset();
        }

        public void Clock()
        {
            _cpu.Execute();
            _fdlp.Clock();
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

        private Processor _cpu;
        private IOBus _io;
        private IPhysicalMemory _mem;

        // IO devices
        private FDLP _fdlp;
    }
}
