using System.Runtime.InteropServices;

public class MidiOutWinMM
{
    [DllImport("winmm.dll")]
    public static extern int midiOutOpen(out IntPtr handle, int deviceID, IntPtr callback, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    public static extern int midiOutShortMsg(IntPtr handle, int message);

    [DllImport("winmm.dll")]
    public static extern int midiOutClose(IntPtr handle);

    [DllImport("winmm.dll")]
    public static extern int midiOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    public static extern int midiOutGetDevCaps(int uDeviceID, ref MIDIOUTCAPS lpMidiOutCaps, int cbMidiOutCaps);

    [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitializeKDMAPIStream();

    [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CloseKDMAPIStream();

    [DllImport("OmniMIDI.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SendDirectData(uint dwMsg);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MIDIOUTCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public ushort wTechnology;
        public ushort wVoices;
        public ushort wNotes;
        public ushort wChannelMask;
        public uint dwSupport;
    }

    public static List<string> GetMidiOutDeviceNames()
    {
        int numDevs = midiOutGetNumDevs();
        var names = new List<string>();
        for (int i = 0; i < numDevs; i++)
        {
            MIDIOUTCAPS caps = new MIDIOUTCAPS();
            midiOutGetDevCaps(i, ref caps, Marshal.SizeOf(typeof(MIDIOUTCAPS)));
            names.Add($"{i}: {caps.szPname}");
        }
        return names;
    }
}
