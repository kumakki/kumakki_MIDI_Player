using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Xml.Serialization;

namespace kumakki_MIDI_Player
{
    public static class MidiEditer
    {
        public static void EventHeader(DataModel data)
        {
            // ヘッダーチャンクの処理
            // ヘッダーは通常、MThdという文字列で始まり、次にヘッダーの長さ、フォーマットタイプ、トラック数、タイムベースが続く
            // 軽量化のため比較は数値で行う
            if (data.ReadMIDI.Length < 14) return; // ヘッダーの長さが足りない場合は終了
            if (data.ReadMIDI[0] != 0x4D || data.ReadMIDI[1] != 0x54 || data.ReadMIDI[2] != 0x68 || data.ReadMIDI[3] != 0x64)
            {
                // ヘッダーが"MThd"でない場合は終了
                return;
            }
            // ここでほしいのは分解能のみ
            data.BPQN = (short)((data.ReadMIDI[12] << 8) | data.ReadMIDI[13]); // 分解能はヘッダーの12バイト目と13バイト目に格納されている
            data.Count1 = 14; // ヘッダーの長さを更新
        }

        public static bool EventTrackHeader(DataModel data) //ヘッダーだったら読み飛ばして、終わりならtrueを返す
        {
            //データが終わる場合、ここの処理のタイミングで残りバイトが0となるはずなので、それを知らせてあげる
            //終わりの場合はCount1がindex範囲外になるので、それを判断する
            if (data.Count1 >= data.ReadMIDI.Length) return true; // データが終わる場合はtrueを返す
            //終わりじゃないなら、ヘッダーかどうかを判断して、ヘッダーなら読み飛ばす
            if (data.ReadMIDI[data.Count1] == 0x4D && data.ReadMIDI[data.Count1 + 1] == 0x54 && data.ReadMIDI[data.Count1 + 2] == 0x72 && data.ReadMIDI[data.Count1 + 3] == 0x6B)
            {
                //ヘッダーだったらそのバイト分読み飛ばす
                data.Count1 += 8; // トラックヘッダーの長さは8バイト
                //トラックが変わるので、デルタタイム合計値を0にし、カラーを次のコードにする
                data.AllDelta = data.BPQN * 4;
                if (data.DrawMode == 0)
                {
                    if (data.NowTrackId >= 15) // 色が64を超えたらリセット
                    {
                        data.NowTrackId = 1;
                    }
                    else
                    {
                        data.NowTrackId += 1;
                    }
                }
                else if (data.DrawMode == 1)
                {
                    data.NowTrackId += 16;
                    if (data.NowTrackId > 111) // TrackIdの最大値は126
                    {
                        data.NowTrackId = 0; // TrackIdをリセット
                    }
                    //LastTickをリセット
                }
                data.NoteBuf = new List<NoteOnBuf>[16, 128];
                for (int i = 0; i < 16; i++)
                {
                    for (int j = 0; j < 128; j++)
                    {
                        data.NoteBuf[i, j] = new List<NoteOnBuf>();
                    }
                }
                return false;
            }
            //ヘッダーじゃなかったら、何もしない
            return false;
        }

        public static void EventNoteOn(DataModel data)
        {
            TickCheck(data);
            byte channel = (byte)(data.ReadMIDI[data.Count1] & 0b_0000_1111);
            byte note = data.ReadMIDI[data.Count1 + 1];

            byte trackId = 0;
            if (data.DrawMode == 0)
            {
                trackId = data.NowTrackId;
            }
            else if (data.DrawMode == 1)
            {
                trackId = (byte)(data.NowTrackId + channel); // TrackIdを追加
            }
            //ノートオンがあった時にその時のデルタタイムを入れておいて、ノートオフがあった時にペアを見つけてノートデータを追加する。
            //これなら無視するときも速いんじゃね？

            //バッファに追加
            data.NoteBuf[channel, note].Add(new NoteOnBuf
            {
                Delta = data.AllDelta, // 現在のデルタタイムを保存
                Ch = channel,
                Note = note,
                Vel = data.ReadMIDI[data.Count1 + 2],
                TrackId = trackId // TrackIdを追加
            });
            //バイトを進める
            data.Count1 += 3; // NoteOnイベントは3バイトで構成されているため、3バイト進める
        }

        public static void EventNoteOff(DataModel data)
        {
            TickCheck(data);
            byte channel = (byte)(data.ReadMIDI[data.Count1] & 0b_0000_1111);
            byte note = (byte)data.ReadMIDI[data.Count1 + 1];

            byte trackId = 0;
            if (data.DrawMode == 0)
            {
                trackId = data.NowTrackId;
            }
            else if (data.DrawMode == 1)
            {
                trackId = (byte)(data.NowTrackId + channel); // TrackIdを追加
            }
            //ノートオンがあった時にその時のデルタタイムを入れておいて、ノートオフがあった時にペアを見つけてノートデータを追加する。
            //これなら無視するときも速いんじゃね？

            //バッファに追加
            //まずはこの時点で一番古いノートオンを探す

            try
            {
                bool ignore = false;

                if (data.NoteBuf[channel, note].Count == 0)
                {
                    data.Count1 += 3;
                    return;
                }

                NoteOnBuf noteOnBuf = noteOnBuf = data.NoteBuf[channel, note][0];
                //各条件に一致したもののみ追加する
                if (data.SettingData.IgnoreAND && noteOnBuf.Vel <= data.SettingData.IgnoreVelAND && data.AllDelta - noteOnBuf.Delta <= data.SettingData.IgnoreGateAND)
                {
                    //AND条件なら、ベロシティもゲートも満たしている場合に無視する
                    //data.NoteBuf[channel, note].RemoveAt(0); // ノートオンを削除
                    //data.Count1 += 3;
                    ignore = true;
                }
                else if (data.SettingData.IgnoreOR && (noteOnBuf.Vel <= data.SettingData.IgnoreVelOR || data.AllDelta - noteOnBuf.Delta <= data.SettingData.IgnoreGateOR))
                {
                    //OR条件なら、ベロシティかゲートのどちらかを満たしている場合に無視する
                    //data.NoteBuf[channel, note].RemoveAt(0); // ノートオンを削除
                    //data.Count1 += 3;
                    ignore = true;
                }
                //条件を満たしてない場合は、noteOnBufの位置にノートオンを、現在位置にノートオフを追加する

                data.Note[noteOnBuf.Delta].NoteOn.Add(new NoteOnData
                {
                    Ch = noteOnBuf.Ch,
                    Note = ignore ? (byte)(noteOnBuf.Note | 128) : noteOnBuf.Note,
                    Vel = noteOnBuf.Vel,
                    TrackId = noteOnBuf.TrackId // TrackIdを追加
                });
                data.NoteBuf[channel, note].RemoveAt(0); // ノートオンを削除
                data.Note[data.AllDelta].NoteOff.Add(new NoteOffData
                {
                    Ch = channel,
                    Note = ignore ? (byte)(note | 128) : note,
                    //Color = data.Color,
                    TrackId = trackId // TrackIdを追加
                });
                data.Count1 += 3; // NoteOffイベントは3バイトで構成されているため、3バイト進める
                return;
            }
            catch
            {
                data.Count1 += 3;
                return;
            }
        }

        public static void EventProgramChange(DataModel data)
        {
            TickCheck(data);
            //プログラムチェンジイベントの処理
            byte channel = (byte)(data.ReadMIDI[data.Count1] & 0b_0000_1111); // チャンネルは下位4ビット
            byte programNumber = data.ReadMIDI[data.Count1 + 1]; // プログラム番号
            int tick = data.AllDelta; // 現在のデルタタイム
                                      // プログラムチェンジイベントの処理
            data.Note[data.AllDelta].ProgramChange.Add(new DataByte
            {
                Ch = channel,
                Data = programNumber
            });
            //バイトを進める
            data.Count1 += 2; // プログラムチェンジイベントは2バイトで構成されているため、2バイト進める
        }

        public static void EventControlChange(DataModel data)
        {
            TickCheck(data);
            //コントロールチェンジイベントの処理
            //ここでほしいのはピッチベンド幅データのみなので、それだけ抽出してあとは読み飛ばす
            byte channel = (byte)(data.ReadMIDI[data.Count1] & 0b_0000_1111); // チャンネルは下位4ビット
            byte controlNumber = data.ReadMIDI[data.Count1 + 1]; // コントロール番号
            byte controlValue = data.ReadMIDI[data.Count1 + 2]; // コントロール値
            int tick = data.AllDelta; // 現在のデルタタイム
                                      // ピッチベンド幅データの処理
            if (controlNumber == 0x06)
            {
                // DataEntry（ピッチベンド幅）をBendRangeに追加
                data.Note[data.AllDelta].BendRange.Add(new DataByte
                {
                    Ch = channel,
                    Data = controlValue
                });
            }
            else if (controlNumber == 0x0B)
            {
                // Expression（表現）をExpressionに追加
                data.Note[data.AllDelta].Expression.Add(new DataByte
                {
                    Ch = channel,
                    Data = controlValue
                });
            }
            //バイトを進める
            data.Count1 += 3; // コントロールチェンジイベントは3バイトで構成されているため、3バイト進める
        }

        public static void EventPitch(DataModel data)
        {
            TickCheck(data);
            //チャンネルやピッチを抽出し、データを追加する
            byte channel = (byte)(data.ReadMIDI[data.Count1] & 0b_0000_1111); // チャンネルは下位4ビット
            short pitch = (short)((data.ReadMIDI[data.Count1 + 1]) | data.ReadMIDI[data.Count1 + 2] << 7); // ピッチは次の2バイト
            int tick = data.AllDelta; // 現在のデルタタイム
                                      // ピッチベンドイベントの処理
                                      //すでに同チャンネルのデータが存在する場合は、より8192に近い値を優先する
            if (data.Note[tick].Pitch.Any(n => n.Ch == channel))
            {
                // すでに同じチャンネルのデータが存在する場合はなにもしない
                if (pitch < 8192)
                {
                    //8192より小さい場合は、既存のデータよりも大きい値を優先する
                    if (data.Note[tick].Pitch.FirstOrDefault(p => p.Ch == channel)?.Data < pitch)
                    {
                        // 既存のデータよりも大きい値を優先する
                        data.Note[tick].Pitch.FirstOrDefault(p => p.Ch == channel)!.Data = pitch;
                    }
                }
                else
                {
                    //8192以上の場合は、既存のデータよりも小さい値を優先する
                    if (data.Note[tick].Pitch.FirstOrDefault(p => p.Ch == channel)?.Data > pitch)
                    {
                        // 既存のデータよりも小さい値を優先する
                        data.Note[tick].Pitch.FirstOrDefault(p => p.Ch == channel)!.Data = pitch;
                    }
                }
                //nullの警告が出ないように書き直す
                //data.Note[data.AllDelta].Pitch.FirstOrDefault(p => p.Ch == channel).Data = pitch; // 既存のデータを更新

            }
            else
            {
                //ない場合はデータを追加
                data.Note[data.AllDelta].Pitch.Add(new DataShort
                {
                    Ch = channel,
                    Data = pitch
                });
            }

            //バイトを進める
            data.Count1 += 3; // ピッチベンドイベントは3バイトで構成されているため、3バイト進める
        }

        public static void EventMeta(DataModel data)
        {
            //メタイベントの処理
            //メタイベントは、0xFFで始まり、次のバイトがタイプを示す
            byte metaType = data.ReadMIDI[data.Count1 + 1]; // メタイベントのタイプ
            int valueLength = GetVariableLength(data, 2); // メタイベントの値の長さを取得
            //テンポデータのみ受け取る
            if (metaType == 0x51) // テンポイベント
            {
                // テンポは3バイトの値で、ミリ秒単位で表される
                int nowTick = data.AllDelta;
                int tempo = (data.ReadMIDI[data.Count1] << 16) | (data.ReadMIDI[data.Count1 + 1] << 8) | data.ReadMIDI[data.Count1 + 2];
                // マイクロ秒単位のままでいいか→よくなかったわ
                //data.Note[data.AllDelta].Tempo = tempo;
                // テンポポイントを追加
                double elapsedMs = 0;
                if (data.TempoPoints.Count > 0)
                {
                    var prev = data.TempoPoints[^1];
                    int tickDiff = nowTick - prev.Tick;
                    // 区間の経過時間（ms）
                    //double sectionMs = prev.Tempo / data.BPQN * tickDiff / 1000;
                    double sectionMs = (prev.Tempo / 1000.0) / data.BPQN * tickDiff;
                    elapsedMs = prev.ElapsedMs + sectionMs;
                }
                else
                {
                    // 最初のテンポポイント（デフォルト500,000μs）
                    //tick0ならこれを初回として経過時間0で追加する
                    if (nowTick == data.BPQN * 4)
                    {
                        elapsedMs = 0;

                        data.TempoPoints.Add(new TempoPoint
                        {
                            Tick = 0,
                            Tempo = tempo,
                            ElapsedMs = elapsedMs
                        });
                        // バイトを進める
                        data.Count1 += 3;
                        return;

                    }
                    else //そうじゃないなら、現在tickまでをデフォルトテンポで計算し、今回tickの経過時間とする
                    {
                        elapsedMs = nowTick * 500.0 / data.BPQN; // デフォルトテンポ: 500,000μs = 500ms
                    }
                }
                data.TempoPoints.Add(new TempoPoint
                {
                    Tick = nowTick,
                    Tempo = tempo,
                    ElapsedMs = elapsedMs
                });


                // バイトを進める
                data.Count1 += 3;
            }
            //トラック終了
            else if (metaType == 0x2F) // トラック終了イベント
            {
                // トラック終了イベントは特に何もしない
                data.Count1 += valueLength; // トラック終了イベントの値の長さ分進める
                data.nextTrackHeader = true; // 次のトラックヘッダーを読み込むためにフラグを立てる
            }
            //それ以外はいらないのでデータ長分読み飛ばす
            else
            {
                // メタイベントの値を読み飛ばす
                data.Count1 += valueLength; // メタイベントの値の長さ分進める
            }
        }

        public static void EventSysEx(DataModel data)
        {
            //システムエクスクルーシブイベントの処理
            //システムエクスクルーシブイベントは、0xF0で始まり、0xF7で終わる
            //ここでは特に何も処理しない
            int valueLength = GetVariableLength(data, 1); // システムエクスクルーシブイベントの値の長さを取得
            // F7の場合は+１のバイトを進める
            if (data.ReadMIDI[data.Count1 + valueLength] == 0xF7)
            {
                data.Count1++; // F7のバイトを読み飛ばす
            }
            data.Count1 += valueLength; // システムエクスクルーシブイベントの値の長さ分進める
        }

        public static void TickCheck(DataModel data)
        {
            if (data.LastTick[data.AllDelta])
            {
                return;
            }
            TickData2 tickData = new TickData2();
            data.Note[data.AllDelta] = tickData;
            data.LastTick[data.AllDelta] = true; // 最後に処理したtickを更新
            Array.Clear(data.LastPitch, 0, data.LastPitch.Length);
        }

        public static int GetVariableLength(DataModel data, int offset)
        {
            //メタイベントの可変長値を取得するメソッド
            //スタートは3バイト目からとする
            int value = 0;
            byte buf = data.ReadMIDI[data.Count1 + offset];
            //最上位ビットが0になるまでループする
            while (buf >= 0x80)
            {
                value = value << 7 | (buf & 0b_0111_1111); // 下位7ビットを取得
                data.Count1++;
                buf = data.ReadMIDI[data.Count1 + offset]; // 次のバイトを取得
            }
            // 最後のバイトを加える
            value = value << 7 | buf; // 最後のバイトを加える
            data.Count1 += 1 + offset; // メタイベントのタイプと長さを読み飛ばすために3バイト進める
            return value; // 可変長値を返す
        }

        public static int GetDelta(DataModel data)
        {
            int delta = 0;
            byte buf = 0;

            for (int i = 0; i < 4; i++)
            {
                buf = data.ReadMIDI[data.Count1];
                //最上位ビットが1ならば、次のバイトも読み込む
                if (buf >= 0x80)
                {
                    delta = delta << 7 | buf & 0b_0111_1111;
                    data.Count1++;
                }
                else
                {
                    //最上位ビットが0なら、デルタタイムを加算して終了
                    delta = delta << 7 | buf;
                    data.Count1++;
                    return delta;
                }
            }
            return -1;
        }

        //public static int GetCurrentTick(double elapsedMs, DataModel data)
        //{
        //    if (data.TempoPoints.Count == 0)
        //        return (int)(elapsedMs * data.BPQN / 500.0); // デフォルトテンポ: 500,000μs = 500ms

        //    TempoPoint current = data.TempoPoints[0];
        //    for (int i = 1; i < data.TempoPoints.Count; i++)
        //    {
        //        if (elapsedMs < data.TempoPoints[i].ElapsedMs)
        //            break;
        //        current = data.TempoPoints[i];
        //    }

        //    // 区間内の経過時間
        //    double sectionMs = elapsedMs - current.ElapsedMs;
        //    // この区間のtick増分
        //    double tickDelta = sectionMs * data.BPQN / (current.Tempo / 1000.0);
        //    return current.Tick + (int)tickDelta;
        //}
    }
}
