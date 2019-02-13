using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.Debugger
{
    public enum ExecutionState
    {
        Debug,
        Step,
        Go,
        Breakpoint,
    }

    public class DebugPrompt
    {
        public DebugPrompt(RidgeSystem system)
        {
            _system = system;
        }

        public ExecutionState Prompt()
        {
            ExecutionState next = ExecutionState.Step;
            PrintStatus();

            bool done = false;
            while (!done)
            {
                Console.Write(":]> ");
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    // Default is to step the processor.
                    next = ExecutionState.Step;
                    break;
                }

                string[] tokens = input.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                switch(tokens[0].ToLowerInvariant())
                {
                    case "s":
                        next = ExecutionState.Step;
                        done = true;
                        break;

                    case "g":
                        if (tokens.Length > 1)
                        {
                            _system.CPU.PC = uint.Parse(tokens[1], System.Globalization.NumberStyles.HexNumber);
                        }
                        next = ExecutionState.Go;
                        done = true;
                        break;

                    case "dv":
                    case "dp":
                        if (tokens.Length < 2)
                        {
                            Console.WriteLine("dp (or dv) <addr> [length]");
                        }
                        else
                        {
                            try
                            {
                                uint start = uint.Parse(tokens[1], System.Globalization.NumberStyles.HexNumber);
                                uint length = tokens.Length > 2 ? uint.Parse(tokens[2], System.Globalization.NumberStyles.HexNumber) : 1;

                                for (uint i = start; i < start + length; )
                                {
                                    if (tokens[0].ToLowerInvariant() == "dv")
                                    {
                                        i += DisassembleVirtual(i);
                                    }
                                    else
                                    {
                                        i += DisassemblePhysical(i);
                                    }
                                }
                            }
                            catch(Exception e)
                            {
                                Console.WriteLine("invalid args.");
                            }
                        }

                        next = ExecutionState.Debug;
                        break;

                    case "trans":
                        if (tokens.Length < 2)
                        {
                            Console.WriteLine("trans <addr> [code|data]");
                        }
                        else
                        {
                            uint vaddr = uint.Parse(tokens[1], System.Globalization.NumberStyles.HexNumber);
                            bool codeSegment = true;
                            if (tokens.Length > 2)
                            {
                                switch(tokens[2].ToLowerInvariant())
                                {
                                    case "code":
                                        codeSegment = true;
                                        break;

                                    case "data":
                                        codeSegment = false;
                                        break;

                                    default:
                                        Console.WriteLine("invalid segment arg, assuming code segment.");
                                        break;
                                }
                            }

                            bool pageFault = false;
                            uint translatedAddress = _system.Memory.TranslateVirtualToReal(
                                codeSegment ? _system.CPU.SR[8] : _system.CPU.SR[9],
                                vaddr,
                                false,
                                false,
                                out pageFault);

                            if (pageFault)
                            {
                                Console.WriteLine("Page Fault.");
                            }
                            else
                            {
                                Console.WriteLine("PA 0x{0:x}", translatedAddress);
                            }
                        }
                        break;

                    case "m":
                        if (tokens.Length < 2)
                        {
                            Console.WriteLine("m <addr> [length]");
                        }
                        else
                        {
                            try
                            {
                                uint start = uint.Parse(tokens[1], System.Globalization.NumberStyles.HexNumber);
                                uint length = tokens.Length > 2 ? uint.Parse(tokens[2], System.Globalization.NumberStyles.HexNumber) : 1;

                                for (uint i = start; i < start + length; i+=4)
                                {
                                    Console.WriteLine("{0:x8}: {1:x8}", i, _system.Memory.ReadWord(i));
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("invalid args.");
                            }
                        }

                        next = ExecutionState.Debug;
                        break;
                }


            }

            return next;
        }

        private void PrintStatus()
        {            
            Console.WriteLine("R0-R16:");
            for (int i = 0; i < 16; i++)
            {
                Console.Write("0x{0:x8} ", _system.CPU.R[i]);
                if (i == 7)
                {
                    Console.WriteLine();
                }
            }            

            Console.WriteLine("\nSR0-SR16:");
            for (int i = 0; i < 16; i++)
            {
                Console.Write("0x{0:x8} ", _system.CPU.SR[i]);
                if (i == 7)
                {
                    Console.WriteLine();
                }
            }

            Console.WriteLine("\nPC=0x{0:x8} Mode={1}", _system.CPU.PC, _system.CPU.Mode);
            
            DisassembleVirtual(_system.CPU.PC);
        }

        private uint DisassembleVirtual(uint addr)
        {
            // TODO: disassembling virtual addresses here ends up modifying the Referenced bits!
            bool pageFault = false;
            CPU.Instruction inst = new CPU.Instruction(_system.Memory, addr, out pageFault);
           
            if (pageFault)
            {
                Console.WriteLine("<page fault>");
            }
            else if (inst.Length == 2)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x4}              {2}", 
                    addr, 
                    _system.Memory.ReadHalfWordV(addr, Memory.SegmentType.Code, out pageFault), 
                    Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else if (inst.Length == 4)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x8}          {2}", 
                    addr, 
                    _system.Memory.ReadWordV(addr, Memory.SegmentType.Code, out pageFault), 
                    Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else if (inst.Length == 6)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x8},0x{2:x4}   {3}", 
                    addr, 
                    _system.Memory.ReadWordV(addr, Memory.SegmentType.Code, out pageFault), 
                    _system.Memory.ReadHalfWordV(addr + 4, Memory.SegmentType.Code, out pageFault), 
                    Ridge.CPU.Disassembler.Disassemble(inst));
            }

            return inst.Length;
        }

        private uint DisassemblePhysical(uint addr)
        {
            CPU.Instruction inst = new CPU.Instruction(_system.Memory, addr);

            if (inst.Length == 2)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x4}              {2}",
                    addr,
                    _system.Memory.ReadHalfWord(addr),
                    Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else if (inst.Length == 4)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x8}          {2}",
                    addr,
                    _system.Memory.ReadWord(addr),
                    Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else if (inst.Length == 6)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x8},0x{2:x4}   {3}",
                    addr,
                    _system.Memory.ReadWord(addr),
                    _system.Memory.ReadHalfWord(addr + 4),
                    Ridge.CPU.Disassembler.Disassemble(inst));
            }

            return inst.Length;
        }


        private RidgeSystem _system;
    }
}
