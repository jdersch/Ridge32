using System;

using Ridge.IO;
using Ridge.Memory;

namespace Ridge.CPU
{
    public enum ProcessorMode
    {
        Kernel = 0,
        User
    }

    public enum TrapType
    {
        KernelCall,
        DataAlignment,
        IllegalInstruction,
        KernelViolation,
        Check,
        TrapInstruction,
        ArithmeticTrap,
    }

    public enum EventType
    {
        KCALL =                0x000,
        DataAlignment        = 0x400,
        IllegalInstruction   = 0x404,
        DoubleBitParityFetch = 0x408,
        DoubleBitParityExecute = 0x40c,
        PageFault            = 0x410,
        KernelViolation      = 0x414,
        CheckTrap            = 0x418,
        ArithmeticTrap       = 0x41c,
        ExternalInterrupt    = 0x420,
        Switch0Interrupt     = 0x424,
        PowerFailWarning     = 0x428,
        Timer1Interrupt      = 0x430,
        Timer2Interrupt      = 0x434,
    }

    /// <summary>
    /// The main Ridge CPU.
    /// </summary>
    public class Processor
    {
        public Processor(IPhysicalMemory mem, IOBus io)
        {
            _mem = mem;
            _io = io;
        }

        public void Reset()
        {
            //
            // Per the HWRef:
            // "The system is booted whenever it is reset: by applying ac power, depressing
            //  the front panel load button, or depressing the reset button on the clock board.
            //  when the system is booted, the cpu microcode sends a boot command to the selected
            //  I/O device... [boot code] is placed in memory at location 3E000H.
            //  After loading memory [at boot] the booting device interrupts
            //  the cpu, and the cpu begins executing in kernel mode at location 3E000H.
            //  SR11, the CCB pointer is set to 1, disabling timer 1 and timer 23 interrupts."
            //

            //
            // Since we're not doing a microcode-level emulation, we'll kick the boot device here
            // to get it to load the appropriate data into main memory and start the CPU in kernel
            // mode in the correct location, with CCB set to 1.
            //

            //
            // TODO: kick I/O.  For now, we're testing with boot data pre-loaded into memory
            // at the requisite location.
            //

            _mode = ProcessorMode.Kernel;
            _pc = 0x3e000;
            _sr[11] = 1;

            // From the "RESET" section on page 6-6:
            // "SR2 = Memory size"  (TODO: is this in bytes, or what?)          
            // "SR14 = 1 (no PCB)"
            _sr[2] = _mem.Size;
            _sr[14] = 1;


        }

        public uint PC
        {
            get { return _pc; }
        }

        public uint[] R
        {
            get { return _r; }
        }

        public uint[] SR
        {
            get { return _sr; }
        }

        public ProcessorMode Mode
        {
            get { return _mode; }
        }

        public void Execute()
        {
            //
            // Check for pending external interrupts.
            _intDevice = _io.InterruptRequested();

            if (_intDevice != null)
            {
                //
                // Yes, we have an external interrupt.
                // Signal an event and do what needs to be done...
                //
                SignalEvent(EventType.ExternalInterrupt, 0);

                //
                // Set the external interrupt flag.  This is used by the ITEST
                // MAINT instruction to allow polling for interrupts in Kernel mode.
                // I am unsure if this is an accurate representation of
                // the actual Ridge behavior, the documentation is 
                // unclear.
                //
                _externalInterrupt = true;
            }            

            //
            // Decode the current instruction.
            // The eventual intent is to cache these Instruction objects
            // to save execution time; for the time being we cons up a new one each
            // time around which is slow and causes GCs, but is simple while we get
            // this thing off the ground.
            //

            // Save PC pre-increment.
            uint opc = _pc;            
            Instruction i;

            if (_mode == ProcessorMode.Kernel)
            {
                i = new Instruction(_mem, _pc);
            }
            else
            {
                //
                // Get the translated physical address, take a page fault if necessary.
                //
                bool pageFault = false;
                uint rPC = TranslateVirtualToReal(
                    _sr[8],      // code segment, obvs.
                    _pc,
                    false,
                    true,
                    out pageFault);

                if (pageFault)
                {                    
                    SignalEvent(EventType.PageFault, _pc);                    
                    return;
                }

                //
                // TODO:
                // instructions straddling a page boundary may lie in different
                // physical areas of memory, or may pagefault...
                //
                i = new Instruction(_mem, rPC);
            }

            _pc += i.Length;
            
            switch(i.Op)
            {
                case Opcode.MOVE:
                    _r[i.Rx] = _r[i.Ry];
                    break;

                case Opcode.NEG:
                    // TODO: traps on overflow
                    _r[i.Rx] = (uint)(-_r[i.Ry]);
                    break;

                case Opcode.ADD:
                    // TODO: traps on overflow
                    _r[i.Rx] = (uint)((int)_r[i.Rx] + (int)_r[i.Ry]);
                    break;

                case Opcode.SUB:
                    // TODO: traps on overflow
                    _r[i.Rx]= (uint)((int)_r[i.Rx] - (int)_r[i.Ry]);
                    break;

                case Opcode.MPY:
                    // TODO: traps on overflow
                    _r[i.Rx] = (uint)((int)_r[i.Rx] * (int)_r[i.Ry]);
                    break;

                case Opcode.DIV:
                    // TODO: traps on div by zero
                    _r[i.Rx] = (uint)((int)_r[i.Rx] / (int)_r[i.Ry]);
                    break;

                case Opcode.REM:
                    // TODO: traps on div by zero
                    _r[i.Rx] = (uint)((int)_r[i.Rx] % (int)_r[i.Ry]);
                    break;

                case Opcode.NOT:
                    _r[i.Rx] = ~_r[i.Ry];
                    break;

                case Opcode.OR:
                    _r[i.Rx] |= _r[i.Ry];
                    break;

                case Opcode.XOR:
                    _r[i.Rx] ^= _r[i.Ry];
                    break;

                case Opcode.AND:
                    _r[i.Rx] &= _r[i.Ry];
                    break;

                case Opcode.CBIT:
                    {
                        // Clears the specified bit in the 64-bit register pair specified by Rx.
                        // NB: Ridge numbers its bits in the opposite of the modern convention;
                        // the MSB is bit 0, LSB is 63...
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp &= ~(0x8000000000000000 >> (int)(_r[i.Ry] & 0x3f));
                        SetRegisterPairValue(i.Rx, rp);                        
                    }
                    break;

                case Opcode.SBIT:
                    {
                        // Sets the specified bit in the 64-bit register pair specified by Rx.
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp |= (0x8000000000000000 >> (int)(_r[i.Ry] & 0x3f));
                        SetRegisterPairValue(i.Rx, rp);                        
                    }
                    break;

                case Opcode.TBIT:
                    {
                        // Sets bit 31 of Rx to the value of the selected bit; clears all other bits in Rx.
                        ulong rp = GetRegisterPairValue(i.Rx);
                        _r[i.Rx] = (uint)((rp & (0x8000000000000000 >> (int)(_r[i.Ry] & 0x3f))) == 0 ? 0 : 1);
                    }
                    break;

                case Opcode.CHK:
                    // Trap if Rx > Ry
                    // TODO: manual conflicts on this, actually...
                    if ((int)_r[i.Rx] > (int)_r[i.Ry])
                    {
                        Trap(TrapType.Check);
                    }
                    break;

                case Opcode.NOP:
                    // I'm eskimo.  There's nothing here.
                    break;

                case Opcode.MOVEI_i:
                    _r[i.Rx] = (uint)i.Ry;
                    break;

                case Opcode.ADDI_i:
                    _r[i.Rx] += (uint)i.Ry;
                    break;

                case Opcode.SUBI_i:
                    _r[i.Rx] -= (uint)i.Ry;
                    break;

                case Opcode.MPYI_i:
                    _r[i.Rx] *= (uint)i.Ry;
                    break;

                case Opcode.NOTI_i:
                    // TODO: does this get masked to 4 bits?
                    _r[i.Rx] = (uint)(~i.Ry & 0xf);
                    break;

                case Opcode.ANDI_i:
                    _r[i.Rx] &= (uint)i.Ry;
                    break;

                case Opcode.CHKI_i:
                    // Trap if NOT (0 <= (Rx) <= Ry)
                    if (!(0 <= (int)_r[i.Rx] && (int)_r[i.Rx] <= i.Ry))
                    {
                        Trap(TrapType.Check);
                    }
                    break;

                case Opcode.FIXT:
                case Opcode.FIXR:
                case Opcode.RNEG:
                case Opcode.RADD:
                case Opcode.RSUB:
                case Opcode.RMPY:
                case Opcode.RDIV:
                case Opcode.MAKERD:
                case Opcode.FLOAT:
                case Opcode.EADD:
                case Opcode.ESUB:
                case Opcode.EMPY:
                case Opcode.EDIV:
                case Opcode.DFIXT:
                case Opcode.DFIXR:
                case Opcode.DRNEG:
                case Opcode.DRADD:
                case Opcode.DRSUB:
                case Opcode.DRMPY:
                case Opcode.DRDIV:
                case Opcode.MAKEDR:
                case Opcode.DFLOAT:
                case Opcode.DRCOMP:
                    throw new NotImplementedException();

                case Opcode.RCOMP:
                    {
                        float rx = GetFloatFromWord(_r[i.Rx]);
                        float ry = GetFloatFromWord(_r[i.Ry]);

                        if (rx < ry)
                        {
                            _r[i.Rx] = 0xffffffff;
                        }
                        else if (rx == ry)
                        {
                            _r[i.Rx] = 0;
                        }
                        else
                        {
                            _r[i.Rx] = 1;
                        }
                    }
                    break;

                case Opcode.DCOMP:
                    {
                        ulong rx = GetRegisterPairValue(i.Rx);
                        ulong ry = GetRegisterPairValue(i.Ry);

                        if ((long)rx < (long)ry)
                        {
                            _r[i.Rx] = 0xffffffff;
                        }
                        else if ((long)rx == (long)ry)
                        {
                            _r[i.Rx] = 0;
                        }
                        else
                        {
                            _r[i.Rx] = 1;
                        }
                    }
                    break;

                case Opcode.LCOMP:
                    {                        
                        if ((int)_r[i.Rx] < (int)_r[i.Ry])
                        {
                            _r[i.Rx] = 0xffffffff;
                        }
                        else if ((int)_r[i.Rx] == (int)_r[i.Ry])
                        {
                            _r[i.Rx] = 0;
                        }
                        else
                        {
                            _r[i.Rx] = 1;
                        }
                    }
                    break;

                case Opcode.TRAP:
                    _sr[3] = (uint)i.Ry;
                    Trap(TrapType.TrapInstruction);
                    break;

                case Opcode.SUS:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    //
                    // "The SUS instruction stores the user program counter and the process clock into the
                    //  PCB pointed to by special register SR14.  In addition, the general-purpose registers are
                    //  stored beginning with Rx and ending with Ry.  The instruction can store from 1 to 16 of
                    //  the general purpose registers.
                    //  If the Rx specification is greater than Ry then only Rx is stored (i.e. register numbers
                    //  do not wrap around).  If SR14 = 1, no registers are stored and the instruction performs
                    //  no operation."
                    //
                    if (_sr[14] != 1)
                    {
                        _mem.WriteWord(_sr[14] + 0x40, _sr[15]);        // sr15 or PC?
                        _mem.WriteWord(_sr[14] + 0x44, (_sr[8] << 16) | (_sr[9] & 0xffff));
                        _mem.WriteWord(_sr[14] + 0x4c, _sr[10]);
                        // TODO: process clock stuff.

                        uint currentReg = (uint)i.Rx;

                        do
                        {
                            _mem.WriteWord(_sr[14] + (currentReg * 4), _r[currentReg++]);
                        } while (currentReg <= i.Ry);
                    }

                    break;

                case Opcode.LUS:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }
                    
                    //
                    // "The LUS instruction is the inverse of the SUS instruction.  It loads the user program
                    //  counter [SR15], the code and data segment numbers [SR8, SR9] and the traps word [SR10]
                    //  from the PCB pointed to by SR14.  From 1 to 16 general registers can also be loaded.
                    //  If Rx is greater than Ry, only Rx is loaded (i.e. register numbers do not wrap around).
                    //  The instruction cache and translation mapping table are flushed.  If SR14 = 1, no
                    //  registers are loaded and the instruction performs no operation."
                    //
                    if (_sr[14] != 1)
                    {
                        // Restore the user PC, code and data segment registers, and traps word from the PCB.
                        _sr[15] = _mem.ReadWord(_sr[14] + 0x40);
                        _sr[8] = (_mem.ReadWord(_sr[14] + 0x44) >> 16);
                        _sr[9] = (_mem.ReadWord(_sr[14] + 0x44) & 0xffff);
                        _sr[10] = _mem.ReadWord(_sr[14] + 0x4c);

                        uint currentReg = (uint)i.Rx;

                        do
                        {
                            _r[currentReg++] = _mem.ReadWord(_sr[14] + (currentReg * 4));
                        } while (currentReg <= i.Ry);

                        //
                        // We don't currently implement the instruction cache or TMT, so don't flush them.
                        // Hard to flush something that doesn't exist.
                        //
                    }
                    break;

                case Opcode.RUM:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    if (_sr[14] == 1)
                    {
                        throw new NotImplementedException("RUM with R14 == 1 not implemented.");
                    }
                    else
                    {
                        _pc = _sr[15];
                        _mode = ProcessorMode.User;
                    }
                    break;

                case Opcode.LDREGS:
                    throw new NotImplementedException();

                case Opcode.TRANS:
                case Opcode.DIRT:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    bool pageFault = false;
                    uint translatedAddress = TranslateVirtualToReal(
                        _r[i.Ry],
                        _r[(i.Ry + 1) & 0xf],
                        i.Op == Opcode.DIRT,      // modified for DIRT instruction
                        true,                     // referenced
                        out pageFault);

                    _r[i.Rx] = pageFault ? 0xffffffff : translatedAddress;
                    break;

                case Opcode.MOVE_sr:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    _sr[i.Rx] = _r[i.Ry];

                    if (i.Rx == 8)
                    {
                        Console.WriteLine("sr8 = {0:x}", _sr[8]);
                    }
                    break;

                case Opcode.MOVE_r:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    _r[i.Rx] = _sr[i.Ry];
                    break;

                case Opcode.MAINT:                    
                    if (!(_mode == ProcessorMode.Kernel ||  // Not kernel mode
                         ((_sr[10] & 0x1) == 1)))           // Not privileged user mode
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    // Ry is used as the subop field.
                    switch((MaintOpcode)i.Ry)
                    {
                        case MaintOpcode.ELOGR:
                            //
                            // From the Proc ref (pg 40):
                            // "The ELOGR instruction reads the memory error logging RAM,
                            // using Rx as an address.  The logging RAM data is returned as a bit in Rx.
                            // Output Rx also contains the processor status, regardless of the input Rx value.
                            // The pertinent status bits are described below.
                            //
                            //   Status Description                     Bit No.
                            //   ------------------                     -------
                            //   Memory error logging RAM data          16
                            //   External interrupt                     27
                            //   Secondary/primary boot device          30
                            //   Load enable switch set                 31
                            // "

                            // TODO: actually interface to all the above things.
                            // Right now, just return 0 for everything, this indicates
                            // that the load enable switch is off (so we just get dumped into RBUG)
                            _r[i.Rx] = (uint)(_externalInterrupt ? 0x10 : 0x00);
                            break;

                        case MaintOpcode.TRAPEXIT:
                            // "The TRAPEXIT instruction sets PC to the value contained in SR0 and begins
                            //  executing at that address...
                            //  The TRAPEXIT instruction flushes the cache and the TMT."
                            _pc = _sr[0];
                            break;

                        case MaintOpcode.ITEST:
                            if (_externalInterrupt)
                            {
                                _r[(i.Rx + 1) & 0xf] = _sr[0];
                                _r[i.Rx] = 1;

                                // clear the interrupt flip flop.
                                _externalInterrupt = false;
                            }
                            else
                            {
                                _r[i.Rx] = 0;
                            }
                            break;

                        case MaintOpcode.MACHINEID:
                            // "The number returned in Rx contains the encoded serial number, the machine
                            //  model number, and the maximum user configuration.
                            //  The two 8-bit values Rx[8..15] and Rx[24..31] can be joined together to determine a 16-
                            //  bit model number.  Adding this value to a serial number base produces the serial
                            //  number of the machine.  The maximum user configuration is determined by bits
                            //  Rx[20..23].  If Rx[20..23] are all 1's, there is no maximum user configuration.
                            //  Specific hardware options can be determined by bits Rx[28..31].  These are:
                            //   31 = Not used.  (Formerly copy-on-write memory controller present).
                            //   30 = Enhanced floating point present.  (Set to 1 for 3200 processor.)
                            //   29 = old/new VRT layout.  Old VRT was limited to 8mbytes of memory.  New VRT is
                            //        limited to 128Mbytes.
                            //   28 = 3200 processor present.
                            //
                            // At the current time we present things as the original Ridge32 CPU, old VRT, normal floating point
                            // with no user limit and serial number 1.
                            //
                            _r[i.Rx] = 0x000100f0;
                            break;

                        default:
                            throw new NotImplementedException(
                                String.Format("Unimplemented MAINT instruction {0}.", (MaintOpcode)i.Ry));
                    }

                    break;
                case Opcode.READ:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    _r[i.Rx] = _io.Read(
                        (_r[i.Ry] & 0xff000000) >> 24,
                        _r[i.Ry] & 0x00ffffff,
                        out _r[(i.Rx + 1) & 0xf]);

                    break;

                case Opcode.WRITE:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                        break;
                    }

                    _r[i.Rx] = _io.Write(
                        (_r[i.Ry] & 0xff000000) >> 24,
                        _r[i.Ry] & 0x00ffffff,
                        _r[i.Rx]);

                    break;

                case Opcode.TEST_gt:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] > (int)_r[i.Ry]) ? 1 : 0);
                    break;

                case Opcode.TEST_lt:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] < (int)_r[i.Ry]) ? 1 : 0);
                    break;

                case Opcode.TEST_eq:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] == (int)_r[i.Ry]) ? 1 : 0);
                    break;

                case Opcode.CALLR:
                    _r[i.Rx] = _pc;     // + 2 already added in
                    _pc = opc + _r[i.Ry];  // remove + 2 added in
                    break;

                case Opcode.TEST_gti:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] > i.Ry) ? 1 : 0);
                    break;

                case Opcode.TEST_lti:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] < i.Ry) ? 1 : 0);
                    break;

                case Opcode.TEST_eqi:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] == i.Ry) ? 1 : 0);
                    break;

                case Opcode.RET:
                    // Save the original PC so it doesn't get stomped if, for example,
                    // RET R11, R11 is done...
                    uint oldPC = _pc;
                    _pc = _r[i.Ry];
                    _r[i.Rx] = oldPC;     // + 2 already added in
                    break;

                case Opcode.TEST_lteq:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] <= (int)_r[i.Ry]) ? 1 : 0);
                    break;

                case Opcode.TEST_gteq:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] >= (int)_r[i.Ry]) ? 1 : 0);
                    break;

                case Opcode.TEST_neq:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] != (int)_r[i.Ry]) ? 1 : 0);
                    break;

                case Opcode.KCALL:
                    throw new NotImplementedException();
                    break;

                case Opcode.TEST_lteqi:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] <= i.Ry) ? 1 : 0);
                    break;

                case Opcode.TEST_gteqi:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] >= i.Ry) ? 1 : 0);
                    break;

                case Opcode.TEST_neqi:
                    _r[i.Rx] = (uint)(((int)_r[i.Rx] != i.Ry) ? 1 : 0);
                    break;

                case Opcode.LSL:
                    _r[i.Rx] = _r[i.Rx] << (int)(_r[i.Ry] & 0x1f);
                    break;

                case Opcode.LSR:
                    _r[i.Rx] = _r[i.Rx] >> (int)(_r[i.Ry] & 0x1f);
                    break;

                case Opcode.ASL:
                    // Fun: 1983 edition of processor ref states two things:
                    //  - Sign bit is held constant through left shift
                    //  - When a bit different than the initial sign bit
                    //    is shifted out of bit one into the sign bit,
                    //    an integer overflow trap is taken..."
                    // The 1986 edition simply says "The ASL instruction
                    // shifts left."
                    // Which is correct?  Only time will tell.
                    // For now, following the 1983 spec since it makes more sense.
                    //

                    // TODO: this could be done more efficiently.
                    {
                        uint signBit = _r[i.Rx] & 0x80000000;
                        for (int s = 0; s < (_r[i.Ry] & 0x1f); s++)
                        {
                            _r[i.Rx] = _r[i.Rx] << 1;
                            uint newSignBit = _r[i.Rx] & 0x80000000;
                            _r[i.Rx] = (_r[i.Rx] & 0x7fffffff) | signBit;

                            if (signBit != newSignBit)
                            {
                                Trap(TrapType.ArithmeticTrap);
                            }
                        }
                    }
                    break;

                case Opcode.ASR:
                    // TODO: again, inefficient.
                    {
                        uint signBit = _r[i.Rx] & 0x80000000;
                        for (int s = 0; s < (_r[i.Ry] & 0x1f); s++)
                        {
                            _r[i.Rx] = (_r[i.Rx] >> 1) | signBit;                            
                        }
                    }
                    break;

                case Opcode.DLSL:
                    {
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp = rp << (int)(_r[i.Ry] & 0x3f);
                        SetRegisterPairValue(i.Rx, rp);                        
                    }
                    break;

                case Opcode.DLSR:
                    {
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp = rp >> (int)(_r[i.Ry] & 0x3f);
                        SetRegisterPairValue(i.Rx, rp);                        
                    }
                    break;

                case Opcode.CSL:
                    {
                        for (int s = 0; s < (_r[i.Ry] & 0x1f); s++)
                        {
                            uint signBit = _r[i.Rx] & 0x80000000;
                            _r[i.Rx] = (_r[i.Rx] << 1) | (signBit >> 31);
                        }
                    }
                    break;

                case Opcode.SEB:
                    // Sign extend byte
                    _r[i.Rx] = _r[i.Ry] & 0xff;
                    _r[i.Rx] |= (_r[i.Ry] & 0x80) == 0 ? 0 : 0xffffff00;
                    break;

                case Opcode.LSLI_i:
                    _r[i.Rx] = _r[i.Rx] << i.Ry;
                    break;

                case Opcode.LSRI_i:
                    _r[i.Rx] = _r[i.Rx] >> i.Ry;
                    break;

                case Opcode.ASLI_i:
                    // See comments for ASL...
                    {
                        uint signBit = _r[i.Rx] & 0x80000000;
                        for (int s = 0; s < i.Ry; s++)
                        {
                            _r[i.Rx] = _r[i.Rx] << 1;
                            uint newSignBit = _r[i.Rx] & 0x80000000;
                            _r[i.Rx] = (_r[i.Rx] & 0x7fffffff) | signBit;

                            if (signBit != newSignBit)
                            {
                                Trap(TrapType.ArithmeticTrap);
                            }
                        }
                    }
                    break;

                case Opcode.ASRI_i:
                    // TODO: again, inefficient.
                    {
                        uint signBit = _r[i.Rx] & 0x80000000;
                        for (int s = 0; s < i.Ry; s++)
                        {
                            _r[i.Rx] = (_r[i.Rx] >> 1) | signBit;
                        }
                    }
                    break;

                case Opcode.DLSLI_i:
                    {
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp = rp << i.Ry;
                        SetRegisterPairValue(i.Rx, rp);
                    }
                    break;

                case Opcode.DLSRI_i:
                    {
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp = rp >> i.Ry;
                        SetRegisterPairValue(i.Rx, rp);
                    }
                    break;

                case Opcode.CSLI_i:
                    {
                        for (int s = 0; s < i.Ry; s++)
                        {
                            uint signBit = _r[i.Rx] & 0x80000000;
                            _r[i.Rx] = (_r[i.Rx] << 1) | (signBit >> 31);
                        }
                    }
                    break;

                case Opcode.SEH:
                    // Sign extend half-word
                    _r[i.Rx] = _r[i.Ry] & 0xffff;
                    _r[i.Rx] |= (_r[i.Ry] & 0x8000) == 0 ? 0 : 0xffff0000;
                    break;

                case Opcode.BR_gts:
                case Opcode.BR_gtl:
                    if ((int)_r[i.Rx] > (int)_r[i.Ry])
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_eqs:
                case Opcode.BR_eql:
                    if ((int)_r[i.Rx] == (int)_r[i.Ry])
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.CALL_s:
                case Opcode.CALL_l:
                    _r[i.Rx] = _pc;
                    _pc = i.BranchAddress;
                    break;

                case Opcode.BR_gtsi:
                case Opcode.BR_gtli:
                    if ((int)_r[i.Rx] > i.Ry)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_ltsi:
                case Opcode.BR_ltli:
                    if ((int)_r[i.Rx] < i.Ry)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_eqsi:
                case Opcode.BR_eqli:
                    if ((int)_r[i.Rx] == i.Ry)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.LOOP_s:
                case Opcode.LOOP_l:
                    _r[i.Rx] += (uint)i.Ry;

                    if ((int)_r[i.Rx] < 0)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_lteqs:
                case Opcode.BR_lteql:
                    if ((int)_r[i.Rx] <= (int)_r[i.Ry])
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_neqs:
                case Opcode.BR_neql:
                    if ((int)_r[i.Rx] != (int)_r[i.Ry])
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_s:
                case Opcode.BR_l:
                    _pc = i.BranchAddress;
                    break;

                case Opcode.BR_lteqsi:
                case Opcode.BR_lteqli:
                    if ((int)_r[i.Rx] <= i.Ry)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_gteqsi:
                case Opcode.BR_gteqli:
                    if ((int)_r[i.Rx] >= i.Ry)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.BR_neqsi:
                case Opcode.BR_neqli:
                    if ((int)_r[i.Rx] != i.Ry)
                    {
                        _pc = i.BranchAddress;
                    }
                    break;

                case Opcode.STOREB_s:
                case Opcode.STOREB_l:
                    WriteByte((uint)i.Displacement, (byte)_r[i.Rx]);
                    break;

                case Opcode.STOREB_sx:
                case Opcode.STOREB_lx:
                    WriteByte((uint)(i.Displacement + (int)_r[i.Ry]), (byte)_r[i.Rx]);
                    break;

                case Opcode.STOREH_s:
                case Opcode.STOREH_l:
                    WriteHalfWord((uint)i.Displacement, (ushort)_r[i.Rx]);
                    break;

                case Opcode.STOREH_sx:
                case Opcode.STOREH_lx:
                    WriteHalfWord((uint)(i.Displacement + (int)_r[i.Ry]), (ushort)_r[i.Rx]);
                    break;

                case Opcode.STORE_s:
                case Opcode.STORE_l:
                    WriteWord((uint)i.Displacement, _r[i.Rx]);
                    break;

                case Opcode.STORE_sx:
                case Opcode.STORE_lx:
                    WriteWord((uint)(i.Displacement + (int)_r[i.Ry]), _r[i.Rx]);
                    break;

                case Opcode.STORED_s:
                case Opcode.STORED_l:
                    WriteDoubleWord((uint)i.Displacement, GetRegisterPairValue(i.Rx));
                    break;

                case Opcode.STORED_sx:
                case Opcode.STORED_lx:
                    WriteDoubleWord((uint)(i.Displacement + (int)_r[i.Ry]), GetRegisterPairValue(i.Rx));
                    break;

                case Opcode.LOADB_ds:
                case Opcode.LOADB_dl:                                       
                    ReadByte(i.Rx, (uint)i.Displacement, SegmentType.Data);
                    break;
                    
                case Opcode.LOADB_dsx:
                case Opcode.LOADB_dlx:
                    ReadByte(i.Rx, (uint)(i.Displacement + (int)_r[i.Ry]), SegmentType.Data);
                    break;

                case Opcode.LOADH_ds:
                case Opcode.LOADH_dl:
                    ReadHalfWord(i.Rx, (uint)i.Displacement, SegmentType.Data);
                    break;

                case Opcode.LOADH_dsx:
                case Opcode.LOADH_dlx:
                    ReadHalfWord(i.Rx, (uint)(i.Displacement + (int)_r[i.Ry]), SegmentType.Data);
                    break;

                case Opcode.LOAD_ds:
                case Opcode.LOAD_dl:
                    ReadWord(i.Rx, (uint)i.Displacement, SegmentType.Data);
                    break;

                case Opcode.LOAD_dsx:
                case Opcode.LOAD_dlx:
                    ReadWord(i.Rx, (uint)(i.Displacement + (int)_r[i.Ry]), SegmentType.Data);
                    break;

                case Opcode.LOADD_ds:
                case Opcode.LOADD_dl:
                    ReadDoubleWord(i.Rx, (uint)i.Displacement, SegmentType.Data);
                    break;

                case Opcode.LOADD_dsx:
                case Opcode.LOADD_dlx:
                    ReadDoubleWord(i.Rx, (uint)(i.Displacement + (int)_r[i.Ry]), SegmentType.Data);
                    break;

                case Opcode.LADDR_ds:
                case Opcode.LADDR_dl:
                    _r[i.Rx] = (uint)i.Displacement;
                    break;

                case Opcode.LADDR_dsx:
                case Opcode.LADDR_dlx:
                    _r[i.Rx] = (uint)(i.Displacement + (int)_r[i.Ry]);
                    break;

                case Opcode.LOADB_cs:
                case Opcode.LOADB_cl:
                    ReadByte(i.Rx, (uint)(opc + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOADB_csx:
                case Opcode.LOADB_clx:
                    ReadByte(i.Rx, (uint)(opc + (int)_r[i.Ry] + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOADH_cs:
                case Opcode.LOADH_cl:
                    ReadHalfWord(i.Rx, (uint)(opc + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOADH_csx:
                case Opcode.LOADH_clx:
                    ReadHalfWord(i.Rx, (uint)(opc + (int)_r[i.Ry] + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOAD_cs:
                case Opcode.LOAD_cl:
                    ReadWord(i.Rx, (uint)(opc + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOAD_csx:
                case Opcode.LOAD_clx:
                    ReadWord(i.Rx, (uint)(opc + (int)_r[i.Ry] + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOADD_cs:
                case Opcode.LOADD_cl:
                    ReadDoubleWord(i.Rx, (uint)(opc + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LOADD_csx:
                case Opcode.LOADD_clx:
                    ReadDoubleWord(i.Rx, (uint)(opc + (int)_r[i.Ry] + i.Displacement), SegmentType.Code);
                    break;

                case Opcode.LADDR_cs:
                case Opcode.LADDR_cl:
                    _r[i.Rx] = (uint)(opc + i.Displacement);
                    break;

                case Opcode.LADDR_csx:
                case Opcode.LADDR_clx:
                    _r[i.Rx] = (uint)(opc + (int)_r[i.Ry] + i.Displacement);
                    break;

                default:
                    // Should eventually Trap.  Throw at the moment while debugging.
                    throw new NotImplementedException(
                        String.Format("Unimplemented opcode {0}.", i.Op));
            }
        }

        //
        // Memory access functions; these translate virtual to physical,
        // check for proper alignment and trap if incorrect.
        //
        private void ReadByte(int reg, uint address, SegmentType segment)
        {
            bool pageFault = false;

            if (_mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    segment == SegmentType.Code ? _sr[8] : _sr[9],
                    address,
                    false,      // not modified
                    true,
                    out pageFault);

                if (pageFault)
                {
                    SignalEvent(EventType.PageFault, address);
                }
            }

            if (!pageFault)
            {
                _r[reg] = _mem.ReadByte(address);
            }
        }

        private void ReadHalfWord(int reg, uint address, SegmentType segment)
        {            
            if ((address & 0x1) != 0)
            {
                Trap(TrapType.DataAlignment);
            }
            else
            {
                bool pageFault = false;
                if (_mode != ProcessorMode.Kernel)
                {
                    address = TranslateVirtualToReal(
                        segment == SegmentType.Code ? _sr[8] : _sr[9],
                        address,
                        false,      // not modified
                        true,
                        out pageFault);

                    if (pageFault)
                    {
                        SignalEvent(EventType.PageFault, address);
                    }
                }

                if (!pageFault)
                {
                    _r[reg] = _mem.ReadHalfWord(address);
                }
            }
        }

        private void ReadWord(int reg, uint address, SegmentType segment)
        {            
            if ((address & 0x3) != 0)
            {
                Trap(TrapType.DataAlignment);
            }
            else
            {
                bool pageFault = false;
                if (_mode != ProcessorMode.Kernel)
                {
                    address = TranslateVirtualToReal(
                        segment == SegmentType.Code ? _sr[8] : _sr[9],
                        address,
                        false,      // not modified
                        true,
                        out pageFault);

                    if (pageFault)
                    {
                        SignalEvent(EventType.PageFault, address);
                    }
                }

                if (!pageFault)
                {
                    _r[reg] = _mem.ReadWord(address);
                }
            }
        }

        private void ReadDoubleWord(int rp, uint address, SegmentType segment)
        {            
            if ((address & 0x7) != 0)
            {
                Trap(TrapType.DataAlignment);         
            }
            else
            {
                bool pageFault = false;
                if (_mode != ProcessorMode.Kernel)
                {
                    address = TranslateVirtualToReal(
                        segment == SegmentType.Code ? _sr[8] : _sr[9],
                        address,
                        false,      // not modified
                        true,
                        out pageFault);

                    if (pageFault)
                    {
                        SignalEvent(EventType.PageFault, address);
                    }
                }

                if (!pageFault)
                {
                    SetRegisterPairValue(rp, _mem.ReadDoubleWord(address));
                }
            }
        }

        private void WriteByte(uint address, byte value)
        {
            bool pageFault = false;
            if (_mode != ProcessorMode.Kernel)
            {
                address = TranslateVirtualToReal(
                    _sr[9],
                    address,
                    true,      
                    true,
                    out pageFault);

                if (pageFault)
                {
                    SignalEvent(EventType.PageFault, address);
                }
            }

            if (!pageFault)
            {
                _mem.WriteByte(address, value);
            }
        }

        private void WriteHalfWord(uint address, ushort value)
        {            
            if ((address & 0x1) != 0)
            {
                Trap(TrapType.DataAlignment);
            }
            else
            {
                bool pageFault = false;
                if (_mode != ProcessorMode.Kernel)
                {
                    address = TranslateVirtualToReal(
                        _sr[9],
                        address,
                        true,
                        true,
                        out pageFault);

                    if (pageFault)
                    {
                        SignalEvent(EventType.PageFault, address);
                    }
                }

                if (!pageFault)
                {
                    _mem.WriteHalfWord(address, value);
                }                
            }
        }

        private void WriteWord(uint address, uint value)
        {            
            if ((address & 0x3) != 0)
            {
                Trap(TrapType.DataAlignment);
            }
            else
            {
                bool pageFault = false;
                if (_mode != ProcessorMode.Kernel)
                {
                    address = TranslateVirtualToReal(
                        _sr[9],
                        address,
                        true,
                        true,
                        out pageFault);

                    if (pageFault)
                    {
                        SignalEvent(EventType.PageFault, address);
                    }
                }

                if (!pageFault)
                {
                    _mem.WriteWord(address, value);
                }
            }
        }

        private void WriteDoubleWord(uint address, ulong value)
        {
            if ((address & 0x7) != 0)
            {
                Trap(TrapType.DataAlignment);
            }
            else
            {
                bool pageFault = false;
                if (_mode != ProcessorMode.Kernel)
                {
                    address = TranslateVirtualToReal(
                        _sr[9],
                        address,
                        true,
                        true,
                        out pageFault);

                    if (pageFault)
                    {
                        SignalEvent(EventType.PageFault, address);
                    }
                }

                if (!pageFault)
                {
                    _mem.WriteDoubleWord(address, value);
                }                
            }
        }

        //
        // Register pair functions
        //
        private ulong GetRegisterPairValue(int rp)
        {
            return ((ulong)_r[rp] << 32) | _r[(rp + 1) & 0xf];
        }

        private void SetRegisterPairValue(int rp, ulong value)
        {
            _r[rp] = (uint)(value >> 32);
            _r[(rp + 1) & 0xf] = (uint)value;
        }

        private void SignalEvent(EventType e, uint data)
        {
            //
            // Do event-specific things, like setting SR0-SR3.
            //
            switch (e)
            {
                case EventType.ExternalInterrupt:
                    _sr[0] = _intDevice.AckInterrupt();
                    _sr[15] = _pc;
                    break;

                case EventType.Switch0Interrupt:
                    _sr[0] = _mode == ProcessorMode.Kernel ? _pc : 1;
                    if (_mode == ProcessorMode.User)
                    {
                        _sr[15] = _pc;
                    }
                    break;

                case EventType.PageFault:
                    _sr[0] = 1;
                    _sr[1] = 0xffffffff;
                    _sr[2] = _sr[8];
                    _sr[3] = data;
                    _sr[15] = _pc;
                    break;

                default:
                    throw new NotImplementedException(
                        String.Format("Unimplemented signal {0}", e));
            }

            //
            // Grab the vector for the event from the CCB.
            // TODO: the documentation is *extremely vague*
            // but it appears that external interrupts are not vectored from the CCB
            // while in Kernel mode, only in User mode.  
            // It does appear that SR registers are still modified as appropriate.
            //
            // Ayway, we were going to grab the vector for the event
            // from the CCB, the offset of which is specified by
            // SR11.
            //            
            if (e != EventType.ExternalInterrupt ||
                _mode == ProcessorMode.User)
            {
                uint vectorAddress = _mem.ReadWord(_sr[11] + (uint)e);

                // Switch to Kernel mode and jump to the requisite vector.
                _mode = ProcessorMode.Kernel;
                _pc = vectorAddress;
            }
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
            // Yielding a 20 bite page # and 12-bit offsets in said page.
            //
            pageFault = false;

            // add SR8/SR9 to bits 0..19 of the virtual address
            uint vrtAddress = (virtualAddress >> 12) + segmentNumber;

            // AND with contents of VRMASK (SR13)
            vrtAddress &= _sr[13];

            // Shift left 3 bits, add to VRT table base address in SR12
            vrtAddress = (vrtAddress << 3) + _sr[12];

            // Grab the VRT entry from the computed address.
            UInt32 vrtEntry0 = _mem.ReadWord(vrtAddress);
            UInt32 vrtEntry1 = _mem.ReadWord(vrtAddress + 4);

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
                    if ((vrtEntry1 & 0xf800) == 0)
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

                        _mem.WriteWord(vrtAddress + 4, vrtEntry1);

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
                        vrtAddress = link + _sr[12];
                        vrtEntry0 = _mem.ReadWord(vrtAddress);
                        vrtEntry1 = _mem.ReadWord(vrtAddress + 4);
                    }
                }
            }            

            return translatedAddress;
        }

        private void Trap(TrapType t)
        {
            throw new NotImplementedException("Traps not yet implemented.");
        }

        //
        // Floating point conversion routines.
        // These all currently use host-native floating point
        // which will need to be corrected in order to properly deal
        // with the various exceptions the Ridge cpu supports...
        //
        private float GetFloatFromWord(uint w)
        {
            _float[0] = (byte)(w >> 24);
            _float[1] = (byte)(w >> 16);
            _float[2] = (byte)(w >> 8);
            _float[3] = (byte)w;

            return BitConverter.ToSingle(_float, 0);
        }

        private double GetDoubleFromWord(ulong w)
        {
            _float[0] = (byte)(w >> 56);
            _float[1] = (byte)(w >> 48);
            _float[2] = (byte)(w >> 40);
            _float[3] = (byte)(w >> 32);
            _float[4] = (byte)(w >> 24);
            _float[5] = (byte)(w >> 16);
            _float[6] = (byte)(w >> 8);
            _float[7] = (byte)w;

            return BitConverter.ToDouble(_float, 0);
        }

        private ProcessorMode _mode;

        private uint[] _r = new uint[16];
        private uint[] _sr = new uint[16];

        private uint _pc;

        private IPhysicalMemory _mem;
        private IOBus           _io;    
        
        //
        // The currently interrupting external device
        //
        private IIODevice       _intDevice;

        //
        // Whether an external interrupt has occurred since the last
        // ITEST MAINT instruction.
        //
        private bool            _externalInterrupt;

        //
        // Scratchpad for floating point conversion operations.
        //
        private byte[]          _float = new byte[8];

        //
        // VRT information
        //
        private enum SegmentType
        {            
            Code,           // Use the code segment (in SR8)
            Data            // Use the data segment (in SR9)
        }
    }
}
