namespace Run {
    class Run {
        static void Main() {
            var program = new Program("next/main");
            program.Parse();
            //program.Print();
            program.Build();
            program.Validate();
            program.Transpile();
        }
    }
}
