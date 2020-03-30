#pragma once
#include <cstdint>
#include <exception>

class GameException : public std::exception
{
  public:
    GameException(const char * message) : std::exception(message) {}
};

class GameInitException : public GameException
{
  public:
    const char* error_string;

    GameInitException(const char * message, const char * error_string = nullptr)
        : GameException(message), error_string(error_string)
    {
    }
};

class NoControllersException : public GameException
{
  public:
    NoControllersException(const char * message) : GameException(message) {}
};