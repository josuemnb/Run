using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Run {
    public class Transpiler(Builder builder) {
        protected readonly Builder Builder = builder;
        protected TextWriter Writer;
        protected string Destination;

        public virtual void Save(string path) {
            Destination = path;
            using var stream = new FileStream(path, FileMode.Create);
            Save(stream);
        }
        public virtual void Save(Stream stream) {

        }
        public virtual bool Compile() {
            return true;
        }
    }
}