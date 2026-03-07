namespace Tunnerer.Desktop.Rendering.Dx11;

using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Tunnerer.Core.Types;

public sealed unsafe partial class Backend
{
    private void UpdateTerrainAuxTexture(byte[]? auxData, Size worldSize, Rect? dirtyRect)
    {
        int worldW = worldSize.X;
        int worldH = worldSize.Y;
        if (auxData == null || worldW <= 0 || worldH <= 0)
            return;

        bool sizeChanged = _terrainAuxTexture == null || _terrainAuxW != worldW || _terrainAuxH != worldH;
        if (sizeChanged)
        {
            if (_terrainAuxSrv != null) { _terrainAuxSrv->Release(); _terrainAuxSrv = null; }
            if (_terrainAuxTexture != null) { _terrainAuxTexture->Release(); _terrainAuxTexture = null; }

            var desc = CreateTextureDesc(
                worldW, worldH, Format.FormatR8G8B8A8Unorm, Usage.Default,
                (uint)BindFlag.ShaderResource, 0);
            fixed (byte* ptr = auxData)
            {
                var sub = new SubresourceData
                {
                    PSysMem = ptr,
                    SysMemPitch = (uint)(worldW * 4),
                    SysMemSlicePitch = 0,
                };
                ID3D11Texture2D* auxTex = null;
                _device->CreateTexture2D(&desc, &sub, &auxTex);
                _terrainAuxTexture = auxTex;
            }
            ID3D11ShaderResourceView* auxSrv = null;
            _device->CreateShaderResourceView((ID3D11Resource*)_terrainAuxTexture, null, &auxSrv);
            _terrainAuxSrv = auxSrv;
            _terrainAuxW = worldW;
            _terrainAuxH = worldH;
            return;
        }

        if (dirtyRect is not Rect rect)
            return;
        RectMath.GetMinMaxInclusive(rect, out int minX, out int minY, out int maxX, out int maxY);
        int rw = maxX - minX + 1;
        int rh = maxY - minY + 1;
        if (rw <= 0 || rh <= 0) return;
        var box = new Box((uint)minX, (uint)minY, 0u, (uint)(maxX + 1), (uint)(maxY + 1), 1u);
        int required = rw * rh * 4;
        if (_terrainAuxUploadScratch == null || _terrainAuxUploadScratch.Length < required)
            _terrainAuxUploadScratch = new byte[required];
        byte[] scratch = _terrainAuxUploadScratch;
        for (int y = 0; y < rh; y++)
        {
            int src = ((minY + y) * worldW + minX) * 4;
            int dst = y * rw * 4;
            Array.Copy(auxData, src, scratch, dst, rw * 4);
        }
        fixed (byte* ptr = scratch)
        {
            _context->UpdateSubresource((ID3D11Resource*)_terrainAuxTexture, 0, &box, ptr, (uint)(rw * 4), 0);
        }
    }
}
