using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VirtualDesktopHelper.Configuration
{
    /// <summary>
    /// Protects configuration secrets with Windows DPAPI for the current user.
    /// A copied configuration file cannot be decrypted by another Windows account.
    /// </summary>
    internal static class DpapiSecretProtector
    {
        private const string Prefix = "dpapi:";
        private const uint CryptProtectUiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CryptProtectData(
            ref DataBlob input,
            string? description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            uint flags,
            out DataBlob output);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(
            ref DataBlob input,
            IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            uint flags,
            out DataBlob output);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);

        public static string Protect(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return string.Empty;
            }

            EnsureWindows();
            return Prefix + Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(secret)));
        }

        public static string Unprotect(string persistedSecret)
        {
            if (string.IsNullOrEmpty(persistedSecret) || !persistedSecret.StartsWith(Prefix, StringComparison.Ordinal))
            {
                // Legacy configuration values remain readable and are encrypted on the next save.
                return persistedSecret;
            }

            EnsureWindows();
            byte[] protectedBytes = Convert.FromBase64String(persistedSecret[Prefix.Length..]);
            return Encoding.UTF8.GetString(Unprotect(protectedBytes));
        }

        private static byte[] Protect(byte[] source) => Transform(source, (ref DataBlob input, out DataBlob output) =>
            CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output));

        private static byte[] Unprotect(byte[] source) => Transform(source, (ref DataBlob input, out DataBlob output) =>
            CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output));

        private static byte[] Transform(byte[] source, CryptTransform transform)
        {
            IntPtr inputMemory = Marshal.AllocHGlobal(source.Length);
            DataBlob input = new() { Size = source.Length, Data = inputMemory };
            try
            {
                Marshal.Copy(source, 0, inputMemory, source.Length);
                if (!transform(ref input, out DataBlob output))
                {
                    throw new InvalidOperationException($"Windows DPAPI failed with error code {Marshal.GetLastWin32Error()}.");
                }

                try
                {
                    byte[] result = new byte[output.Size];
                    Marshal.Copy(output.Data, result, 0, output.Size);
                    return result;
                }
                finally
                {
                    LocalFree(output.Data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(inputMemory);
            }
        }

        private static void EnsureWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Timely secrets can only be protected with Windows DPAPI on Windows.");
            }
        }

        private delegate bool CryptTransform(
            ref DataBlob input,
            out DataBlob output);
    }
}
