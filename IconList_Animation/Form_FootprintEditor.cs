
using Pokemon3genHackLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;

namespace IconList_Animation
{
    public partial class Form_FootprintEditor : Form
    {
        //-------------------------------------------------------------------------------
        // フィールド定義
        //-------------------------------------------------------------------------------
        private PictureBox pb;
        private Bitmap footprint;
        private int scale = 12;
        private bool isMouseDown = false;
        private bool isBucketMode = false;
        private string FileBasePath = "";
        private Byte[] romData;
        private int totalPokemonNum;

        private Button btnSave, btnPen, btnBucket;
        private Label lblOffset;
        private HexTextBox txtOffset;

        private Stack<ImageState> undoStack = new Stack<ImageState>();
        private Stack<ImageState> redoStack = new Stack<ImageState>();
        private const int MaxHistory = 20;

        public Bitmap EditedBitmap { get; private set; }
        public int Offset { get; private set; }
        public int Table { get; private set; }

        //-------------------------------------------------------------------------------
        // コンストラクタ
        //-------------------------------------------------------------------------------
        public Form_FootprintEditor(Bitmap footprintImage, string FileBasePath, int totalNum, Byte[] Binary_Data, int offset = 0, int table = 0, Point? parentLocation = null)
        {
            InitializeComponent();
            this.Text = "FootPrint Editor";
            this.Size = new Size(280, 350);
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ShowInTaskbar = true;
            this.MaximizeBox = false;
            this.FileBasePath = FileBasePath;
            this.totalPokemonNum = totalNum;

            this.footprint = new Bitmap(footprintImage);
            this.EditedBitmap = new Bitmap(footprintImage);
            this.Offset = offset;
            this.Table = table;
            romData = Binary_Data;

            //-----------------
            // pb：表示エリア
            //-----------------
            pb = new PictureBox
            {
                Location = new Point(20, 60),
                Size = new Size(16 * scale, 16 * scale),
                Image = Resize(footprint),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            pb.Paint += (s, e) => DrawGrid(e.Graphics, pb.Image, scale);
            pb.MouseDown += Pb_MouseDown;
            pb.MouseMove += Pb_MouseMove;
            pb.MouseUp += (s, e) => isMouseDown = false;

            //-----------------
            // ツールボタン類
            //-----------------
            btnSave = new Button { Location = new Point(20, 10), Size = new Size(32, 32), Image = Properties.Resources.save_icon };
            btnSave.Click += (s, e) => SaveEditedBitmap();

            btnPen = new Button { Location = new Point(60, 10), Size = new Size(32, 32), Image = Properties.Resources.pen_icon };
            btnPen.Click += (s, e) => { isBucketMode = false; HighlightButton(btnPen); };

            btnBucket = new Button { Location = new Point(100, 10), Size = new Size(32, 32), Image = Properties.Resources.bucket_icon };
            btnBucket.Click += (s, e) => { isBucketMode = true; HighlightButton(btnBucket); };

            //-----------------
            // オフセット表示
            //-----------------
            lblOffset = new Label
            {
                Location = new Point(pb.Left + 30, pb.Bottom + 5),
                AutoSize = true,
                Text = "Offset: 0x"
            };

            txtOffset = new HexTextBox
            {
                Location = new Point(pb.Left + 90, pb.Bottom + 3),
                Width = 70,
                ReadOnly = false,
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle,
                Text = $"{offset:X8}"                
            };

            //-----------------
            // 初期選択強調
            //-----------------
            HighlightButton(btnPen);

            //-----------------
            // ツールチップス追加
            //-----------------
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(btnSave, LanguageManager.Get("ToolTips_Saveicon"));
            toolTip.SetToolTip(btnPen, LanguageManager.Get("ToolTips_tool_pen"));
            toolTip.SetToolTip(btnBucket, LanguageManager.Get("ToolTips_tool_Flood"));

            //-----------------
            // コントロール追加
            //-----------------
            this.Controls.Add(pb);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnPen);
            this.Controls.Add(btnBucket);
            this.Controls.Add(lblOffset);
            this.Controls.Add(txtOffset);

            toolStripStatusLabel_SelectMode.Text = "Mode：FootPrint image";
            if (parentLocation.HasValue)
            {
                // メインフォームの右横あたりに表示
                this.Location = new Point(parentLocation.Value.X + 50, parentLocation.Value.Y + 50);
            }

        }

        //-------------------------------------------------------------------------------
        // Undo履歴追加
        //-------------------------------------------------------------------------------
        private void PushUndo()
        {
            undoStack.Push(new ImageState(footprint));
            while (undoStack.Count > MaxHistory)
                undoStack = new Stack<ImageState>(undoStack.Reverse().Take(MaxHistory));
            redoStack.Clear();
        }

        //-------------------------------------------------------------------------------
        // キー入力：Ctrl+Z/Y/V
        //-------------------------------------------------------------------------------
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                Undo(); return true;
            }
            else if (keyData == (Keys.Control | Keys.Y))
            {
                Redo(); return true;
            }
            else if (keyData == (Keys.Control | Keys.V))
            {
                PasteClipboardImage(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        //-------------------------------------------------------------------------------
        // Undo処理
        //-------------------------------------------------------------------------------
        private void Undo()
        {
            if (undoStack.Count == 0) return;
            redoStack.Push(new ImageState(footprint));
            footprint = undoStack.Pop().Image;
            pb.Image = Resize(footprint);
        }

        //-------------------------------------------------------------------------------
        // Redo処理
        //-------------------------------------------------------------------------------
        private void Redo()
        {
            if (redoStack.Count == 0) return;
            undoStack.Push(new ImageState(footprint));
            footprint = redoStack.Pop().Image;
            pb.Image = Resize(footprint);
        }

        //-------------------------------------------------------------------------------
        // 貼り付け処理
        //-------------------------------------------------------------------------------
        private void PasteClipboardImage()
        {
            if (!Clipboard.ContainsImage()) return;
            Bitmap pasted = new Bitmap(Clipboard.GetImage());

            PushUndo();

            for (int y = 0; y < Math.Min(16, pasted.Height); y++)
            {
                for (int x = 0; x < Math.Min(16, pasted.Width); x++)
                {
                    var pixel = pasted.GetPixel(x, y);
                    footprint.SetPixel(x, y, (pixel.R + pixel.G + pixel.B) / 3 < 128 ? Color.Black : Color.White);
                }
            }

            pb.Image = Resize(footprint);
        }

        //-------------------------------------------------------------------------------
        // クリック/ドラッグ処理
        //-------------------------------------------------------------------------------
        private void Pb_MouseDown(object sender, MouseEventArgs e)
        {
            isMouseDown = true;
            PushUndo();
            EditPixel(e);
        }

        private void Pb_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown) EditPixel(e);
        }

        private void EditPixel(MouseEventArgs e)
        {
            int x = e.X / scale;
            int y = e.Y / scale;
            if (x >= 0 && x < 16 && y >= 0 && y < 16)
            {
                Color drawColor = (e.Button == MouseButtons.Right) ? Color.White : Color.Black;

                if (isBucketMode)
                    FloodFill(footprint, x, y, footprint.GetPixel(x, y), drawColor);
                else
                    footprint.SetPixel(x, y, drawColor);

                pb.Image = Resize(footprint);
            }
        }

        //-------------------------------------------------------------------------------
        // 編集画像のセーブ処理
        //-------------------------------------------------------------------------------
        private void SaveEditedBitmap()
        {
            EditedBitmap = new Bitmap(footprint);
            string targetDir = "";
            string path = "";
            string name = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}";

            Form mainFormRaw = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "Main_Form");
            if (mainFormRaw is IconList_Animation.Main_Form mainForm)
            {
                var dialog = new SaveLocationDialog(
                    LanguageManager.Get("SaveDialog_Rom_Selecter1"),
                    LanguageManager.Get("SaveDialog_Rom_Selecter2"),
                    null, // ← nullでも可
                    LanguageManager.Get("SaveDialog_Rom_Selecter3"),
                    LanguageManager.Get("SaveDialog_Rom_Selecter4"));

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string FileName = Path.GetFileNameWithoutExtension(FileBasePath);

                    if (dialog.SelectedChoice == SaveLocationDialog.SaveChoice.Tool)
                    {
                        targetDir = FileBasePath;
                        targetDir = Path.Combine(Path.GetDirectoryName(targetDir), $"{name}.gba");
                    }
                    else if (dialog.SelectedChoice == SaveLocationDialog.SaveChoice.Icon)
                    {
                        targetDir = FileBasePath;
                    }

                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        // Bitmap を ExportSprite に直接渡す（CreateIndexedImage は使わない）
                        byte[] tileDataFP = GBATileMode0.ExtractFootprintData(EditedBitmap);

                        try
                        {
                            int offset1 = (int)($"0x{txtOffset.Text}").ConvertFromHex();

                            // ROM配列にオフセット指定で上書き
                            WriteImageDataToRom(romData, offset1, tileDataFP);
                            RomEditExtensions.SetWord(romData, Table, offset1 + 0x08000000); // 指定テーブルに4byteオフセットを代入
                        }
                        catch
                        {
                            // 入力オフセットが間違っていた場合は取り込み元のアドレスを使用する
                            WriteImageDataToRom(romData, Offset, tileDataFP);
                        }

                        // 保存
                        File.WriteAllBytes(targetDir, romData);
                        MessageBox.Show(LanguageManager.Get("complete_OtherMethod"));

                        // メインフォーム再読み込み（StopAnimation呼び出し）
                        mainForm.LoadIconsFromRom(targetDir, totalPokemonNum);
                    }
                }

            }
        }

        //-------------------------------------------------------------------------------
        // 拡大描画用Bitmap生成
        //-------------------------------------------------------------------------------
        private Bitmap Resize(Bitmap original)
        {
            Bitmap bmp = new Bitmap(original.Width * scale, original.Height * scale);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(original, 0, 0, bmp.Width, bmp.Height);
            }
            return bmp;
        }

        //-------------------------------------------------------------------------------
        // グリッド描画
        //-------------------------------------------------------------------------------
        private void DrawGrid(Graphics g, System.Drawing.Image img, int scale)
        {
            if (img == null) return;
            int w = img.Width, h = img.Height;
            using (Pen pen = new Pen(Color.FromArgb(100, Color.Gray)))
            {
                for (int x = 0; x <= w; x++) g.DrawLine(pen, x * scale, 0, x * scale, h * scale);
                for (int y = 0; y <= h; y++) g.DrawLine(pen, 0, y * scale, w * scale, y * scale);
            }
        }

        //-------------------------------------------------------------------------------
        // バケツ塗り
        //-------------------------------------------------------------------------------
        private void FloodFill(Bitmap bmp, int x, int y, Color target, Color replacement)
        {
            if (target.ToArgb() == replacement.ToArgb()) return;
            var q = new Queue<Point>();
            q.Enqueue(new Point(x, y));
            while (q.Count > 0)
            {
                Point pt = q.Dequeue();
                if (pt.X < 0 || pt.X >= 16 || pt.Y < 0 || pt.Y >= 16) continue;
                if (bmp.GetPixel(pt.X, pt.Y).ToArgb() != target.ToArgb()) continue;

                bmp.SetPixel(pt.X, pt.Y, replacement);
                q.Enqueue(new Point(pt.X + 1, pt.Y));
                q.Enqueue(new Point(pt.X - 1, pt.Y));
                q.Enqueue(new Point(pt.X, pt.Y + 1));
                q.Enqueue(new Point(pt.X, pt.Y - 1));
            }
        }

        //-------------------------------------------------------------------------------
        // 選択中のボタン枠を強調
        //-------------------------------------------------------------------------------
        private void HighlightButton(Button selected)
        {
            btnPen.FlatStyle = FlatStyle.Standard;
            btnBucket.FlatStyle = FlatStyle.Standard;
            selected.FlatStyle = FlatStyle.Popup;
        }

        private void Form_FootprintEditor_Load(object sender, EventArgs e)
        {

        }

        //-------------------------------------------------------------------------------
        // ロムbinaryデータに上書き
        //-------------------------------------------------------------------------------
        public static void WriteImageDataToRom(byte[] romData, int offset, byte[] imageData)
        {
            if (offset + imageData.Length > romData.Length)
                throw new ArgumentOutOfRangeException("ROMサイズを超えています。");

            Array.Copy(imageData, 0, romData, offset, imageData.Length);
        }
        //-------------------------------------------------------------------------------
        // 編集履歴保存用クラス
        //-------------------------------------------------------------------------------
        private class ImageState
        {
            public Bitmap Image { get; }
            public ImageState(Bitmap bmp) => Image = (Bitmap)bmp.Clone();
        }
    }
}
