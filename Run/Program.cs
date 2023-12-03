namespace Run {
    class Run {
        static void Main() {
            var program = new V12.Program("run/source/main");
            program.Parse();
            program.Build();
            //program.Print();
            program.Validate();
            program.Transpile();
        }
    }
}
