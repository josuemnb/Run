namespace Run {
    class Run {
        static void Main() {
            var program = new V12.Program("next/main");
            program.Parse();
            //program.Print();
            program.Build();
            program.Validate();
            program.Transpile();
        }
    }
}
