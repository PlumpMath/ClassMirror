#include "TestClass.h"
#include <iostream>

namespace TestCase {
	TestClass::TestClass() : count(0) {
		std::cout << "Instance created" << std::endl;
	}

	TestClass::~TestClass() {
		std::cout << "Instance destroyed" << std::endl;
	}

	int TestClass::method() {
		return count;
	}

	void TestClass::add(int a) {
		count += a;
	}
	void TestClass::subtract(int a) {
		count -= a;
	}

	void TestClass::hello() {
		std::cout << "hello" << std::endl;
	}
}