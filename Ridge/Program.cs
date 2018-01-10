using System;
using System.IO;

using Ridge.Debugger;

namespace Ridge
{
    class Program
    {
        static void Main(string[] args)
        {
            RidgeSystem system = new RidgeSystem();
            system.Reset();

            Console.CancelKeyPress += OnBreak;

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

            DebugPrompt debugger = new DebugPrompt(system);
            _execState = ExecutionState.Debug;

            while (true)
            {                
                switch(_execState)
                {
                    case ExecutionState.Debug:
                        _execState = debugger.Prompt();
                        break;

                    case ExecutionState.Step:
                        system.Clock();
                        _execState = ExecutionState.Debug;
                        break;

                    case ExecutionState.Go:
                        //try
                        {
                            system.Clock();
                        }
                        //catch(Exception e)
                        {
                        //    Console.WriteLine("Execution error: {0}", e.Message);
                          //  _execState = ExecutionState.Debug;
                        }
                        break;

                }

                if (system.CPU.PC == 0x0)
                {
                    _execState = ExecutionState.Step;
                }
            }            
        }

        private static void OnBreak(object sender, ConsoleCancelEventArgs e)
        {
            //
            // break into the debugger.
            //
            _execState = ExecutionState.Debug;

            e.Cancel = true;            
        }

        private static ExecutionState _execState;
    }
}
