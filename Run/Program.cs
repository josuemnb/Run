using System;
using System.Diagnostics;

namespace Run {
    class Run {
        static void Main() {
            var start = Stopwatch.StartNew();
            var program = new Program("next/program");
            program.Parse();
            program.Build(true);
            //program.Print();
            program.Validate();
            program.Transpile();
            Console.WriteLine("Took in " + start.ElapsedMilliseconds + " ms");
            Console.WriteLine("Parsed " + program.Lines + " lines");
        }
    }
}
