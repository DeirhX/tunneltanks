#pragma once
#include <exception>

class GameException : public std::exception
{
public:
	GameException(const char* message) : std::exception(message) {}
};

