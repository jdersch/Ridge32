using System;
using System.IO;

namespace Ridge
{
    class Program
    {
        static void Main(string[] args)
        {
            System system = new System();
            system.Reset();

            Console.WriteLine("Yet-To-Be-Named Ridge emulator v0.0.\n");

            // hack in boot data
            byte[] buffer = new byte[8192];
            using (FileStream fs = new FileStream("Boot\\bootblock.raw", FileMode.Open, FileAccess.Read))
            {
                fs.Read(buffer, 0, 8192);
            }

            for (uint i = 0; i < 8192; i++)
            {
                system.Memory.WriteByte(0x3e000 + i, buffer[i]);
            }

            while (true)
            {
                //PrintStatus(system);
                system.Clock();

                //Console.ReadKey();
                //Console.WriteLine();
            }
        }

        private static void PrintStatus(System s)
        {
            Console.WriteLine("PC=0x{0:x8} Mode={1}", s.CPU.PC, s.CPU.Mode);
            for (int i = 0; i < 16; i++)
            {
                Console.Write("R{0}=0x{1:x8} ", i, s.CPU.R[i]);
                if (i == 7)
                {
                    Console.WriteLine();
                }
            }

            Console.WriteLine();

            for (int i = 0; i < 16; i++)
            {
                Console.Write("S{0}=0x{1:x8} ", i, s.CPU.SR[i]);
                if (i == 7)
                {
                    Console.WriteLine();
                }
            }

            CPU.Instruction inst = new CPU.Instruction(s.Memory, s.CPU.PC);

            Console.WriteLine();

            if (inst.Length == 4)
            {
                Console.WriteLine("{0:x8} {1}", s.Memory.ReadWord(s.CPU.PC), Ridge.CPU.Disassembler.Disassemble(inst));
            }
            else
            {
                Console.WriteLine("{0:x8},{1:x4} {2}", s.Memory.ReadWord(s.CPU.PC), s.Memory.ReadHalfWord(s.CPU.PC+4), Ridge.CPU.Disassembler.Disassemble(inst));
            }
        }
    }
}
