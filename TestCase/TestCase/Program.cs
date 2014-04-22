using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCase {
    class Program {
        static void Main(string[] args) {
            TestClass.hello();
            using (var test = new TestClass()) {
                for (int i = 0; i < 10; ++i) {
                    test.add(i);
                }
                for (int i = 0; i < 10; ++i) {
                    test.substract(i);
                }
                Console.WriteLine(test.method());
            }
        }
    }
}
