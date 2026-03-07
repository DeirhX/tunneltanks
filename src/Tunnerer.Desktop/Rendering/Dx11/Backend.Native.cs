namespace Tunnerer.Desktop.Rendering.Dx11;

using ImGuiNET;
using System.Diagnostics;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Tunnerer.Core.Config;
using Tunnerer.Core.Types;
using Tunnerer.Desktop.Config;
using Tunnerer.Desktop.Rendering;

public sealed unsafe partial class Backend
{
    private void UploadNative(int w, int h, in GamePixelsUpload upload)
    {
        EnsureSceneTexture(w, h);
        long t0 = Stopwatch.GetTimestamp();
        UpdateTerrainAuxTexture(upload.TerrainAux, upload.View.WorldSize, upload.AuxDirtyRect);
        _profileAuxUploadTicks += Stopwatch.GetTimestamp() - t0;
        t0 = Stopwatch.GetTimestamp();
        bool useNativeContinuous = upload.UseNativeContinuous &&
                                   upload.NativeSourcePixels != null &&
                                   upload.View.WorldSize.X > 0 &&
                                   upload.View.WorldSize.Y > 0;
        if (useNativeContinuous)
        {
            EnsureNativeSourceTexture(upload.View.WorldSize.X, upload.View.WorldSize.Y);
            fixed (uint* ptr = upload.NativeSourcePixels)
            {
                _context->UpdateSubresource(
                    (ID3D11Resource*)_nativeSourceTexture, 0, null, ptr,
                    (uint)(upload.View.WorldSize.X * sizeof(uint)), 0);
            }
            RunNativeContinuousTerrainPass(upload);
            _profileNativeTerrainPassTicks += Stopwatch.GetTimestamp() - t0;
        }
        else
        {
            fixed (uint* ptr = upload.Pixels)
            {
                _context->UpdateSubresource(
                    (ID3D11Resource*)_sceneTexture, 0, null, ptr, (uint)(w * sizeof(uint)), 0);
            }
            _profileSceneUploadTicks += Stopwatch.GetTimestamp() - t0;
        }
        EnsurePostTarget(w, h);
        t0 = Stopwatch.GetTimestamp();
        RunPostProcessPass(upload);
        _profilePostPassTicks += Stopwatch.GetTimestamp() - t0;
        _profileFrameCount++;
        if (_detailedProfileEnabled && _profileFrameCount >= 240)
        {
            FlushDetailedProfile();
        }
    }

    private void EnsureSceneTexture(int w, int h)
    {
        if (_sceneTexture != null && _sceneTexW == w && _sceneTexH == h)
            return;
        if (_sceneRtv != null) { _sceneRtv->Release(); _sceneRtv = null; }
        if (_sceneTexture != null) { _sceneTexture->Release(); _sceneTexture = null; }

        var desc = new Texture2DDesc
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)(BindFlag.ShaderResource | BindFlag.RenderTarget),
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* tex = null;
        _device->CreateTexture2D(&desc, null, &tex);
        _sceneTexture = tex;
        if (_sceneSrv != null) { _sceneSrv->Release(); _sceneSrv = null; }
        ID3D11ShaderResourceView* srv = null;
        _device->CreateShaderResourceView((ID3D11Resource*)tex, null, &srv);
        _sceneSrv = srv;
        ID3D11RenderTargetView* rtv = null;
        _device->CreateRenderTargetView((ID3D11Resource*)tex, null, &rtv);
        _sceneRtv = rtv;
        _displaySrv = _sceneSrv;
        _sceneTexW = w;
        _sceneTexH = h;
    }

    private void EnsureNativeSourceTexture(int w, int h)
    {
        if (_nativeSourceTexture != null && _nativeSourceW == w && _nativeSourceH == h)
            return;
        if (_nativeSourceSrv != null) { _nativeSourceSrv->Release(); _nativeSourceSrv = null; }
        if (_nativeSourceTexture != null) { _nativeSourceTexture->Release(); _nativeSourceTexture = null; }

        var desc = new Texture2DDesc
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* tex = null;
        if (_device->CreateTexture2D(&desc, null, &tex) < 0 || tex == null)
            return;
        _nativeSourceTexture = tex;
        ID3D11ShaderResourceView* srv = null;
        _device->CreateShaderResourceView((ID3D11Resource*)tex, null, &srv);
        _nativeSourceSrv = srv;
        _nativeSourceW = w;
        _nativeSourceH = h;
    }

    private void RunNativeContinuousTerrainPass(in GamePixelsUpload upload)
    {
        if (_sceneRtv == null || _nativeSourceSrv == null || _terrainPs == null || _postParamsBuffer == null)
            return;
        PrepareFullscreenPass(_sceneRtv, _sceneTexW, _sceneTexH, _terrainPs);
        ID3D11ShaderResourceView* sourceSrv = _nativeSourceSrv;
        _context->PSSetShaderResources(0, 1, &sourceSrv);
        ID3D11ShaderResourceView* auxSrv = _terrainAuxSrv;
        _context->PSSetShaderResources(1, 1, &auxSrv);
        UpdatePostParamsBuffer(upload);
        ID3D11Buffer* cb = _postParamsBuffer;
        _context->PSSetConstantBuffers(0, 1, &cb);
        _context->Draw(3, 0);
        ID3D11ShaderResourceView* nullSrv = null;
        _context->PSSetShaderResources(0, 1, &nullSrv);
        _context->PSSetShaderResources(1, 1, &nullSrv);
        _displaySrv = _sceneSrv;
        TryDumpDebugTexture(_sceneTexture, "scene");
    }

    private void EnsurePostTarget(int w, int h)
    {
        if (_postTexture != null && _sceneTexW == w && _sceneTexH == h)
            return;
        if (_postRtv != null) { _postRtv->Release(); _postRtv = null; }
        if (_postSrv != null) { _postSrv->Release(); _postSrv = null; }
        if (_postTexture != null) { _postTexture->Release(); _postTexture = null; }

        var desc = new Texture2DDesc
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)(BindFlag.RenderTarget | BindFlag.ShaderResource),
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* tex = null;
        if (_device->CreateTexture2D(&desc, null, &tex) < 0 || tex == null)
            return;
        _postTexture = tex;
        ID3D11ShaderResourceView* postSrv = null;
        _device->CreateShaderResourceView((ID3D11Resource*)tex, null, &postSrv);
        _postSrv = postSrv;
        ID3D11RenderTargetView* postRtv = null;
        _device->CreateRenderTargetView((ID3D11Resource*)tex, null, &postRtv);
        _postRtv = postRtv;
    }

    private void RunPostProcessPass(in GamePixelsUpload upload)
    {
        if (_postRtv == null || _sceneSrv == null || _postPs == null || _postParamsBuffer == null)
        {
            _displaySrv = _sceneSrv;
            return;
        }

        long t0 = Stopwatch.GetTimestamp();
        PrepareFullscreenPass(_postRtv, _sceneTexW, _sceneTexH, _postPs);
        _profilePostSetupTicks += Stopwatch.GetTimestamp() - t0;

        ID3D11ShaderResourceView* sceneSrv = _sceneSrv;
        _context->PSSetShaderResources(0, 1, &sceneSrv);
        ID3D11ShaderResourceView* auxSrv = _terrainAuxSrv;
        _context->PSSetShaderResources(1, 1, &auxSrv);

        t0 = Stopwatch.GetTimestamp();
        UpdatePostParamsBuffer(upload);
        ID3D11Buffer* cb = _postParamsBuffer;
        _context->PSSetConstantBuffers(0, 1, &cb);
        _profilePostCbUpdateTicks += Stopwatch.GetTimestamp() - t0;
        t0 = Stopwatch.GetTimestamp();
        _context->Draw(3, 0);
        _profilePostDrawTicks += Stopwatch.GetTimestamp() - t0;

        ID3D11ShaderResourceView* nullSrv = null;
        _context->PSSetShaderResources(0, 1, &nullSrv);
        _context->PSSetShaderResources(1, 1, &nullSrv);
        _displaySrv = _postSrv != null ? _postSrv : _sceneSrv;
        TryCapturePendingPostScreenshot();
        TryDumpDebugTexture(_postTexture, "post");
    }

    private void TryCapturePendingPostScreenshot()
    {
        string? targetPath = _pendingScreenshotPath;
        if (string.IsNullOrWhiteSpace(targetPath) || _postTexture == null || _sceneTexW <= 0 || _sceneTexH <= 0)
            return;

        _pendingScreenshotPath = null;
        if (DumpTextureToPpm(_postTexture, targetPath))
            Console.WriteLine($"[Render] Screenshot saved: {targetPath}");
        else
            Console.WriteLine($"[Render] Screenshot failed: {targetPath}");
    }

    private void TryDumpDebugTexture(ID3D11Texture2D* srcTex, string name)
    {
        if (!_debugDumpFrames || _debugDumpDone || srcTex == null || _sceneTexW <= 0 || _sceneTexH <= 0)
            return;

        string debugDir = Path.Combine(AppContext.BaseDirectory, "debug");
        Directory.CreateDirectory(debugDir);
        string outPath = Path.Combine(debugDir, $"{name}-dump.ppm");
        if (!DumpTextureToPpm(srcTex, outPath))
            return;
        Console.WriteLine($"[DX11] dumped {name} frame to {outPath}");
        if (string.Equals(name, "post", StringComparison.Ordinal))
            _debugDumpDone = true;
    }

    private bool DumpTextureToPpm(ID3D11Texture2D* srcTex, string outPath)
    {
        if (srcTex == null || _sceneTexW <= 0 || _sceneTexH <= 0)
            return false;

        var desc = new Texture2DDesc
        {
            Width = (uint)_sceneTexW,
            Height = (uint)_sceneTexH,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Staging,
            BindFlags = 0,
            CPUAccessFlags = (uint)CpuAccessFlag.Read,
            MiscFlags = 0,
        };

        ID3D11Texture2D* staging = null;
        if (_device->CreateTexture2D(&desc, null, &staging) < 0 || staging == null)
            return false;

        try
        {
            _context->CopyResource((ID3D11Resource*)staging, (ID3D11Resource*)srcTex);
            MappedSubresource mapped;
            if (_context->Map((ID3D11Resource*)staging, 0, Silk.NET.Direct3D11.Map.Read, 0, &mapped) < 0)
                return false;

            try
            {
                string? parent = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                string header = $"P6\n{_sceneTexW} {_sceneTexH}\n255\n";
                byte[] headerBytes = global::System.Text.Encoding.ASCII.GetBytes(header);
                fs.Write(headerBytes, 0, headerBytes.Length);

                byte* srcBase = (byte*)mapped.PData;
                int rowPitch = (int)mapped.RowPitch;
                byte[] rgb = new byte[_sceneTexW * 3];
                for (int y = 0; y < _sceneTexH; y++)
                {
                    byte* row = srcBase + y * rowPitch;
                    int dst = 0;
                    for (int x = 0; x < _sceneTexW; x++)
                    {
                        int si = x * 4; // BGRA
                        rgb[dst++] = row[si + 2];
                        rgb[dst++] = row[si + 1];
                        rgb[dst++] = row[si + 0];
                    }
                    fs.Write(rgb, 0, rgb.Length);
                }
                return true;
            }
            finally
            {
                _context->Unmap((ID3D11Resource*)staging, 0);
            }
        }
        finally
        {
            staging->Release();
        }
    }

    private void PrepareFullscreenPass(ID3D11RenderTargetView* target, int width, int height, ID3D11PixelShader* pixelShader)
    {
        ID3D11RenderTargetView* rtv = target;
        _context->OMSetRenderTargets(1, &rtv, null);
        _context->OMSetBlendState(null, null, 0xFFFFFFFFu);
        _context->OMSetDepthStencilState(null, 0);
        _context->RSSetState(null); // Ensure previous ImGui scissor-enabled state does not leak.
        var viewport = new Viewport(0, 0, (uint)Math.Max(1, width), (uint)Math.Max(1, height), 0f, 1f);
        _context->RSSetViewports(1, &viewport);
        _context->IASetInputLayout(null);
        _context->IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);
        _context->VSSetShader(_fullscreenVs, null, 0);
        _context->PSSetShader(pixelShader, null, 0);
        ID3D11SamplerState* sampler = _fullscreenSampler;
        _context->PSSetSamplers(0, 1, &sampler);
    }

}
