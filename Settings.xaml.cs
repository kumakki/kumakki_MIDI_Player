using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace kumakki_MIDI_Player
{
    /// <summary>
    /// Settings.xaml の相互作用ロジック
    /// </summary>
    public partial class Settings : Window
    {
        public SettingData settingData;
        public bool IsOK = false;

        public Settings(SettingData setting)
        {
            InitializeComponent();
            settingData = setting;
            chk_EnableFilterOr.IsChecked = settingData.IgnoreOR;
            chk_EnableFilterAnd.IsChecked = settingData.IgnoreAND;
            txt_IgnoreGateOr.Text = settingData.IgnoreGateOR.ToString();
            txt_IgnoreVelOr.Text = settingData.IgnoreVelOR.ToString();
            txt_IgnoreGateAnd.Text = settingData.IgnoreGateAND.ToString();
            txt_IgnoreVelAnd.Text = settingData.IgnoreVelAND.ToString();
            chk_ApplyIgnoreToDrawing.IsChecked = settingData.ApplyIgnoreToDrawing;
        }

        private void OnInput_(object sender, TextCompositionEventArgs e)
        {
            if (CheckInput(((TextBox)sender).Text, e.Text))
            {
                //入力が無効な場合、イベントをキャンセル
                e.Handled = true;
            }
        }

        private bool CheckInput(string text1, string text2)
        {
            //まず入力された文字が数字かどうかをチェック
            if (!char.IsDigit(text2, 0) && text2 != ".")
            {
                //数字でなければイベントをキャンセル
               return true;
            }

            //次に元のテキストと入力された数字を結合して、1～127の範囲に収まるかをチェック
            string newText = text1 + text2;
            if (newText.Length > 0)
            {
                //入力された文字列が空でない場合、数値に変換して範囲をチェック
                if (int.TryParse(newText, out int value))
                {
                    if (value < 0 || value > 127)
                    {
                        //範囲外の場合はイベントをキャンセル
                        return true;
                    }
                }
                else
                {
                    //数値に変換できない場合もイベントをキャンセル
                    return true;
                }
            }
            return false;
        }

        private void OnClick_OK(object sender, RoutedEventArgs e)
        {
            //設定を保存
            settingData.IgnoreOR = chk_EnableFilterOr.IsChecked ?? false;
            settingData.IgnoreAND = chk_EnableFilterAnd.IsChecked ?? false;
            settingData.IgnoreGateOR = Int32.Parse(txt_IgnoreGateOr.Text);
            settingData.IgnoreVelOR = Int32.Parse(txt_IgnoreVelOr.Text);
            settingData.IgnoreGateAND = Int32.Parse(txt_IgnoreGateAnd.Text);
            settingData.IgnoreVelAND = Int32.Parse(txt_IgnoreVelAnd.Text);
            settingData.ApplyIgnoreToDrawing = chk_ApplyIgnoreToDrawing.IsChecked ?? false;
            IsOK = true;
            this.Close();
        }

        private void OnClick_Cancel(object sender, RoutedEventArgs e)
        {
            //設定を保存せずに閉じる
            IsOK = false;
            this.Close();
        }
    }
}
