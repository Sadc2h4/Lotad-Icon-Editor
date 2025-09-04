//-----------------
// Form_ImageViewer.cs：操作可能エリアと描画エリアを分離
//-----------------

using Pokemon3genHackLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace IconList_Animation
{
    public partial class Form_ImageViewer : Form
    {
        private PictureBox pb1, pb2, pbOverlay;   // 表示ピクチャーボックス指定
        private Bitmap image1, image2;            // 読込画像データ指定
        private Label lblOffset1, lblOffset2;                 // オフセットラベル
        private HexTextBox txtOffset1, txtOffset2;   // オフセットテキストボックスの指定
        private int Offset1, Offset2, OffsetP, pallet4str;
        private Byte[] romData;
        private string imageFolderPath = "";      // 画像の保存場所パス
        private string FileBasePath = "";         // 読込対象のパス
        private int totalPokemonNum = 412;        // ポケモン最大読込数

        private int scale = 12;                   // 拡大倍率
        private int alphaPercentage = 100;        // overlayの初期透過率
        private bool isEdited = false;            // 画面編集済みフラグ（初期はfalse）

        private Color selectedColor;              // 選択中の色の保持
        private Panel selectedColorBox;           // ↑を表示するパネル
        private Func<Color> getSelectedColorFunc; // メインフォームからの引き渡し色
        private List<Color> basePalette;　　　　　// imageの16色パレット情報
        List<List<Color>> ExternalPalettes = new List<List<Color>>();
        private ComboBox cbPaletteSelect;


        // default色の指定
        private Color baseFillColor = Color.Transparent;

        // 状態管理
        private enum SelectionMode { None, Selecting, Selected, Moving }
        private SelectionMode selectionMode = SelectionMode.None;
        private Point selectionStart, selectionEnd;
        private Rectangle selectionRect;
        private Point dragStartScreenPosition;
        private Point currentSelectionPosition;
        private bool isClickOnly = false; // 範囲なしのクリックかどうか判定用

        // コピー＆描画用
        private Bitmap selectedRegionImage1 = null;
        private Bitmap selectedRegionImage2 = null;
        private Bitmap image1BeforeMove = null;
        private Bitmap image2BeforeMove = null;

        private bool isDraggingSelection = false;
        private Rectangle previousSelectionRect; // 移動前の範囲を保持

        private bool isEditOperationInProgress = false;
        private PictureBox lastClickedBox = null; // 最後にクリックされたpbを記録

        // 戻る/進むの処理用
        private class ImageState
        {
            public Bitmap Image1 { get; }
            public Bitmap Image2 { get; }
            public int PaletteIndex { get; }

            public ImageState(Bitmap img1, Bitmap img2, int paletteIndex)
            {
                Image1 = (Bitmap)img1.Clone();
                Image2 = (Bitmap)img2.Clone();
                PaletteIndex = paletteIndex;
            }
        }

        private Stack<ImageState> undoStack = new Stack<ImageState>();
        private Stack<ImageState> redoStack = new Stack<ImageState>();
        private const int MaxHistory = 20; // 記憶できる最大数

        // モード管理用
        private enum EditMode
        {
            Pen,       // 単点描画
            Bucket,    // 同色塗りつぶし
            Replace    // 全体色置換
        }
        private EditMode currentEditMode = EditMode.Pen;
        private Button btnPen, btnBucket, btnReplace, btnSave, btnPalette, btnOpenFolder;

        //-------------------------------------------------------------------------------
        // コンストラクタ
        //-------------------------------------------------------------------------------
　　    public Form_ImageViewer(
          Bitmap img1, Bitmap img2, string imageFolderPath, int totalNum, string FileBasePath,
          Func<Color> getSelectedColor, List<Color> palette, 
          Byte[] Binary_Data, int get_offset1 = 0, int get_offset2 = 0,  int pallet_offset = 0, int pallet_str = 0,
          string Select_mode ="")
        {
            InitializeComponent();
            this.Text = "Image Editor";
            this.Size = new Size(850, 530);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.imageFolderPath = imageFolderPath;
            this.FileBasePath = FileBasePath;

            image1 = new Bitmap(img1);
            image2 = new Bitmap(img2);
            Offset1 = get_offset1;
            Offset2 = get_offset2;
            OffsetP = pallet_offset;
            pallet4str = pallet_str;
            romData = Binary_Data;
            totalPokemonNum = totalNum;
            getSelectedColorFunc = getSelectedColor;
            selectedColor = getSelectedColor != null ? getSelectedColor() : Color.Black;
            baseFillColor = selectedColor;
            this.basePalette = palette ?? new List<Color>(); // null対策

            //-----------------
            // pb1：操作対象（image1）
            //-----------------
            pb1 = new PictureBox
            {
                Location = new Point(20, 60),
                Size = new Size(32 * scale, 32 * scale),
                Image = ResizeImage(image1, scale),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            pb1.Paint += (s, e) => DrawGrid(e.Graphics, pb1.Image, scale);
            pb1.MouseDown += (s, e) =>
            {
                lastClickedBox = pb1;
                HandleMouseDown(new Point(e.X / scale, e.Y / scale), e.Button, ref image1, pb1);
            };
            pb1.MouseMove += (s, e) =>
            {
                lastClickedBox = pb1;
                HandleMouseMove(new Point(e.X / scale, e.Y / scale), e.Button, ref image1, pb1);                
            };
            pb1.MouseUp += (s, e) =>
            {
                lastClickedBox = pb1;
                HandleMouseUp(new Point(e.X / scale, e.Y / scale), e.Button, ref image1, pb1);                
            };


            //-----------------
            // pb2：image2表示用（操作不可）
            //-----------------
            pb2 = new PictureBox
            {
                Location = new Point(pb1.Right + 20, 60),
                Size = new Size(32 * scale, 32 * scale),
                Image = ResizeImage(image1, scale),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            pb2.Paint += (s, e) => DrawGrid(e.Graphics, pb2.Image, scale);

            //-----------------
            // pbOverlay：image1の透過オーバーレイ（上に乗せる・操作なし）
            //-----------------
            pbOverlay = new PictureBox
            {
                BackColor = Color.Transparent,
                Location = new Point(0, 0),
                Size = new Size(32 * scale, 32 * scale)
            };
            pbOverlay.Paint += (s, e) => DrawOverlayWithAlpha(e.Graphics, image2, scale, alphaPercentage);
            pbOverlay.MouseDown += (s, e) =>
            {
                lastClickedBox = pbOverlay;
                HandleMouseDown(new Point(e.X / scale, e.Y / scale), e.Button, ref image2, pbOverlay);
            };
            pbOverlay.MouseMove += (s, e) =>
            {
                lastClickedBox = pbOverlay;
                HandleMouseMove(new Point(e.X / scale, e.Y / scale), e.Button, ref image2, pbOverlay);
            };

            pbOverlay.MouseUp += (s, e) =>
            {
                lastClickedBox = pbOverlay;
                HandleMouseUp(new Point(e.X / scale, e.Y / scale), e.Button, ref image2, pbOverlay);
            };

            pb2.Controls.Add(pbOverlay);
            pbOverlay.BringToFront();

            //-----------------
            // 差分透明度スライダー
            //-----------------
            int yBase = 32 * scale + 90;
            //var labelAlpha = new Label { Text = "差分透明度", Location = new Point(360, yBase - 30), AutoSize = true };
            var slider = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Value = alphaPercentage, Location = new Point(720 - 250 - 50, 10), Width = 250 };
            slider.Scroll += (s, e) => { alphaPercentage = slider.Value; pbOverlay.Invalidate(); };

            //-----------------
            // Offset 1 表示
            //-----------------
            Label lblOffset1 = new Label
            {
                Text = "Offset: 0x",
                Location = new Point(pb1.Left + 100, pb1.Bottom + 5),
                AutoSize = true
            };

            txtOffset1 = new HexTextBox
            {
                Location = new Point(pb1.Left + 110 + 50, pb1.Bottom + 3),
                Width = 100,
                ReadOnly = false,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Text = $"{get_offset1:X8}"                  
            };

            //-----------------
            // Offset 2 表示
            //-----------------
            Label lblOffset2 = new Label
            {
                Text = "Offset: 0x",
                Location = new Point(pb2.Left + 100, pb2.Bottom + 5),
                AutoSize = true
            };

            txtOffset2 = new HexTextBox
            {
                Location = new Point(pb2.Left + 100 + 50, pb2.Bottom + 5),
                Width = 100,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center,
                Text = $"{get_offset2:X8}"
            };
            

            //-----------------
            // 選択色表示
            //-----------------
            var selectedColorLabel = new Label { Text = LanguageManager.Get("Select_color"), Location = new Point(680, 20), AutoSize = true };
            selectedColorBox = new Panel { Location = new Point(760, 18), Size = new Size(32, 20), BorderStyle = BorderStyle.FixedSingle, BackColor = selectedColor };

            //-----------------
            // ツールボタン表示
            //-----------------
            btnSave = new Button { Location = new Point(20, 10), Size = new Size(32, 32), Image = Properties.Resources.save_icon };
            btnOpenFolder = new Button { Location = new Point(60, 10), Size = new Size(32, 32), Image = Properties.Resources.folder_icon };
            btnPalette = new Button { Location = new Point(100, 10), Size = new Size(32, 32), Image = Properties.Resources.palette_icon };

            btnPen = new Button { Location = new Point(180, 10), Size = new Size(32, 32), Image = Properties.Resources.pen_icon };
            btnBucket = new Button { Location = new Point(220, 10), Size = new Size(32, 32), Image = Properties.Resources.bucket_icon };
            btnReplace = new Button { Location = new Point(260, 10), Size = new Size(32, 32), Image = Properties.Resources.magic_icon };

            // 初期選択（ペン）
            SetEditMode(EditMode.Pen);

            btnSave.Click += (s, e) => Saveimg();
            btnOpenFolder.Click += (s, e) => OpenFolder();
            btnPalette.Click += (s, e) => palletMode();

            btnPen.Click += (s, e) => SetEditMode(EditMode.Pen);
            btnBucket.Click += (s, e) => SetEditMode(EditMode.Bucket);
            btnReplace.Click += (s, e) => SetEditMode(EditMode.Replace);

            toolStripStatusLabel_SelectMode.Text = Select_mode;


            //-----------------
            // パレット選択プルダウン
            //-----------------
            this.cbPaletteSelect = new ComboBox
            {
                Location = new Point(300, 15),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // PalletResources フォルダからパレット名を読み込む
            string paletteDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PalletResources");
            var paletteFiles = Directory.GetFiles(paletteDir, "*.png").OrderBy(f => f).ToList();

            foreach (var file in paletteFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                cbPaletteSelect.Items.Add(fileName);
            }

            // PalletResources からパレット一覧を収集
            List<List<Color>> externalPalettes = new List<List<Color>>();
            foreach (var file in paletteFiles)
            {
                using (var bmp = new Bitmap(file))
                {
                    externalPalettes.Add(ExtractPalette(bmp));
                }
            }
            ExternalPalettes = externalPalettes; // default色リストを保存

            // プルダウンを変更した際の処理
            int matchedIndex = FindMatchingPaletteId(basePalette, externalPalettes);
            cbPaletteSelect.SelectedIndex = matchedIndex >= 0 ? matchedIndex : 0;

            cbPaletteSelect.SelectedIndexChanged += (s, e) =>
            {
                string selectedName = cbPaletteSelect.SelectedItem.ToString();
                string path = Path.Combine(paletteDir, selectedName + ".png");

                if (File.Exists(path))
                {
                    using (var bmp = new Bitmap(path))
                    {
                        List<Color> newPalette = ExtractPalette(bmp);
                        basePalette = newPalette;
                                                

                        // 再描画用にパレット変換した画像を作成
                        image1 = ConvertImageToPalette(image1, newPalette);
                        image2 = ConvertImageToPalette(image2, newPalette);

                        pb1.Image = ResizeImage(image1, scale);
                        pb2.Image = ResizeImage(image2, scale);
                        pbOverlay.Invalidate();

                        // Main_Form 側のパレットも更新
                        Form mainForm = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "Main_Form");
                        if (mainForm is Main_Form mf)
                        {
                            mf.Invoke(new Action(() => mf.DrawPalette(newPalette)));

                            // ROMモード時は、最適なパレットIDを検索し、ROMデータ更新
                            if (toolStripStatusLabel_SelectMode.Text == "Mode：Rom image")
                            {
                                int bestIndex = mf.FindBestPalette(image1, mf.GetPalettes());
                                romData[pb1.Focused ? Offset1 : Offset2] = (byte)bestIndex;
                            }
                        }
                        //PushUndo(); // 戻る処理の記録
                    }
                }

            };

            //-----------------
            // ツールチップス追加
            //-----------------
            ToolTip toolTip = new ToolTip();

            toolTip.SetToolTip(btnSave, LanguageManager.Get("ToolTips_Saveicon"));
            toolTip.SetToolTip(btnOpenFolder, LanguageManager.Get("ToolTips_FolderOpen"));
            toolTip.SetToolTip(btnPalette, LanguageManager.Get("ToolTips_palletMode"));
            toolTip.SetToolTip(btnPen, LanguageManager.Get("ToolTips_tool_pen"));
            toolTip.SetToolTip(btnBucket, LanguageManager.Get("ToolTips_tool_Flood"));
            toolTip.SetToolTip(btnReplace, LanguageManager.Get("ToolTips_tool_stick"));
            toolTip.SetToolTip(cbPaletteSelect, LanguageManager.Get("ToolTips_palletDropDown"));

            //-----------------
            // コントロール追加
            //-----------------
            this.Controls.Add(pb1);
            this.Controls.Add(pb2);
            this.Controls.Add(slider);
            this.Controls.Add(selectedColorLabel);
            this.Controls.Add(selectedColorBox);
            this.Controls.Add(txtOffset1);
            this.Controls.Add(lblOffset1);
            this.Controls.Add(txtOffset2);
            this.Controls.Add(lblOffset2);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnOpenFolder);
            this.Controls.Add(btnPalette);
            this.Controls.Add(btnPen);
            this.Controls.Add(btnBucket);
            this.Controls.Add(btnReplace);
            this.Controls.Add(cbPaletteSelect);

            PushUndo(); // 初期表示の画面を記録する
        }

        //-------------------------------------------------------------------------------------
        // マウスダウン共通処理
        //-------------------------------------------------------------------------------------
        private void HandleMouseDown(Point grid, MouseButtons button , ref Bitmap targetImage, PictureBox targetBox)
        {
            if (button == MouseButtons.Left)
            {
                if (!isEditOperationInProgress)
                {
                    PushUndo();  // 1回の操作の始まりでのみ履歴記録
                    isEditOperationInProgress = true;
                }
            }

            if (button == MouseButtons.Right)
            {
                // 右クリック時は座標の色を選択
                if (grid.X >= 0 && grid.Y >= 0 && grid.X < targetImage.Width && grid.Y < targetImage.Height)
                    SetSelectedColor(targetImage.GetPixel(grid.X, grid.Y));
                return;
            }

            if (button == MouseButtons.Left && selectionMode == SelectionMode.Selected && selectionRect.Contains(grid))
            {
                // 選択済み範囲をクリック → 移動開始
                selectionMode = SelectionMode.Moving;
                dragStartScreenPosition = Cursor.Position;

                if (targetImage == image1)
                    image1BeforeMove = (Bitmap)targetImage.Clone();
                else if (targetImage == image2)
                    image2BeforeMove = (Bitmap)targetImage.Clone();

                previousSelectionRect = selectionRect; // ← 元の場所を保持
                isDraggingSelection = true;
                return;
            }

            if (selectionMode == SelectionMode.Selected || selectionMode == SelectionMode.Selecting)
            {
                // 既に表示済みの赤枠が有った際は消す処理
                selectionMode = SelectionMode.None;
                selectionRect = Rectangle.Empty;

                // 他方のPictureBoxを再描画（赤枠を消す）
                if (targetImage == image1)
                    pbOverlay.Invalidate();
                else
                    pb1.Invalidate();
            }

            // それ以外はクリック or 範囲選択判定へ
            if (button == MouseButtons.Left)
            {
                isClickOnly = true;
                selectionStart = selectionEnd = grid;
                selectionMode = SelectionMode.None;
            }
        }

        //-------------------------------------------------------------------------------------
        // マウスムーブ共通処理
        //-------------------------------------------------------------------------------------
        private void HandleMouseMove(Point grid, MouseButtons button, ref Bitmap targetImage, PictureBox targetBox)
        {            
            if (button != MouseButtons.Left)
                return;

            if (isClickOnly &&
                (Math.Abs(grid.X - selectionStart.X) > 0 || Math.Abs(grid.Y - selectionStart.Y) > 0))
            {
                selectionMode = SelectionMode.Selecting;
                selectionEnd = grid;
                targetBox.Invalidate();
                return;
            }

            if (selectionMode == SelectionMode.Selecting)
            {
                selectionEnd = grid;
                targetBox.Invalidate();
            }

            if (selectionMode == SelectionMode.Moving && isDraggingSelection)
            {
                Point current = Cursor.Position;
                int dx = (current.X - dragStartScreenPosition.X) / scale;
                int dy = (current.Y - dragStartScreenPosition.Y) / scale;

                if (dx != 0 || dy != 0)
                {
                    // 対象画像に応じて移動前バックアップを使う
                    Bitmap before = targetImage == image1 ? image1BeforeMove : image2BeforeMove;

                    if (before != null)
                    {
                        targetImage = (Bitmap)before.Clone();

                        // 対象画像に応じて選択領域を取得
                        Bitmap selectedRegion = targetImage == image1 ? selectedRegionImage1 : selectedRegionImage2;

                        if (selectedRegion != null)
                        {
                            MoveSelectedRegion(dx, dy, ref targetImage, selectedRegion);
                            dragStartScreenPosition = Cursor.Position;
                            targetBox.Image = ResizeImage(targetImage, scale);
                            targetBox.Invalidate();
                        }
                    }
                }
            }
        }

        //-------------------------------------------------------------------------------------
        // マウスアップ共通処理
        //-------------------------------------------------------------------------------------
        private void HandleMouseUp(Point grid, MouseButtons button, ref Bitmap targetImage, PictureBox targetBox)
        {            

            if (selectionMode == SelectionMode.Selecting)
            {
                // グリッドの範囲選択決定処理
                selectionEnd = grid;
                int x1 = Math.Min(selectionStart.X, selectionEnd.X);
                int y1 = Math.Min(selectionStart.Y, selectionEnd.Y);
                int x2 = Math.Max(selectionStart.X, selectionEnd.X);
                int y2 = Math.Max(selectionStart.Y, selectionEnd.Y);
                selectionRect = new Rectangle(x1, y1, x2 - x1 + 1, y2 - y1 + 1);
                selectionRect.Intersect(new Rectangle(0, 0, targetImage.Width, targetImage.Height));　// 範囲が外に飛び出さないようにする
                CopySelectedRegion(ref targetImage);
                currentSelectionPosition = selectionRect.Location;
                selectionMode = SelectionMode.Selected;
                isEdited = true;
            }

            else if (isClickOnly &&
                    grid.X >= 0 && grid.Y >= 0 &&
                    grid.X < targetImage.Width && grid.Y < targetImage.Height)
            {
                //PushUndo(); // 戻る処理の記録

                if (currentEditMode == EditMode.Pen)
                {
                    targetImage.SetPixel(grid.X, grid.Y, selectedColor);
                }
                else if (currentEditMode == EditMode.Bucket)
                {
                    Color target = targetImage.GetPixel(grid.X, grid.Y);
                    FloodFill(grid.X, grid.Y, target, selectedColor, ref targetImage);
                }
                else if (currentEditMode == EditMode.Replace)
                {
                    Color target = targetImage.GetPixel(grid.X, grid.Y);
                    ReplaceAll(target, selectedColor, ref targetImage);
                }

                isEdited = true;
            }

            else if (selectionMode == SelectionMode.Moving && isDraggingSelection)
            {
                // 範囲移動終了
                //PushUndo(); // 戻る処理の記録
                //selectionMode = SelectionMode.Selected;

                selectionMode = SelectionMode.None;
                selectionStart = Point.Empty;
                selectionEnd = Point.Empty;
                selectionRect = Rectangle.Empty;


                isDraggingSelection = false;
                isEdited = true;
            }

            isClickOnly = false;
            targetBox.Image = ResizeImage(targetImage, scale);
            targetBox.Invalidate();

            isEditOperationInProgress = false; // 操作フラグをfalseにする
        }

        //-------------------------------------------------------------------------------------
        // バケツ塗りの処理
        //-------------------------------------------------------------------------------------
        private void FloodFill(int x, int y, Color targetColor, Color newColor, ref Bitmap bmp)
        {
            if (targetColor.ToArgb() == newColor.ToArgb()) return;

            Queue<Point> q = new Queue<Point>();
            q.Enqueue(new Point(x, y));

            while (q.Count > 0)
            {
                Point p = q.Dequeue();
                if (p.X < 0 || p.Y < 0 || p.X >= bmp.Width || p.Y >= bmp.Height)
                    continue;

                if (bmp.GetPixel(p.X, p.Y).ToArgb() != targetColor.ToArgb())
                    continue;

                bmp.SetPixel(p.X, p.Y, newColor);

                q.Enqueue(new Point(p.X - 1, p.Y));
                q.Enqueue(new Point(p.X + 1, p.Y));
                q.Enqueue(new Point(p.X, p.Y - 1));
                q.Enqueue(new Point(p.X, p.Y + 1));
            }
        }

        //-------------------------------------------------------------------------------------
        // 色置換の処理
        //-------------------------------------------------------------------------------------
        private void ReplaceAll(Color from, Color to, ref Bitmap bmp)
        {
            if (from.ToArgb() == to.ToArgb()) return;

            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).ToArgb() == from.ToArgb())
                        bmp.SetPixel(x, y, to);
        }

        //-------------------------------------------------------------------------------------
        // モード選択切り替え処理
        //-------------------------------------------------------------------------------------
        private void SetEditMode(EditMode mode)
        {
            currentEditMode = mode;

            // 全部デフォルトに戻す
            btnPen.FlatStyle = FlatStyle.Standard;
            btnBucket.FlatStyle = FlatStyle.Standard;
            btnReplace.FlatStyle = FlatStyle.Standard;

            btnPen.FlatAppearance.BorderSize = 1;
            btnBucket.FlatAppearance.BorderSize = 1;
            btnReplace.FlatAppearance.BorderSize = 1;

            // 選択中のボタンだけ強調
            Button selected = null;
            switch (mode)
            {
                case EditMode.Pen: selected = btnPen; break;
                case EditMode.Bucket: selected = btnBucket; break;
                case EditMode.Replace: selected = btnReplace; break;
            }

            if (selected != null)
            {
                selected.FlatStyle = FlatStyle.Flat;
                selected.FlatAppearance.BorderColor = Color.Blue;
                selected.FlatAppearance.BorderSize = 2;
            }
        }
        //-------------------------------------------------------------------------------------
        // 編集画像のセーブ処理
        //-------------------------------------------------------------------------------------
        private void Saveimg()
        {            
            if (!isEdited)
            {
                MessageBox.Show(LanguageManager.Get("Error_No_change"));
                return;
            }

            string targetDir = "";
            string path = "";
            string name = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}";

            Form mainFormRaw = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "Main_Form");
            if (mainFormRaw is IconList_Animation.Main_Form mainForm)
            {

                //-----------------
                // romモード時の処理
                //-----------------
                if (toolStripStatusLabel_SelectMode.Text == "Mode：Rom image")
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
                            byte[] tileData1 = GBATileMode0.ExportSprite(image1, basePalette.ToArray());
                            byte[] tileData2 = GBATileMode0.ExportSprite(image2, basePalette.ToArray());

                            try
                            {
                                // テキストボックスのアドレスを使用して保存
                                int offset1 = (int)($"0x{txtOffset1.Text}").ConvertFromHex();
                                int offset2 = offset1 + 512;


                                WriteImageDataToRom(romData, offset1, tileData1);
                                WriteImageDataToRom(romData, offset2, tileData2);

                                RomEditExtensions.SetWord(romData, pallet4str, offset1 + 0x08000000); // 指定テーブルに4byteオフセットを代入

                            }
                            catch (FormatException ex)
                            {
                                // 入力オフセットが間違っていた場合は取り込み元のアドレスを使用する

                                // ROM配列にオフセット指定で上書き
                                WriteImageDataToRom(romData, Offset1, tileData1);
                                WriteImageDataToRom(romData, Offset2, tileData2);
                            }


                            if (cbPaletteSelect.SelectedIndex >= 0)
                            {
                                int paletteId = cbPaletteSelect.SelectedIndex; // Pallet_1 → index 0

                                if (paletteId < 256) // ROMの仕様に収まる範囲
                                {
                                    romData[OffsetP] = (byte)paletteId;
                                }
                            }

                            // 保存
                            File.WriteAllBytes(targetDir, romData);
                            MessageBox.Show(LanguageManager.Get("complete_OtherMethod"));

                            // メインフォーム再読み込み（StopAnimation呼び出し）
                            mainForm.LoadIconsFromRom(targetDir, totalPokemonNum);
                        }

                    }
                }
                else
                //-----------------
                // Folderモード時の処理
                //-----------------
                {
                    // カスタムダイアログで保存場所を指定
                    var dialog = new SaveLocationDialog(
                        LanguageManager.Get("SaveDialog_Folder_Selecter1"),
                        LanguageManager.Get("SaveDialog_Folder_Selecter2"),
                        null, // ← nullでも可
                        LanguageManager.Get("SaveDialog_Folder_Selecter3"),
                        LanguageManager.Get("SaveDialog_Folder_Selecter4"));
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        string FileName = Path.GetFileNameWithoutExtension(imageFolderPath);

                        if (dialog.SelectedChoice == SaveLocationDialog.SaveChoice.Tool)
                            targetDir = AppDomain.CurrentDomain.BaseDirectory;
                        else if (dialog.SelectedChoice == SaveLocationDialog.SaveChoice.Icon)
                            targetDir = imageFolderPath;


                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            path = Path.Combine(targetDir, name);
                            Directory.CreateDirectory(path);

                            // パレットを使用して減色＆インデックス形式で保存
                            Bitmap indexed1 = CreateIndexedImage(image1, basePalette);
                            Bitmap indexed2 = CreateIndexedImage(image2, basePalette);

                            indexed1.Save(Path.Combine(path, $"{FileName}(1).bmp"), ImageFormat.Bmp);
                            indexed2.Save(Path.Combine(path, $"{FileName}(2).bmp"), ImageFormat.Bmp);

                            indexed1.Dispose();
                            indexed2.Dispose();
                        }
                                                
                        MessageBox.Show(LanguageManager.Get("complete_OtherMethod"));

                        // メインフォーム再読み込み（StopAnimation呼び出し）
                        mainForm.LoadIcons(imageFolderPath);
                    };
                }
            }  
        }

        public static void WriteImageDataToRom(byte[] romData, int offset, byte[] imageData)
        {
            if (offset + imageData.Length > romData.Length)
                throw new ArgumentOutOfRangeException("ROMサイズを超えています。");

            Array.Copy(imageData, 0, romData, offset, imageData.Length);
        }

        //-------------------------------------------------------------------------------------
        // 編集画像のフォルダを開く処理
        //-------------------------------------------------------------------------------------
        private void OpenFolder()
        {
            if (!string.IsNullOrEmpty(imageFolderPath) && Directory.Exists(imageFolderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", imageFolderPath);
            }
        }


        //-------------------------------------------------------------------------------------
        // フォームのパレットモード
        //-------------------------------------------------------------------------------------
        private void palletMode()
        {
            Form mainFormRaw = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "Main_Form");

            if (mainFormRaw is IconList_Animation.Main_Form mainForm)
            {
                // アニメーション停止（StopAnimation呼び出し）
                mainForm.StopAnimation();

                // メインフォームの高さを変更（パレットだけ表示）
                int originalWidth = mainForm.Width;
                int paletteHeight = mainForm.PalettePanel.Bottom + 90; // パレット部＋余白

                mainForm.Height = paletteHeight;
                mainForm.Width = originalWidth;

                // イメージビュアーの真上に配置
                Point viewerScreenPos = this.PointToScreen(Point.Empty);
                mainForm.Location = new Point(viewerScreenPos.X -10 , viewerScreenPos.Y - mainForm.Height - 24);

                // 表示状態にして bring to front
                //mainForm.TopMost = true;
                mainForm.BringToFront();
            }

            // イメージビュアーフォームも前面に（TopMostがtrueになってる場合）
            //this.TopMost = true;
            this.BringToFront();
        }

        //-------------------------------------------------------------------------------------
        // 選択範囲の内容を保持
        //-------------------------------------------------------------------------------------
        private void CopySelectedRegion(ref Bitmap targetImage)
        {
            int x0 = Math.Max(selectionRect.X, 0);
            int y0 = Math.Max(selectionRect.Y, 0);
            int maxW = targetImage.Width - x0;
            int maxH = targetImage.Height - y0;

            int w = Math.Min(selectionRect.Width, maxW);
            int h = Math.Min(selectionRect.Height, maxH);

            if (w <= 0 || h <= 0) return; // 無効な範囲を指定（はみ出し禁止）

            Bitmap region = new Bitmap(w, h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    region.SetPixel(x, y, targetImage.GetPixel(x0 + x, y0 + y));
                }
            }

            if (targetImage == image1)
                selectedRegionImage1 = region;
            else if (targetImage == image2)
                selectedRegionImage2 = region;
        }

        //-------------------------------------------------------------------------------------
        // パレットID=0で塗り潰し
        //-------------------------------------------------------------------------------------
        private void FillRegion(Rectangle rect, ref Bitmap targetImage)
        {
            // image1 に対しては baseFillColor を使用
            // image2（overlay）などには別の色を使いたい場合はここで条件分岐
            Color fill = (targetImage == image1 || targetImage == image2) ? baseFillColor : Color.Transparent;

            for (int y = 0; y < rect.Height; y++)
            {
                for (int x = 0; x < rect.Width; x++)
                {
                    int px = rect.X + x;
                    int py = rect.Y + y;

                    if (px >= 0 && py >= 0 && px < targetImage.Width && py < targetImage.Height)
                    {
                        targetImage.SetPixel(px, py, fill);
                    }
                }
            }
        }

        //-------------------------------------------------------------------------------------
        // 選択範囲を移動（上書き）
        //-------------------------------------------------------------------------------------
        private void MoveSelectedRegion(int dx, int dy, ref Bitmap targetImage, Bitmap region)
        {
            if (region == null) return;

            int w = region.Width;
            int h = region.Height;
            int newX = currentSelectionPosition.X + dx;
            int newY = currentSelectionPosition.Y + dy;

            if (newX < 0 || newY < 0 || newX + w > targetImage.Width || newY + h > targetImage.Height)
                return;

            // 先に元の位置を塗り潰し
            FillRegion(previousSelectionRect, ref targetImage);

            // 選択範囲を貼り付け
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    targetImage.SetPixel(newX + x, newY + y, region.GetPixel(x, y));

            currentSelectionPosition = new Point(newX, newY);
            selectionRect = new Rectangle(newX, newY, w, h);
        }

        //-------------------------------------------------------------------------------------
        // グリッド・選択枠表示
        //-------------------------------------------------------------------------------------
        private void DrawGrid(Graphics g, Image img, int scale)
        {
            if (img == null) return;
            int w = img.Width;
            int h = img.Height;

            using (Pen pen = new Pen(Color.FromArgb(100, Color.Gray)))
            {
                for (int x = 0; x <= w; x++)
                    g.DrawLine(pen, x * scale, 0, x * scale, h * scale);
                for (int y = 0; y <= h; y++)
                    g.DrawLine(pen, 0, y * scale, w * scale, y * scale);
            }

            if (selectionMode != SelectionMode.None)
            {
                int x1 = Math.Min(selectionStart.X, selectionEnd.X);
                int y1 = Math.Min(selectionStart.Y, selectionEnd.Y);
                int x2 = Math.Max(selectionStart.X, selectionEnd.X);
                int y2 = Math.Max(selectionStart.Y, selectionEnd.Y);

                using (Pen redPen = new Pen(Color.Red, 2))
                {
                    if (selectionMode == SelectionMode.Selecting)
                        redPen.DashStyle = DashStyle.Dash;

                    g.DrawRectangle(redPen, x1 * scale, y1 * scale, (x2 - x1 + 1) * scale, (y2 - y1 + 1) * scale);
                }
            }
        }

        //-------------------------------------------------------------------------------------
        // オーバーレイ描画（image1を半透明表示）
        //-------------------------------------------------------------------------------------
        private void DrawOverlayWithAlpha(Graphics g, Image overlay, int scale, int alphaPercent)
        {
            if (image1 == null || overlay == null) return;

            int w = overlay.Width * scale;
            int h = overlay.Height * scale;
            float alpha = alphaPercent / 100f;

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // 下地として image1 を先に描画
            g.DrawImage(ResizeImage(image1, scale), 0, 0, w, h);

            // 上に overlay（image2）を透過付きで重ねる
            using (ImageAttributes ia = new ImageAttributes())
            {
                ColorMatrix cm = new ColorMatrix { Matrix33 = alpha };
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                g.DrawImage(overlay,
                    new Rectangle(0, 0, w, h),
                    0, 0, overlay.Width, overlay.Height,
                    GraphicsUnit.Pixel,
                    ia);
            }

            DrawGrid(g, new Bitmap(w, h), scale);
        }

        //-------------------------------------------------------------------------------------
        // 拡大描画用Bitmap生成
        //-------------------------------------------------------------------------------------
        private Bitmap ResizeImage(Bitmap img, int scale)
        {
            Bitmap bmp = new Bitmap(img.Width * scale, img.Height * scale);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, 0, 0, bmp.Width, bmp.Height);
            }
            return bmp;
        }

        //-------------------------------------------------------------------------------------
        // 選択中の色をメインフォームから受取って変更
        //-------------------------------------------------------------------------------------
        public void SetSelectedColor(Color color)
        {
            selectedColor = color;

            if (selectedColorBox != null)
                selectedColorBox.BackColor = color;
        }

        //-------------------------------------------------------------------------------------
        // コピペ対応処理
        //-------------------------------------------------------------------------------------
        private void PasteClipboardImageToActiveTarget()
        {
            if (!Clipboard.ContainsImage()) return;

            Bitmap clipboardImg = new Bitmap(Clipboard.GetImage());
            if (clipboardImg == null) return;

            Bitmap target = pb1.Focused ? image1 : image2;

            // --- パレット変換処理 ---
            Bitmap converted = new Bitmap(target.Width, target.Height);
            using (Graphics g = Graphics.FromImage(converted))
            {
                g.DrawImage(clipboardImg, new Rectangle(0, 0, converted.Width, converted.Height));
            }

            for (int y = 0; y < converted.Height; y++)
            {
                for (int x = 0; x < converted.Width; x++)
                {
                    Color original = converted.GetPixel(x, y);
                    Color mapped = FindNearestPaletteColor(original, basePalette);
                    converted.SetPixel(x, y, mapped);
                }
            }

            //PushUndo(); // Undo対象に記録

            if (lastClickedBox == pb1) // 最後に触ったpbで分岐
            {
                image1 = converted;
                pb1.Image = ResizeImage(image1, scale);
                pb1.Invalidate();
            }
            else
            {
                image2 = converted;
                pbOverlay.Invalidate();
            }

            isEdited = true;
        }

        //-----------------
        // 近い色に変換する処理
        //-----------------
        private Color FindNearestPaletteColor(Color target, List<Color> palette)
        {
            Color nearest = palette.FirstOrDefault();
            int minDist = int.MaxValue;

            foreach (Color p in palette)
            {
                int dist = (p.R - target.R) * (p.R - target.R)
                         + (p.G - target.G) * (p.G - target.G)
                         + (p.B - target.B) * (p.B - target.B);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = p;
                }
            }

            return nearest;
        }


        //-------------------------------------------------------------------------------------
        // 保存時の近似色マッピング
        //-------------------------------------------------------------------------------------
        private int ColorDistance(Color c1, Color c2)
        {
            return (c1.R - c2.R) * (c1.R - c2.R) +
                   (c1.G - c2.G) * (c1.G - c2.G) +
                   (c1.B - c2.B) * (c1.B - c2.B);
        }

        //-----------------
        // インデックス付きビットマップを作る処理
        //-----------------

        private Bitmap CreateIndexedImage(Bitmap src, List<Color> palette)
        {
            Bitmap indexed = new Bitmap(src.Width, src.Height, PixelFormat.Format8bppIndexed);

            ColorPalette pal = indexed.Palette;
            for (int i = 0; i < palette.Count; i++)
                pal.Entries[i] = palette[i];
            for (int i = palette.Count; i < 256; i++)
                pal.Entries[i] = Color.Black;
            indexed.Palette = pal;

            BitmapData data = indexed.LockBits(new Rectangle(0, 0, src.Width, src.Height),
                                               ImageLockMode.WriteOnly,
                                               PixelFormat.Format8bppIndexed);

            int stride = data.Stride;
            byte[] bytes = new byte[stride * src.Height];

            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    Color c = src.GetPixel(x, y);
                    int closest = 0;
                    int minDist = int.MaxValue;

                    for (int i = 0; i < palette.Count; i++)
                    {
                        int dist = ColorDistance(c, palette[i]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            closest = i;
                        }
                    }

                    bytes[y * stride + x] = (byte)closest;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            indexed.UnlockBits(data);
            return indexed;
        }

        //-------------------------------------------------------------------------------------
        // 戻る・進む処理
        //-------------------------------------------------------------------------------------
        private void PushUndo()
        {
            int currentPaletteIndex = cbPaletteSelect?.SelectedIndex ?? 0;
            undoStack.Push(new ImageState(image1, image2, currentPaletteIndex));

            // 上限を超えたら古い履歴を削除
            while (undoStack.Count > MaxHistory)
                undoStack = new Stack<ImageState>(undoStack.Reverse().Take(MaxHistory));

            redoStack.Clear();
        }

        //-----------------
        // キー入力判定
        //-----------------
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                Undo();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.Y))
            {
                Redo();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.V))
            {
                PasteClipboardImageToActiveTarget();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        //-----------------
        // 一つ前に戻る（Undo）
        //-----------------
        private void Undo()
        {
            if (undoStack.Count == 0) return;
                        
            redoStack.Push(new ImageState(image1, image2, cbPaletteSelect.SelectedIndex));
            var state = undoStack.Pop();

            image1 = (Bitmap)state.Image1.Clone();
            image2 = (Bitmap)state.Image2.Clone();
            cbPaletteSelect.SelectedIndex = state.PaletteIndex;
            RefreshImages();
        }

        //-----------------
        // 一つ先に進む（Redo）
        //-----------------
        private void Redo()
        {
            if (redoStack.Count == 0) return;

            undoStack.Push(new ImageState(image1, image2, cbPaletteSelect.SelectedIndex));
            var state = redoStack.Pop();

            image1 = (Bitmap)state.Image1.Clone();
            image2 = (Bitmap)state.Image2.Clone();
            cbPaletteSelect.SelectedIndex = state.PaletteIndex;
            RefreshImages();
        }

        //-----------------
        // 画面更新
        //-----------------
        private void RefreshImages()
        {
            pb1.Image = ResizeImage(image1, scale);
            pb2.Image = ResizeImage(image1, scale);
            pbOverlay.Invalidate();
        }


        //-------------------------------------------------------------------------------------
        // パレット抜き出し処理
        //-------------------------------------------------------------------------------------
        private Bitmap ConvertImageToPalette(Bitmap original, List<Color> palette)
        {
            Bitmap newImage = new Bitmap(original.Width, original.Height);
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color pixel = original.GetPixel(x, y);
                    Color closest = palette.OrderBy(c => ColorDistance(pixel, c)).First();
                    newImage.SetPixel(x, y, closest);
                }
            }
            return newImage;
        }

        private List<Color> ExtractPalette(Bitmap bmp)
        {
            HashSet<Color> seen = new HashSet<Color>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color color = bmp.GetPixel(x, y);
                    if (seen.Add(color) && seen.Count == 16)
                        return seen.ToList();
                }
            }
            return seen.ToList();
        }
        //-------------------------------------------------------------------------------------
        // パレットの完全一致確認
        //-------------------------------------------------------------------------------------
        private int FindMatchingPaletteId(List<Color> iconPalette, List<List<Color>> externalPalettes)
        {
            var iconSet = new HashSet<int>(iconPalette.Select(c => c.ToArgb()));

            for (int i = 0; i < externalPalettes.Count; i++)
            {
                var externalSet = new HashSet<int>(externalPalettes[i].Select(c => c.ToArgb()));

                if (iconSet.SetEquals(externalSet)) // 順番を無視して比較
                {
                    return i;
                }
            }

            return -1;
        }

        //-------------------------------------------------------------------------------------
        // オフセット入力値をアドレス（int）に変換【未使用処理です】
        //-------------------------------------------------------------------------------------
        private int ParseOffset(string hexText)
        {
            if (hexText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hexText = hexText.Substring(2);

            if (int.TryParse(hexText, System.Globalization.NumberStyles.HexNumber, null, out int result))
                return result;

            throw new FormatException("不正なオフセット形式です: " + hexText);
        }


        private void Form_ImageViewer_Load(object sender, EventArgs e) { }
    }
}

