# Install script for directory: H:/Playground/RAWtoJXL/native/rawspeed/src/utilities/identify

# Set the install prefix
if(NOT DEFINED CMAKE_INSTALL_PREFIX)
  set(CMAKE_INSTALL_PREFIX "C:/Program Files/rawtojxl-rawspeed-cli")
endif()
string(REGEX REPLACE "/$" "" CMAKE_INSTALL_PREFIX "${CMAKE_INSTALL_PREFIX}")

# Set the install configuration name.
if(NOT DEFINED CMAKE_INSTALL_CONFIG_NAME)
  if(BUILD_TYPE)
    string(REGEX REPLACE "^[^A-Za-z0-9_]+" ""
           CMAKE_INSTALL_CONFIG_NAME "${BUILD_TYPE}")
  else()
    set(CMAKE_INSTALL_CONFIG_NAME "Release")
  endif()
  message(STATUS "Install configuration: \"${CMAKE_INSTALL_CONFIG_NAME}\"")
endif()

# Set the component getting installed.
if(NOT CMAKE_INSTALL_COMPONENT)
  if(COMPONENT)
    message(STATUS "Install component: \"${COMPONENT}\"")
    set(CMAKE_INSTALL_COMPONENT "${COMPONENT}")
  else()
    set(CMAKE_INSTALL_COMPONENT)
  endif()
endif()

# Is this installation the result of a crosscompile?
if(NOT DEFINED CMAKE_CROSSCOMPILING)
  set(CMAKE_CROSSCOMPILING "FALSE")
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Dd][Ee][Bb][Uu][Gg])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/Debug/rs-identify.exe")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee][Ww][Ii][Tt][Hh][Aa][Ss][Ss][Ee][Rr][Tt][Ss])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/ReleaseWithAsserts/rs-identify.exe")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/Release/rs-identify.exe")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Cc][Oo][Vv][Ee][Rr][Aa][Gg][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/Coverage/rs-identify.exe")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Ss][Aa][Nn][Ii][Tt][Ii][Zz][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/Sanitize/rs-identify.exe")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Tt][Ss][Aa][Nn])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/TSan/rs-identify.exe")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Ff][Uu][Zz][Zz])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE EXECUTABLE FILES "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/Fuzz/rs-identify.exe")
  endif()
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Dd][Ee][Bb][Uu][Gg])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-Debug.cmake" OPTIONAL)
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee][Ww][Ii][Tt][Hh][Aa][Ss][Ss][Ee][Rr][Tt][Ss])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-ReleaseWithAsserts.cmake" OPTIONAL)
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-Release.cmake" OPTIONAL)
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Cc][Oo][Vv][Ee][Rr][Aa][Gg][Ee])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-Coverage.cmake" OPTIONAL)
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Ss][Aa][Nn][Ii][Tt][Ii][Zz][Ee])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-Sanitize.cmake" OPTIONAL)
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Tt][Ss][Aa][Nn])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-TSan.cmake" OPTIONAL)
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Ff][Uu][Zz][Zz])$")
    include("H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/CMakeFiles/rs-identify.dir/install-cxx-module-bmi-Fuzz.cmake" OPTIONAL)
  endif()
endif()

string(REPLACE ";" "\n" CMAKE_INSTALL_MANIFEST_CONTENT
       "${CMAKE_INSTALL_MANIFEST_FILES}")
if(CMAKE_INSTALL_LOCAL_ONLY)
  file(WRITE "H:/Playground/RAWtoJXL/native/build/rawspeed/src/utilities/identify/install_local_manifest.txt"
     "${CMAKE_INSTALL_MANIFEST_CONTENT}")
endif()
