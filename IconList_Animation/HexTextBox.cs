using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IconList_Animation
{
    public class HexTextBox : TextBox
    {
        //-------------------------------------------------------------------------------
        // binary用テキストボックス
        //-------------------------------------------------------------------------------
        public HexTextBox()
        {
            this.TextAlign = HorizontalAlignment.Center;
            this.CharacterCasing = CharacterCasing.Upper;
            this.MaxLength = 8; // 最大8桁のHex（例：FFFFFFFF）
            this.KeyPress += OnKeyPress;
            this.TextChanged += OnTextChanged;
        }

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (!Uri.IsHexDigit(e.KeyChar))
            {
                e.Handled = true;
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            string clean = string.Concat(this.Text.Where(Uri.IsHexDigit)).ToUpper();
            if (this.Text != clean)
            {
                this.Text = clean;
                this.SelectionStart = clean.Length;
            }
        }

        public int Value
        {
            get
            {
                if (int.TryParse(this.Text, System.Globalization.NumberStyles.HexNumber, null, out int result))
                    return result;
                return 0;
            }
            set
            {
                this.Text = value.ToString("X8");
            }
        }
    }

}
