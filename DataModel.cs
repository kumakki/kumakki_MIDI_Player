using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kumakki_MIDI_Player
{
    public class DataModel
    {
        public byte[] ReadMIDI = [];
        //public TickData1[] Note1 = [];
        //public List<Track> Tracks = [];
        public byte NowTrackId = 126;
        public Dictionary<int, TickData2> Note = [];
        public int Count1 = 0;
        public int Count2 = 0;
        public int Count3 = 0;
        public int LoadingOffset = 0;
        public int AllDelta = 0;
        public int DrawMode = 0;
        public byte Color = 0;
        public short BPM = 0;
        public short BPQN = 0; // Beats Per Quarter Note
        public List<TempoPoint> TempoPoints = [];
        //public int LastTick = -1; // 最後に処理したtick
        public bool nextTrackHeader = true;
        public bool[] LastTick = [];
        public bool[] LastPitch  = new bool[16]; // 最後に処理したピッチベンドの状態（true: ピッチベンドが発生した、false: 発生していない）
        public SettingData SettingData = new SettingData();

        //新たな試み、ノートをセットで追加してみる
        //public Dictionary<int, TickData3> Note2 = [];
        public List<NoteOnBuf>[,] NoteBuf = new List<NoteOnBuf>[16, 128];
       
    }

    public class TickData1
    {
        public NoteData1[,] Note = new NoteData1[16, 128];
        public short Tempo = 0;
    }

    public struct NoteData1
    {
        public byte Status;
        public byte Count;
    }

    public class TickData2
    {
        //public List<NoteOnData> NoteOn = [];
        //public List<NoteOffData> NoteOff = [];
        public List<DataByte> BendRange = [];
        public List<DataByte> ProgramChange = [];
        public List<DataByte> Expression = [];
        public List<DataShort> Pitch = [];
        public List<NoteOnData> NoteOn = [];
        public List<NoteOffData> NoteOff = [];
    }

    public struct NoteOnData
    {
        //public short TrackId;
        public byte Ch;
        public byte Note;
        public byte Vel;
        //public short Count;
        //public byte Color;
        public byte TrackId;
    }

    

    public struct NoteOffData
    {
        //public short TrackId;
        public byte Ch;
        public byte Note;
        //public short Count;
        //public byte Color;
        public byte TrackId;
    }

    public class DataByte
    {
        public byte Ch;
        public byte Data;
    }

    public class DataShort
    {
        public byte Ch;
        public short Data;
    }

    public struct TempoPoint
    {
        public int Tick;      // テンポ変更が発生したtick
        public int Tempo;     // μs/四分音符
        public double ElapsedMs; // その時点での累積経過時間（ミリ秒）
    }

    public class NoteData
    {
        public short TrackId;
        public byte Ch;
        public byte Note;
        public byte Vel;
        public int Gate;
    }

    public struct NoteOnBuf
    {
        public int Delta;
        public byte Ch;
        public byte Note;
        public byte Vel;
        public byte TrackId;
    }

    public class  SettingData
    {
        //int frameRate = 143; // フレームレート
        public bool IgnoreOR = false;
        public bool IgnoreAND = false;
        public int IgnoreGateOR = 0; // ゲート無視時間（ms）
        public int IgnoreVelOR = 0;
        public int IgnoreGateAND = 0;
        public int IgnoreVelAND = 0;
        public bool ApplyIgnoreToDrawing = false;
        //int AudioMode = 2; // オーディオモード（0: 無効, 1: MIDI Out, 2: KDMAPI）
        //int MIDIDeviceId = 0;
        //List<string> MIDIDeviceList = new List<string>(); 
    }
}
