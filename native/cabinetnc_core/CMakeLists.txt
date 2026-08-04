cmake_minimum_required(VERSION 3.16)
project(cabinetnc_core LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(CLIPPER2_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/Clipper2Lib)

add_library(cabinetnc_core STATIC
  src/offset.cpp
  ${CLIPPER2_DIR}/src/clipper.engine.cpp
  ${CLIPPER2_DIR}/src/clipper.offset.cpp
  ${CLIPPER2_DIR}/src/clipper.rectclip.cpp
)
target_include_directories(cabinetnc_core PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/include
  ${CLIPPER2_DIR}/include
)

add_executable(cabinetnc_offset src/cli_main.cpp)
target_link_libraries(cabinetnc_offset PRIVATE cabinetnc_core)

# ponytail: static+CLI first; WASM / node-addon later when toolchain is routine.
install(TARGETS cabinetnc_offset RUNTIME DESTINATION bin)
