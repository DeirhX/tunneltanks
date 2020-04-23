#pragma once

#include "bitmaps.h"
#include "game_system.h"

class SdlBmpDecoder : public BmpDecoder
{
    ColorBitmap LoadRGBA(std::string_view relative_image_path) override;
    MonoBitmap LoadGrayscale(std::string_view relative_image_path) override;
    MonoBitmap LoadGrayscaleFromRGBA(std::string_view relative_image_path) override;
};

class BmpFile
{
    template <typename BitmapType, typename RawDataType, typename RawDataDecodeFunc>
    static BitmapType LoadFromFile(std::string_view file_name, RawDataDecodeFunc DecodeFunc);
  public:
    static void SaveToFile(const ColorBitmap & data, std::string_view file_name);
    static ColorBitmap LoadRGBAFromFile(std::string_view file_name);
    static MonoBitmap LoadGrayscaleFromFile(std::string_view file_name);
    static MonoBitmap LoadGrayscaleFromRGBA(std::string_view file_name);
};

