using System;
using System.Diagnostics;

namespace Run {
    class Run {
        static void Main(params string[] args) {
            var program = new Program(args.Length > 0 ? args[0] : "testes/tostring");
            program.Parse();
            program.Build(true);
            program.Validate();
            program.Transpile();
            program.Compile();
            program.PrintResults();
        }
    }
}
