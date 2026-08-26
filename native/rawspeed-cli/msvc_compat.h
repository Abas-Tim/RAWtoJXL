#if defined(_MSC_VER) && !defined(__clang__)

#ifndef __attribute__
#define __attribute__(x)
#endif

#ifndef __PRETTY_FUNCTION__
#define __PRETTY_FUNCTION__ __FUNCSIG__
#endif

#ifndef __builtin_unreachable
#define __builtin_unreachable() __assume(false)
#endif

#include <cstdint>
#include <limits>
#include <type_traits>

namespace rawspeed_msvc {

template <typename T>
inline bool ovf_add(T a, T b, T* r) {
  static_assert(std::is_integral_v<T> && sizeof(T) <= 4, "unsupported width");
  long long s = static_cast<long long>(a) + b;
  *r = static_cast<T>(s);
  return s > static_cast<long long>(std::numeric_limits<T>::max()) ||
         s < static_cast<long long>(std::numeric_limits<T>::min());
}

template <typename T>
inline bool ovf_mul(T a, T b, T* r) {
  static_assert(std::is_integral_v<T> && sizeof(T) <= 4, "unsupported width");
  long long p = static_cast<long long>(a) * b;
  *r = static_cast<T>(p);
  return p > static_cast<long long>(std::numeric_limits<T>::max()) ||
         p < static_cast<long long>(std::numeric_limits<T>::min());
}

} // namespace rawspeed_msvc

#define __builtin_sadd_overflow(a, b, c) rawspeed_msvc::ovf_add((a), (b), (c))
#define __builtin_mul_overflow(a, b, c) rawspeed_msvc::ovf_mul((a), (b), (c))

#endif
