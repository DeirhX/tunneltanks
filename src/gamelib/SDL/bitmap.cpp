#include "bitmap.h"
#include <SDL.h>
#include <algorithm>
#include <cassert>

#include <gamelib.h>
#include <memalloc.h>
#include <types.h>
#include <vector>

#include "exceptions.h"
#include "require_sdl.h"

ColorBitmap SdlBmpDecoder::LoadRGBA(std::string_view relative_image_path)
{
    return BmpFile::LoadRGBAFromFile(relative_image_path);
}

MonoBitmap SdlBmpDecoder::LoadGrayscale(std::string_view relative_image_path)
{
    return BmpFile::LoadGrayscaleFromFile(relative_image_path);
}

MonoBitmap SdlBmpDecoder::LoadGrayscaleFromRGBA(std::string_view relative_image_path)
{
    return BmpFile::LoadGrayscaleFromRGBA(relative_image_path);
}

void BmpFile::SaveToFile(const ColorBitmap & data, std::string_view file_name)
{
    auto native_surface = holder_with_deleter<SDL_Surface>(
        SDL_CreateRGBSurface(SDL_SWSURFACE, data.GetSize().x, data.GetSize().y, 32, 0, 0, 0, 0),
        [](SDL_Surface * surface) { SDL_FreeSurface(surface); });

    if (!native_surface)
        throw GameException("Failed to allocate surface");
    /* Raising this semaphore so often is probably gonna hurt performance, but
	 * it's not like we're generating a bmp every second...  :) */
    if (SDL_MUSTLOCK(native_surface))
        SDL_LockSurface(native_surface.get());

    /* Get the address, and the mapped color: */
    assert(sizeof(Color) == native_surface->format->BytesPerPixel);
    assert(native_surface->pitch * native_surface->h == data.GetLength() * sizeof(Color));

    for (int i = 0; i < data.GetLength(); ++i)
    {
        std::uint32_t mapped_color = SDL_MapRGBA(native_surface->format, data[i].r, data[i].g, data[i].b, data[i].a);
        std::memcpy(((std::uint8_t *)native_surface->pixels) + sizeof(Color) * i, &mapped_color, sizeof(Color));
    }

    /* ... and unlock, and we're good! :) */
    if (SDL_MUSTLOCK(native_surface))
        SDL_UnlockSurface(native_surface.get());

    if (SDL_SaveBMP(native_surface.get(), file_name.data()) != 0)
        throw GameException("Failed save bitmap");
}

template <typename BitmapType, typename RawDataType, typename RawDataDecodeFunc>
BitmapType BmpFile::LoadFromFile(std::string_view file_name, RawDataDecodeFunc DecodeFunc)
{
    const auto native_surface = std::unique_ptr<SDL_Surface, void (*)(SDL_Surface *)>(
        SDL_LoadBMP(file_name.data()), [](SDL_Surface * surface) { SDL_FreeSurface(surface); });
    if (!native_surface)
        throw GameException("Failed to load bitmap");

    assert(sizeof(RawDataType) == native_surface->format->BytesPerPixel);
    auto loaded_data = BitmapType(Size{native_surface->w, native_surface->h});

    /* Go through every source pixel in native SDL format and convert it to our Color/monochrome pixel structure */
    for (int i = 0; i < native_surface->w * native_surface->h; ++i)
    {
        DecodeFunc(static_cast<RawDataType *>(native_surface->pixels)[i],
                   native_surface->format, &loaded_data[i]);
    }

    /* Freed automatically */
    return loaded_data;
}

ColorBitmap BmpFile::LoadRGBAFromFile(std::string_view file_name)
{
    auto decode_func = [](std::uint32_t file_data, SDL_PixelFormat * file_data_format, Color * target_data) {
        /* I want only alpha which I know will be saved last. The rest can be thrown away.*/
        SDL_GetRGBA(file_data, file_data_format, &target_data->r, &target_data->g, &target_data->b, &target_data->a);
    };

    return BmpFile::LoadFromFile<ColorBitmap, std::uint32_t>(file_name, decode_func);
}

MonoBitmap BmpFile::LoadGrayscaleFromFile(std::string_view file_name)
{
    auto decode_func = [](std::uint8_t file_data, SDL_PixelFormat * file_data_format, std::uint8_t * target_data) {
        *target_data = file_data;
    };
    return BmpFile::LoadFromFile<MonoBitmap, std::uint8_t>(file_name, decode_func);
}

MonoBitmap BmpFile::LoadGrayscaleFromRGBA(std::string_view file_name)
{
    auto decode_func = [](std::uint32_t file_data, SDL_PixelFormat * file_data_format, std::uint8_t * target_data) {
        std::uint8_t discard;
        /* I want only alpha which I know will be saved last. The rest can be thrown away.*/
        SDL_GetRGBA(file_data, file_data_format, &discard, &discard, &discard, target_data);
    };

    return BmpFile::LoadFromFile<MonoBitmap, std::uint32_t>(file_name, decode_func);
}
