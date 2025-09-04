using System;
using System.Drawing;
using System.Windows.Forms;

namespace IconList_Animation
{
    public partial class SaveLocationDialog : Form
    {
        public enum SaveChoice { Tool, Icon, Cancel }
        public SaveChoice SelectedChoice { get; private set; } = SaveChoice.Cancel;

        public SaveLocationDialog(
          string textTool = "",
          string textIcon = "",
          Image optionalImage = null,
          string cancelText = "キャンセル",
          string headerText = "操作を選択してください：") 
        {
            this.Text = "Message notification";
            this.Size = new Size(460, optionalImage != null ? 280 : 200);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int currentY = 20;
            int add_Ypoint = 30;

            Label lbl = new Label
            {
                Text = headerText,
                AutoSize = true,
                Location = new Point(20, currentY)
            };
            this.Controls.Add(lbl);
            currentY += 50;

            // 画像がある場合だけ追加
            if (optionalImage != null)
            {
                PictureBox picBox = new PictureBox
                {
                    Image = optionalImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(400, 120),
                    Location = new Point(20, currentY)
                };
                this.Controls.Add(picBox);
                currentY += picBox.Height + 10;
                add_Ypoint = 10;
            }

            int buttonWidth = 180;
            int spacing = 20;
            int buttonY = currentY;

            int btnX = 20;
            int buttonCount = 0;

            if (!string.IsNullOrWhiteSpace(textTool))
            {
                Button btnTool = new Button
                {
                    Text = textTool,
                    Location = new Point(btnX, buttonY),
                    Width = buttonWidth
                };
                btnTool.Click += (s, e) => { SelectedChoice = SaveChoice.Tool; this.DialogResult = DialogResult.OK; };
                this.Controls.Add(btnTool);
                btnX += buttonWidth + spacing;
                buttonCount++;
            }

            if (!string.IsNullOrWhiteSpace(textIcon))
            {
                Button btnIcon = new Button
                {
                    Text = textIcon,
                    Location = new Point(btnX, buttonY),
                    Width = buttonWidth
                };
                btnIcon.Click += (s, e) => { SelectedChoice = SaveChoice.Icon; this.DialogResult = DialogResult.OK; };
                this.Controls.Add(btnIcon);
                buttonCount++;
            }

            // キャンセルボタン
            Button btnCancel = new Button
            {
                Text = string.IsNullOrWhiteSpace(cancelText) ? "キャンセル" : cancelText,
                Width = 100,
                Location = new Point((this.ClientSize.Width - 100) / 2, buttonY + add_Ypoint) // ← 中央に配置
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; };
            this.Controls.Add(btnCancel);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.Name = "SaveLocationDialog";
            this.ResumeLayout(false);
        }
    }
}
