cmake_minimum_required(VERSION 3.13)

################################################################################
### COMPILING THE PROJECT: #####################################################
################################################################################

project(tunneltanks VERSION 2.0)

find_package(SDL2 REQUIRED)
find_package(Boost REQUIRED)

message(STATUS "SDL2 Include path: ${SDL2_INCLUDE_DIRS}")

# Support both 32 and 64 bit builds
if (${CMAKE_SIZEOF_VOID_P} MATCHES 8)
  set(SDL_LIBRARY "${CMAKE_CURRENT_LIST_DIR}/sdl2/lib/x64/SDL2.lib;${CMAKE_CURRENT_LIST_DIR}/lib/x64/SDL2main.lib")
else ()
  set(SDL_LIBRARY "${CMAKE_CURRENT_LIST_DIR}/sdl2/lib/x86/SDL2.lib;${CMAKE_CURRENT_LIST_DIR}/lib/x86/SDL2main.lib")
endif ()

# Set up the compiler, and find all source files:
file(GLOB source_files "src/*.c" "src/*.cpp" "src/include/*.cpp" "src/gamelib/SDL/*.c" "src/gamelib/SDL/*.cpp")

# Link and compile:
add_executable( tunneltanks ${source_files} )

if (NOT WINDOWS)
	target_compile_options(tunneltanks PUBLIC "-Wall" "-Wextra" "-pedantic" "-Werror")
endif()

target_compile_definitions(tunneltanks PUBLIC "USE_SDL_GAMELIB" "DEBUG_TRACE_LEVEL=3")
target_compile_features(tunneltanks PUBLIC cxx_std_20)
target_link_libraries(tunneltanks PUBLIC ${SDL2_LIBRARIES} Boost::boost)
target_include_directories(tunneltanks PUBLIC ${SDL2_INCLUDE_DIRS} "src/include" "src" "src/gamelib" "src/gamelib/SDL")

configure_file(${CMAKE_SOURCE_DIR}/resources/fonts/broddmin_5x10.bmp ${CMAKE_BINARY_DIR}/resources/fonts/broddmin_5x10.bmp COPYONLY)

# Enable the make install:
install(
	TARGETS tunneltanks
	RUNTIME DESTINATION bin
)


################################################################################
### PACKAGING THE PROJECT: #####################################################
################################################################################

# Set all the files needed for proper packaging:
CONFIGURE_FILE(${PROJECT_SOURCE_DIR}/pack/config.cpack.in      ${PROJECT_SOURCE_DIR}/pack/config.cpack      @ONLY)
CONFIGURE_FILE(${PROJECT_SOURCE_DIR}/pack/target.tbz2.cpack.in ${PROJECT_SOURCE_DIR}/pack/target.tbz2.cpack @ONLY)
CONFIGURE_FILE(${PROJECT_SOURCE_DIR}/pack/target.deb.cpack.in  ${PROJECT_SOURCE_DIR}/pack/target.deb.cpack  @ONLY)
CONFIGURE_FILE(${PROJECT_SOURCE_DIR}/pack/target.rpm.cpack.in  ${PROJECT_SOURCE_DIR}/pack/target.rpm.cpack  @ONLY)

# Add a custom target to make a binary tarball:
add_custom_target(package-tbz2
	${CMAKE_CPACK_COMMAND}
	--config ${CMAKE_SOURCE_DIR}/pack/target.tbz2.cpack
)

# Another custom target to make a deb file: (For Debian/Ubuntu/Mint/etc...)
add_custom_target(package-deb
	${CMAKE_CPACK_COMMAND}
	--config ${CMAKE_SOURCE_DIR}/pack/target.deb.cpack
)

# Another custom target to make a rpm file: (For Fedora/Red Hat/etc...)
add_custom_target(package-rpm
	${CMAKE_CPACK_COMMAND}
	--config ${CMAKE_SOURCE_DIR}/pack/target.rpm.cpack
)


################################################################################
### CLEANING THE PROJECT: ######################################################
################################################################################

# One other final custom target for a dist-clean:
add_custom_target(dist-clean
	rm -rf
	CMakeFiles
	CMakeCache.txt
	cmake_install.cmake
	Makefile
	install_manifest.txt
	CPackConfig.cmake
	CPackSourceConfig.cmake
	_CPack_Packages
	tunneltanks
	tunneltanks_*.deb
	tunneltanks_*.tar.bz2
	tunneltanks_*.rpm
	debug_start.bmp
	debug_end.bmp
	pack/*.cpack
)

