#include <memory.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

typedef struct Arena Arena;
typedef struct Region Region;

static short NormalZone = 12342;
static short FreeZone = 12340;
static short ScopeZone = 12343;
static short RegionZone = 12341;
static uint16_t RegionID = 0;
const int SizeOfPointer = 18;

typedef struct Region {
    short magic;
    uint16_t id;
    int size;
    int offset;
    int capacity;
    int children_size;
    int children_capacity;
    char* data;
    Region** children;
    Region* parent;
} Region;

typedef struct Arena {
    int size;
    int capacity;
    Region* region;
} Arena;

void* Allocate(int size) { return malloc(size); }
void Free(void* ptr) { free(ptr); }

static Arena* arena = NULL;
Region* RegionNew(int capacity);
bool GetDefinition(void* ptr, int* typeID, int* size, Region* region);
void SetDefinition(void* ptr, int typeID, int size, Region* region);

int Align(int size, int alignment) {
    int diff = size % alignment;
    if (diff != 0) size += alignment - diff;
    return size;
}

bool RegionResize(Region* region) {
    if (region->children == NULL) {
        region->children_capacity = 4;
        region->children =
            (Region**)Allocate(sizeof(Region*) * region->children_capacity);
        if (region->children == NULL) {
            puts("Failed to allocate memory for children");
            exit(-1);
        }
        return true;
    }
    if (region->children_size >= region->children_capacity) {
        region->children_capacity *= 2;
        region->children = (Region**)realloc(
            region->children, sizeof(Region*) * region->children_capacity);
        if (region->children == NULL) {
            puts("Failed to allocate memory for children");
            exit(-1);
        }
    }
    return true;
}

Region* RegionAdd(Region* parent, int size) {
    int capacity = arena->capacity;
    if (capacity < size) capacity = size;
    Region* region = RegionNew(capacity);
    if (region == NULL) {
        return NULL;
    }
    if (RegionResize(parent) == false) {
        return NULL;
    }
    region->parent = parent;
    parent->children[parent->children_size] = region;
    parent->children_size++;
    return region;
}

Region* RegionNew(int capacity) {
    capacity = Align(capacity, 4);
    Region* region = (Region*)Allocate(sizeof(Region));
    if (region == NULL) {
        puts("Failed to allocate memory for region");
        exit(-1);
    }
    region->magic = RegionZone;
    region->id = RegionID++;
    region->size = 0;
    region->offset = 0;
    region->children_size = 0;
    region->children_capacity = 4;
    region->capacity = capacity;
    region->children = NULL;
    region->data = (char*)Allocate(capacity);
    if (region->data == NULL) {
        puts("Failed to allocate memory for region data");
        exit(-1);
    }
    return region;
}

void RegionReset(Region* region) {
    if (region->children) {
        for (int i = 0; i < region->children_size; i++) {
            RegionReset(region->children[i]);
        }
    }
    region->size = 0;
    region->offset = 0;
    region->children_size = 0;
}

void RegionClose(Region* region) {
    if (region == NULL) return;

    if (region->children) {
        for (int i = region->children_size - 1; i >= 0; i--) {
            RegionClose(region->children[i]);
        }
        Free(region->children);
    }
    if (region->parent) region->parent->children_size--;
    region->children_size = 0;
    region->offset = 0;
    region->size = 0;
    region->capacity = 0;
    region->children = NULL;
    region->parent = NULL;
    Free(region->data);
    Free(region);
}

void* AllocPointer(Region* region, int size, int typeID) {
    void* ptr = region->data + region->offset;
    SetDefinition(ptr, typeID, size, region);
    region->size += size + SizeOfPointer;
    region->offset += size + SizeOfPointer;
    arena->size += size + SizeOfPointer;
    return (void*)(ptr + SizeOfPointer);
}

int RegionFind(Region* region, int* size) {
    if (region->offset < SizeOfPointer) {
        return -1;
    }
    char* data = region->data;
    int index = 0, tempSize = 0, tempTypeID = 0;
    Region tempRegion = { 0 };
    while (index < region->offset) {
        if (GetDefinition(data, &tempTypeID, &tempSize, &tempRegion) == false) {
            return -1;
        }
        if (tempSize >= *size) {
            *size = tempSize;
            return index;
        }
        data += *size + SizeOfPointer;
        index += *size + SizeOfPointer;
    }
    return -1;
}

void* RegionAlloc(Region* region, int size, int typeID) {
    void* ptr = NULL;
    if (region->offset + size + SizeOfPointer < region->capacity) {
        return AllocPointer(region, size, typeID);
    }
    if (region->children) {
        for (int i = 0; i < region->children_size; i++) {
            ptr = RegionAlloc(region->children[i], size, typeID);
            if (ptr) return ptr;
        }
    }
    if (region->size + size + SizeOfPointer < region->capacity) {
        int index = RegionFind(region, &size);
        if (index >= 0) {
            int temp = region->offset;
            region->offset = index;
            void* ptr = AllocPointer(region, size, typeID);
            region->offset = temp + size + SizeOfPointer;
            return ptr;
        }
    }
    return NULL;
}

void ArenaInit(int initial_capacity) {
    if (arena != NULL) return;

    arena = (Arena*)Allocate(sizeof(Arena));
    arena->size = 0;

    arena->capacity = Align(initial_capacity, 4);
    arena->region = RegionNew(initial_capacity);
}

void ArenaClose() {
    RegionClose(arena->region);
    arena->size = 0;
    arena->region->size = 0;
}

void SetDefinition(void* ptr, int typeID, int size, Region* region) {
    char* data = (char*)ptr;
    *((int*)data) = typeID;
    data += sizeof(int);
    *((int*)data) = size;
    data += sizeof(int);
    *((Region**)data) = region;
    data += sizeof(Region*);
    if (region) {
        *((short*)data) = NormalZone;
    } else {
        *((short*)data) = ScopeZone;
    }
}

bool GetDefinition(void* ptr, int* typeID, int* size, Region* region) {
    char* data = (char*)ptr;
    data -= sizeof(short);
    short magic = *((short*)data);
    if (magic < FreeZone || magic > ScopeZone) {
        printf("Magic number is not correct: %d\n", magic);
        return false;
    }
    if (magic == NormalZone) {
        data -= sizeof(Region*);
        if (region) region = ((Region*)data);
    }
    data -= sizeof(int);
    if (size) *size = *((int*)data);
    data -= sizeof(int);
    if (typeID) *typeID = *((int*)data);
    return true;
}

Region* GetRegion(char* ptr, int* size) {
    char* backup = ptr;
    ptr -= sizeof(short);
    short mn = *((short*)ptr);
    if ((mn & NormalZone) == 0) {
        printf("Magic number wrong: %d\n", mn);
        return NULL;
    }
    ptr -= sizeof(Region*);
    Region* region = *((Region**)ptr);
    if (region == NULL) {
        printf("Region is null\n");
        return NULL;
    }
    ptr -= sizeof(int);
    if (size) *size = *((int*)ptr);
    if (backup >= region->data && backup < (region->data + region->capacity)) {
        return region;
    }
    if (backup) printf("ptr: %d\n", backup);
    if (region) {
        printf("region->data: %d\n", region->data);
        printf("region->size: %d\n", region->size);
    }
    return NULL;
}

void* ArenaAlloc(int size, void* context, int typeID) {
    Region* region = arena->region;
    if (context && *(short*)context == RegionZone) {
        region = (Region*)context;
    }
    void* ptr = RegionAlloc(region, size, typeID);
    if (ptr == NULL) {
        Region* new_region = RegionAdd(region, size);
        if (new_region == NULL) return NULL;
        ptr = RegionAlloc(new_region, size, typeID);
    }
    if (ptr) {
        memset(ptr, 0, size);
    }
    return ptr;
}

void RegionFree(Region* region, void* ptr, int size) {
    ptr -= sizeof(short);
    *((short*)ptr) = FreeZone;
    int subtract = size + SizeOfPointer;
    if (subtract > region->size) {
        subtract = region->size;
    }
    if (region->offset == region->size) {
        region->offset -= subtract;
    }
    region->size -= subtract;
    arena->size -= subtract;
}

bool Valid(void* ptr) {
    long l = (long)ptr;
    return l > LONG_MIN && l < LONG_MAX;
}

bool ArenaFree(void* ptr) {
    if (!Valid(ptr)) return false;
    printf("Freeing %d\n", arena->size);
    int size = 0;
    Region* region = GetRegion((char*)ptr, &size);
    if (region == NULL) return false;

    RegionFree(region, ptr, size);
    return true;
}

bool ArenaIS(void* ptr, int id) {
    if (!Valid(ptr)) {
        return false;
    }
    int typeID = 0;
    if (GetDefinition(ptr, &typeID, 0, 0) == false) return false;
    return typeID == id;
}

void* ArenaScope(int size, int typeID) {
    void* ptr = alloca(size + (SizeOfPointer - sizeof(Region*)));
    if (ptr == NULL) {
        puts("Failed to allocate scope memory");
        return NULL;
    }
    SetDefinition(ptr, typeID, size, NULL);
    return (void*)(ptr + (SizeOfPointer - sizeof(Region*)));
}

// int main() {
//   ArenaInit(1024 * 1024);
//   int size = 100000;
//   for (int i = 0; i < size; i++) {
//     void* ptr = ArenaAlloc(8 * 1024, NULL, 10);
//     if (ptr == NULL) {
//       puts("Failed to allocate memory");
//       break;
//     }
//     if (ArenaIS(ptr, 10) == false) {
//       puts("Failed to check type");
//       break;
//     }
//     ArenaFree(ptr);
//   }
//   void* scope = ArenaScope(100, 10);
//   if (ArenaIS(scope, 10) == false) {
//     puts("Failed to check scope type");
//   }
//   printf("Allocated %d byes\n", arena->size);
//   ArenaClose();
//   puts("Done");
//   return 0;
// }