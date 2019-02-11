using System;
using System.IO;

using Ridge.Debugger;

namespace Ridge
{
    class Program
    {
        static void Main(string[] args)
        {
            Hackeroo = false;

            RidgeSystem system = new RidgeSystem();
            system.Reset();

            Console.CancelKeyPress += OnBreak;

            Console.WriteLine("Yet-To-Be-Named Ridge emulator v0.0.\n");

            
            // Read in boot data from FDLP (TODO: base this on LOAD switch, etc.)
            system.FDLP.Boot();
            

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
                        try
                        {
                            system.Clock();
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine("Execution error: {0}", e.Message);
                            _execState = ExecutionState.Debug;
                        }
                        break;

                }

                if (Hackeroo) // || system.CPU.PC == 0x0004341e)
                {
                   _execState = ExecutionState.Step;
                   Hackeroo = false;
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

        public static bool Hackeroo;

        private static ExecutionState _execState;
    }
}
