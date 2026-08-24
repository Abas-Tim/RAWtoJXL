cmake_minimum_required(VERSION 3.16)
project(rawtojxl-rawspeed-cli CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_subdirectory(rawspeed)

add_executable(rawspeed-cli rawspeed-cli/main.cpp)
target_link_libraries(rawspeed-cli PRIVATE rawspeed)
target_compile_options(rawspeed-cli PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/EHsc>)
