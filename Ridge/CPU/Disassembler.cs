using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.CPU
{
    public static class Disassembler
    {
        static Disassembler()
        {
        
        }

        public static string Disassemble(Instruction i)
        {
            string disassembly = String.Empty;

            // Grab the enumeration value and strip everything
            // including and after the underscore (if present)
            string mnemonic = i.Op.ToString();
            int underscoreIndex = mnemonic.IndexOf('_');

            if (underscoreIndex >= 0)
            {
                mnemonic = mnemonic.Substring(0, underscoreIndex);
            }

            switch(i.Op)
            {
                // Ops with no arguments
                case Opcode.NOP:
                case Opcode.RUM:
                
                    disassembly = mnemonic;
                    break;

                // Two register format
                case Opcode.MOVE:
                case Opcode.NEG:
                case Opcode.ADD:
                case Opcode.SUB:
                case Opcode.MPY:
                case Opcode.DIV:
                case Opcode.REM:
                case Opcode.NOT:
                case Opcode.OR:
                case Opcode.XOR:
                case Opcode.AND:
                case Opcode.CBIT:
                case Opcode.TBIT:
                case Opcode.CHK:
                case Opcode.FIXT:
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
                case Opcode.SUS:
                case Opcode.LUS:
                case Opcode.LDREGS:
                case Opcode.TRANS:
                case Opcode.DIRT:
                case Opcode.READ:
                case Opcode.WRITE:
                case Opcode.CALLR:
                case Opcode.RET:
                case Opcode.LSL:
                case Opcode.LSR:                
                case Opcode.ASL:
                case Opcode.ASR:
                case Opcode.DLSL:
                case Opcode.DLSR:
                case Opcode.CSL:
                    disassembly = String.Format("{0} r{1},r{2}", mnemonic, i.Rx, i.Ry);
                    break;

                case Opcode.MOVEI_i:
                case Opcode.ADDI_i:
                case Opcode.SUBI_i:
                case Opcode.MPYI_i:
                case Opcode.ANDI_i:
                case Opcode.CHKI_i:
                case Opcode.LSLI_i:
                case Opcode.DLSLI_i:
                case Opcode.LSRI_i:
                case Opcode.DLSRI_i:
                case Opcode.ASLI_i:
                case Opcode.ASRI_i:
                case Opcode.CSLI_i:
                    disassembly = String.Format("{0} r{1},0x{2:x1}", mnemonic, i.Rx, i.Ry);
                    break;

                case Opcode.TEST_eq:
                case Opcode.TEST_gt:
                case Opcode.TEST_gteq:
                case Opcode.TEST_lt:
                case Opcode.TEST_lteq:
                case Opcode.TEST_neq:
                    disassembly = String.Format("TEST r{0}{1}r{2}", i.Rx, GetLogicalOp(i.Op), i.Ry);
                    break;

                case Opcode.TEST_eqi:
                case Opcode.TEST_gti:
                case Opcode.TEST_gteqi:
                case Opcode.TEST_lti:
                case Opcode.TEST_lteqi:
                case Opcode.TEST_neqi:
                    disassembly = String.Format("TEST r{0}{1}0x{2:x1}", i.Rx, GetLogicalOp(i.Op), i.Ry);
                    break;

                case Opcode.BR_eql:
                case Opcode.BR_gtl:
                case Opcode.BR_lteql:
                case Opcode.BR_neql:
                case Opcode.BR_eqs:
                case Opcode.BR_gts:
                case Opcode.BR_lteqs:
                case Opcode.BR_neqs:
                    disassembly = String.Format("BR r{0}{1}r{2},0x{3:x}{4} [0x{5:x}]", i.Rx, GetLogicalOp(i.Op), i.Ry, i.Displacement, GetLength(i), i.BranchAddress);
                    break;                

                case Opcode.BR_eqli:                
                case Opcode.BR_gtli:                
                case Opcode.BR_lteqli:                
                case Opcode.BR_neqli:
                case Opcode.BR_eqsi:
                case Opcode.BR_gtsi:
                case Opcode.BR_lteqsi:
                case Opcode.BR_neqsi:
                    disassembly = String.Format("BR r{0}{1}0x{2:x1},0x{3:x}{4} [0x{5:x}]", i.Rx, GetLogicalOp(i.Op), i.Ry, i.Displacement, GetLength(i), i.BranchAddress);
                    break;                    

                case Opcode.STOREB_l:
                case Opcode.STOREH_l:
                case Opcode.STORE_l:
                case Opcode.STORED_l:
                case Opcode.STOREB_s:
                case Opcode.STOREH_s:
                case Opcode.STORE_s:
                case Opcode.STORED_s:
                    disassembly = String.Format("{0} r{1},0x{2:x}{3}", mnemonic, i.Rx, i.Displacement, GetLength(i));
                    break;

                case Opcode.STOREB_lx:
                case Opcode.STOREH_lx:
                case Opcode.STORE_lx:
                case Opcode.STORED_lx:
                case Opcode.STOREB_sx:
                case Opcode.STOREH_sx:
                case Opcode.STORE_sx:
                case Opcode.STORED_sx:
                    disassembly = String.Format("{0} r{1},r{2},0x{3:x}{4}", mnemonic, i.Rx, i.Ry, i.Displacement, GetLength(i));
                    break;

                case Opcode.LADDR_cl:
                case Opcode.LADDR_cs:
                    disassembly = String.Format("LADDR r{0},PC+0x{1:x}{2}", i.Rx, i.Displacement, GetLength(i));
                    break;                    

                case Opcode.LADDR_dl:
                case Opcode.LADDR_ds:
                    disassembly = String.Format("LADDR r{0},0x{1:x}{2}", i.Rx, i.Displacement, GetLength(i));
                    break;

                case Opcode.LADDR_clx:
                case Opcode.LADDR_csx:
                    disassembly = String.Format("LADDR r{0},PC+r{1}+0x{2:x}{3}", i.Rx, i.Ry, i.Displacement, GetLength(i));
                    break;

                case Opcode.LADDR_dlx:
                case Opcode.LADDR_dsx:
                    disassembly = String.Format("LADDR r{0},r{1}+0x{2:x}{3}", i.Rx, i.Ry, i.Displacement, GetLength(i));
                    break;

                case Opcode.LOADB_cl:
                case Opcode.LOADB_cs:
                case Opcode.LOADD_cl:
                case Opcode.LOADD_cs:
                case Opcode.LOADH_cl:
                case Opcode.LOADH_cs:
                case Opcode.LOAD_cl:
                case Opcode.LOAD_cs:
                    disassembly = String.Format("{0}P r{1},PC+0x{2:x}{3}", mnemonic, i.Rx, i.Displacement, GetLength(i));
                    break;

                case Opcode.LOADB_clx:
                case Opcode.LOADB_csx:
                case Opcode.LOADD_clx:
                case Opcode.LOADD_csx:
                case Opcode.LOADH_clx:
                case Opcode.LOADH_csx:
                case Opcode.LOAD_clx:
                case Opcode.LOAD_csx:
                    disassembly = String.Format("{0}P r{1},PC+r{2}+0x{3:x}{4}", mnemonic, i.Rx, i.Ry, i.Displacement, GetLength(i));
                    break;

                case Opcode.LOADB_dl:
                case Opcode.LOADB_ds:
                case Opcode.LOADD_dl:
                case Opcode.LOADD_ds:
                case Opcode.LOADH_dl:
                case Opcode.LOADH_ds:
                case Opcode.LOAD_dl:
                case Opcode.LOAD_ds:
                    disassembly = String.Format("{0} r{1},0x{2:x}{3}", mnemonic, i.Rx, i.Displacement, GetLength(i));
                    break;

                case Opcode.LOADB_dlx:
                case Opcode.LOADB_dsx:
                case Opcode.LOADD_dlx:
                case Opcode.LOADD_dsx:
                case Opcode.LOADH_dlx:
                case Opcode.LOADH_dsx:
                case Opcode.LOAD_dlx:
                case Opcode.LOAD_dsx:
                    disassembly = String.Format("{0} r{1},r{2}+0x{3:x}{4}", mnemonic, i.Rx, i.Ry, i.Displacement, GetLength(i));
                    break;

                case Opcode.LOOP_l:
                case Opcode.LOOP_s:
                    disassembly = String.Format("LOOP r{0},{1},0x{2:x}{3} [0x{4:x}]", i.Rx, i.Ry, i.Displacement, GetLength(i), i.BranchAddress);
                    break;

                case Opcode.CALL_l:
                case Opcode.CALL_s:
                    disassembly = String.Format("CALL r{0},0x{1:x}{2} [0x{3:x}]", i.Rx, i.Displacement, GetLength(i), i.BranchAddress);
                    break;

                case Opcode.BR_s:
                case Opcode.BR_l:
                    disassembly = String.Format("BR 0x{0:x}{1} [0x{2:x}]", i.Displacement, GetLength(i), i.BranchAddress);
                    break;

                case Opcode.MOVE_r:
                    disassembly = String.Format("MOVE sr{0},r{1}", i.Rx, i.Ry);
                    break;

                case Opcode.MOVE_sr:
                    disassembly = String.Format("MOVE r{0},sr{1}", i.Rx, i.Ry);
                    break;

                case Opcode.KCALL:
                    disassembly = String.Format("KCALL 0x{0:x2}", (i.Rx << 4) | i.Ry);
                    break;

                case Opcode.TRAP:
                    disassembly = String.Format("TRAP 0x{0:x1}", i.Ry);
                    break;

                case Opcode.MAINT:
                    disassembly = String.Format("MAINT r{0},{1}", i.Rx, (MaintOpcode)i.Ry);
                    break;

                default:
                    disassembly = String.Format("Illegal Opcode 0x{0:x8} (1)", (int)i.Op, i.Op);
                    break;
            }

            return disassembly;
        }

        private static string GetLength(Instruction i)
        {
            return i.Length == 6 ? ",L" : String.Empty;
        }

        private static string GetLogicalOp(Opcode op)
        {
            switch (op)
            {
                case Opcode.TEST_gt:
                case Opcode.TEST_gti:                
                case Opcode.BR_gtl:
                case Opcode.BR_gtli:
                case Opcode.BR_gts:
                case Opcode.BR_gtsi:
                    return ">";

                case Opcode.TEST_lt:
                case Opcode.TEST_lti:                                
                case Opcode.BR_ltli:                
                case Opcode.BR_ltsi:
                    return "<";

                case Opcode.TEST_eq:
                case Opcode.TEST_eqi:
                case Opcode.BR_eql:
                case Opcode.BR_eqli:
                case Opcode.BR_eqs:
                case Opcode.BR_eqsi:
                    return "=";

                case Opcode.TEST_neq:
                case Opcode.TEST_neqi:
                case Opcode.BR_neql:
                case Opcode.BR_neqli:
                case Opcode.BR_neqs:
                case Opcode.BR_neqsi:
                    return "<>";

                case Opcode.TEST_gteq:
                case Opcode.TEST_gteqi:
                case Opcode.BR_gteqli:
                case Opcode.BR_gteqsi:
                    return ">=";

                case Opcode.TEST_lteq:
                case Opcode.TEST_lteqi:
                case Opcode.BR_lteql:
                case Opcode.BR_lteqli:
                case Opcode.BR_lteqs:
                case Opcode.BR_lteqsi:
                    return "<=";

                default:
                    return "??";

            }        
        }
    }
}
