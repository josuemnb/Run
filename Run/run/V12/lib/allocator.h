#pragma once

#include <limits.h>
#include <malloc.h>
#include <memory.h>
#include <stdio.h>
#include <stdlib.h>

int negate(int i) { return i > 0 ? -i : i; }

const int BIG = 1024 * 1024;
const int MEDIUM = 64 * 1024;
const int SMALL = 256;
static int Init = 0;
static short MagicNumber = 12345;

static long ScopeID = 0;

#define class typedef struct

class MemoryPage MemoryPage;
class MemoryPage {
	int Items;
	char* Pointer;
	int Offset;
	int Size;
	int Capacity;
	MemoryPage* Next;
	MemoryPage* Previous;
	int Scope;
	void (*Reset)(MemoryPage*);
	MemoryPage* (*Add)(MemoryPage*);
	void (*Destroy)(MemoryPage*);
} MemoryPage;

// class Pointer {
//   char* Ptr;
//   MemoryPage* MemoryPage;
//   int Size;
// } Pointer;

void* Alloc(int);
void* AllocType(int, int);
void SetupAlloc();
int Free(void* ptr);
int GetPointer(char*, MemoryPage*, int*);
MemoryPage* PageNew(int capacity);
char* NextSpace(MemoryPage* page, char* ptr);
char* FindSpace(MemoryPage* page, int size);
char* PageAlloc(MemoryPage* page, int size, int type);

class PageAllocation {
	MemoryPage* MemoryPage;
	void* Pointer;
} PageAllocation;

MemoryPage* CurrentPage = 0, * HeadPage = 0, * CurrentScope = 0;

MemoryPage* PageAdd(MemoryPage*);
void PageReset(MemoryPage* s) {
	MemoryPage* current = s;
	while (current) {
		current->Size = 0;
		current->Offset = 0;
		current->Items = 0;
		current = current->Next;
	}
}

void PageDestroy(MemoryPage* s) {
	MemoryPage* current = s;
	if (s->Previous) {
		CurrentScope = s->Previous;
		s->Previous->Next = 0;
	}
	while (current) {
		current->Size = 0;
		current->Offset = 0;
		free(current->Pointer);
		current->Pointer = 0;
		current = current->Next;
		if (current) {
			Free((char*)current->Previous);
			current->Previous = 0;
		}
	}
}

MemoryPage* MemoryScope() {
	SetupAlloc();
	MemoryPage* n = PageAdd(CurrentPage);
	n->Scope = ScopeID++;
	n->Previous = CurrentScope;
	if (CurrentScope) {
		CurrentScope->Next = n;
	}
	CurrentScope = n;
	return n;
}

MemoryPage* PageAdd(MemoryPage* s) {
	MemoryPage* n = PageNew(s->Capacity);
	n->Scope = s->Scope;
	n->Previous = s;
	s->Next = n;
	CurrentScope = n;
	return n;
}

int Valid(void* ptr) {
	long l = (long)ptr;
	return l > 0 && l < ULONG_MAX;
}

void PageCheck(MemoryPage* page) {
	//   while (page) {
	//     if (page->Size > 0) {
	//       Pointer* ptr = 0;
	//       char* o = 0;
	//       while ((ptr = NextSpace(page, o))) {
	//         o = ptr->Ptr;
	//         int sz = ptr->Size;
	//         // if (sz >= sizeof(Object) && GetType(o)) {
	//         // 	printf("%s\n", ((Object*)o)->Type->Name);
	//         // } else {
	//         // 	printf("Remaining undeleted memory size of %d\n", sz);
	//         // }
	//         o += sz;
	//       }
	//     }
	//     page = page->Next;
	//   }
}

void AllocatorClear() {
	// if (RegistredTypes) {
	// 	Free(RegistredTypes->Types);
	// 	Free(RegistredTypes);
	// }
	// PageCheck(HeadPage);
}

void PageInitialize(MemoryPage* p, int capacity) {
	p->Size = 0u;
	p->Next = 0;
	p->Items = 0;
	p->Scope = 0;
	// p->MaxFree = 0;
	// p->MinFree = 0;
	p->Offset = 0u;
	p->Previous = 0;
	p->Capacity = capacity > BIG ? capacity : BIG;
	p->Pointer = (char*)malloc(p->Capacity);
}

MemoryPage* PageNew(int capacity) {
	MemoryPage* p = (MemoryPage*)malloc(sizeof(MemoryPage));
	if (!p) abort();
	PageInitialize(p, capacity);
	p->Add = PageAdd;
	p->Destroy = PageDestroy;
	p->Reset = PageReset;
	return p;
}

char* NextSpace(MemoryPage* page, char* ptr) {
	if (!ptr) {
		ptr = page->Pointer + sizeof(int) + sizeof(MemoryPage*);
	}
	while (ptr < page->Pointer + page->Offset) {
		int size = 0, type = 0;
		MemoryPage page;
		if (!(size = GetPointer(ptr, &page, &type))) {
			return ptr;
		}
		if (size > 0) {
			return ptr;
		}
		ptr += abs(size);
	}
	return 0;
}

inline char* FindSpace(MemoryPage* page, int size) {
	char* ptr = page->Pointer + sizeof(int) + sizeof(MemoryPage*);
	int bestSize = INT_MAX;
	char* bestPointer = 0;
	int sz = 0;
	int count = 0;
	while (ptr < page->Pointer + page->Offset) {
		MemoryPage page;
		int type = 0;
		int sz = GetPointer(ptr, &page, &type);
		if (sz < 0) {
			sz *= -1;
			if (sz >= size && sz <= size * 1.5) {
				bestPointer = ptr;
				break;
			}
			if (bestSize > sz) {
				bestSize = sz;
				bestPointer = ptr;
			}
		}
		ptr += sz;
		count++;
	}
	if (!bestPointer) {
		return 0;
	}
	*((int*)(bestPointer - sizeof(int))) = sz;
	page->Size += sz;
	page->Items++;
	return bestPointer;
}

MemoryPage* FreePage(MemoryPage* current) {
	MemoryPage* temp = 0;
	if (current->Next) {
		temp = current->Next;
		if (current->Previous) {
			current->Previous->Next = current->Next;
		}
		current->Next->Previous = current->Previous;
	}
	else if (current->Previous) {
		temp = current->Previous;
		current->Previous->Next = 0;
	}
	free(current->Pointer);
	free(current);
	if (temp) {
		current = temp;
	}
	else {
		current = 0;
		HeadPage = 0;
	}
	return current;
}

int GetPointer(char* ptr, MemoryPage* page, int* type) {
	if (!Valid(ptr)) {
		return 0;
	}
	ptr -= sizeof(short);
	if (!*ptr) {
		return 0;
	}
	short mn = *((short*)ptr);
	if (mn != MagicNumber) {
		return 0;
	}
	ptr -= sizeof(int);
	if (!*ptr) {
		return 0;
	}
	int size = *((int*)ptr);
	if (size <= 0) {
		return 0;
	}
	ptr -= sizeof(MemoryPage*);
	if (!*ptr) {
		return 0;
	}
	*page = **((MemoryPage**)ptr);

	ptr -= sizeof(int);
	if (!*ptr) {
		return 0;
	}
	*type = *((int*)ptr);
	return size;
}

int Free(void* p) {
	char* ptr = (char*)p;
	int size = 0, type = 0;
	MemoryPage page;
	if (!(size = GetPointer(ptr, &page, &type))) {
		return 0;
	}
	if (size < 0) {
		return 0;
	}
	ptr -= sizeof(int);
	*((int*)ptr) = negate(size);
	page.Size -= size;
	page.Items--;
	CurrentPage = page.Size > 0 ? &page : FreePage(&page);
	return size;
}

void* AssignPointer(char* dest, char* src) {
	//   Pointer* p1 = GetPointer(dest);
	//   Pointer* p2 = GetPointer(src);
}

void SetupAlloc() {
	if (!Init) {
		Init = 1;
		atexit(AllocatorClear);
		// RegistredTypes = 0;
	}
	if (!HeadPage) {
		HeadPage = CurrentPage = PageNew(BIG);
	}
}

MemoryPage* PageResize(MemoryPage* page, int size) {
	MemoryPage* temp = page;
	while (1) {
		int available = temp->Capacity - temp->Offset;
		if (size > available) {
			// if (space >= size) {
			//	if (page->MinFree >= size && page->MaxFree >= size) {
			//		char* ptr = FindSpace(page, size);
			//		if (ptr) {
			//			allocation.Pointer = ptr;
			//			goto end;
			//		}
			//	}
			// } else
			if (temp->Next) {
				temp = temp->Next;
				continue;
			}
			temp->Next = PageNew(size);
			temp->Next->Previous = temp;
			temp = temp->Next;
		}
		return temp;
	}
}

char* SetPointer(MemoryPage* page, int size, int type) {
	char* ptr = page->Pointer + page->Offset;
	*ptr = type;
	ptr += sizeof(int);
	*((MemoryPage**)ptr) = page;
	ptr += sizeof(MemoryPage*);
	*((int*)ptr) = size;
	ptr += sizeof(int);
	*((short*)ptr) = MagicNumber;
	ptr += sizeof(short);
	return ptr;
}

char* PageAlloc(MemoryPage* page, int size, int type) {
	int real = size + (sizeof(int) * 2) + sizeof(short) + sizeof(MemoryPage*);
	MemoryPage* alloc = PageResize(page, real);
	//   if (alloc->Pointer) {
	//     if (CurrentScope) {
	//       CurrentScope = alloc;
	//     } else {
	//       CurrentPage = alloc;
	//     }
	//     return (char*)(alloc->Pointer + alloc->Offset);
	//   }
	page = alloc;
	char* ptr = SetPointer(page, size, type);
	page->Size += real;
	page->Offset += real;
	page->Items++;
	if (CurrentScope) {
		CurrentScope = page;
	}
	else {
		CurrentPage = page;
	}
	return ptr;
}

void* Alloc(int size) {
	return AllocType(size, 0);
}

void* AllocType(int size, int type) {
	if (size <= 0) return 0;
	SetupAlloc();
	return PageAlloc(CurrentScope ? CurrentScope : CurrentPage, size, type);
}

void* Realloc(void* ptr, int newsize) {
	if (!Valid(ptr)) {
		return 0;
	}
	MemoryPage page;
	int type = 0;
	int oldSize = GetPointer((char*)ptr, &page, &type);
	void* newPtr = Alloc(newsize);
	if (!newPtr) {
		abort();
	}
	memcpy(newPtr, ptr, oldSize);
	Free((char*)ptr);
	return newPtr;
}