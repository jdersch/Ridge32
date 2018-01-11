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
                        next = ExecutionState.Go;
                        done = true;
                        break;

                    case "d":

                        if (tokens.Length < 2)
                        {
                            Console.WriteLine("d <addr> [length]");
                        }
                        else
                        {
                            try
                            {
                                uint start = uint.Parse(tokens[1], System.Globalization.NumberStyles.HexNumber);
                                uint length = tokens.Length > 2 ? uint.Parse(tokens[2], System.Globalization.NumberStyles.HexNumber) : 1;

                                for (uint i = start; i < start + length; )
                                {
                                  //  i += Disassemble(i);
                                }
                            }
                            catch(Exception e)
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
            if (_system.CPU.Mode == CPU.ProcessorMode.Kernel)
            {                
                Disassemble(_system.CPU.PC, _system.CPU.PC);
            }
            else
            {
                bool pageFault = false;
                uint realPC = _system.Memory.TranslateVirtualToReal(_system.CPU.SR[8], _system.CPU.PC, false, false, out pageFault);

                if (pageFault)
                {                    
                    Console.WriteLine("<pagefault>");
                }
                else
                {                 
                    Disassemble(realPC, _system.CPU.PC);
                }
            }

        }

        private uint Disassemble(uint addr, uint vaddr)
        {
            CPU.Instruction inst = new CPU.Instruction(_system.Memory, addr);
           
            if (inst.Length == 2)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x4}              {2}", addr, _system.Memory.ReadHalfWord(addr), Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else if (inst.Length == 4)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x8}          {2}", addr, _system.Memory.ReadWord(addr), Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else if (inst.Length == 6)
            {
                Console.WriteLine("0x{0:x8}: 0x{1:x8},0x{2:x4}   {3}", addr, _system.Memory.ReadWord(addr), _system.Memory.ReadHalfWord(addr + 4), Ridge.CPU.Disassembler.Disassemble(inst));
            }

            return inst.Length;
        }


        private RidgeSystem _system;
    }
}
