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
            // Decode the current instruction.
            // The eventual intent is to cache these Instruction objects
            // to save execution time; for the time being we cons up a new one each
            // time around which is slow and causes GCs, but is simple while we get
            // this thing off the ground.
            //
            
            // Save PC pre-increment.
            uint opc = _pc;
            Instruction i = new Instruction(_mem, _pc);
            _pc += i.Length;

            switch(i.Op)
            {
                case Opcode.MOVE:
                    _r[i.Rx] = _r[i.Ry];
                    break;

                case Opcode.NEG:
                    _r[i.Rx] = (uint)(-_r[i.Ry]);
                    break;

                case Opcode.ADD:
                    // TODO: traps on overflow
                    _r[i.Rx] += _r[i.Ry];
                    break;

                case Opcode.SUB:
                    // TODO: traps on overflow
                    _r[i.Rx] -= _r[i.Ry];
                    break;

                case Opcode.MPY:
                    // TODO: traps on overflow
                    _r[i.Rx] *=  _r[i.Ry];
                    break;

                case Opcode.DIV:
                    // TODO: traps on div by zero
                    _r[i.Rx] /= _r[i.Ry];
                    break;

                case Opcode.REM:
                    // TODO: traps on div by zero
                    _r[i.Rx] = _r[i.Rx] % _r[i.Ry];
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
                        rp &= ~((ulong)0x8000000000000000 >> (i.Ry & 0x3f));
                        SetRegisterPairValue(i.Rx, rp);                        
                    }
                    break;

                case Opcode.SBIT:
                    {
                        // Sets the specified bit in the 64-bit register pair specified by Rx.
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp |= ((ulong)0x8000000000000000 >> (i.Ry & 0x3f));
                        SetRegisterPairValue(i.Rx, rp);                        
                    }
                    break;

                case Opcode.TBIT:
                    {
                        // Sets bit 31 of Rx to the value of the selected bit; clears all other bits in Rx.
                        ulong rp = GetRegisterPairValue(i.Rx);
                        _r[i.Rx] = (uint)((rp & ((ulong)0x8000000000000000 >> (i.Ry & 0x3f))) == 0 ? 0 : 1);
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

                case Opcode.MOVE_i:
                    _r[i.Rx] = (uint)i.Ry;
                    break;

                case Opcode.ADD_i:
                    _r[i.Rx] += (uint)i.Ry;
                    break;

                case Opcode.SUB_i:
                    _r[i.Rx] -= (uint)i.Ry;
                    break;

                case Opcode.MPY_i:
                    _r[i.Rx] *= (uint)i.Ry;
                    break;

                case Opcode.NOT_i:
                    // TODO: does this get masked to 4 bits?
                    _r[i.Rx] = (uint)(~i.Ry & 0xf);
                    break;

                case Opcode.AND_i:
                    _r[i.Rx] &= (uint)i.Ry;
                    break;

                case Opcode.CHK_i:
                    // Trap if NOT (0 <= (Rx) <= Ry)
                    if (!(0 <= (int)_r[i.Rx] && (int)_r[i.Rx] <= i.Ry))
                    {
                        Trap(TrapType.Check);
                    }
                    break;

                case Opcode.FIXT:
                    float fVal = GetFloatFromWord(_r[i.Ry]);
                    _r[i.Rx] = (uint)((int)fVal);
                    break;

                case Opcode.FIXR:
                case Opcode.RNEG:
                case Opcode.RADD:
                case Opcode.RSUB:
                case Opcode.RMPY:
                case Opcode.RDIV:
                case Opcode.MAKERD:
                case Opcode.LCOMP:
                case Opcode.FLOAT:
                case Opcode.RCOMP:
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
                case Opcode.DCOMP:
                case Opcode.DFLOAT:
                case Opcode.DRCOMP:
                    throw new NotImplementedException();
                    break;

                case Opcode.TRAP:
                    _sr[3] = (uint)i.Ry;
                    Trap(TrapType.TrapInstruction);
                    break;

                case Opcode.SUS:
                case Opcode.LUS:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                    }
                    throw new NotImplementedException();
                    break;

                case Opcode.RUM:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                    }

                    _pc = _sr[15];
                    _mode = ProcessorMode.User;
                    break;

                case Opcode.LDREGS:
                case Opcode.TRANS:
                case Opcode.DIRT:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                    }
                    throw new NotImplementedException();
                    break;

                case Opcode.MOVE_sr:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                    }

                    _sr[i.Rx] = _r[i.Ry];
                    break;

                case Opcode.MOVE_r:
                    if (_mode != ProcessorMode.Kernel)
                    {
                        Trap(TrapType.KernelViolation);
                    }

                    _r[i.Rx] = _sr[i.Ry];
                    break;

                case Opcode.MAINT:                    
                    if (!(_mode == ProcessorMode.Kernel ||  // Not kernel mode
                         ((_sr[10] & 0x1) == 1)))           // Not privileged user mode
                    {
                        Trap(TrapType.KernelViolation);
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
                            _r[i.Rx] = 0;
                            break;

                        case MaintOpcode.TRAPEXIT:
                            // "The TRAPEXIT instruction sets PC to the value contained in SR0 and begins
                            //  executing at that address...
                            //  The TRAPEXIT instruction flushes the cache and the TMT."
                            _pc = _sr[0];
                            break;

                        case MaintOpcode.ITEST:
                            if (_interrupt)
                            {
                                _interrupt = false;
                                _r[(i.Rx + 1) & 0xf] = _ioir;
                                _r[i.Rx] = 1;
                            }
                            else
                            {
                                _r[i.Rx] = 0;
                            }
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

                case Opcode.LSL_i:
                    _r[i.Rx] = _r[i.Rx] << i.Ry;
                    break;

                case Opcode.LSR_i:
                    _r[i.Rx] = _r[i.Rx] >> i.Ry;
                    break;

                case Opcode.ASL_i:
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

                case Opcode.ASR_i:
                    // TODO: again, inefficient.
                    {
                        uint signBit = _r[i.Rx] & 0x80000000;
                        for (int s = 0; s < i.Ry; s++)
                        {
                            _r[i.Rx] = (_r[i.Rx] >> 1) | signBit;
                        }
                    }
                    break;

                case Opcode.DLSL_i:
                    {
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp = rp << i.Ry;
                        SetRegisterPairValue(i.Rx, rp);
                    }
                    break;

                case Opcode.DLSR_i:
                    {
                        ulong rp = GetRegisterPairValue(i.Rx);
                        rp = rp >> i.Ry;
                        SetRegisterPairValue(i.Rx, rp);
                    }
                    break;

                case Opcode.CSL_i:
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

                //
                // TODO for all LOAD/STORE instructions --
                // Data Alignment traps for LOAD/STORE operations not on
                // the correct data-size boundary.
                //
                case Opcode.STOREB_s:
                case Opcode.STOREB_l:
                    _mem.WriteByte((uint)i.Displacement, (byte)_r[i.Rx]);
                    break;

                case Opcode.STOREB_sx:
                case Opcode.STOREB_lx:
                    _mem.WriteByte((uint)(i.Displacement + (int)_r[i.Ry]), (byte)_r[i.Rx]);
                    break;

                case Opcode.STOREH_s:
                case Opcode.STOREH_l:
                    _mem.WriteHalfWord((uint)i.Displacement, (ushort)_r[i.Rx]);
                    break;

                case Opcode.STOREH_sx:
                case Opcode.STOREH_lx:
                    _mem.WriteHalfWord((uint)(i.Displacement + (int)_r[i.Ry]), (ushort)_r[i.Rx]);
                    break;

                case Opcode.STORE_s:
                case Opcode.STORE_l:
                    _mem.WriteWord((uint)i.Displacement, _r[i.Rx]);
                    break;

                case Opcode.STORE_sx:
                case Opcode.STORE_lx:
                    _mem.WriteWord((uint)(i.Displacement + (int)_r[i.Ry]), _r[i.Rx]);
                    break;

                case Opcode.STORED_s:
                case Opcode.STORED_l:
                    _mem.WriteDoubleWord((uint)i.Displacement, GetRegisterPairValue(i.Rx));
                    break;

                case Opcode.STORED_sx:
                case Opcode.STORED_lx:
                    _mem.WriteDoubleWord((uint)(i.Displacement + (int)_r[i.Ry]), GetRegisterPairValue(i.Rx));
                    break;

                case Opcode.LOADB_ds:
                case Opcode.LOADB_dl:
                    _r[i.Rx] = _mem.ReadByte((uint)i.Displacement);
                    break;

                case Opcode.LOADB_dsx:
                case Opcode.LOADB_dlx:
                    _r[i.Rx] = _mem.ReadByte((uint)(i.Displacement + (int)_r[i.Ry]));
                    break;

                case Opcode.LOADH_ds:
                case Opcode.LOADH_dl:
                    _r[i.Rx] = _mem.ReadHalfWord((uint)i.Displacement);
                    break;

                case Opcode.LOADH_dsx:
                case Opcode.LOADH_dlx:
                    _r[i.Rx] = _mem.ReadHalfWord((uint)(i.Displacement + (int)_r[i.Ry]));
                    break;

                case Opcode.LOAD_ds:
                case Opcode.LOAD_dl:
                    _r[i.Rx] = _mem.ReadWord((uint)i.Displacement);
                    break;

                case Opcode.LOAD_dsx:
                case Opcode.LOAD_dlx:
                    _r[i.Rx] = _mem.ReadWord((uint)(i.Displacement + (int)_r[i.Ry]));
                    break;

                case Opcode.LOADD_ds:
                case Opcode.LOADD_dl:
                    SetRegisterPairValue(i.Rx, _mem.ReadDoubleWord((uint)i.Displacement));
                    break;

                case Opcode.LOADD_dsx:
                case Opcode.LOADD_dlx:
                    SetRegisterPairValue(i.Rx, _mem.ReadDoubleWord((uint)(i.Displacement + (int)_r[i.Ry])));
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
                    _r[i.Rx] = _mem.ReadByte((uint)(opc + i.Displacement));
                    break;

                case Opcode.LOADB_csx:
                case Opcode.LOADB_clx:
                    _r[i.Rx] = _mem.ReadByte((uint)(opc + (int)_r[i.Ry] + i.Displacement));
                    break;

                case Opcode.LOADH_cs:
                case Opcode.LOADH_cl:
                    _r[i.Rx] = _mem.ReadHalfWord((uint)(opc + i.Displacement));
                    break;

                case Opcode.LOADH_csx:
                case Opcode.LOADH_clx:
                    _r[i.Rx] = _mem.ReadHalfWord((uint)(opc + (int)_r[i.Ry] + i.Displacement));
                    break;

                case Opcode.LOAD_cs:
                case Opcode.LOAD_cl:
                    _r[i.Rx] = _mem.ReadWord((uint)(opc + i.Displacement));
                    break;

                case Opcode.LOAD_csx:
                case Opcode.LOAD_clx:
                    _r[i.Rx] = _mem.ReadWord((uint)(opc + (int)_r[i.Ry] + i.Displacement));
                    break;

                case Opcode.LOADD_cs:
                case Opcode.LOADD_cl:
                    SetRegisterPairValue(i.Rx, _mem.ReadDoubleWord((uint)(opc + i.Displacement)));
                    break;

                case Opcode.LOADD_csx:
                case Opcode.LOADD_clx:
                    SetRegisterPairValue(i.Rx, _mem.ReadDoubleWord((uint)(opc + (int)_r[i.Ry] + i.Displacement)));
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
                    break;
            }
        }

        /// <summary>
        /// This is currently completely wrong.
        /// </summary>
        public void Interrupt(uint ioir)
        {
            _interrupt = true;
            _ioir = ioir;
        }

        private ulong GetRegisterPairValue(int rp)
        {
            return ((ulong)_r[rp] << 32) | _r[(rp + 1) & 0xf];
        }

        private void SetRegisterPairValue(int rp, ulong value)
        {
            _r[rp] = (uint)(value >> 32);
            _r[(rp + 1) & 0xf] = (uint)value;
        }

        private void Trap(TrapType t)
        {
            throw new NotImplementedException("Traps not yet implemented.");
        }

        private float GetFloatFromWord(uint w)
        {
            throw new NotImplementedException("GetFloatFromWord not yet implemented.");
        }

        private ProcessorMode _mode;

        private uint[] _r = new uint[16];
        private uint[] _sr = new uint[16];

        private uint _pc;

        private IPhysicalMemory _mem;
        private IOBus           _io;

        // Interrupt stuff
        private uint _ioir;
        private bool _interrupt;
    }
}
