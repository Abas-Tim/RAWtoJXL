#include <version>
  #define STR_HELPER(x) #x
  #define STR(x) STR_HELPER(x)
  #if defined(_LIBCPP_VERSION) && _LIBCPP_VERSION < 16000
  #pragma message("Unsupported libc++ version: " STR(_LIBCPP_VERSION))
  #error
  #endif
  int main() { return 0; }
  