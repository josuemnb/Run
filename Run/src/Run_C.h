#include <malloc.h>
#include <stdio.h>
#include <setjmp.h>
#include <stdlib.h>
#include <wchar.h>
#include <string.h>
#include <stddef.h>
#include <signal.h>
#include <stdarg.h>
#include <stdbool.h>

void resizeMap();
void* Alloc(int size, int id);
bool IS(int address, int is);

#define SCOPE(T)  __builtin_alloca(sizeof(T))
#define DELETE(V) free(V); V = 0
#define CAST(T,exp) (*(T*)exp)
#define SIZEOF(V) (int)((char *)(&V+1)-(char*)(&V))
#define CONVERT(T,ptr) *((T*)ptr)

#define type typedef struct
#define pointer void*
#define null NULL

#define TRY(j) do { jmp_buf j; switch (setjmp(j)) { case 0: 
#define CATCH(x) break; case x:
#define ENDTRY } } while (0)
#define THROW(j,x) longjmp(j, x)

#define NEW(T,total,id) (T*)Alloc(sizeof(T)*total, id)

static const ReflectionType* __TypesMap__;

int* mapAlloc;
int mapSize = 0;
int mapCapacity = 32 * 1024;

#define REGISTER(value, id)            \
    mapAlloc[mapSize++] = (int)(&value); \
    mapAlloc[mapSize++] = id

#define REGISTER_VAR(var, value, id)   \
    var = value;\
    resizeMap();                       \
    mapAlloc[mapSize++] = (long)(&var); \
    mapAlloc[mapSize++] = id;          \
    value

type ReflectionArgument ReflectionArgument;
type ReflectionMember ReflectionMember;
type ReflectionType ReflectionType;

void resizeMap() {
    if (mapSize == 0) {
        mapAlloc = (int*)malloc(mapCapacity * sizeof(int));
    } else if (mapSize >= mapCapacity) {
        mapCapacity *= 1.5;
        mapAlloc = (int*)realloc(mapAlloc, mapCapacity * sizeof(int));
    }
}

void* Alloc(int size, int id) {
    void* mem = malloc(size);
    resizeMap();
    REGISTER(mem, id);
    return mem;
}

bool IS(int address, int is) {
    for (int i = 0; i < mapSize; i++) {
        if (mapAlloc[i] == address) {
            int id = mapAlloc[i + 1];
            if (id == is) return true;
        again:
            if (id < 0) return false;
            ReflectionType* tp = __TypesMap__[id];
            if (!tp) return false;
            if (tp->id == is) return true;
            id = tp->based;
            goto again;
        }
    }
    return false;
}