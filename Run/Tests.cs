using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace Run {

    [TestClass]
    public class Tests {

        [TestMethod]
        public void Query() {
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestFuncCall() {
            TestCode("""
                func test {
                }
                
                main {
                    test()
                }
                """);
        }

        [TestMethod]
        public void TestClass() {
            TestCode("""
              class cls {
                this(s:int) {
                }
              }
              """);
        }

        [TestMethod]
        public void TestVars() {
            TestCode("""
                var i:int
                var c:char
                var s = "ok"
                var b = 10 + 10
                """);
        }

        [TestMethod]
        public void TestGenerics() {
            TestCode("""
              class Array<T> {
                var size:int
                var items:T[]

                this(s:int) {
                  this.size = s
                  this.items = C.malloc(this.size * sizeof(T))
                }
              }

              class String:Array<char> {
              }
              main {
                var s = new String[10]
              }
              """);
        }

        [TestMethod]
        public void TestVariadic() {
            TestCode("""
              func p(t:int, ...args:int) {
               var a = args[0]
                var j = 10
              }

              main {
              }
              """);
        }

        [TestMethod]
        public void TestArray() {
            TestCode("""
              main {
                var a = new char[10]
                var a = new int[20]
              }
              """);
        }

        public void TestCode(string code) {
            var program = new V12.Program(new MemoryStream(Encoding.UTF8.GetBytes(code)));
            program.Parse();
            program.Build();
            program.Validate();
            program.Transpile();
            Assert.IsTrue(true);
        }
    }
}
