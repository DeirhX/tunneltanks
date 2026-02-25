#pragma once

#pragma warning(push)
#pragma warning(disable : 5054)
#include <nanogui/nanogui.h>
#pragma warning(pop)
#include <thread>
namespace crust
{

class GuiEngine
{
  public:
    GuiEngine();
    void Present();

    void ShowSuchTestWindow();

  private:
    std::thread gui_thread;
};

} // namespace crust