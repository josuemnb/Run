using System;
using System.Diagnostics;

namespace Run {
    class Run {
        static void Main() {
            var start = Stopwatch.StartNew();
            var program = new Program("next/main");
            program.Parse();
            program.Build();
            //program.Print();
            program.Validate();
            program.Transpile();
            Console.WriteLine("Took in " + start.ElapsedMilliseconds + " ms");
            Console.WriteLine("Parsed " + program.Lines + " lines");
        }
    }
}
