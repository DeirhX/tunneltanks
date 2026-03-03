namespace Tunnerer.Desktop.Rendering.Dx11;

using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public sealed unsafe partial class Backend
{
    private static string GetShaderOutputPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", fileName);

    private static byte[]? LoadShaderBytecode(string shaderFileName)
    {
        string path = GetShaderOutputPath(shaderFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Render] Failed to read shader bytecode '{path}': {e.Message}");
            return null;
        }
    }

    private ID3DBlob* CompileShaderFromSourceFile(string shaderFileName, string entryPoint, string target)
    {
        string path = GetShaderOutputPath(shaderFileName);
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Render] Shader source missing: '{path}'.");
            return null;
        }

        try
        {
            return CompileShader(File.ReadAllText(path), entryPoint, target);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Render] Failed to compile shader source '{path}': {e.Message}");
            return null;
        }
    }

    private bool TryGetShaderBytecode(string shaderBaseName, string target, out byte[]? bytecode)
    {
        bytecode = LoadShaderBytecode($"{shaderBaseName}.cso");
        if (bytecode != null)
            return true;

#if DEBUG
        Console.WriteLine($"[Render] Missing '{shaderBaseName}.cso'; using Debug runtime compile fallback.");
        ID3DBlob* blob = CompileShaderFromSourceFile($"{shaderBaseName}.hlsl", "main", target);
        if (blob == null)
            return false;

        try
        {
            bytecode = new byte[(int)BlobGetBufferSize(blob)];
            Marshal.Copy((nint)BlobGetBufferPointer(blob), bytecode, 0, bytecode.Length);
            return true;
        }
        finally
        {
            BlobRelease(blob);
        }
#else
        Console.WriteLine($"[Render] Missing precompiled shader '{shaderBaseName}.cso'.");
        return false;
#endif
    }

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

    #pragma warning disable CS0649 // Interop structs are populated by native code / binary layout.
    private struct ID3DBlob
    {
        public void** LpVtbl;
    }
    #pragma warning restore CS0649
}
