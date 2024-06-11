//add string class implementation with support for:
// 1. string concatenation
// 2. string comparison
// 3. string assignment
// 4. string length
// 5. string indexing
// 6. string slicing
// 7. string find
// 8. string replace
// 9. string split
// 10. string join
// 11. string reverse
// 12. string upper
// 13. string lower
// 15. string capitalize
// 16. string swapcase
// 17. string isdigit
// 18. string isalpha
// 19. string isalnum
// 20. string islower
// 21. string isupper
// 23. string startswith
// 24. string endswith
// 25. string strip
// 26. string lstrip
// 27. string rstrip
// 28. string count
// 29. string optimize literal string usage in code
//		ex: "hello" + "world" -> "helloworld"
//		ex: "hello" + "world" + 10 -> "helloworld10"
//		ex: "hello" + i + "world" -> calculate the max size to this concatenation. then a string is created with that size and them items are added 

// 30. calculate the max string size of the values/variables

int Builtin_StringSizeOf(void* ptr, int type) {
	switch (type) {
		if (type >= 64) {
			unsigned long value = (*(unsigned long*)ptr);
			if (value >= 10_000_000_000_000_000_000) return 20;
			if (value >= 1_000_000_000_000_000_000) return 19;
			if (value >= 100_000_000_000_000_000) return 18;
			if (value >= 10_000_000_000_000_000) return 17;
			if (value >= 1_000_000_000_000_000) return 16;
			if (value >= 100_000_000_000_000) return 15;
			if (value >= 10_000_000_000_000) return 14;
			if (value >= 1_000_000_000_000) return 13;
			if (value >= 100_000_000_000) return 12;
			if (value >= 10_000_000_000) return 11;
		}
		if (type >= 32) {
			unsigned int value = (*(unsigned int*)ptr);
			if (value >= 1_000_000_000) return 10;
			if (value >= 100_000_000) return 9;
			if (value >= 10_000_000) return 8;
			if (value >= 1_000_000) return 7;
			if (value >= 100_000) return 6;
		}
		if (type >= 8) {
			unsigned short value = (*(unsigned short*)ptr);
			if (value >= 10_000) return 5;
			if (value >= 1_000) return 4;
		}
		if (type >= 1) {
			unsigned char value = (*(unsigned char*)ptr);
			if (value >= 100) return 3;
			if (value >= 10) return 2;
		}
		return 1;
	}
