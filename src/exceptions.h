#pragma once
#include <stdexcept>

class GameException : public std::runtime_error
{
  public:
    GameException(const char * message) : std::runtime_error(message) {}
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

class RenderException : public GameException
{
  public:
    const char * error_string;

    RenderException(const char * message, const char * error_string = nullptr)
        : GameException(message), error_string(error_string)
    {
    }
};

class NoControllersException : public GameException
{
  public:
    NoControllersException(const char * message) : GameException(message) {}
};