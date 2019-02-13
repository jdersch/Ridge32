using Ridge.Memory;

namespace Ridge.CPU
{
    /// <summary>
    /// Opcode enumerations.  Opcode is in CAPS, any modifications
    /// follow after underscore in lowercase and may be combined.  Modifications are:
    /// - i : immediate
    /// - sr : special reg
    /// - r : reg
    /// - gt : greater than
    /// - lt : less than
    /// - eq : equal to
    /// - neq : not equal to
    /// - s : short displacement
    /// - l : long displacement
    /// - d : data segment
    /// - c : code segment
    /// - x : indexed
    /// </summary>
    public enum Opcode
    {     
        Invalid     = 0x00,

        // Register
        MOVE        = 0x01,
        NEG         = 0x02,
        ADD         = 0x03,
        SUB         = 0x04,
        MPY         = 0x05,
        DIV         = 0x06,
        REM         = 0x07,
        NOT         = 0x08,
        OR          = 0x09,
        XOR         = 0x0a,
        AND         = 0x0b,
        CBIT        = 0x0c,
        SBIT        = 0x0d,
        TBIT        = 0x0e,
        CHK         = 0x0f,

        NOP         = 0x10,
        MOVEI_i     = 0x11,
        ADDI_i      = 0x13,
        SUBI_i      = 0x14,
        MPYI_i      = 0x15,
        NOTI_i      = 0x18,
        ANDI_i      = 0x1b,
        CHKI_i      = 0x1f,

        FIXT        = 0x20,
        FIXR        = 0x21,
        RNEG        = 0x22,
        RADD        = 0x23,
        RSUB        = 0x24,
        RMPY        = 0x25,
        RDIV        = 0x26,
        MAKERD      = 0x27,
        LCOMP       = 0x28,
        FLOAT       = 0x29,
        RCOMP       = 0x2a,
        EADD        = 0x2c,
        ESUB        = 0x2d,
        EMPY        = 0x2e,
        EDIV        = 0x2f,

        DFIXT       = 0x30,
        DFIXR       = 0x31,
        DRNEG       = 0x32,
        DRADD       = 0x33,
        DRSUB       = 0x34,
        DRMPY       = 0x35,
        DRDIV       = 0x36,
        MAKEDR      = 0x37,
        DCOMP       = 0x38,
        DFLOAT      = 0x39,
        DRCOMP      = 0x3a,
        TRAP        = 0x3b,

        SUS         = 0x40,
        LUS         = 0x41,
        RUM         = 0x42,
        LDREGS      = 0x43,
        TRANS       = 0x44,
        DIRT        = 0x45,
        MOVE_sr     = 0x46,
        MOVE_rs     = 0x47,
        MAINT       = 0x4c,
        READ        = 0x4e,
        WRITE       = 0x4f,

        TEST_gt     = 0x50,
        TEST_lt     = 0x51,
        TEST_eq     = 0x52,
        CALLR       = 0x53,
        TEST_gti    = 0x54,
        TEST_lti    = 0x55,
        TEST_eqi    = 0x56,
        RET         = 0x57,
        TEST_lteq   = 0x58,
        TEST_gteq   = 0x59,
        TEST_neq    = 0x5a,
        KCALL       = 0x5b,
        TEST_lteqi  = 0x5c,
        TEST_gteqi  = 0x5d,
        TEST_neqi   = 0x5e,

        LSL         = 0x60,
        LSR         = 0x61,
        ASL         = 0x62,
        ASR         = 0x63,
        DLSL        = 0x64,
        DLSR        = 0x65,
        CSL         = 0x68,
        SEB         = 0x6a,

        LSLI_i      = 0x70,
        LSRI_i      = 0x71,
        ASLI_i      = 0x72,
        ASRI_i      = 0x73,
        DLSLI_i     = 0x74,
        DLSRI_i     = 0x75,
        CSLI_i      = 0x78,
        SEH         = 0x7a,

        // Memory Reference
        BR_gts      = 0x80,
        BR_eqs      = 0x82,
        CALL_s      = 0x83,
        BR_gtsi     = 0x84,
        BR_ltsi     = 0x85,
        BR_eqsi     = 0x86,
        LOOP_s      = 0x87,
        BR_lteqs    = 0x88,
        BR_neqs     = 0x8a,
        BR_s        = 0x8b,
        BR_lteqsi   = 0x8c,
        BR_gteqsi   = 0x8d,
        BR_neqsi    = 0x8e,

        BR_gtl      = 0x90,
        BR_eql      = 0x92,
        CALL_l      = 0x93,
        BR_gtli     = 0x94,
        BR_ltli     = 0x95,
        BR_eqli     = 0x96,
        LOOP_l      = 0x97,
        BR_lteql    = 0x98,
        BR_neql     = 0x9a,
        BR_l        = 0x9b,
        BR_lteqli   = 0x9c,
        BR_gteqli   = 0x9d,
        BR_neqli    = 0x9e,

        STOREB_s    = 0xa0,
        STOREB_sx   = 0xa1,
        STOREH_s    = 0xa2,
        STOREH_sx   = 0xa3,
        STORE_s     = 0xa6,
        STORE_sx    = 0xa7,
        STORED_s    = 0xa8,
        STORED_sx   = 0xa9,

        STOREB_l    = 0xb0,
        STOREB_lx   = 0xb1,
        STOREH_l    = 0xb2,
        STOREH_lx   = 0xb3,
        STORE_l     = 0xb6,
        STORE_lx    = 0xb7,
        STORED_l    = 0xb8,
        STORED_lx   = 0xb9,

        LOADB_ds    = 0xc0,
        LOADB_dsx   = 0xc1,
        LOADH_ds    = 0xc2,
        LOADH_dsx   = 0xc3,
        LOAD_ds     = 0xc6,
        LOAD_dsx    = 0xc7,
        LOADD_ds    = 0xc8,
        LOADD_dsx   = 0xc9,
        LADDR_ds    = 0xce,
        LADDR_dsx   = 0xcf,

        LOADB_dl    = 0xd0,
        LOADB_dlx   = 0xd1,
        LOADH_dl    = 0xd2,
        LOADH_dlx   = 0xd3,
        LOAD_dl     = 0xd6,
        LOAD_dlx    = 0xd7,
        LOADD_dl    = 0xd8,
        LOADD_dlx   = 0xd9,
        LADDR_dl    = 0xde,
        LADDR_dlx   = 0xdf,

        LOADB_cs    = 0xe0,
        LOADB_csx   = 0xe1,
        LOADH_cs    = 0xe2,
        LOADH_csx   = 0xe3,
        LOAD_cs     = 0xe6,
        LOAD_csx    = 0xe7,
        LOADD_cs    = 0xe8,
        LOADD_csx   = 0xe9,
        LADDR_cs    = 0xee,
        LADDR_csx   = 0xef,

        LOADB_cl    = 0xf0,
        LOADB_clx   = 0xf1,
        LOADH_cl    = 0xf2,
        LOADH_clx   = 0xf3,
        LOAD_cl     = 0xf6,
        LOAD_clx    = 0xf7,
        LOADD_cl    = 0xf8,
        LOADD_clx   = 0xf9,
        LADDR_cl    = 0xfe,
        LADDR_clx   = 0xff,    
    }

    public enum MaintOpcode
    {
        ELOGR       = 0,
        ELOGW       = 1,
        TWRITED     = 5,
        FLUSH       = 6,
        TRAPEXIT    = 7,
        ITEST       = 8,
        MACHINEID   = 10,
        VERSION     = 11,
        CREG        = 12,
        RDLOG       = 13
    }

    public class Instruction
    {
        /// <summary>
        /// Decodes the instruction in physical memory at the given address.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        public Instruction(IPhysicalMemory mem, uint address)
        {
            //
            // Read the first halfword of the instruction in, determine its type and see if we need go further.
            //
            ushort hword = mem.ReadHalfWord(address);

            Op = (Opcode)(hword >> 8);

            Rx = (hword & 0xf0) >> 4;
            Ry = hword & 0xf;

            if ((hword & 0x8000) != 0)
            {
                //
                // This is a memory reference instruction.
                // Is this a short or long displacement instruction?
                //
                if ((hword & 0x1000) != 0)
                {
                    Displacement = (int)mem.ReadWord(address + 2);
                    Length = 6;
                }
                else
                {
                    Displacement = (short)mem.ReadHalfWord(address + 2);
                    Length = 4;
                }

                // Compute the displacement address (PC + Displacement) for branches.
                // The LSB is used for branch prediction logic
                BranchAddress = (uint)((address + Displacement) & 0xfffffffe);
            }
            else
            {
                Length = 2;
            }
        }

        /// <summary>
        /// Decodes the instruction in virtual memory at the given address.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        public Instruction(IVirtualMemory vMem, uint address, out bool pageFault)
        {
            //
            // Read the first halfword of the instruction in, determine its type and see if we need go further.
            //
            ushort hword = vMem.ReadHalfWordV(address, SegmentType.Code, out pageFault);

            Op = (Opcode)(hword >> 8);

            Rx = (hword & 0xf0) >> 4;
            Ry = hword & 0xf;

            if ((hword & 0x8000) != 0)
            {
                //
                // This is a memory reference instruction.
                // Is this a short or long displacement instruction?
                //
                if ((hword & 0x1000) != 0)
                {
                    Displacement = (int)vMem.ReadWordV(address + 2, SegmentType.Code, out pageFault);
                    Length = 6;
                }
                else
                {
                    Displacement = (short)vMem.ReadHalfWordV(address + 2, SegmentType.Code, out pageFault);
                    Length = 4;
                }

                // Compute the displacement address (PC + Displacement) for branches.
                // The LSB is used for branch prediction logic
                BranchAddress = (uint)((address + Displacement) & 0xfffffffe);
            }
            else
            {
                Length = 2;
            }
        }

        /// <summary>
        /// Manually creates an instruction given address, opcode, etc.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="opcode"></param>
        /// <param name="displacement"></param>
        /// <param name="rx"></param>
        /// <param name="ry"></param>
        /// <param name="length"></param>
        public Instruction(uint address, Opcode opcode, int displacement, int rx, int ry, uint length)
        {
            Op = opcode;
            Displacement = displacement;
            BranchAddress = (ushort)(address + displacement);
            Rx = rx;
            Ry = ry;
            Length = length;
        }        

        /// <summary>
        /// The actual operation
        /// </summary>
        public Opcode Op;

        /// <summary>
        /// The signed displacement 
        /// </summary>
        public int Displacement;

        /// <summary>
        /// The precomputed address (PC + Displacement)
        /// </summary>
        public uint BranchAddress;

        /// <summary>
        /// The registers involved 
        /// </summary>
        public int Rx;
        public int Ry;

        /// <summary>
        /// The length of the instruction, in bytes. 
        /// </summary>
        public uint Length;        
    }
}
