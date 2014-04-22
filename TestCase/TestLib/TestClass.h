#pragma once

namespace TestCase {
	
	class TestClass {
			int count;
		public:
			TestClass();
			~TestClass();
			int method();
			void add(int a);
			void subtract(int a);
			static void hello();
		};
	}
