#include <drawbuffer.h>
#include <memalloc.h>
#include <memory>

/* TODO: We're using color structures here because we started with Uint32 values
 *       and this was an easier transition. Eventually, all colors will be in a
 *       central array, and the pixel data will simply be 1-byte indexes. */

DrawBuffer::DrawBuffer(Size size): size(size), default_color(0, 0, 0)
{
	pixel_data.reset(new Color[size.x * size.y]);
}

void DrawBuffer::SetPixel(Position offset, Color color)
{
	if (offset.x < 0 || offset.y < 0 || offset.x >= size.x || offset.y >= size.y) return;
	pixel_data.get()[offset.y * size.x + offset.x] = color;
}

Color DrawBuffer::GetPixel(Position offset)
{
	if (offset.x < 0 || offset.y < 0 || offset.x >= size.x || offset.y >= size.y) return this->default_color;
	return pixel_data.get()[offset.y * size.x + offset.x];
}
