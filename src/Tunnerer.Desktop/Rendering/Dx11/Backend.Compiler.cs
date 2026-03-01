namespace Tunnerer.Desktop.Rendering.Dx11;

using System.Runtime.InteropServices;
using System.Text;

public sealed unsafe partial class Backend
{
    private ID3DBlob* CompileShader(string source, string entryPoint, string target)
    {
        byte[] srcBytes = Encoding.UTF8.GetBytes(source);
        byte[] entryBytes = Encoding.ASCII.GetBytes(entryPoint + "\0");
        byte[] targetBytes = Encoding.ASCII.GetBytes(target + "\0");
        fixed (byte* pSrc = srcBytes)
        fixed (byte* pEntry = entryBytes)
        fixed (byte* pTarget = targetBytes)
        {
            const uint D3DCOMPILE_ENABLE_STRICTNESS = 0x800;
            const uint D3DCOMPILE_OPTIMIZATION_LEVEL3 = 0x4000;
            uint flags = D3DCOMPILE_ENABLE_STRICTNESS | D3DCOMPILE_OPTIMIZATION_LEVEL3;
            ID3DBlob* code = null;
            ID3DBlob* errors = null;
            int hr = D3DCompile(
                pSrc, (nuint)srcBytes.Length,
                null, null, null,
                (sbyte*)pEntry, (sbyte*)pTarget,
                flags, 0,
                &code, &errors);
            if (hr < 0 || code == null)
            {
                if (errors != null)
                {
                    string msg = Marshal.PtrToStringAnsi((nint)BlobGetBufferPointer(errors)) ?? "unknown";
                    Console.WriteLine($"[Render] D3DCompile failed: {msg}");
                    BlobRelease(errors);
                }
                return null;
            }
            if (errors != null)
                BlobRelease(errors);
            return code;
        }
    }

    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int D3DCompile(
        byte* pSrcData,
        nuint srcDataSize,
        sbyte* pSourceName,
        void* pDefines,
        void* pInclude,
        sbyte* pEntryPoint,
        sbyte* pTarget,
        uint flags1,
        uint flags2,
        ID3DBlob** ppCode,
        ID3DBlob** ppErrorMsgs);

    private static void* BlobGetBufferPointer(ID3DBlob* blob)
    {
        var fn = (delegate* unmanaged[Stdcall]<ID3DBlob*, void*>)(blob->LpVtbl[3]);
        return fn(blob);
    }

    private static nuint BlobGetBufferSize(ID3DBlob* blob)
    {
        var fn = (delegate* unmanaged[Stdcall]<ID3DBlob*, nuint>)(blob->LpVtbl[4]);
        return fn(blob);
    }

    private static void BlobRelease(ID3DBlob* blob)
    {
        var fn = (delegate* unmanaged[Stdcall]<ID3DBlob*, uint>)(blob->LpVtbl[2]);
        _ = fn(blob);
    }

    private struct ID3DBlob
    {
        public void** LpVtbl;
    }

    private unsafe struct PostParamsCBuffer
    {
        public float TexelSizeX, TexelSizeY, PixelScale, Time;
        public float WorldSizeX, WorldSizeY, CameraPixelsX, CameraPixelsY;
        public float ViewSizeX, ViewSizeY, UseTerrainAux, BloomThreshold;
        public float BloomStrength, BloomWeightCenter, BloomWeightAxis, BloomWeightDiagonal;
        public float VignetteStrength, EdgeLightStrength, EdgeLightBias, TankHeatGlowR;
        public float TankHeatGlowG, TankHeatGlowB, TankHeatGlowA, TerrainHeatGlowR;
        public float TerrainHeatGlowG, TerrainHeatGlowB, TerrainHeatThreshold, TerrainMaskEdgeStrength;
        public float TerrainMaskCaveDarken,
                     TerrainMaskSolidLift,
                     TerrainMaskOutlineDarken,
                     TerrainMaskRimLift;
        public float TerrainMaskBoundaryScale, VignetteInnerRadius, VignetteOuterRadius, Quality;
        public float MaterialEnergyR, MaterialEnergyG, MaterialEnergyB, MaterialEnergyStrength;
        public float MaterialScorchedR, MaterialScorchedG, MaterialScorchedB, MaterialScorchedStrength;
        public float MaterialPulseFreq, MaterialPulseMin, MaterialPulseRange, MaterialPulsePad;
        public float NativeEdgeSoftness, NativeBoundaryBlend, NativeSampleFactor, NativePad;
        public float TankGlowCount, _pad0, _pad1, _pad2;
        public fixed float TankGlow[32];
    }
}
