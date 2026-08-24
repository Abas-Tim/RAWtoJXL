#include <version>
  #define STR_HELPER(x) #x
  #define STR(x) STR_HELPER(x)
  #if defined(__GLIBCXX__)
  #if !defined(_GLIBCXX_RELEASE) || _GLIBCXX_RELEASE < 12
  #pragma message("Unsupported libstdc++ version: " STR(_GLIBCXX_RELEASE))
  #error
  #endif
  #endif
  int main() { return 0; }
  