#include <climits>
#include <cstddef>
#include <cstdint>
static_assert(CHAR_BIT == 8); // !!!
static_assert(sizeof(std::byte) == 1);
static_assert(sizeof(char) == 1);
static_assert(sizeof(signed char) == 1);
static_assert(sizeof(unsigned char) == 1);
static_assert(sizeof(int8_t) == 1);
static_assert(sizeof(uint8_t) == 1);
static_assert(sizeof(int16_t) == 2);
static_assert(sizeof(uint16_t) == 2);
static_assert(sizeof(int32_t) == 4);
static_assert(sizeof(uint32_t) == 4);
static_assert(sizeof(int64_t) == 8);
static_assert(sizeof(uint64_t) == 8);
static_assert(sizeof(float) == 4);
static_assert(sizeof(double) == 8);
int main() { return 0; }
