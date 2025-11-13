using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.PerformanceData;


//using System.Drawing;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static kumakki_MIDI_Player.MidiEditer;



namespace kumakki_MIDI_Player
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DataModel data = new DataModel();
        int drawMode = 1;
        int device = 0;
        Rectangle[,] keyRects = new Rectangle[16, 128]; // キーの矩形を保持する3次元配列
        double lateTime = 0;
        bool pause = false;
        bool initialized = false;
        double bpm = 0;
        int frameCount = 0;
        DateTime lastFrameTime = DateTime.Now;
        bool stop = false;
        IntPtr midiHandle;
        bool isPlaying = false; // 再生中かどうか
        int[,,] buf = new int[16, 128, 16]; //描画用バッファ //再生中のノーツ
        int[,] bufLast = new int[16, 128];
        byte[,] bufLastBar = new byte[128, 541];
        byte[] bufLastNote = new byte[128];
        Color[,] keyColors = new Color[16, 17]; // キーの色を保持する配列
        Color[] keyColors2 = new Color[256];
        long[] noteCount = [];
        bool loading = false;
        int[] tickToTime = [];
        int[] timeToTick = [];
        double[] tickBPM = [];
        long loadingByte = 0;
        bool sliderChanging = false; // スライダーの値が変更中かどうか
        bool sliderChanged = false; // スライダーの値が変更されたかどうか
        int sliderChangeValueBefore = 0;
        int sliderChangeValueAfter = 0;
        int lastCount = 1;
        KeyRectInfo[,] keyRectsInfo = new KeyRectInfo[16, 128]; // キーの位置を保持する配列

        WriteableBitmap bmpWhite = new WriteableBitmap(1201, 577, 96, 96, PixelFormats.Bgra32, null);
        WriteableBitmap bmpBlack = new WriteableBitmap(1201, 577, 96, 96, PixelFormats.Bgra32, null);
        int sliderSetValue = 0;
        int playMode = 2;   //再生モードの変更、0: 音なし, 1: MIDIOut送信, 2: KDMAPI送信, 3: 自前再生
        long lastNoteCount = 0; // 最後のノート数
        int loadingKbn = 0;
        double basisMs = 40; //1ピクセルあたり1ミリ秒とする
        double speed = 4.25;
        SettingData settingData = new SettingData();
        int frameRate = 60;
        int baseRenderingTime = 0;

        int whiteKeyWidth = 0;
        int whiteKeyHeight = 0;

        byte[][] pixelsBlack = new byte[36][];
        byte[][] pixelsWhite = new byte[36][];


        byte[,] viewData = new byte[0, 0];


        int[] keyPositions = [];

        public MainWindow()
        {
            InitializeComponent();
            Loaded += WPFLoad;
            //CompositionTarget.Rendering += OnRendering;
        }

        private void WPFLoad(object sender, RoutedEventArgs e)
        {
            // 初期化処理などが必要であればここに記述
            bool mode2 = false;
            playMode = 0;
            drawMode = 1;

            double opacity = 0.01; // デフォルトの透明度

            //設定ファイル読み込み
            if (System.IO.File.Exists("config.txt"))
            {
                foreach (string line in System.IO.File.ReadAllLines("config.txt"))
                {
                    try
                    {
                        if (line.Length > 6 && line.Substring(0, 6) == "Speed=")
                        {
                            double buf = Double.Parse(line.Substring(6, line.Length - 6).Trim());
                            if (buf > 8)
                            {
                                speed = 8;
                                Speed.Text = $"Speed: 8";
                                Slider_Speed.Value = 0.25;
                                basisMs = 800;
                            }
                            else if (buf < 0.25)
                            {
                                speed = 0.25;
                                Speed.Text = $"Speed: 0.25";
                                Slider_Speed.Value = 8;
                                basisMs = 25;
                            }
                            else
                            {
                                speed = buf;
                                Speed.Text = $"Speed: {buf}";
                                Slider_Speed.Value = 8.25 - buf;
                                basisMs = buf * 100;
                            }
                        }
                        else if (line.Length > 9 && line.Substring(0, 9) == "DrawMode=")
                        {
                            int buf = Int32.Parse(line.Substring(9, line.Length - 9).Trim());
                            if (buf == 0 || buf == 1)
                            {
                                drawMode = buf;
                            }
                        }
                        else if (line.Length > 10 && line.Substring(0, 10) == "AudioMode=")
                        {
                            int buf = Int32.Parse(line.Substring(10, line.Length - 10).Trim());
                            if (buf == 0 || buf == 1 || buf == 2)
                            {
                                playMode = buf;
                            }
                        }
                        else if (line.Length > 12 && line.Substring(0, 12) == "WindowWidth=")
                        {
                            int buf = Int32.Parse(line.Substring(12, line.Length - 12).Trim());
                            if (buf > 0)
                            {
                                KMPWindow.Width = buf;
                            }
                        }
                        else if (line.Length > 13 && line.Substring(0, 13) == "WindowHeight=")
                        {
                            int buf = Int32.Parse(line.Substring(13, line.Length - 13).Trim());
                            if (buf > 0)
                            {
                                KMPWindow.Height = buf;
                            }
                        }
                        else if (line.Length > 10 && line.Substring(0, 10) == "FrameRate=")
                        {
                            int buf = Int32.Parse(line.Substring(10, line.Length - 10).Trim());
                            if (buf > 0)
                            {
                                frameRate = buf;
                            }
                        }
                        //else if (line.Length > 16 && line.Substring(0, 6) == "MIDIOutDeviceId=")
                        //{
                        //    int buf = Int32.Parse(line.Substring(16, line.Length - 6).Trim());
                        //}
                        else if (line.Length > 12 && line.Substring(0, 12) == "Debug:Mode2=")
                        {
                            int buf = Int32.Parse(line.Substring(12, line.Length - 12).Trim());
                            if (buf == 0)
                            {
                                mode2 = false;
                            }
                            else if (buf == 1)
                            {
                                mode2 = true;
                            }
                        }
                        else if (line.Length > 14 && line.Substring(0, 14) == "Debug:Opacity=")
                        {
                            double buf = Double.Parse(line.Substring(14, line.Length - 14).Trim());
                            opacity = buf;
                        }
                        else if (line.Length > 9 && line.Substring(0, 9) == "ZoomMode=")
                        {
                            int buf = Int32.Parse(line.Substring(9, line.Length - 9).Trim());
                            if (buf == 0)
                            {
                                KeyImageBlack.Stretch = Stretch.None;
                                KeyImageWhite.Stretch = Stretch.None;
                            }
                            else if (buf == 1)
                            {
                                KeyImageBlack.Stretch = Stretch.Fill;
                                KeyImageWhite.Stretch = Stretch.Fill;
                            }
                        }
                    }
                    catch
                    {

                    }
                }
            }


            //再生ボタンなどの初期化
            btn_Load.IsEnabled = true;
            btn_Play.IsEnabled = false;
            btn_Stop.IsEnabled = false;
            btn_Pause.IsEnabled = false;
            NPS.Text = "NPS: 0"; // NPSをリセット
            NoteCount.Text = "Notes: 0"; // ノート数をリセット
            Time.Text = "Time: 0:00"; // 経過時間をリセット
            nowTick.Text = "0 ticks"; // 現在のTickをリセット
            lateTimeText.Text = "Latency: 0.000s"; // 遅延時間をリセット
            BPM.Text = "BPM: 0"; // BPMをリセット
            bpqn.Text = "BPQN: 0"; // BPQNをリセット

            if (mode2)
            {
                KeyImageWhite.Opacity = 1; // キーイメージの透明度を設定
                KeyImageBlack.Opacity = 1; // キーイメージの透明度を設定
                ViewGrid.Opacity = opacity;
            }
            else
            {
                btn_Play.Content = "▶";
                btn_Stop.Content = "■";
                btn_Pause.Content = "||"; // 一時停止ボタンの内容を設定
                btn_Setting.Content = "⚙"; // 一時停止ボタンの内容を設定
            }

            whiteKeyWidth = (int)(ViewGrid.ActualWidth / 75);
            whiteKeyHeight = (int)(ViewGrid.ActualHeight / 16);
            bufLastBar = new byte[128, whiteKeyHeight * 15 + 1];

            //カラーの初期化（ランダム）
            if (drawMode == 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    // Replace the problematic line with the following code:
                    for (int j = 0; j < 16; j++)
                    {
                        // ランダムな色を生成
                        keyColors[i, j] = Color.FromArgb(255, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256));

                    }
                    keyColors[i, 16] = Colors.White;
                }
                Speed.Visibility = Visibility.Hidden; // スピードスライダーを非表示
                Slider_Speed.Visibility = Visibility.Hidden; // スピードスライダーを非表示
            }
            else if (drawMode == 1)
            {
                pixelsBlack = new byte[whiteKeyHeight][];
                pixelsWhite = new byte[whiteKeyHeight][];
                for (int i = 0; i < whiteKeyHeight; i++)
                {
                    pixelsWhite[i] = new byte[(whiteKeyWidth * 75 + 1) * 15 * 4];
                    pixelsBlack[i] = new byte[(whiteKeyWidth * 75 + 1) * 15 * 4];
                }

                for (int j = 0; j < 127; j++)
                {
                    keyColors2[j] = RandomColor(); ; // 初期化
                    keyColors2[j | 128] = Color.FromArgb(255, (byte)(keyColors2[j].R * 0.2f), (byte)(keyColors2[j].G * 0.2f), (byte)(keyColors2[j].B * 0.2f));
                }
                keyColors2[127] = Colors.Transparent; // 最後の色を白に設定
            }

            bmpWhite = new WriteableBitmap(whiteKeyWidth * 75 + 1, whiteKeyHeight * 16 + 1, 96, 96, PixelFormats.Bgra32, null);
            bmpBlack = new WriteableBitmap(whiteKeyWidth * 75 + 1, whiteKeyHeight * 16 + 1, 96, 96, PixelFormats.Bgra32, null);
            KeyImageWhite.Source = bmpWhite; // XAML側の<Image x:Name="KeyImage" />に表示
            KeyImageBlack.Source = bmpBlack; // XAML側の<Image x:Name="KeyImage" />に表示



            keyPositions = [
                0, whiteKeyWidth - 7, whiteKeyWidth, whiteKeyWidth * 2 - 5, whiteKeyWidth * 2, whiteKeyWidth * 3, whiteKeyWidth * 4 - 6, whiteKeyWidth * 4, whiteKeyWidth * 5 - 5, whiteKeyWidth * 5, whiteKeyWidth * 6 - 3, whiteKeyWidth * 6
            ];

            keyPositions = [
                0, whiteKeyWidth - (int)(whiteKeyWidth / 2.28), whiteKeyWidth, whiteKeyWidth * 2 - (int)(whiteKeyWidth / 3.2), whiteKeyWidth * 2, whiteKeyWidth * 3, whiteKeyWidth * 4 - (int)(whiteKeyWidth / 2.0), whiteKeyWidth * 4, whiteKeyWidth * 5 - (int)(whiteKeyWidth / 2.66), whiteKeyWidth * 5, whiteKeyWidth * 6 - (int)(whiteKeyWidth / 4), whiteKeyWidth * 6
            ];


            for (byte ch = 0; ch < 16; ch++)
            {
                for (byte note = 0; note < 128; note++)
                {
                    int keyIndex = note % 12; // 0-11の範囲に変換
                    int octave = (int)Math.Floor(note / 12.0); // オクターブを計算
                    int keyPositionX = octave * whiteKeyWidth * 7 + keyPositions[keyIndex]; // キーのX位置を取得
                    int keyPositionY = ch * whiteKeyHeight; // キーのY位置を取得（0-15の範囲）

                    if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                    {
                        // 白鍵の位置
                        keyRectsInfo[ch, note] = new KeyRectInfo()
                        {
                            X = keyPositionX,
                            Y = keyPositionY,
                            Width = whiteKeyWidth,
                            Height = whiteKeyHeight
                        };
                    }
                    else
                    {
                        // 黒鍵の位置（白鍵の上に配置）
                        keyRectsInfo[ch, note] = new KeyRectInfo()
                        {
                            X = keyPositionX, // 黒鍵は白鍵の右側に配置
                            Y = keyPositionY, // 黒鍵は白鍵の上に配置
                            Width = (int)(whiteKeyWidth / 1.33),
                            Height = (int)(whiteKeyHeight / 1.71)
                        };
                    }
                    //最初は白塗りつぶしで初期化
                }
            }


            var deviceNames = MidiOutWinMM.GetMidiOutDeviceNames();
            string msg = string.Join("\n", deviceNames);
            //MessageBox.Show(msg, "MIDIデバイス一覧");

            if (playMode == 0)
            {
                // 音なしモードの場合は何もしない
                StatusText.Text = "Status: No sound mode selected";
            }
            else if (playMode == 1)
            {
                StatusText.Text = "Status: MIDIOut mode selected";
                int result = MidiOutWinMM.midiOutOpen(out midiHandle, device, IntPtr.Zero, IntPtr.Zero, 0); // 0はデバイス番号
            }
            else if (playMode == 2)
            {
                StatusText.Text = "Status: KDMAPI mode selected";
                int result = MidiOutWinMM.InitializeKDMAPIStream(); // KDMAPIを初期化
            }



            Slider_PlayTime.Maximum = 0;
            Slider_PlayTime.Value = 0;

            initialized = true;

            RenderingLoop();

        }

        private async void RenderingLoop()
        {
            double frameInterval = 1000.0 / frameRate;
            DateTime nextFrame = DateTime.Now;

            while (true)
            {
                OnRendering();

                nextFrame = nextFrame.AddMilliseconds(frameInterval);
                int waitMs = (int)(nextFrame - DateTime.Now).TotalMilliseconds;
                if (waitMs > 0)
                {
                    await Task.Delay(waitMs);
                }
                else
                {
                    // 遅延時は次フレーム予定を現在時刻にリセット

                    await Task.Delay(1);
                    nextFrame = DateTime.Now;
                }
            }
        }

        private void OnClick_Play(object sender, RoutedEventArgs e)
        {
            btn_Load.IsEnabled = false;
            btn_Play.IsEnabled = false;
            btn_Stop.IsEnabled = true;
            btn_Pause.IsEnabled = true;
            StatusText.Text = "Status: Playing";

            //停止中からの再生か一時停止中からの再生かで処理を分ける
            if (pause)
            {
                pause = false;
            }
            else
            {
                // 再生開始時

                isPlaying = true;
                _ = Task.Run(() => MIDIPlaying());

            }
        }

        private async Task MIDIPlaying()
        {

            try
            {
                data.Count2 = 0;

                if (drawMode == 0)
                {                  //描画用バッファの初期化
                    for (int i = 0; i < 16; i++)
                    {
                        for (int j = 0; j < 128; j++)
                        {
                            for (int k = 0; k < 16; k++)
                            {
                                buf[i, j, k] = 0; //初期化
                            }
                            bufLast[i, j] = 17; //初期化
                        }
                    }
                }

                //最初に基準となる開始時間を取得
                DateTime startTime = DateTime.Now;

                int maxTick = data.Note.Keys.Max();

                //for (data.Count2 = 0; data.Count2 <= maxTick; data.Count2++)
                while (data.Count2 < maxTick)
                {
                    //再生処理

                    if (sliderChanged)
                    {
                        sliderChanged = false;

                        AllNoteOff(); //全ノートオフを送信

                        startTime = DateTime.Now - TimeSpan.FromMilliseconds(sliderChangeValueAfter); // スタートタイムを更新
                        data.Count2 = timeToTick[sliderChangeValueAfter * 100] / 100; // チックを更新

                    }

                    if (pause)
                    {
                        //一時停止開始
                        DateTime startPause = DateTime.Now;

                        while (pause)
                        {
                            //一時停止中は何もしない
                            await Task.Delay(10); // 100ms待機

                            if (stop)
                            {
                                //再生停止
                                lateTime = 0;
                                stop = false;
                                AllNoteOff(); //全ノートオフを送信
                                data.Count2 = 0;
                                baseRenderingTime = 0;
                                bpm = 0;
                                isPlaying = false; // 再生中フラグを更新
                                return;
                            }

                            if (sliderChanged)
                            {
                                sliderChanged = false;

                                AllNoteOff(); //全ノートオフを送信

                                data.Count2 = timeToTick[sliderChangeValueAfter * 100] / 100; // チックを更新
                            }
                        }
                        isPlaying = true;
                        startTime = DateTime.Now - TimeSpan.FromMilliseconds(tickToTime[data.Count2 * 100] / 100); // スタートタイムを更新
                    }

                    if (stop)
                    {
                        //再生停止
                        lateTime = 0;
                        stop = false;
                        AllNoteOff(); //全ノートオフを送信
                        data.Count2 = 0;
                        baseRenderingTime = 0;

                        bpm = 0;
                        isPlaying = false; // 再生中フラグを更新
                        return;
                    }

                    baseRenderingTime = (int)((DateTime.Now - startTime).TotalMicroseconds / 10);

                    // 現在tickに対応する経過msを計算
                    //double targetMs = GetElapsedMsFromTick(data);   //目標経過時間
                    double targetMs = tickToTime[data.Count2 * 100] / 100;
                    double elapsedMs = (DateTime.Now - startTime).TotalMilliseconds; //実際の経過時間
                                                                                     //目標経過時間に達するまで待機

                    //もし目標経過時間に間に合ってない場合、遅延時間を記録する
                    lateTime = (elapsedMs - targetMs) / 1000; //遅延時間を記録（秒）

                    if (elapsedMs < targetMs)
                    {
                        //目標時間に達していない場合は待機
                        await Task.Delay((int)(targetMs - elapsedMs));
                    }
                    else
                    {
                        baseRenderingTime -= (int)(lateTime * 1000000);
                    }



                    //await Task.Delay(waitMs);

                    //現在チックにデータがあれば処理する
                    if (data.Note.TryGetValue(data.Count2, out var tickData))
                    {
                        //プログラムチェンジの処理
                        if (tickData.ProgramChange.Count > 0)
                        {
                            foreach (var programChange in tickData.ProgramChange)
                            {
                                if (playMode == 0)
                                {
                                    continue; // 音なしモードの場合は何もしない
                                }
                                else if (playMode == 1)
                                {
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, 0xC0 | programChange.Ch | (programChange.Data << 8)); // プログラムチェンジメッセージを送信
                                }
                                else if (playMode == 2)
                                {
                                    MidiOutWinMM.SendDirectData((uint)(0xC0 | programChange.Ch | (programChange.Data << 8))); // プログラムチェンジメッセージを送信
                                }
                            }
                        }

                        // エクスプレッションの処理
                        if (tickData.Expression.Count > 0)
                        {
                            foreach (var expression in tickData.Expression)
                            {
                                if (playMode == 0)
                                {
                                    continue; // 音なしモードの場合は何もしない
                                }
                                else if (playMode == 1)
                                {
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, 0xB0 | expression.Ch | (0x0B << 8) | (expression.Data << 16)); // エクスプレッションメッセージを送信
                                }
                                else if (playMode == 2)
                                {
                                    MidiOutWinMM.SendDirectData((uint)(0xB0 | expression.Ch | (0x0B << 8) | (expression.Data << 16))); // エクスプレッションメッセージを送信
                                }
                            }
                        }


                        // ノートオフの処理
                        if (tickData.NoteOff.Count > 0)
                        {
                            for (int count = 0; count < tickData.NoteOff.Count; count++)
                            {
                                var noteOff = tickData.NoteOff[count]; // ノートオフデータを取得

                                //ノートの高さの最上位ビットが1なら無視
                                if ((noteOff.Note & 0x80) != 0)
                                {
                                    continue; // 無視
                                }

                                if (drawMode == 0)
                                {
                                    ////ノートオフの処理
                                    if (buf[noteOff.Ch, noteOff.Note, noteOff.TrackId] > 0)
                                    {
                                        buf[noteOff.Ch, noteOff.Note, noteOff.TrackId] -= 1;
                                    }
                                }

                                //モードによって再生方法を変更
                                if (playMode == 0)
                                {
                                    continue; // 音なしモードの場合は何もしない
                                }
                                else if (playMode == 1)
                                {
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, noteOff.Note << 8 | 0x80 | noteOff.Ch); // ノートオフメッセージを送信
                                }
                                else if (playMode == 2)
                                {
                                    MidiOutWinMM.SendDirectData((uint)(noteOff.Note << 8 | 0x80 | noteOff.Ch)); // ノートオフメッセージを送信
                                }
                            }
                        }

                        //ノートオンの処理
                        if (tickData.NoteOn.Count > 0)
                        {
                            //foreach (var noteOn in tickData.NoteOn)
                            for (int count = 0; count < tickData.NoteOn.Count; count++)
                            {
                                var noteOn = tickData.NoteOn[count]; // ノートオフデータを取得

                                //ノートの高さの最上位ビットが1なら無視
                                if ((noteOn.Note & 0x80) != 0)
                                {
                                    continue; // 無視
                                }

                                if (drawMode == 0)
                                {
                                    buf[noteOn.Ch, noteOn.Note, noteOn.TrackId] += 1;
                                }

                                if (playMode == 0)
                                {
                                    continue; // 音なしモードの場合は何もしない
                                }
                                else if (playMode == 1)
                                {
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, noteOn.Vel << 16 | noteOn.Note << 8 | 0x90 | noteOn.Ch); // ノートオンメッセージを送信
                                }
                                else if (playMode == 2)
                                {
                                    MidiOutWinMM.SendDirectData((uint)(noteOn.Vel << 16 | noteOn.Note << 8 | 0x90 | noteOn.Ch)); // ノートオンメッセージを送信
                                }
                            }
                        }

                        //ピッチベンド幅の処理
                        if (tickData.BendRange.Count > 0)
                        {
                            foreach (var bend in tickData.BendRange)
                            {
                                // ピッチベンドの処理
                                if (playMode == 0)
                                {
                                    continue; // 音なしモードの場合は何もしない
                                }
                                else if (playMode == 1)
                                {
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, 0xB0 | bend.Ch | (0x65 << 8) | (0x00 << 16)); // RPN MSB
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, 0xB0 | bend.Ch | (0x64 << 8) | (0x00 << 16)); // RPN LSB
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, 0xB0 | bend.Ch | (0x06 << 8) | (bend.Data << 16)); // Data Entry
                                }
                                else if (playMode == 2)
                                {
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, (int)(0xB0 | bend.Ch | (0x65 << 8) | (0x00 << 16))); // RPN MSB
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, (int)(0xB0 | bend.Ch | (0x64 << 8) | (0x00 << 16))); // RPN LSB
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, (int)(0xB0 | bend.Ch | (0x06 << 8) | (bend.Data << 16))); // Data Entry
                                }
                            }
                        }

                        //ピッチ変更の処理 
                        if (tickData.Pitch.Count > 0)
                        {
                            foreach (var pitch in tickData.Pitch)
                            {
                                if (playMode == 0)
                                {
                                    continue; // 音なしモードの場合は何もしない
                                }
                                else if (playMode == 1)
                                {
                                    int value = pitch.Data;
                                    int lsb = value & 0x7F;
                                    int msb = (value >> 7) & 0x7F;
                                    int msg = (0xE0 | pitch.Ch) | (lsb << 8) | (msb << 16);
                                    MidiOutWinMM.midiOutShortMsg(midiHandle, msg); // ピッチ変更メッセージを送信
                                }
                                else if (playMode == 2)
                                {
                                    int value = pitch.Data;
                                    int lsb = value & 0x7F;
                                    int msb = (value >> 7) & 0x7F;
                                    int msg = (0xE0 | pitch.Ch) | (lsb << 8) | (msb << 16);
                                    MidiOutWinMM.SendDirectData((uint)msg); // ピッチ変更メッセージを送信
                                }
                            }
                        }
                    }
                    data.Count2++; // チックを進める
                }
                // 再生終了時の処理
                lateTime = 0;
                AllNoteOff(); //全ノートオフを送信
                data.Count2 = 0;
                baseRenderingTime = 0;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    btn_Load.IsEnabled = true;
                    btn_Play.IsEnabled = true;
                    btn_Stop.IsEnabled = false;
                    btn_Pause.IsEnabled = false;
                    Slider_PlayTime.Value = tickToTime[data.Count2];
                    NoteCount.Text = "Notes: 0"; // ノート数をリセット
                    Time.Text = "Time: 0:00"; // 経過時間をリセット
                    nowTick.Text = "0 ticks"; // 現在のTickをリセット
                    lateTimeText.Text = "Latency: 0.000s"; // 遅延時間をリセット
                    BPM.Text = "BPM: 0"; // BPMをリセット
                    StatusText.Text = "Status: Stopped";
                });
                pause = false;
                stop = false;
                bpm = 0;
                isPlaying = false;
            }
            catch (Exception ex)
            {
                // 例外が発生した場合の処理
                MessageBox.Show($"{data.Count2} An unknown error has occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 再生終了時の処理
                lateTime = 0;
                AllNoteOff(); //全ノートオフを送信
                data.Count2 = 0;
                baseRenderingTime = 0;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    btn_Load.IsEnabled = true;
                    btn_Play.IsEnabled = true;
                    btn_Stop.IsEnabled = false;
                    btn_Pause.IsEnabled = false;
                    Slider_PlayTime.Value = tickToTime[data.Count2];
                    NoteCount.Text = "Notes: 0"; // ノート数をリセット
                    Time.Text = "Time: 0:00"; // 経過時間をリセット
                    nowTick.Text = "0 ticks"; // 現在のTickをリセット
                    lateTimeText.Text = "Latency: 0.000s"; // 遅延時間をリセット
                    BPM.Text = "BPM: 0"; // BPMをリセット
                    StatusText.Text = "Status: Stopped";
                });
                pause = false;
                stop = false;
                bpm = 0;
                isPlaying = false;
            }
        }


        private void AllNoteOff()
        {
            //全チャンネルの全ノートオフを送信
            for (int ch = 0; ch < 16; ch++)
            {
                for (int note = 0; note < 128; note++)
                {
                    for (int color = 0; color < 16; color++)
                    {
                        buf[ch, note, color] = 0; // バッファをリセット
                    }
                    bufLast[ch, note] = 17; // バッファをリセット

                    if (playMode == 0)
                    {
                        continue; // 音なしモードの場合は何もしない
                    }
                    else if (playMode == 1)
                    {
                        MidiOutWinMM.midiOutShortMsg(midiHandle, note << 8 | 0x80 | ch); // ノートオフメッセージを送信
                    }
                    else if (playMode == 2)
                    {
                        MidiOutWinMM.SendDirectData((uint)(note << 8 | 0x80 | ch)); // ノートオフメッセージを送信
                    }
                }
            }
        }

        //private void OnRendering(object? sender, EventArgs e)
        private void OnRendering()
        {
            if (initialized == false) return;

            if (loading)
            {
                switch (loadingKbn)
                {
                    case 0:
                        if (loadingByte < 1000)
                        {
                            StatusText.Text = $"Status: Loading... {loadingByte} B";
                        }
                        else if (loadingByte < 1024 * 1000)
                        {
                            StatusText.Text = $"Status: Loading... {loadingByte / 1024.0:F2} KB";
                        }
                        else if (loadingByte < 1024 * 1024 * 1000)
                        {
                            StatusText.Text = $"Status: Loading... {loadingByte / (1024.0 * 1024.0):F2} MB";
                        }
                        else
                        {
                            StatusText.Text = $"Status: Loading... {loadingByte / (1024.0 * 1024.0 * 1024.0):F2} GB";
                        }
                        break;
                    case 1:
                        StatusText.Text = "Status: Calculating Tick Time...";
                        break;
                    case 2:
                        StatusText.Text = "Status: Counting Notes...";
                        break;
                    case 3:
                        //全てのトラックのなかで最大のTickを取得
                        int maxTick = data.Note.Keys.Max();
                        StatusText.Text = $"Status: Creating View Data... {data.Count3} / {maxTick}";
                        break;
                    case 4:
                        maxTick = data.Note.Keys.Max();
                        StatusText.Text = $"Status; Applying Ignore Filter... {data.Count3} / {maxTick}";
                        break;
                }
            }

            string test1 = "";

            if (drawMode == 0 && loading == false)
            {
                for (byte i = 0; i < 16; i++)
                {
                    for (byte j = 0; j < 128; j++)
                    {
                        //ool noteOn = false;
                        byte color = 16;
                        var rect = keyRects[i, j];
                        for (byte k = 15; k < 16; k--)
                        {
                            if (buf[i, j, k] > 0)
                            {
                                //noteOn = true; // ノートオン状態
                                color = k;
                                break;
                            }
                        }

                        //ノートのオンオフによってキーの表示非表示を切り替える
                        if (bufLast[i, j] != color)
                        {
                            bufLast[i, j] = color;
                            var keyInfo = keyRectsInfo[i, j];
                            int w = keyInfo.Width, h = keyInfo.Height;
                            int stride = w * 4;
                            byte[] pixels = new byte[w * h * 4];

                            for (int y = 0; y < h; y++)
                            {
                                for (int x = 0; x < w; x++)
                                {
                                    bool isBorder = (x == 0 || y == 0 || x == w - 1 || y == h - 1);
                                    Color c = isBorder ? Colors.Black : keyColors[i, color];
                                    int idx = (y * w + x) * 4;
                                    pixels[idx + 0] = c.B;
                                    pixels[idx + 1] = c.G;
                                    pixels[idx + 2] = c.R;
                                    pixels[idx + 3] = c.A;

                                    //GL.BindTexture(TextureTarget.Texture2D, textureId);
                                    //GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, pixelsWhite);
                                }
                            }

                            int keyIndex = j % 12;
                            if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                            {
                                // 白鍵
                                bmpWhite.WritePixels(
                                    new Int32Rect(keyInfo.X, keyInfo.Y, w, h),
                                    pixels, stride, 0
                                );
                            }
                            else
                            {
                                // 黒鍵
                                bmpBlack.WritePixels(
                                    new Int32Rect(keyInfo.X, keyInfo.Y, w, h),
                                    pixels, stride, 0
                                );
                            }
                        }
                    }
                }
            }
            else if (drawMode == 1 && loading == false)
            {
                int nowTick = data.Count2;
                int maxIndex = whiteKeyHeight * 15 - 1;
                int viewDataLength = viewData.GetLength(0);

                int barHeight = whiteKeyHeight * 15;




                bool[] updateWhite = new bool[whiteKeyHeight]; // 白鍵の更新フラグ
                bool[] updateBlack = new bool[whiteKeyHeight]; // 黒鍵の更新フラグ

                for (int i = 0; i < whiteKeyHeight; i++)
                {
                    updateWhite[i] = false;
                    updateBlack[i] = false;
                }
                test1 = "";


                int maxTickToTime = tickToTime.Length - 1;
                int maxTimeToTick = timeToTick.Length - 1;

                Parallel.For(0, whiteKeyHeight * 15, iRaw =>
                //for (int iRaw = whiteKeyHeight * 15 - 1; iRaw >= 0; iRaw--)
                {
                    int i = iRaw; // 逆順にしたい場合
                    int sbn = i / 15; // 0-whiteKeyHeightの範囲に変換
                    int sbnY = i % 15; // 0-15の範囲に変換

                    //デバッグ
                    //int test = whiteKeyHeight * 15 - 1;
                    //if (i != test)
                    //{
                    //    continue;
                    //}


                    int nowIndex = 0;
                    int nextIndex = 0;
                    int backIndex = 0;
                    bool drawWhite = false;
                    //ミリ秒ベースのスピード

                    int targetMs = 0;
                    int targetMs2 = 0;
                    int targetMs3 = 0;
                    int drawType = 0;


                    if (tickToTime.Length <= nowTick)
                    {
                        drawWhite = true;
                    }
                    else
                    {
                        //ターゲットミリ秒のチックを取得

                        targetMs = (int)(baseRenderingTime + (basisMs * (maxIndex - i)));
                        targetMs2 = (int)(baseRenderingTime + (basisMs * (maxIndex - i + 1)));
                        targetMs3 = (int)(baseRenderingTime + (basisMs * (maxIndex - i - 1)));
                        //はみ出した分は透明として処理
                        if (maxTimeToTick > targetMs)
                        {
                            //if (targetMs < 0)
                            //{
                            //    int ii = 1;
                            //}
                            nowIndex = (int)(Math.Round(timeToTick[targetMs] * 0.01));
                        }
                        else
                        {
                            drawWhite = true;
                        }
                        if (maxTimeToTick > targetMs2 && maxTimeToTick > targetMs3 && targetMs2 > 0 && targetMs3 > 0)
                        {
                            nextIndex = (int)(Math.Round(timeToTick[targetMs2] * 0.01));
                            backIndex = (int)(Math.Round(timeToTick[targetMs3] * 0.01));
                            if (nextIndex - nowIndex > 1)
                            {
                                //前ピクセルとの間に2チック以上ある場合は、その間に枠線があるかを調べる
                                drawType = 1;
                            }
                            else if (nextIndex == nowIndex)
                            {
                                //前ピクセルとの間に0チックしかない場合は、複数回まとめて描画し最後の1回だけ
                                drawType = 2;
                            }
                            else if (nextIndex - nowIndex == 1)
                            {
                                //前ピクセルとの間に1チックしかない場合は、そのまま描画
                                drawType = 2;
                            }
                        }
                    }


                    if (viewDataLength <= nowIndex || drawWhite)
                    {
                        //はみ出した分は透明として処理
                        for (byte j = 0; j < 128; j++)
                        {
                            if (bufLastBar[j, i] == 127)
                            {
                                continue; // 前回と同じ色なら何もしない
                            }
                            bufLastBar[j, i] = 127;

                            var keyInfo = keyRectsInfo[15, j];
                            int w = keyInfo.Width;

                            int keyIndex = j % 12;
                            if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                            {
                                // 白鍵の場合
                                if (updateWhite[sbn] == false)
                                {
                                    updateWhite[sbn] = true; // 白鍵の更新フラグを立てる
                                }
                                for (int x = 0; x < w; x++)
                                {
                                    int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;
                                    pixelsWhite[sbn][idx + 0] = 0;
                                    pixelsWhite[sbn][idx + 1] = 0;
                                    pixelsWhite[sbn][idx + 2] = 0;
                                    pixelsWhite[sbn][idx + 3] = 0;
                                }
                            }
                            else
                            {
                                // 黒鍵の場合
                                if (updateBlack[sbn] == false)
                                {
                                    updateBlack[sbn] = true; // 黒鍵の更新フラグを立てる
                                }
                                for (int x = 0; x < w; x++)
                                {
                                    int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;
                                    pixelsBlack[sbn][idx + 0] = 0;
                                    pixelsBlack[sbn][idx + 1] = 0;
                                    pixelsBlack[sbn][idx + 2] = 0;
                                    pixelsBlack[sbn][idx + 3] = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (drawType == 0)
                        {
                            //普通の色描画
                            for (byte j = 0; j < 128; j++)
                            {
                                byte colorByte = viewData[nowIndex, j];
                                if (bufLastBar[j, i] == colorByte)
                                {
                                    continue; // 前回と同じ色なら何もしない
                                }
                                bufLastBar[j, i] = colorByte;
                                //ノートのオンオフによってキーの表示非表示を切り替える
                                var keyInfo = keyRectsInfo[15, j];
                                int w = keyInfo.Width;
                                Color c = keyColors2[colorByte];
                                Color c2 = keyColors2[colorByte | 128];

                                int keyIndex = j % 12;
                                if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                                {
                                    // 白鍵の場合
                                    if (updateWhite[sbn] == false)
                                    {
                                        updateWhite[sbn] = true; // 白鍵の更新フラグを立てる
                                    }
                                    for (int x = 0; x < w; x++)
                                    {
                                        bool isBorder1 = (x == 0 || x == w - 1);
                                        int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;

                                        if (isBorder1)
                                        {
                                            // 枠線の色を設定
                                            pixelsWhite[sbn][idx + 0] = c2.B;
                                            pixelsWhite[sbn][idx + 1] = c2.G;
                                            pixelsWhite[sbn][idx + 2] = c2.R;
                                            pixelsWhite[sbn][idx + 3] = c2.A;
                                        }
                                        else
                                        {
                                            // 中身の色を設定
                                            pixelsWhite[sbn][idx + 0] = c.B;
                                            pixelsWhite[sbn][idx + 1] = c.G;
                                            pixelsWhite[sbn][idx + 2] = c.R;
                                            pixelsWhite[sbn][idx + 3] = c.A;
                                        }
                                    }
                                }
                                else
                                {
                                    // 黒鍵の場合
                                    if (updateBlack[sbn] == false)
                                    {
                                        updateBlack[sbn] = true; // 黒鍵の更新フラグを立てる
                                    }
                                    for (int x = 0; x < w; x++)
                                    {
                                        bool isBorder1 = (x == 0 || x == w - 1);
                                        int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;

                                        if (isBorder1)
                                        {
                                            // 枠線の色を設定
                                            pixelsBlack[sbn][idx + 0] = c2.B;
                                            pixelsBlack[sbn][idx + 1] = c2.G;
                                            pixelsBlack[sbn][idx + 2] = c2.R;
                                            pixelsBlack[sbn][idx + 3] = c2.A;
                                        }
                                        else
                                        {
                                            // 中身の色を設定
                                            pixelsBlack[sbn][idx + 0] = c.B;
                                            pixelsBlack[sbn][idx + 1] = c.G;
                                            pixelsBlack[sbn][idx + 2] = c.R;
                                            pixelsBlack[sbn][idx + 3] = c.A;
                                        }
                                    }
                                }
                            }
                        }
                        else if (drawType == 1)
                        {
                            //1ピクセル中に複数チックがある場合の描画
                            for (byte j = 0; j < 128; j++)
                            {
                                byte colorByte = viewData[nowIndex, j];
                                bool isBorder3 = false;
                                //ノートのオンオフによってキーの表示非表示を切り替える
                                var keyInfo = keyRectsInfo[15, j];
                                int w = keyInfo.Width;
                                Color c = keyColors2[colorByte];
                                Color c2 = keyColors2[colorByte | 128];
                                byte bufColor = colorByte;
                                if (viewData[nowIndex, j] < 128)
                                {
                                    for (int index = backIndex + 1; index < nextIndex; index++)
                                    {
                                        if ((colorByte |= 128) == viewData[index, j])
                                        {
                                            bufColor = (byte)(colorByte | 128);
                                            isBorder3 = true; // 枠線がある場合
                                            break;
                                        }
                                    }
                                }
                                if (bufLastBar[j, i] == bufColor)
                                {
                                    continue; // 前回と同じ色なら何もしない
                                }
                                bufLastBar[j, i] = bufColor;

                                int keyIndex = j % 12;
                                if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                                {
                                    // 白鍵の場合
                                    if (updateWhite[sbn] == false)
                                    {
                                        updateWhite[sbn] = true; // 白鍵の更新フラグを立てる
                                    }
                                    for (int x = 0; x < w; x++)
                                    {
                                        bool isBorder2 = (x == 0 || x == w - 1);
                                        int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;

                                        if (isBorder2 || isBorder3)
                                        {
                                            // 枠線の色を設定
                                            pixelsWhite[sbn][idx + 0] = c2.B;
                                            pixelsWhite[sbn][idx + 1] = c2.G;
                                            pixelsWhite[sbn][idx + 2] = c2.R;
                                            pixelsWhite[sbn][idx + 3] = c2.A;
                                        }
                                        else
                                        {
                                            // 中身の色を設定
                                            pixelsWhite[sbn][idx + 0] = c.B;
                                            pixelsWhite[sbn][idx + 1] = c.G;
                                            pixelsWhite[sbn][idx + 2] = c.R;
                                            pixelsWhite[sbn][idx + 3] = c.A;
                                        }
                                    }
                                }
                                else
                                {
                                    // 黒鍵の場合
                                    if (updateBlack[sbn] == false)
                                    {
                                        updateBlack[sbn] = true; // 黒鍵の更新フラグを立てる
                                    }
                                    for (int x = 0; x < w; x++)
                                    {
                                        bool isBorder2 = (x == 0 || x == w - 1);
                                        int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;

                                        if (isBorder2 || isBorder3)
                                        {
                                            // 枠線の色を設定
                                            pixelsBlack[sbn][idx + 0] = c2.B;
                                            pixelsBlack[sbn][idx + 1] = c2.G;
                                            pixelsBlack[sbn][idx + 2] = c2.R;
                                            pixelsBlack[sbn][idx + 3] = c2.A;
                                        }
                                        else
                                        {
                                            // 中身の色を設定
                                            pixelsBlack[sbn][idx + 0] = c.B;
                                            pixelsBlack[sbn][idx + 1] = c.G;
                                            pixelsBlack[sbn][idx + 2] = c.R;
                                            pixelsBlack[sbn][idx + 3] = c.A;
                                        }
                                    }
                                }
                            }
                        }
                        else if (drawType == 2)
                        {
                            //同じチックを複数ピクセルに描画する場合、最後の1回だけを枠線とする。

                            for (byte j = 0; j < 128; j++)
                            {
                                byte colorByte = viewData[nowIndex, j];

                                //ノートのオンオフによってキーの表示非表示を切り替える
                                var keyInfo = keyRectsInfo[15, j];
                                int w = keyInfo.Width;
                                Color c = keyColors2[colorByte & 127];
                                Color c2 = keyColors2[colorByte | 128];
                                bool isBorder1 = false;
                                byte bufColor = (byte)(viewData[nowIndex, j] & 127);
                                if (colorByte >= 128)
                                {
                                    //次チックが同じ色なら、最初のチックだけ枠線とする

                                    if ((viewData[nowIndex + 1, j] & 127) == (viewData[nowIndex, j] & 127) && backIndex != nowIndex)
                                    {
                                        bufColor = viewData[nowIndex, j];
                                        isBorder1 = true;
                                    }
                                    else if ((viewData[nowIndex + 1, j] & 127) != (viewData[nowIndex, j] & 127) && nowIndex != nextIndex)
                                    {
                                        bufColor = viewData[nowIndex, j];
                                        isBorder1 = true;
                                    }
                                }
                                if (bufLastBar[j, i] == bufColor)
                                {
                                    //continue; // 前回と同じ色なら何もしない
                                }
                                bufLastBar[j, i] = bufColor;

                                int keyIndex = j % 12;
                                if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                                {
                                    // 白鍵の場合
                                    if (updateWhite[sbn] == false)
                                    {
                                        updateWhite[sbn] = true; // 白鍵の更新フラグを立てる
                                    }
                                    for (int x = 0; x < w; x++)
                                    {
                                        bool isBorder2 = (x == 0 || x == w - 1);
                                        int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;

                                        if (isBorder1 || isBorder2)
                                        {
                                            // 枠線の色を設定
                                            pixelsWhite[sbn][idx + 0] = c2.B;
                                            pixelsWhite[sbn][idx + 1] = c2.G;
                                            pixelsWhite[sbn][idx + 2] = c2.R;
                                            pixelsWhite[sbn][idx + 3] = c2.A;
                                        }
                                        else
                                        {
                                            // 中身の色を設定
                                            pixelsWhite[sbn][idx + 0] = c.B;
                                            pixelsWhite[sbn][idx + 1] = c.G;
                                            pixelsWhite[sbn][idx + 2] = c.R;
                                            pixelsWhite[sbn][idx + 3] = c.A;
                                        }
                                    }
                                }
                                else
                                {
                                    // 黒鍵の場合
                                    if (updateBlack[sbn] == false)
                                    {
                                        updateBlack[sbn] = true; // 黒鍵の更新フラグを立てる
                                    }
                                    for (int x = 0; x < w; x++)
                                    {
                                        bool isBorder2 = (x == 0 || x == w - 1);
                                        int idx = (sbnY * (whiteKeyWidth * 75 + 1) + (keyInfo.X + x)) * 4;

                                        if (isBorder1 || isBorder2)
                                        {
                                            // 枠線の色を設定
                                            pixelsBlack[sbn][idx + 0] = c2.B;
                                            pixelsBlack[sbn][idx + 1] = c2.G;
                                            pixelsBlack[sbn][idx + 2] = c2.R;
                                            pixelsBlack[sbn][idx + 3] = c2.A;
                                        }
                                        else
                                        {
                                            // 中身の色を設定
                                            pixelsBlack[sbn][idx + 0] = c.B;
                                            pixelsBlack[sbn][idx + 1] = c.G;
                                            pixelsBlack[sbn][idx + 2] = c.R;
                                            pixelsBlack[sbn][idx + 3] = c.A;
                                        }
                                    }
                                }
                            }
                        }
                    }
                //}
                });

                for (byte j = 0; j < 128; j++)
                {
                    byte color = 0;
                    if (viewData.Length <= nowTick)
                    {
                        color = 127;
                    }
                    else
                    {
                        color = (byte)(viewData[nowTick, j] & 127);
                    }
                    if (bufLastNote[j] == color)
                    {
                        continue; // 前回と同じ色なら何もしない
                    }
                    bufLastNote[j] = color;

                    //ノートのオンオフによってキーの表示非表示を切り替える
                    var keyInfo = keyRectsInfo[15, j];
                    int w = keyInfo.Width, h = keyInfo.Height;
                    int stride = w * 4;
                    byte[] pixels = new byte[w * h * 4];
                    Color c = keyColors2[color]; // 色を設定
                    if (color == 127)
                    {
                        c = Colors.White;
                    }
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            bool isBorder = (x == 0 || y == 0 || x == w - 1 || y == h - 1);

                            if (isBorder)
                            {
                                int idx = (y * w + x) * 4;
                                pixels[idx + 0] = 0;
                                pixels[idx + 1] = 0;
                                pixels[idx + 2] = 0;
                                pixels[idx + 3] = 255;
                            }
                            else
                            {
                                int idx = (y * w + x) * 4;
                                pixels[idx + 0] = c.B;
                                pixels[idx + 1] = c.G;
                                pixels[idx + 2] = c.R;
                                pixels[idx + 3] = 255;
                            }
                        }
                    }

                    int keyIndex = j % 12;
                    if (keyIndex == 0 || keyIndex == 2 || keyIndex == 4 || keyIndex == 5 || keyIndex == 7 || keyIndex == 9 || keyIndex == 11)
                    {
                        // 白鍵
                        bmpWhite.WritePixels(
                            new Int32Rect(keyInfo.X, keyInfo.Y, w, h),
                            pixels, stride, 0
                        );
                    }
                    else
                    {
                        // 黒鍵
                        bmpBlack.WritePixels(
                            new Int32Rect(keyInfo.X, keyInfo.Y, w, h),
                            pixels, stride, 0
                        );
                    }
                }

                for (byte i = 0; i < whiteKeyHeight; i++)
                {
                    if (updateWhite[i])
                    {
                        // 白鍵
                        bmpWhite.WritePixels(
                            new Int32Rect(0, i * 15, (whiteKeyWidth * 75 + 1), 15),
                            pixelsWhite[i], (whiteKeyWidth * 75 + 1) * 4, 0
                        );
                    }
                    if (updateBlack[i])
                    {
                        // 黒鍵
                        bmpBlack.WritePixels(
                            new Int32Rect(0, i * 15, (whiteKeyWidth * 75 + 1), 15),
                            pixelsBlack[i], (whiteKeyWidth * 75 + 1) * 4, 0
                        );
                    }
                }



            }


            if (isPlaying)
            {
                if (sliderChanging == false)
                {
                    sliderSetValue = tickToTime[data.Count2 * 100] / 100;
                    Slider_PlayTime.Value = sliderSetValue;
                }

                nowTick.Text = data.Count2.ToString("N0") + " ticks";


                NoteCount.Text = "Notes: " + noteCount[data.Count2].ToString("N0");
                BPM.Text = $"BPM: {tickBPM[data.Count2].ToString("N0")}"; // BPMを表示
                int nowSec = (int)(tickToTime[data.Count2 * 100] / 100000); // 現在のTickに対応する経過時間を取得

                int minutes = nowSec / 60;
                int seconds = nowSec % 60;
                Time.Text = $"Time: {minutes}:{seconds:D2}";

                if (lateTime > 0)
                {
                    double lateBuf = Math.Round(lateTime, 3); // 遅延時間を小数点以下3桁に丸める
                                                              // 遅延時間がある場合、遅延時間を表示
                    lateTimeText.Text = $"Latency: {lateBuf:F3}s";
                }
                else
                {
                    lateTimeText.Text = "Latency: 0.000s";
                }
            }



            if (data != null && data.Count2 != 0 && noteCount.Length >= data.Count2)
            {
                NPS.Text = $"NPS: {(noteCount[data.Count2] - lastNoteCount):N0}"; // NPSを表示
                lastNoteCount = noteCount[data.Count2]; // 最後のノート数を更新

                int nowTick = data.Count2;
                for (int i = 0; i < tickToTime.Length; i++)
                {
                    if (tickToTime[i] >= tickToTime[nowTick] - 1000)
                    {
                        NPS.Text = $"NPS: {(noteCount[nowTick] - noteCount[i]):N0}"; // NPSを更新
                        break;
                    }
                }
            }
            else
            {
                NPS.Text = "NPS: 0";
            }

            //1秒間に描画したフレームをカウントし、描画
            frameCount += 1;
            if ((DateTime.Now - lastFrameTime).TotalSeconds >= 1)
            {
                // 1秒経過したらフレーム数を表示
                FPS.Text = $"FPS: {frameCount:N0}";
                frameCount = 0; // フレーム数をリセット
                lastFrameTime = DateTime.Now; // 最後のフレーム時間を更新

            }
        }

        private void OnClick_Stop(object sender, RoutedEventArgs e)
        {
            stop = true;
            isPlaying = false; // 再生中フラグを更新

            btn_Load.IsEnabled = true;
            btn_Play.IsEnabled = true;
            AllNoteOff(); //全ノートオフを送信
            btn_Stop.IsEnabled = false;
            btn_Pause.IsEnabled = false;
            Slider_PlayTime.Value = 0;
            StatusText.Text = "Status: Stopped";
            NoteCount.Text = "Notes: 0"; // ノート数をリセット
            Time.Text = "Time: 0:00"; // 経過時間をリセット
            nowTick.Text = "0 ticks"; // 現在のTickをリセット
            lateTimeText.Text = "Latency: 0.000s"; // 遅延時間をリセット
            BPM.Text = "BPM: 0"; // BPMをリセット
        }

        private void OnClick_Pause(object sender, RoutedEventArgs e)
        {
            pause = true;
            isPlaying = false; // 再生中フラグを更新

            btn_Play.IsEnabled = true;
            btn_Stop.IsEnabled = true;
            btn_Pause.IsEnabled = false;
            StatusText.Text = "Status: Paused";
        }

        private void OnClick_Quit(object sender, RoutedEventArgs e)
        {
            //MIDIのデバイスを閉じる
            //MidiOutWinMM.midiOutClose(device); // 0はデバイス番号
            //MidiOutWinMM.CloseKDMAPIStream();

            //アプリケーションを終了
            Application.Current.Shutdown();
        }

        private void OnClick_Setting(object sender, RoutedEventArgs e)
        {
            //設定ウィンドウを開く
            Settings settingsWindow = new Settings(settingData);
            settingsWindow.Owner = this; // 親ウィンドウを設定
            settingsWindow.ShowDialog(); // モーダルダイアログとして表示

            if (settingsWindow.IsOK)
            {
                settingData = settingsWindow.settingData; // 設定データを更新
            }
        }

        private async void OnClick_Load(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "MIDI Files (*.mid)|*.mid|All Files (*.*)|*.*";
            string filePath = "";
            loadingKbn = 0;

            if (openFileDialog.ShowDialog() == true)
            {
                btn_Load.IsEnabled = false; // 読み込み中はボタンを無効化
                btn_Play.IsEnabled = false;
                btn_Stop.IsEnabled = false;
                btn_Pause.IsEnabled = false;
                btn_Setting.IsEnabled = false;

                filePath = openFileDialog.FileName;
                //読み込み処理を非同期で実行
                int res1 = 0;
                int res2 = 0;
                int res3 = 0;
                int res4 = 0;
                await Task.Run(() =>
                {
                    res1 = LoadMIDIFile(filePath);
                    loadingKbn = 1;
                    res2 = DoNoteCount();
                    loadingKbn = 2;
                    res3 = SetTickTime();
                    //if (settingData.IgnoreOR || settingData.IgnoreAND)
                    //{
                    //    loadingKbn = 4;
                    //    ConvertMIDIData();
                    //}
                    if (drawMode == 1)
                    {
                        loadingKbn = 3;
                        res4 = MakeViewData();
                    }
                    loadingKbn = 0;
                });



                if (res1 == -1 || res2 == -1 || res3 == -1 || res4 == -1)
                {
                    loading = false;
                    loadingByte = 0;
                    MessageBox.Show("Failed to load MIDI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "Status: Failed to load MIDI";
                    Slider_PlayTime.Maximum = 1;
                    Slider_PlayTime.Value = 0; // スライダーを初期化
                    btn_Load.IsEnabled = true;
                    btn_Setting.IsEnabled = true;
                }
                else if (res1 == 0 && res2 == 0 && res3 == 0 && res4 == 0)
                {
                    //読み込み成功
                    btn_Play.IsEnabled = true;
                    btn_Stop.IsEnabled = false;
                    btn_Pause.IsEnabled = false;
                    data.Count2 = 0;
                    loading = false;
                    loadingByte = 0;
                    StatusText.Text = "Status: MIDI loaded successfully";
                    bpqn.Text = $"BPQN: {data.BPQN.ToString("N0")}";
                    btn_Load.IsEnabled = true;
                    //全てのトラックのなかで最大のTickを取得
                    int maxTick = data.Note.Keys.Max();
                    Slider_PlayTime.Maximum = tickToTime[maxTick * 10] / 10 - 1;
                    Slider_PlayTime.Value = 0; // スライダーを初期化
                    btn_Setting.IsEnabled = true;
                }
                else
                {
                    loading = false;
                    loadingByte = 0;
                    MessageBox.Show("An unknown error has occurred.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "Status: Failed to load MIDI";
                    Slider_PlayTime.Maximum = 1;
                    Slider_PlayTime.Value = 0; // スライダーを初期化
                    btn_Load.IsEnabled = true;
                    btn_Setting.IsEnabled = true;
                }
            }
            else
            {
                return;
            }
        }

        private int LoadMIDIFile(string filePath)
        {
            try
            {
                data = new DataModel();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Status: Loading...";
                });

                data.DrawMode = drawMode;
                data.SettingData = settingData;
                loading = true;
                loadingByte = 0;
                lastCount = 1;
                data.LastTick = new bool[10000000];
                data.Color = 1;

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int basisReadByte = 1024 * 1024 * 1; // 512MBずつ読む

                    byte[] buffer1 = new byte[basisReadByte];
                    byte[] buffer2 = new byte[basisReadByte];

                    int bytesRead;
                    int readCount = 0;
                    byte runningStatus = 0;
                    bool loop = true;



                    while (loop)
                    {

                        //カウントが2MB目に突入したら入れ替える
                        if (data.Count1 >= basisReadByte || readCount == 0)
                        {
                            //読み込み状態によって次のデータを読み込む

                            int countSpan = data.Count1 - basisReadByte;

                            //初回のみ
                            if (readCount == 0)
                            {
                                bytesRead = stream.Read(buffer2, 0, basisReadByte);
                                //読み込んだサイズにリサイズ
                                if (bytesRead != basisReadByte)
                                {
                                    Array.Resize(ref buffer2, bytesRead);
                                }
                                buffer1 = (byte[])buffer2.Clone();
                            }
                            else
                            {

                                //buffer1 = (byte[])buffer2.Clone();
                                int buf = data.ReadMIDI.Length - basisReadByte;
                                if (buf == basisReadByte)
                                {
                                    Buffer.BlockCopy(data.ReadMIDI, basisReadByte, buffer1, 0, basisReadByte);
                                }
                                else
                                {
                                    Buffer.BlockCopy(data.ReadMIDI, basisReadByte, buffer1, 0, buf);
                                    Array.Resize(ref buffer1, buf);
                                }
                                //Buffer.BlockCopy(data.ReadMIDI, data.ReadMIDI.Length - basisReadByte, buffer1, 0, data.ReadMIDI.Length - basisReadByte);
                            }
                            buffer2 = new byte[basisReadByte];
                            bytesRead = stream.Read(buffer2, 0, basisReadByte);
                            //読み込んだサイズにリサイズ
                            if (bytesRead != basisReadByte)
                            {
                                Array.Resize(ref buffer2, bytesRead);
                            }

                            //2MB分読み込んでくっつける
                            data.ReadMIDI = new byte[buffer1.Length + buffer2.Length];
                            System.Buffer.BlockCopy(buffer1, 0, data.ReadMIDI, 0, buffer1.Length);
                            System.Buffer.BlockCopy(buffer2, 0, data.ReadMIDI, buffer1.Length, buffer2.Length);


                            //読み込んだら読み込み回数を更新し、カウントを0にする
                            readCount++;
                            data.Count1 = 0 + countSpan;

                            //初回のみ
                            if (readCount == 1)
                            {
                                //ヘッダーチャンクを処理
                                EventHeader(data);
                            }
                        }

                        //読み込み中バイトを保存
                        loadingByte = (long)data.Count1 + ((long)readCount - 1) * (long)basisReadByte;

                        if (data.nextTrackHeader)
                        {
                            //トラックヘッダを処理
                            data.nextTrackHeader = false;
                            bool end = EventTrackHeader(data);
                            if (end)
                            {
                                //トラックヘッダが終わったら、ループを抜ける
                                loop = false;
                                continue;
                            }
                        }


                        //デルタタイムを取得
                        int res = GetDelta(data);
                        if (res == -1)
                        {
                            //デルタタイムが取得できなかった場合、読み込み終了
                            return -1;
                        }
                        data.AllDelta += res;

                        //ここから、ステータスバイトによって処理を分岐していく
                        //まずは今見るバイトを取得
                        byte status = data.ReadMIDI[data.Count1];



                        //ランニングステータスだったら、保持してあるステータスをセットする
                        if (status < 0x80)
                        {
                            status = runningStatus;
                        }
                        else
                        {
                            //ランニングステータスを更新
                            runningStatus = status;
                        }

                        //ステータスバイトによって処理を分岐
                        switch (status >> 4)
                        {
                            case 0x8: // Note Off
                                EventNoteOff(data);
                                break;
                            case 0x9: // Note On
                                      //ステータスが9でもVelocityが0ならNote Offと同じ扱い
                                if (data.ReadMIDI[data.Count1 + 2] == 0)
                                {
                                    EventNoteOff(data);
                                    //data.Count1 += 3;
                                }
                                else
                                {
                                    EventNoteOn(data);
                                }
                                break;
                            case 0xA: // Polyphonic Key Pressure (Aftertouch)
                                // ここでは特に処理しない
                                data.Count1 += 3; // 3バイト読み飛ばす
                                break;
                            case 0xB: // Control Change
                                // コントロールチェンジの処理
                                EventControlChange(data);
                                break;
                            case 0xC: // Program Change
                                EventProgramChange(data);
                                break;
                            case 0xD: // Channel Pressure (Aftertouch)
                                // ここでは特に処理しない
                                data.Count1 += 2; // 2バイト読み飛ばす
                                break;
                            case 0xE: // Pitch Bend Change
                                //ピッチの変更は取得する
                                EventPitch(data);
                                break;
                            case 0xF: // System Exclusive or Meta Event
                                if (status == 0xFF) // メタイベント
                                {
                                    EventMeta(data);
                                }
                                else
                                {
                                    EventSysEx(data);
                                }
                                break;
                        }

                    }

                }



                return 0;
            }
            catch
            {
                return -1;
            }
        }

        private void OnValueChanged_PlayTime(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderSetValue == e.NewValue)
            {
                //プログラムによる変更
            }
            else
            {
                //ユーザーによる変更
                sliderChangeValueBefore = (int)e.OldValue; // スライダーの値を保存
                sliderChangeValueAfter = (int)e.NewValue; // スライダーの値を保存
                sliderChanged = true;

                if (pause)
                {
                    int nowSec = (int)(Slider_PlayTime.Value / 1000); // 現在のTickに対応する経過時間を取得

                    int minutes = nowSec / 60;
                    int seconds = nowSec % 60;
                    Time.Text = $"Time: {minutes}:{seconds:D2}";
                }
            }
        }

        private int DoNoteCount()
        {
            try
            {
                int maxTick = data.Note.Keys.Max();
                noteCount = new long[maxTick + 1]; // 最大チック数に合わせて配列を初期化
                long note = 0;

                for (int i = 0; i < maxTick; i++)
                {
                    if (data.Note.TryGetValue(i, out var tickData))
                    {
                        //foreach (var noteOn in tickData.NoteOn)
                        //{
                        //    note += 1;
                        //}
                        note += tickData.NoteOn.Count;
                    }
                    noteCount[i + 1] = note; // ノートオンの数をカウント

                }



                return 0;
            }
            catch
            {
                return -1;
            }
            //MessageBox.Show(note.ToString());
        }

        private int SetTickTime()
        {
            try
            {
                //各tickごとの経過時間と各経過時間ごとのTickを事前に計算して起き、再生中の負担を減らす
                int maxTick = data.Note.Keys.Max() * 100;
                tickToTime = new int[maxTick + 1];
                tickBPM = new double[maxTick + 1];
                lastCount = 1;

                for (int i = 0; i <= maxTick; i++)
                {
                    int tick = i;

                    if (data.TempoPoints.Count == 0)
                    {
                        tickToTime[i] = (int)Math.Round(500000 / data.BPQN * tick / 1000.0);
                        tickBPM[i] = 120.00; // デフォルトのBPMを計算
                        continue;
                    }

                    var points = data.TempoPoints;
                    TempoPoint current = points[0];
                    for (int j = lastCount; j < points.Count; j++)
                    {
                        lastCount = j - 1;
                        if (tick == points[j].Tick * 100) //同じだったらその時点での経過時間をそのまま返す
                        {
                            bpm = Math.Round(60.0 / points[j].Tempo * 1000000, 3);
                            tickToTime[i] = (int)points[j].ElapsedMs * 100;
                            tickBPM[i] = Math.Round(60.0 / points[j].Tempo * 1000000, 3);
                            continue;
                        }
                        else if (tick < points[j].Tick * 100) //現在tickより大きいテンポポイントが見つかったら、その直前が一番大きいテンポポイントである
                        {
                            current = points[j - 1];
                            break;
                        }
                        else if (j == points.Count - 1) //最後のテンポポイントまで来たら、現在のテンポポイントをそのまま使う
                        {
                            current = points[j];
                            break;
                        }
                    }
                    // 現在のテンポポイントからの経過時間を計算
                    int tickDelta = tick - current.Tick * 100;

                    double sectionMs = (current.Tempo / 1000.0) / data.BPQN * tickDelta;

                    tickToTime[i] = (int)Math.Round(current.ElapsedMs * 100 + sectionMs);
                    tickBPM[i] = Math.Round(60.0 / current.Tempo * 1000000, 3);
                }

                lastCount = 1;
                timeToTick = new int[tickToTime[maxTick]];
                for (int i = 0; i < tickToTime[maxTick]; i++)
                {
                    for (int j = lastCount; j < tickToTime.Length; j++)
                    {
                        if (tickToTime[j] > i)
                        {
                            lastCount = j - 1;
                            timeToTick[i] = j - 1;
                            break;
                        }
                    }
                }
                return 0;
            }
            catch
            {
                return -1;
            }

        }

        private int MakeViewData()
        {
            try
            {
                //全てのトラックのなかで最大のTickを取得
                int maxTick = data.Note.Keys.Max();
                viewData = new byte[maxTick + 1, 128];
                int[,] bufView = new int[128, 128];

                if (settingData.ApplyIgnoreToDrawing)
                {
                    //こちらは無視されたデータを描画にも反映させる

                    for (data.Count3 = 0; data.Count3 <= maxTick; data.Count3++)
                    {
                        bool[] noteChangeOn = new bool[128];
                        bool[] noteChangeOff = new bool[128];
                        if (data.Note.TryGetValue(data.Count3, out var tickData))
                        {
                            // ノートオフの処理
                            if (tickData.NoteOff.Count > 0)
                            {
                                for (int count = 0; count < tickData.NoteOff.Count; count++)
                                {
                                    var noteOff = tickData.NoteOff[count]; // ノートオフデータを取得

                                    if ((noteOff.Note & 128) != 0)
                                    {
                                        continue;
                                    }

                                    ////ノートオフの処理
                                    bufView[noteOff.Note, noteOff.TrackId] -= 1;
                                    noteChangeOff[noteOff.Note] = true;

                                    //前回表示色と一致する場合は前回表示色を枠線とする
                                    if ((viewData[data.Count3 - 1, noteOff.Note] & 127) == noteOff.TrackId)
                                    {
                                        viewData[data.Count3 - 1, noteOff.Note] |= 128;
                                    }

                                }
                            }

                            //ノートオンの処理
                            if (tickData.NoteOn.Count > 0)
                            {
                                //foreach (var noteOn in tickData.NoteOn)
                                for (int count = 0; count < tickData.NoteOn.Count; count++)
                                {
                                    var noteOn = tickData.NoteOn[count]; // ノートオフデータを取得

                                    if ((noteOn.Note & 128) != 0)
                                    {
                                        continue;
                                    }

                                    bufView[noteOn.Note, noteOn.TrackId] += 1;
                                    noteChangeOn[noteOn.Note] = true;

                                    //ノートオンがあった場合は優先的にそれを表示する、そして枠線とする
                                    viewData[data.Count3, noteOn.Note] = noteOn.TrackId |= 128;
                                }
                            }
                        }

                        for (int note = 0; note < 128; note++)
                        {
                            if (!noteChangeOn[note] && noteChangeOff[note])
                            {
                                //ノートオフになった場合は違う色があるかもしれないので調べ直す
                                byte nowColor = 0;
                                viewData[data.Count3, note] = 127;
                                for (int i = 126; i >= 0; i--)
                                {
                                    if (bufView[note, i] > 0)
                                    {
                                        viewData[data.Count3, note] = (byte)i;
                                        nowColor = (byte)i; // 現在の色を保存
                                        i = 0;
                                    }
                                }
                            }
                            else if (!noteChangeOn[note] && !noteChangeOff[note])
                            {
                                if (data.Count3 == 0)
                                {
                                    //ノートオンもノートオフもない場合は127をセット
                                    viewData[data.Count3, note] = 127;
                                }
                                else
                                {
                                    //ノートオンもノートオフもない場合は前回の値をそのまま引き継ぐ
                                    viewData[data.Count3, note] = (byte)(viewData[data.Count3 - 1, note] & 127);
                                }
                            }

                        }
                    }
                    //最後のチックにある白以外のノートを枠線とする
                    for (int note = 0; note < 128; note++)
                    {
                        if (viewData[maxTick, note] < 127)
                        {
                            viewData[maxTick, note] |= 0x80; // 上位ビットをセットして枠線にする
                        }
                    }
                }
                else
                {
                    //こちらは無視されたデータも描画する

                    for (data.Count3 = 0; data.Count3 <= maxTick; data.Count3++)
                    {
                        bool[] noteChangeOn = new bool[128];
                        bool[] noteChangeOff = new bool[128];
                        if (data.Note.TryGetValue(data.Count3, out var tickData))
                        {
                            // ノートオフの処理
                            if (tickData.NoteOff.Count > 0)
                            {
                                for (int count = 0; count < tickData.NoteOff.Count; count++)
                                {
                                    var noteOff = tickData.NoteOff[count]; // ノートオフデータを取得

                                    byte note = (byte)(noteOff.Note & 127);

                                    ////ノートオフの処理
                                    bufView[note, noteOff.TrackId] -= 1;
                                    noteChangeOff[note] = true;

                                    //前回表示色と一致する場合は前回表示色を枠線とする
                                    if ((viewData[data.Count3 - 1, note] & 127) == noteOff.TrackId)
                                    {
                                        viewData[data.Count3 - 1, note] |= 128;
                                    }

                                }
                            }

                            //ノートオンの処理
                            if (tickData.NoteOn.Count > 0)
                            {
                                //foreach (var noteOn in tickData.NoteOn)
                                for (int count = 0; count < tickData.NoteOn.Count; count++)
                                {
                                    var noteOn = tickData.NoteOn[count]; // ノートオフデータを取得

                                    byte note = (byte)(noteOn.Note & 127);

                                    bufView[note, noteOn.TrackId] += 1;
                                    noteChangeOn[note] = true;

                                    //ノートオンがあった場合は優先的にそれを表示する、そして枠線とする
                                    viewData[data.Count3, note] = noteOn.TrackId |= 128;
                                }
                            }
                        }

                        for (int note = 0; note < 128; note++)
                        {
                            if (!noteChangeOn[note] && noteChangeOff[note])
                            {
                                //ノートオフになった場合は違う色があるかもしれないので調べ直す
                                byte nowColor = 0;
                                viewData[data.Count3, note] = 127;
                                for (int i = 126; i >= 0; i--)
                                {
                                    if (bufView[note, i] > 0)
                                    {
                                        viewData[data.Count3, note] = (byte)i;
                                        nowColor = (byte)i; // 現在の色を保存
                                        i = 0;
                                    }
                                }
                            }
                            else if (!noteChangeOn[note] && !noteChangeOff[note])
                            {
                                if (data.Count3 == 0)
                                {
                                    //ノートオンもノートオフもない場合は127をセット
                                    viewData[data.Count3, note] = 127;
                                }
                                else
                                {
                                    //ノートオンもノートオフもない場合は前回の値をそのまま引き継ぐ
                                    viewData[data.Count3, note] = (byte)(viewData[data.Count3 - 1, note] & 127);
                                }
                            }

                        }
                    }
                    //最後のチックにある白以外のノートを枠線とする
                    for (int note = 0; note < 128; note++)
                    {
                        if (viewData[maxTick, note] < 127)
                        {
                            viewData[maxTick, note] |= 0x80; // 上位ビットをセットして枠線にする
                        }
                    }
                }
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        private void OnValueChanged_Speed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double newValue = 8.25 - e.NewValue;
            Speed.Text = $"Speed: {newValue}";
            basisMs = newValue * 100;
        }

        private static Color RandomColor()
        {
            // HSVで範囲指定
            double h = Random.Shared.NextDouble() * 360.0;      // 色相
            double s = 0.5 + Random.Shared.NextDouble() * 0.35; // 彩度0.5～0.85
            double v = 0.7 + Random.Shared.NextDouble() * 0.25; // 明度0.7～0.95

            // HSV→RGB変換
            int hi = (int)(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r = 0, g = 0, b = 0;
            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }
            return Color.FromArgb(255, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        struct KeyRectInfo
        {
            public int X, Y, Width, Height;
        }

    }
}