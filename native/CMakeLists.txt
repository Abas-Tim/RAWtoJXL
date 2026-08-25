cmake_minimum_required(VERSION 3.16)
project(rawtojxl-rawspeed-cli CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(MSVC)
  add_compile_options(/Zc:preprocessor /EHsc /FI"${CMAKE_CURRENT_SOURCE_DIR}/rawspeed-cli/msvc_compat.h")
endif()

add_subdirectory(rawspeed)

add_executable(rawspeed-cli rawspeed-cli/main.cpp)
target_link_libraries(rawspeed-cli PRIVATE rawspeed)
target_include_directories(rawspeed-cli PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/rawspeed/src)
