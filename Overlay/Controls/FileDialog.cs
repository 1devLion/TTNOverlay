using System.Runtime.InteropServices;

namespace TTNOverlay.Overlay;

/// <summary>
/// P/Invoke wrapper around the Win32 common file-open dialog.
/// </summary>
internal static class FileDialog
{
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;

    private const int OFN_NOCHANGEDIR = 0x00000008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAME lpofn);

    private const string Filter = "Archivos WAV (*.wav)\0*.wav\0\0";

    private const string ImageFilter = "Images (*.gif;*.png;*.jpg;*.jpeg;*.webp)\0*.gif;*.png;*.jpg;*.jpeg;*.webp\0\0";

    private const int MaxPathBufferChars = 1024;

    public static string? PickWavFile(IntPtr ownerHwnd) => PickFile(ownerHwnd, Filter);

    public static string? PickImageFile(IntPtr ownerHwnd) => PickFile(ownerHwnd, ImageFilter);

    private static string? PickFile(IntPtr ownerHwnd, string filter)
    {
        var buffer = Marshal.AllocHGlobal(MaxPathBufferChars * sizeof(char));
        try
        {
            Marshal.WriteInt16(buffer, 0);

            var ofn = new OPENFILENAME
            {
                hwndOwner = ownerHwnd,
                lpstrFilter = filter,
                lpstrFile = buffer,
                nMaxFile = MaxPathBufferChars,
                Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
            };
            ofn.lStructSize = Marshal.SizeOf<OPENFILENAME>();

            return GetOpenFileNameW(ref ofn) ? Marshal.PtrToStringUni(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}