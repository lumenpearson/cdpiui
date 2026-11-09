using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Shared.Security;

/// <summary>Uses the same Windows catalog hash as Test-FileCatalog, including PE hashing rules.</summary>
internal sealed class WindowsCatalogHash : IDisposable
{
    private readonly CatalogContext _context;
    private readonly int _hashBytes;

    public WindowsCatalogHash(string algorithm)
    {
        _hashBytes = algorithm == "SHA256" ? 32 : algorithm == "SHA1" ? 20 : throw new ArgumentException("Unsupported catalog hash.", nameof(algorithm));
        if (!CryptCATAdminAcquireContext2(out _context, IntPtr.Zero, algorithm, IntPtr.Zero, 0))
        {
            _context?.Dispose();
            throw new IOException("Cannot acquire the catalog hash context.", Marshal.GetHRForLastWin32Error());
        }
    }

    public string Compute(FileStream stream)
    {
        // New-FileCatalog represents empty files by an empty catalog-level attribute.
        if (stream.Length == 0) return string.Empty;
        long position = stream.Position;
        try
        {
            stream.Position = 0;
            byte[] hash = new byte[_hashBytes];
            uint size = (uint)hash.Length;
            if (!CryptCATAdminCalcHashFromFileHandle2(_context, stream.SafeFileHandle, ref size, hash, 0) || size != hash.Length)
                throw new IOException("Cannot hash the held catalog member.", Marshal.GetHRForLastWin32Error());
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
        finally { stream.Position = position; }
    }

    public void Dispose() => _context.Dispose();

    private sealed class CatalogContext : SafeHandleZeroOrMinusOneIsInvalid
    {
        public CatalogContext() : base(true) { }
        protected override bool ReleaseHandle() => CryptCATAdminReleaseContext(handle, 0);
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(out CatalogContext context, IntPtr subsystem, string algorithm, IntPtr policy, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(CatalogContext context, SafeFileHandle file, ref uint size, [Out] byte[] hash, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr context, uint flags);
}
