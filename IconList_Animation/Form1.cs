using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using IconList_Animation;
using Pokemon3genHackLibrary;
using System.Globalization;
using System.Threading;


namespace IconList_Animation
{
    public partial class Main_Form : Form
    {        
        private List<(PictureBox box, Image image1, Image image2, string folderPath, int throw_Address1, int throw_Address2, int pallet_address, int palletoffset)> icons
            = new List<(PictureBox box, Image image1, Image image2, string folderPath, int throw_Address1, int throw_Address2,int pallet_address, int palletoffset) >();

        private List<(PictureBox box, Bitmap image, int offset, int table)> footprints 
            = new List<(PictureBox, Bitmap, int, int)>();

        private System.Windows.Forms.Timer animationTimer;
        private bool showFirstImage = true;

        private int displayScale = 1; // 倍率
        private int displayCount = 20; //アイコン表示数 
        private const int baseIconSize = 32; // 元のアイコンサイズ
        private string currentBasePath = ""; //再読み込み時のpath指定
        public int totalPokemonNum = 412;

        private bool showBG0 = false;
        private bool showPaletteId = false;
        private bool showscaleUp = false;
        private bool showFootPrint = false;

        private List<List<Color>> externalPalettes = new List<List<Color>>();

        private Button processButton;
        private List<List<Color>> palettes;

        private Color selectedColor = Color.Black;
        private Form_ImageViewer viewerForm = null;　//メインからサブに色の受け渡し

        private Byte[] Binary_Data;

        public Panel PalettePanel => palettePanel;
        private Label label_DexHeader;
        private ToolTip toolTip = new ToolTip(); // 吹き出し表示

        PrivateFontCollection customFontCollection = new PrivateFontCollection();
        Font customFont;

        public Main_Form()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DrawEmptyPalette(); // 空のパレットを表示させる
            InitializeCustomFont();   // カスタムフォントを読込ませる

            // 言語設定の登録
            if (toolStripComboBox_SetLanguage.Items.Count == 0)
            {
                toolStripComboBox_SetLanguage.Items.Add("日本語");
                toolStripComboBox_SetLanguage.Items.Add("English");
            }

            // 前回の言語設定があれば選択
            string savedLang = Properties.Settings.Default.Language;
            if (savedLang == "ja-JP")
                toolStripComboBox_SetLanguage.SelectedItem = "日本語";
            else if (savedLang == "en")
                toolStripComboBox_SetLanguage.SelectedItem = "English";
            else
                toolStripComboBox_SetLanguage.SelectedIndex = 0;                     

            label_DexHeader = new Label
            {
                Text = "LOTAD EDITOR POKEDEX",
                Font = new Font(customFont.FontFamily, 24, FontStyle.Bold),
               
                AutoSize = true,
                ForeColor = Color.DimGray,
                BackColor = Color.Transparent,
                Location = new Point((this.Width) + 300, palettePanel.Bottom - 10), 
                UseCompatibleTextRendering = true
            };
            this.Controls.Add(label_DexHeader);
            label_DexHeader.BringToFront();


        }

        //-------------------------------------------------------------------------------
        // 言語設定
        //-------------------------------------------------------------------------------
        private void toolStripComboBox_SetLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            // ComboBoxから選択された言語を取得
            string selected = toolStripComboBox_SetLanguage.SelectedItem.ToString();
            string cultureCode;

            switch (selected)
            {
                case "English":
                    cultureCode = "en";
                    break;
                case "日本語":
                    cultureCode = "ja-JP";
                    break;
                default:
                    cultureCode = "ja-JP"; // フォールバック
                    break;
            }

            // ✅ JSONのローカライズリソースを読み込み
            LanguageManager.Load(cultureCode);

            // ✅ 保存して次回起動時にも適用
            Properties.Settings.Default.Language = cultureCode;
            Properties.Settings.Default.Save();

            toolStripMenuItem1.Text = LanguageManager.Get("menu_File_title");
            ToolStripMenuItem_OpenRom.Text = LanguageManager.Get("menu_File_ReadRom");
            toolStripMenuItem_OpenFolder.Text = LanguageManager.Get("menu_File_ReadFolder");

            toolStripMenuItem2.Text = LanguageManager.Get("menu_mode_title");
            toolStripMenuItem_touka.Text = LanguageManager.Get("menu_mode_Touka");
            toolStripMenuItem_PalletID.Text = LanguageManager.Get("menu_mode_PalletID");
            toolStripMenuItem_imageSize.Text = LanguageManager.Get("menu_mode_Dex");
            toolStripMenuItem_FootPrint.Text = LanguageManager.Get("menu_mode_titleFootPrint");

            toolStripMenuItem4.Text = LanguageManager.Get("menu_other_title");
            toolStripMenuItem_ExtractIcon.Text = LanguageManager.Get("menu_other_iconExtract");
            toolStripMenuItem_IconPalletChange.Text = LanguageManager.Get("menu_other_iconConvert");
            ToolStripMenuItem_BasepalletFolder.Text = LanguageManager.Get("menu_other_palletFolder");

        }

        //-------------------------------------------------------------------------------
        //カスタムフォントを取得して表示する
        //-------------------------------------------------------------------------------
        private void InitializeCustomFont()
        {
            var asm = Assembly.GetExecutingAssembly();
            string resourceName = "IconList_Animation.Resources.Shield.otf"; // 実際のリソース名に合わせる

            using (Stream fontStream = asm.GetManifestResourceStream(resourceName))
            {
                if (fontStream == null)
                {
                    MessageBox.Show("カスタムフォントリソースが見つかりません: " + resourceName);
                    return;
                }

                byte[] fontData = new byte[fontStream.Length];
                fontStream.Read(fontData, 0, fontData.Length);

                IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                Marshal.Copy(fontData, 0, fontPtr, fontData.Length);

                customFontCollection.AddMemoryFont(fontPtr, fontData.Length);
                Marshal.FreeCoTaskMem(fontPtr);

                if (customFontCollection.Families.Length > 0)
                {
                    FontFamily fam = customFontCollection.Families[0];
                    FontStyle style = fam.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                    customFont = new Font(fam, Math.Max(1, 12 * displayScale), style);
                }
                else
                {
                    MessageBox.Show("フォントの読み込みに失敗しました");
                }
            }
        }


        //-------------------------------------------------------------------------------
        //ダイアログを呼び出してルート対象のファイルのパスを取得する
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_OpenFolder_Click(object sender, EventArgs e)
        {
            // 参照先のパス名
            var path = "";

            // フォルダダイアログを生成
            using (FolderBrowserDialog op = new FolderBrowserDialog())
            {
                op.Description = "フォルダを選択してください";
                op.RootFolder = Environment.SpecialFolder.Desktop;
                op.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); // 初期表示パス
                op.ShowNewFolderButton = true;

                // ダイアログ表示
                DialogResult dialog = op.ShowDialog();

                // 「開く」ボタンが選択された際の処理
                if (dialog == DialogResult.OK && !string.IsNullOrWhiteSpace(op.SelectedPath))
                {
                    toolStripStatusLabel_FilePath.Text = op.SelectedPath;
                    toolStripStatusLabel_Mode.Text = "Mode：Folder";
                    toolStripStatusLabel_ViewIcon.Image = Properties.Resources.folder_Small_icon;
                    // 初期化後にアイコン読み込みを開始

                    try
                    {
                        LoadPalettes();
                        LoadIcons(op.SelectedPath);
                        StartAnimation();
                    }
                    catch
                    {
                        toolStripStatusLabel_Information.Text = LanguageManager.Get("Error_Read_miss");
                    }

                }
                // キャンセル時は何もしない場合は else を省略してもOK
            }

        }

        //-------------------------------------------------------------------------------
        //ドラッグアンドドロップした際の処理
        //-------------------------------------------------------------------------------
        private void Main_Form_DragDrop(object sender, DragEventArgs e)
        {
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths.Length == 0) return;

            string path = paths[0];
            string extension = Path.GetExtension(path).ToLower();

            try
            {
                LoadPalettes();

                if (File.Exists(path) && extension == ".gba")
                {
                    // ROMファイルの場合
                    toolStripStatusLabel_FilePath.Text = path;
                    toolStripStatusLabel_Mode.Text = "Mode：Rom image";
                    toolStripStatusLabel_ViewIcon.Image = Properties.Resources.gba_Small_icon;
                    LoadIconsFromRom(path, totalPokemonNum);
                    StartAnimation();
                }
                else if (Directory.Exists(path))
                {
                    // フォルダの場合
                    toolStripStatusLabel_FilePath.Text = path;
                    toolStripStatusLabel_Mode.Text = "Mode：Folder";
                    toolStripStatusLabel_ViewIcon.Image = Properties.Resources.folder_Small_icon;
                    LoadIcons(path);
                    StartAnimation();
                }
            }
            catch
            {
                toolStripStatusLabel_Information.Text = LanguageManager.Get("Error_Read_miss");
            }

        }
        private void Main_Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }

        }

        private void palettePanel_DragDrop(object sender, DragEventArgs e)
        {
            Main_Form_DragDrop(sender,e);
        }

        private void palettePanel_DragEnter(object sender, DragEventArgs e)
        {
            Main_Form_DragEnter(sender,e);
        }
        private void scrollPanel_DragDrop(object sender, DragEventArgs e)
        {
            Main_Form_DragDrop(sender, e);
        }

        private void scrollPanel_DragEnter(object sender, DragEventArgs e)
        {
            Main_Form_DragEnter(sender, e);
        }

        //-------------------------------------------------------------------------------
        // アイコンを一旦消してカスタムコンポーネントで画像を表示
        //-------------------------------------------------------------------------------
        public void LoadIcons(string baseDir)
        {
            scrollPanel.AutoScroll = false; // 読み込み中はスクロール禁止
            scrollPanel.Controls.Clear();
            icons.Clear();
            toolStripStatusLabel_RomID.Text = "";

            currentBasePath = baseDir;

            var iconDirs = Directory.GetDirectories(baseDir, "*", SearchOption.AllDirectories)
                .OrderBy(path =>
                {
                    string folderName = Path.GetFileName(path);
                    string digits = new string(folderName.Where(char.IsDigit).ToArray());
                    return int.TryParse(digits, out int num) ? num : int.MaxValue;
                })
                .ToList();

            int x = 10, y = 10;
            int iconSize = baseIconSize * displayScale;
            int spacing = 8;
            int count = 0;
            int panelSpacing = 8;  // パネル間隔

            Bitmap image1,image2;

            toolStripProgressBar_Loading.Minimum = 0;
            toolStripProgressBar_Loading.Maximum = iconDirs.Count;
            toolStripProgressBar_Loading.Value = 0;

            // メインフォームの大きさ調整
            UpdateFormSizeBasedOnDisplayCount(iconDirs.Count);

            try
            {



                foreach (var dir in iconDirs)
                {
                    var bmp1 = Directory.GetFiles(dir, "*?(1).bmp").FirstOrDefault();
                    var bmp2 = Directory.GetFiles(dir, "*?(2).bmp").FirstOrDefault();

                    if (bmp1 != null && bmp2 != null)
                    {
                        // ------------------------------------------------------------------------------
                        // 背景色を透過する処理
                        if (showBG0)
                        {
                            image1 = (Bitmap)LoadImageWithOptionalTransparency(bmp1);
                            image2 = (Bitmap)LoadImageWithOptionalTransparency(bmp2);
                        }
                        else
                        {
                            image1 = new Bitmap(bmp1);
                            image2 = new Bitmap(bmp2);
                        }
                        // ------------------------------------------------------------------------------

                        var pb = new SharpPictureBox //カスタムしたシャープ描写可能のpictureBoxで表示
                        {
                            Width = iconSize,
                            Height = iconSize,
                            Location = new Point(x, y),
                            Image = image1,
                            SizeMode = PictureBoxSizeMode.StretchImage,
                            Interpolation = InterpolationMode.NearestNeighbor,
                            Cursor = Cursors.Hand
                        };

                        pb.Click += PictureBox_Click;               // 既存：パレット表示
                        pb.DoubleClick += PictureBox_DoubleClick;   // 新規：画像拡大ビュー表示
                        toolTip.SetToolTip(pb, LanguageManager.Get("ToolTips_OpenEditor"));

                        icons.Add((pb, image1, image2, dir, 0, 0, 0, 0));

                        var iconPalette = ExtractPaletteColors((Bitmap)image1); // 色取得の処理を変更
                        int matchedIndex = FindMatchingPaletteId(iconPalette);

                        // パレットIDを記載する処理
                        if (showscaleUp) // 図鑑モード
                        {
                            var panel = new Panel
                            {
                                Width = iconSize + 2,
                                Height = iconSize + 24 + 4, // 枠の余白指定
                                BorderStyle = BorderStyle.FixedSingle,
                                Location = new Point(x, y),
                                BackColor = Color.White
                            };

                            var label = new Label
                            {
                                Text = $"{count + 1:D3}",
                                Font = customFont,
                                UseCompatibleTextRendering = true,
                                ForeColor = Color.DimGray,
                                AutoSize = true,
                                Location = new Point(2, 2)
                            };
                            panel.Controls.Add(label);
                            label.BringToFront();

                            pb.Location = new Point((panel.Width - pb.Width) / 2, label.Bottom + 4);
                            panel.Controls.Add(pb);

                            // パレットID 表示（オプション）
                            if (showPaletteId)
                            {
                                int overlaySize = 16 * displayScale;
                                var pidLabel = new Label
                                {
                                    Text = matchedIndex.ToString(),
                                    Font = new Font("Arial", 10 * displayScale, FontStyle.Bold),
                                    ForeColor = Color.Black,
                                    BackColor = Color.FromArgb(180, Color.White),
                                    AutoSize = false,
                                    Width = overlaySize,
                                    Height = overlaySize,
                                    TextAlign = ContentAlignment.MiddleCenter,
                                    Location = new Point(pb.Width - overlaySize - 2, pb.Height - overlaySize - 2)
                                };
                                pb.Controls.Add(pidLabel);
                                pidLabel.BringToFront();
                            }

                            scrollPanel.Controls.Add(panel);
                        }
                        else // 通常表示
                        {
                            pb.Location = new Point(x, y);
                            scrollPanel.Controls.Add(pb);

                            // パレットID 表示（オプション）
                            if (showPaletteId)
                            {
                                int overlaySize = 16 * displayScale;
                                var pidLabel = new Label
                                {
                                    Text = matchedIndex.ToString(),
                                    Font = new Font("Arial", 10 * displayScale, FontStyle.Bold),
                                    ForeColor = Color.Black,
                                    BackColor = Color.FromArgb(180, Color.White),
                                    AutoSize = false,
                                    Width = overlaySize,
                                    Height = overlaySize,
                                    TextAlign = ContentAlignment.MiddleCenter,
                                    Location = new Point(pb.Width - overlaySize - 2, pb.Height - overlaySize - 2)
                                };
                                pb.Controls.Add(pidLabel);
                                pidLabel.BringToFront();
                            }
                        }

                        x += iconSize + spacing;
                        count++;

                        if (count % displayCount == 0)
                        {
                            x = 10;
                            y += showscaleUp ? (iconSize + 24 + spacing) : (iconSize + spacing);
                        }
                    }

                    if (toolStripProgressBar_Loading.Value < toolStripProgressBar_Loading.Maximum)
                    {
                        toolStripProgressBar_Loading.Value++;
                    }
                    Application.DoEvents(); // プログレスバー表示更新
                }

                if (count == 0)
                {
                    // 読込対象のアイコンが見つからない場合注意messageを表示
                    var dialog = new SaveLocationDialog(
                        "",
                        "",
                        Properties.Resources.Folder_rule,
                        "OK",
                         LanguageManager.Get("Error_Nothing_Folder1") + "\n" +
                         LanguageManager.Get("Error_Nothing_Folder2") + "\n" +
                         LanguageManager.Get("Error_Nothing_Folder3") + "\n");
                    dialog.ShowDialog();
                }
            }
            catch
            {

            }

            scrollPanel.AutoScrollMinSize = new Size(x + iconSize + 20, y + iconSize + 50);
            scrollPanel.AutoScroll = true; // 読込後にスクロールできるように戻す
            toolStripStatusLabel_Information.Text = LanguageManager.Get("complete_iconLoad");
            toolStripProgressBar_Loading.Value = 0;
            toolStripMenuItem_FootPrint.Visible = false;
        }

        //-------------------------------------------------------------------------------
        //透過色の指定
        //-------------------------------------------------------------------------------
        private Image LoadImageWithOptionalTransparency(string path)
        {
            Bitmap original = new Bitmap(path);

            if (toolStripStatusLabel_ToukaFlg.Text == "Transparent：ON")
            {
                Color targetColor = ColorTranslator.FromHtml("#639C84");
                int tolerance = 10;
                return MakeTransparentFast(original, targetColor, tolerance);
            }

            return original;
        }
        private Bitmap ApplyTransparencyFromPalette(Bitmap bmp)
        {
            if (toolStripStatusLabel_ToukaFlg.Text != "Transparent：ON")
                return bmp;

            Color targetColor = ColorTranslator.FromHtml("#639C84");
            int tolerance = 10;
            return MakeTransparentFast(bmp, targetColor, tolerance);

        }

        //-------------------------------------------------------------------------------
        //パレット元画像とパレット内容を比較する処理
        //-------------------------------------------------------------------------------
        private int FindMatchingPaletteId(List<Color> iconPalette)
        {
            var iconSet = new HashSet<int>(iconPalette.Select(c => c.ToArgb()));

            for (int i = 0; i < externalPalettes.Count; i++)
            {
                var externalSet = new HashSet<int>(externalPalettes[i].Select(c => c.ToArgb()));

                if (iconSet.SetEquals(externalSet)) // 順番は無視して一致チェック
                {
                    return i;
                }
            }

            return -1;
        }

        //-------------------------------------------------------------------------------
        //透過色への高速変換
        //-------------------------------------------------------------------------------
        private Bitmap MakeTransparentFast(Bitmap original, Color targetColor, int tolerance)
        {
            // 入力画像を Format32bppArgb に強制変換
            Bitmap src = new Bitmap(original.Width, original.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.DrawImage(original, new Rectangle(0, 0, src.Width, src.Height));
            }

            Bitmap bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
            var srcData = src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, src.PixelFormat);

            int bytesPerPixel = 4;

            unsafe
            {
                byte* destPtr = (byte*)bmpData.Scan0;
                byte* srcPtr = (byte*)srcData.Scan0;

                for (int y = 0; y < bmp.Height; y++)
                {
                    byte* destRow = destPtr + (y * bmpData.Stride);
                    byte* srcRow = srcPtr + (y * srcData.Stride);

                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int index = x * bytesPerPixel;

                        byte b = srcRow[index];
                        byte g = srcRow[index + 1];
                        byte r = srcRow[index + 2];
                        byte a = 255;

                        if (Math.Abs(r - targetColor.R) <= tolerance &&
                            Math.Abs(g - targetColor.G) <= tolerance &&
                            Math.Abs(b - targetColor.B) <= tolerance)
                        {
                            a = 0;
                        }

                        destRow[index] = b;
                        destRow[index + 1] = g;
                        destRow[index + 2] = r;
                        destRow[index + 3] = a;
                    }
                }
            }

            bmp.UnlockBits(bmpData);
            src.UnlockBits(srcData);

            //original.Dispose();
            src.Dispose();
            return bmp;
        }

        //-----------------
        // アニメーション起動
        //-----------------
        private void StartAnimation()
        {
            if (animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }

            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 500;
            animationTimer.Tick += (s, e) =>
            {
                foreach (var (box, image1, image2, dir, throw_Address1, throw_Address2, pallet_address, palletoffset) in icons)
                {
                    box.Image = showFirstImage ? image2 : image1;
                }
                showFirstImage = !showFirstImage;
            };
            animationTimer.Start();

            // 初期状態として「アニメーション 停止」に設定
            toolStripMenuItem3.Text = "■";
        }

        //-----------------
        // アニメーション停止
        //-----------------
        public void StopAnimation()
        {
            if (animationTimer != null && animationTimer.Enabled)
            {
                animationTimer.Stop();
                toolStripMenuItem3.Text = "▶️";
            }
        }

        // ====================================================================================

        //-------------------------------------------------------------------------------
        // 背景色透過のセッティング
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_touka_Click(object sender, EventArgs e)
        {
            if (showBG0)
            {
                // 透過が未実行の際
                showBG0 = false;
                toolStripStatusLabel_ToukaFlg.Text = "Transparent：OFF";
                toolStripMenuItem_touka.Image = Properties.Resources.check_off_icon;
            }
            else
            {
                // 透過が実行済みの際
                showBG0 = true;
                toolStripStatusLabel_ToukaFlg.Text = "Transparent：ON";
                toolStripMenuItem_touka.Image = Properties.Resources.check_on_icon;
            }

            if (toolStripStatusLabel_FilePath.Text != "-image folder path-")
            {
                // 再読み込みがRomモードかの分岐
                if (toolStripStatusLabel_Mode.Text == "Mode：Rom image")
                {
                    // Rom読込の場合
                    LoadIconsFromRom(toolStripStatusLabel_FilePath.Text, totalPokemonNum);
                }
                else
                {
                    // フォルダ読込の場合
                    LoadIcons(toolStripStatusLabel_FilePath.Text);
                }
            }

        }

        //-------------------------------------------------------------------------------
        // パレットID表示のセッティング
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_PalletID_Click(object sender, EventArgs e)
        {
            if (showPaletteId)
            {
                // ID表示がONの場合
                showPaletteId = false;
                toolStripStatusLabel_PalletID.Text = "Pallet ID：OFF";
                toolStripMenuItem_PalletID.Image = Properties.Resources.check_off_icon;

            }
            else
            {
                // ID表示がOFFの場合
                showPaletteId = true;
                toolStripStatusLabel_PalletID.Text = "Pallet ID：ON";
                toolStripMenuItem_PalletID.Image = Properties.Resources.check_on_icon;

            }

            LoadExternalPalettes(); // 外部パレットの読込

            // 読み込みモードで分岐
            if (toolStripStatusLabel_Mode.Text == "Mode：Rom image")
            {
                // Rom読込の場合
                LoadIconsFromRom(toolStripStatusLabel_FilePath.Text, totalPokemonNum);
            }
            else if (toolStripStatusLabel_Mode.Text == "Mode：Folder")
            {
                // フォルダ読込の場合
                LoadIcons(toolStripStatusLabel_FilePath.Text);
            }

        }

        //-------------------------------------------------------------------------------
        // 表示スケールのセッティング
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_imageSize_Click(object sender, EventArgs e)
        {
            if (showscaleUp)
            {
                // ID表示がONの場合
                showscaleUp = false;
                toolStripStatusLabel_Size.Text = "PokeDex：OFF";
                displayScale = 1;
                displayCount = 20;
                toolStripMenuItem_imageSize.Image = Properties.Resources.check_off_icon;
            }
            else
            {
                // ID表示がOFFの場合
                showscaleUp = true;
                toolStripStatusLabel_Size.Text = "PokeDex：ON";
                displayScale = 3;
                displayCount = 15;
                toolStripMenuItem_imageSize.Image = Properties.Resources.check_on_icon;
            }

            if (toolStripStatusLabel_FilePath.Text != "-image folder path-")
            {
                
                

                if (toolStripStatusLabel_Mode.Text == "Mode：Rom image")
                {
                    // Rom読込の場合
                    LoadIconsFromRom(toolStripStatusLabel_FilePath.Text, totalPokemonNum);
                }
                else if (toolStripStatusLabel_Mode.Text == "Mode：Folder")
                {
                    // フォルダ読込の場合
                    LoadIcons(toolStripStatusLabel_FilePath.Text);
                }

            }

        }

        //-------------------------------------------------------------------------------
        // 足跡アイコンのセッティング
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_FootPrint_Click(object sender, EventArgs e)
        {
            if (showFootPrint)
            {
                // ID表示がONの場合
                showFootPrint = false;
                toolStripMenuItem_FootPrint.Image = Properties.Resources.check_off_icon;

            }
            else
            {
                // ID表示がOFFの場合
                showFootPrint = true;
                toolStripMenuItem_FootPrint.Image = Properties.Resources.check_on_icon;

            }

            LoadExternalPalettes(); // 外部パレットの読込

            // 読み込みモードで分岐
            if (toolStripStatusLabel_Mode.Text == "Mode：Rom image")
            {
                // Rom読込の場合
                LoadIconsFromRom(toolStripStatusLabel_FilePath.Text, totalPokemonNum);
            }
            else if (toolStripStatusLabel_Mode.Text == "Mode：Folder")
            {
                // フォルダ読込の場合
                LoadIcons(toolStripStatusLabel_FilePath.Text);
            }

        }

        // ====================================================================================
        
        //-------------------------------------------------------------------------------
        // 空のパレット表示エリア
        //-------------------------------------------------------------------------------
        private void DrawEmptyPalette()
        {
            palettePanel.Controls.Clear();
            int boxWidth = 40;
            int boxHeight = 20;
            int spacing = 10;
            int baseX = 10;
            int baseY = 5;

            for (int i = 0; i < 16; i++)
            {
                int x = baseX + i * (boxWidth + spacing);

                var labelHex = new Label
                {
                    Text = "",
                    Font = new Font("Consolas", 8),
                    AutoSize = true,
                    Location = new Point(x, baseY)
                };
                palettePanel.Controls.Add(labelHex);

                var label565 = new Label
                {
                    Text = "",
                    Font = new Font("Consolas", 8),
                    AutoSize = true,
                    Location = new Point(x, baseY + 15)
                };
                palettePanel.Controls.Add(label565);

                var box = new Panel
                {
                    BackColor = Color.LightGray,
                    Width = boxWidth,
                    Height = boxHeight,
                    Location = new Point(x, baseY + 30),
                    BorderStyle = BorderStyle.FixedSingle
                };
                palettePanel.Controls.Add(box);
            }

            palettePanel.Height = baseY + 30 + boxHeight + 5;
        }


        //-------------------------------------------------------------------------------
        // ピクチャーボックスをクリックした際の処理
        //-------------------------------------------------------------------------------
        private void PictureBox_Click(object sender, EventArgs e)
        {
            var pb = sender as PictureBox;
            if (pb?.Image is Bitmap bmp)
            {
                // 実際の使用色ではなく、パレットから取得
                var colors = ExtractPaletteColors(bmp);
                DrawPalette(colors);
            }
        }

        //-------------------------------------------------------------------------------
        // ピクチャーボックスをダブルクリックするとフォーム表示する処理
        //-------------------------------------------------------------------------------
        private void PictureBox_DoubleClick(object sender, EventArgs e)
        {
            var pb = sender as PictureBox;
            var icon = icons.FirstOrDefault(t => t.box == pb);
            var colors = ExtractPaletteColors((Bitmap)icon.image1); // パレット取得            


            if (icon.box != null)
            {
                string folderPath = icon.folderPath;

                // パレットエリアで選択された色を渡す関数（選択されていなければ黒）
                Func<Color> getSelectedColor = () => selectedColor;

                // すでに開いていれば再表示だけ（再生成防止）
                if (viewerForm == null || viewerForm.IsDisposed)
                {
                    viewerForm = new Form_ImageViewer(
                        new Bitmap(icon.image1),
                        new Bitmap(icon.image2),
                        folderPath,
                        totalPokemonNum,
                        toolStripStatusLabel_FilePath.Text,
                        () => selectedColor,
                        colors,
                        Binary_Data,                        
                        icon.throw_Address1,
                        icon.throw_Address2,
                        icon.pallet_address,
                        icon.palletoffset,
                        toolStripStatusLabel_Mode.Text                        
                    );
                    viewerForm.Show();
                }
                else
                {
                    viewerForm.BringToFront();
                }
            }
        }


        private List<Color> ExtractPaletteColors(Bitmap bmp)
        {
            var entries = bmp.Palette.Entries;
            if (entries.Length >= 16)
            {
                return entries.Take(16).ToList();
            }
            else
            {
                // フォールバック：現在の使用色抽出を使用
                return ExtractUsedColors(bmp, 16);
            }
        }

        //-------------------------------------------------------------------------------
        // 色抽出
        //-------------------------------------------------------------------------------
        private List<Color> ExtractUsedColors(Bitmap bmp, int maxColors)
        {
            var colorSet = new HashSet<Color>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    if (color.A > 0) // 透過を除外
                        colorSet.Add(color);

                    if (colorSet.Count >= maxColors)
                        return colorSet.ToList();
                }
            }
            return colorSet.ToList();
        }

        //-------------------------------------------------------------------------------
        // ピクチャボックスclick時のパレット情報表示
        //-------------------------------------------------------------------------------
        public void DrawPalette(List<Color> colors)
        {
            palettePanel.Controls.Clear();
            int boxWidth = 40;
            int boxHeight = 20;
            int spacing = 10;
            int baseX = 10;
            int baseY = 5;

            Panel firstColorPanel = null; // ← 最初の有効色Panelを保持する変数

            for (int i = 0; i < 16; i++)
            {
                Color color = i < colors.Count ? colors[i] : Color.LightGray;
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                ushort rgb565 = RgbTo565(color);
                string rgb565Hex = $"{rgb565:X4}";

                int x = baseX + i * (boxWidth + spacing);
                bool isUnused = color.ToArgb() == Color.LightGray.ToArgb();

                // 上段：カラーコードラベル
                if (!isUnused)
                {
                    var labelHex = new Label
                    {
                        Text = hex,
                        Font = new Font("Consolas", 8),
                        ForeColor = Color.Black,
                        AutoSize = true,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Location = new Point(x - 5, baseY),
                        Cursor = Cursors.Hand
                    };
                    labelHex.Click += (s, e) =>
                    {
                        Clipboard.SetText(hex);
                        ShowCopyStatusMessage($"カラーコードをクリップボードにコピーしました: {hex}");
                    };
                    palettePanel.Controls.Add(labelHex);

                    var label565 = new Label
                    {
                        Text = rgb565Hex,
                        Font = new Font("Consolas", 8),
                        ForeColor = Color.Black,
                        AutoSize = true,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Location = new Point(x, baseY + 15),
                        Cursor = Cursors.Hand
                    };
                    label565.Click += (s, e) =>
                    {
                        Clipboard.SetText(rgb565Hex);
                        ShowCopyStatusMessage($"RGB565値（16bit）をクリップボードにコピーしました: {hex}");
                    };
                    palettePanel.Controls.Add(label565);
                }

                // 下段：カラーパネル（常に表示）
                var box = new Panel
                {
                    BackColor = color,
                    Width = boxWidth,
                    Height = boxHeight,
                    Location = new Point(x, baseY + 30),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand
                };

                // 最初の有効色パネルを記録しておく
                if (!isUnused && firstColorPanel == null)
                {
                    firstColorPanel = box;
                }

                // クロージャ変数キャプチャの回避（colorはloop変数）
                Color colorCopy = color;
                string hexCopy = hex;

                box.Click += (s, e) =>
                {
                    if (!isUnused)
                    {
                        // すべてのパネル枠を通常表示に戻す
                        foreach (Control ctrl in palettePanel.Controls)
                        {
                            if (ctrl is Panel p)
                                p.BorderStyle = BorderStyle.FixedSingle;
                        }

                        // 選択パネルを強調表示
                        box.BorderStyle = BorderStyle.Fixed3D;

                        // 選択色更新と表示
                        selectedColor = colorCopy;
                        Clipboard.SetText(hexCopy);
                        //ShowCopyStatusMessage($"カラーコードをクリップボードにコピーしました: {hexCopy}");

                        // ビューアが開いていれば色反映
                        if (viewerForm?.Visible == true)
                        {
                            viewerForm.SetSelectedColor(selectedColor);
                        }
                    }
                };

                palettePanel.Controls.Add(box);
            }

            //-------------------------------------------------------------------------------
            // 初期表示として最初の有効パネルを選択状態にする
            //-------------------------------------------------------------------------------
            if (firstColorPanel != null)
            {
                firstColorPanel.BorderStyle = BorderStyle.Fixed3D;
                selectedColor = firstColorPanel.BackColor;

                if (viewerForm?.Visible == true)
                {
                    viewerForm.SetSelectedColor(selectedColor);
                }
            }

            palettePanel.Height = baseY + 30 + boxHeight + 5;
        }


        //-------------------------------------------------------------------------------
        // 色の2byte指定値を取得
        //-------------------------------------------------------------------------------
        private ushort RgbTo565(Color color)
        {
            int r = color.R >> 3;
            int g = color.G >> 2;
            int b = color.B >> 3;
            return (ushort)((r << 11) | (g << 5) | b);
        }

        //-------------------------------------------------------------------------------
        // パレット情報をクリック時の処理
        //-------------------------------------------------------------------------------
        private void ShowCopyStatusMessage(string text)
        {
            toolStripStatusLabel_Information.Text = text;
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 2000; // 表示は2秒間
            timer.Tick += (s, e) =>
            {
                toolStripStatusLabel_Information.Text = "";
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }


        // ====================================================================================



        //-------------------------------------------------------------------------------
        // ベースとなるパレット画像をリソースフォルダから外部取得
        //-------------------------------------------------------------------------------
        private void LoadExternalPalettes()
        {
            externalPalettes.Clear();
            string paletteFolder = Path.Combine(Application.StartupPath, "PalletResources");

            var files = Directory.GetFiles(paletteFolder, "*.png");
            foreach (var file in files)
            {
                Bitmap bmp = new Bitmap(file);
                var palette = ExtractPaletteColors(bmp); // パレット取得の処理を統一しておく
                externalPalettes.Add(palette);
            }
        }

        //-------------------------------------------------------------------------------
        // ベースとなるパレット画像をリソースフォルダから外部取得
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            if (animationTimer != null)
            {
                if (animationTimer.Enabled)
                {
                    animationTimer.Stop();
                    toolStripMenuItem3.Text = "▶️";
                }
                else
                {
                    animationTimer.Start();
                    toolStripMenuItem3.Text = "■";
                }
            }
        }


        // ====================================================================================

        //-------------------------------------------------------------------------------
        // フォルダ内の画像をパレット調整する処理
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_IconPalletChange_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("この処理を実行すると，選択したフォルダ内の全ての画像に指定されたパレットを\n自動でpalletReferenceフォルダに登録した内容に調整が出来ます．",
                    "パレット調整を実行しますか?",
                    MessageBoxButtons.OKCancel
                    );

            //何が選択されたか調べる
            if (result == DialogResult.OK)
            {
                //「はい」が選択された時
                Convert_pallet_Exchange();
            }
            else
            {
                //「キャンセル」が選択された時
            }
        }

        private void Convert_pallet_Exchange()
        { 

            toolStripStatusLabel_Information.Text = LanguageManager.Get("complete_palletChange1");
            
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string folder = dialog.SelectedPath;
                    string nowTime = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss");

                    var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                                         .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                    toolStripProgressBar_Loading.Minimum = 0;
                    toolStripProgressBar_Loading.Maximum = files.Count;
                    toolStripProgressBar_Loading.Value = 0;

                    foreach (string file in files)
                    {
                        using (Bitmap original = new Bitmap(file))
                        {
                            string imageName = Path.GetFileNameWithoutExtension(file);
                            string folderName = new DirectoryInfo(Path.GetDirectoryName(file)).Name;
                            string saveDir;

                            if ((original.Width == 32 && original.Height == 64) || (original.Width == 64 && original.Height == 32))
                            {
                                saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nowTime}_Converted", folderName, imageName + ".Icon");
                                Directory.CreateDirectory(saveDir);

                                if (original.Width == 32) // 縦長
                                {
                                    Bitmap upper = original.Clone(new Rectangle(0, 0, 32, 32), PixelFormat.Format24bppRgb);
                                    Bitmap lower = original.Clone(new Rectangle(0, 32, 32, 32), PixelFormat.Format24bppRgb);
                                    SaveConverted(upper, imageName + "(1)", saveDir);
                                    SaveConverted(lower, imageName + "(2)", saveDir);
                                }
                                else // 横長
                                {
                                    Bitmap left = original.Clone(new Rectangle(0, 0, 32, 32), PixelFormat.Format24bppRgb);
                                    Bitmap right = original.Clone(new Rectangle(32, 0, 32, 32), PixelFormat.Format24bppRgb);
                                    SaveConverted(left, imageName + "(1)", saveDir);
                                    SaveConverted(right, imageName + "(2)", saveDir);
                                                                    }
                            }
                            else if (original.Width == 32 && original.Height == 32)
                            {
                                saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nowTime}_Converted", folderName, imageName + ".Icon");
                                Directory.CreateDirectory(saveDir);

                                // (1)は通常の画像
                                SaveConverted(original, imageName + "(1)", saveDir);

                                // パレット先頭色を取得
                                List<Color> paletteForImage = palettes[FindBestPalette(original, palettes)];
                                Color baseColor = paletteForImage[0];

                                // (2)は1ドット上にずらした画像、下端に baseColor を埋める
                                Bitmap shifted = new Bitmap(32, 32, PixelFormat.Format24bppRgb);
                                using (Graphics g = Graphics.FromImage(shifted))
                                {
                                    using (SolidBrush brush = new SolidBrush(baseColor))
                                    {
                                        g.FillRectangle(brush, 0, 0, 32, 32); // 背景塗り（先頭色）
                                    }

                                    g.DrawImage(original, new Rectangle(0, -1, 32, 32)); // 上へ1ドットずらし描画
                                }

                                SaveConverted(shifted, imageName + "(2)", saveDir);
                                shifted.Dispose();
                            }
                            else
                            {
                                saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nowTime}_Converted", folderName);
                                Directory.CreateDirectory(saveDir);

                                SaveConverted(original, imageName, saveDir);
                            }
                        }

                        if (toolStripProgressBar_Loading.Value < toolStripProgressBar_Loading.Maximum)
                        {
                            toolStripProgressBar_Loading.Value++;                            
                        }
                    }

                    MessageBox.Show(LanguageManager.Get("complete_palletChange2"), "完了");
                    toolStripProgressBar_Loading.Value = 0;
                    toolStripStatusLabel_Information.Text = LanguageManager.Get("complete_palletChange2");

                    string openDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nowTime}_Converted");
                    if (Directory.Exists(openDir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", openDir);
                    }
                }
            }        
        }
        //-------------------------------------------------------------------------------
        // フォルダ内にある画像からパレットを抽出して比較に使用する
        //-------------------------------------------------------------------------------
        private void LoadPalettes()
        {
            string resourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PalletResources");
            if (!Directory.Exists(resourceDir))
            {
                MessageBox.Show(LanguageManager.Get("Error_PalletResource"), "Error");
                return;
                
            }

            string[] paletteFiles = Directory.GetFiles(resourceDir, "*.png");

            palettes = new List<List<Color>>();
            foreach (string file in paletteFiles)
            {
                if (File.Exists(file))
                {
                    using (Bitmap bmp = new Bitmap(file))
                    {
                        var extracted = ExtractPalette(bmp);
                        if (extracted != null && extracted.Count > 0)
                        {
                            palettes.Add(extracted);
                        }
                        else
                        {
                            MessageBox.Show($"パレット画像「{Path.GetFileName(file)}」に有効な色が含まれていません。", "パレット読み込みエラー");
                        }
                    }
                }
            }

            if (palettes.Count == 0)
            {
                MessageBox.Show(LanguageManager.Get("Error_Disabled Palette"), "Error");
            }
        }

        private void SaveConverted(Bitmap part, string name, string saveDir)
        {
            int bestIndex = FindBestPalette(part, palettes);
            Bitmap newImg = CreateIndexedImage(part, palettes[bestIndex]);
            newImg.Save(Path.Combine(saveDir, name + ".bmp"), ImageFormat.Bmp);
        }

        private List<Color> ExtractPalette(Bitmap bmp, int maxColors = 16)
        {
            HashSet<Color> seen = new HashSet<Color>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color color = bmp.GetPixel(x, y);
                    if (seen.Add(color) && seen.Count == maxColors)
                        return seen.ToList();
                }
            }
            return seen.ToList();
        }

        private int ColorDistance(Color c1, Color c2)
        {
            return (c1.R - c2.R) * (c1.R - c2.R) +
                   (c1.G - c2.G) * (c1.G - c2.G) +
                   (c1.B - c2.B) * (c1.B - c2.B);
        }

        public int FindBestPalette(Bitmap bmp, List<List<Color>> palettes)
        {
            Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (colorCounts.ContainsKey(c))
                        colorCounts[c]++;
                    else
                        colorCounts[c] = 1;
                }
            }

            int bestIndex = 0;
            long bestDistance = long.MaxValue;

            for (int i = 0; i < palettes.Count; i++)
            {
                long total = 0;
                foreach (var pair in colorCounts)
                {
                    int minDist = palettes[i].Min(p => ColorDistance(pair.Key, p));
                    total += minDist * pair.Value;
                }

                if (total < bestDistance)
                {
                    bestDistance = total;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        public List<List<Color>> GetPalettes()
        {
            return palettes ?? new List<List<Color>>();
        }

        private Bitmap CreateIndexedImage(Bitmap src, List<Color> palette)
        {
            Bitmap indexed = new Bitmap(src.Width, src.Height, PixelFormat.Format8bppIndexed);

            ColorPalette pal = indexed.Palette;
            for (int i = 0; i < palette.Count; i++)
                pal.Entries[i] = palette[i];
            for (int i = palette.Count; i < 256; i++)
                pal.Entries[i] = Color.Black;
            indexed.Palette = pal;

            BitmapData srcData = src.LockBits(new Rectangle(0, 0, src.Width, src.Height),
                                              ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = indexed.LockBits(new Rectangle(0, 0, indexed.Width, indexed.Height),
                                                  ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

            int srcStride = srcData.Stride;
            int dstStride = dstData.Stride;
            int height = src.Height;
            int width = src.Width;

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;

                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = srcPtr + (y * srcStride);
                    byte* dstRow = dstPtr + (y * dstStride);

                    for (int x = 0; x < width; x++)
                    {
                        int b = srcRow[x * 4 + 0];
                        int g = srcRow[x * 4 + 1];
                        int r = srcRow[x * 4 + 2];

                        int minDist = int.MaxValue;
                        byte bestIndex = 0;

                        for (byte i = 0; i < palette.Count; i++)
                        {
                            Color c = palette[i];
                            int dist = (r - c.R) * (r - c.R) + (g - c.G) * (g - c.G) + (b - c.B) * (b - c.B);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                bestIndex = i;
                            }
                        }

                        dstRow[x] = bestIndex;
                    }
                }
            }

            src.UnlockBits(srcData);
            indexed.UnlockBits(dstData);
            return indexed;
        }

        // ====================================================================================

        //-------------------------------------------------------------------------------
        // romデータからバージョンを取得する
        //-------------------------------------------------------------------------------
        public static string GetRomGameCode(byte[] romData)
        {
            if (romData == null || romData.Length < 176)
                throw new ArgumentException("ROMデータが不足しています。");

            // ROMの172〜175バイト（0xAC〜0xAF）を取得し、ASCIIに変換
            return Encoding.ASCII.GetString(romData, 172, 4);
        }
        public static string GetCopyright(byte[] romData)
        {
            if (romData == null || romData.Length < 176)
                throw new ArgumentException("ROMデータが不足しています。");

            // ROMの264〜295バイトを取得し、ASCIIに変換
            return Encoding.ASCII.GetString(romData, 264, 32);
        }

        //-------------------------------------------------------------------------------
        // Rom内のアイコンを取得して保存
        //-------------------------------------------------------------------------------
        private void ToolStripMenuItem_OpenRom_Click(object sender, EventArgs e)
        {
            //ファイルダイアログを生成する
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "gbaファイルを選択してください";
            ofd.Filter = "gbaファイル (*.gba)|*.gba";
            ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ofd.RestoreDirectory = true;
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;

            //ファイルダイアログを表示する
            DialogResult dialog = ofd.ShowDialog();

            //「開く」ボタンが選択された際の処理
            if (dialog == DialogResult.OK)
            {
                string selectedFile = ofd.FileName;
                toolStripStatusLabel_Mode.Text = "Mode：Rom image";
                toolStripStatusLabel_ViewIcon.Image = Properties.Resources.gba_Small_icon;
                toolStripStatusLabel_FilePath.Text = selectedFile;

                // 必要に応じてファイルパスを使って処理
                try 
                {
                    LoadPalettes();
                    LoadIconsFromRom(selectedFile, totalPokemonNum);
                    StartAnimation();
                }
                catch
                {
                    toolStripStatusLabel_Information.Text = LanguageManager.Get("Error_Read_miss");
                }

            }
            //「キャンセル」時の処理
            else if (dialog == DialogResult.Cancel)
            {
                // キャンセル時の任意の処理（必要があれば）
            }
        }

        //-------------------------------------------------------------------------------
        // Rom内のアイコンを取得してメインフォームに表示
        //-------------------------------------------------------------------------------
        public void LoadIconsFromRom(string romPath, int iconCount)
        {
            scrollPanel.AutoScroll = false;
            scrollPanel.Controls.Clear();
            icons.Clear();
            toolStripStatusLabel_RomID.Text = "";
            
            int x = 10, y = 10;
            int iconSize = baseIconSize * displayScale;
            int spacing = 10;
            int count = 0;

            int panelSpacing = 8;  // パネル間隔
            SharpPictureBox fpBox = null;


            byte[] romData = File.ReadAllBytes(romPath);
            Binary_Data = romData;
            string gameCode = GetRomGameCode(romData);
            string gameCopyright = GetCopyright(romData);

            int pokeIconImageAddress = GetDataBase.GetPokeIconImageAddress(romData);
            int pokeIconPalAddress = GetDataBase.GetPokeIconPalAddress(romData);
            int pokeIconPalAttrAddress = GetDataBase.GetPokeIconPalAttrAddress(romData);
            int pokeFootprintAddress = 0;

            Panel panel = new Panel();

            toolStripProgressBar_Loading.Minimum = 0;
            toolStripProgressBar_Loading.Maximum = iconCount;
            toolStripProgressBar_Loading.Value = 0;

            // メインフォームの大きさ調整
            UpdateFormSizeBasedOnDisplayCount(iconCount);

            // Cartridge種別確認
            toolStripStatusLabel_RomID.Text = gameCode;
            if (gameCode == "BPRJ")
            {
                pokeFootprintAddress = 4211044;
                toolStripStatusLabel_ViewIcon.Image = Properties.Resources.FR_icon;
            }
            else if (gameCode == "BPRE")
            {
                pokeFootprintAddress = 4455089;
                toolStripStatusLabel_ViewIcon.Image = Properties.Resources.FR_icon;
            }
            else if (gameCode == "BPEJ")
            {
                pokeFootprintAddress = 5517672;
                toolStripStatusLabel_ViewIcon.Image = Properties.Resources.EM_icon;
            }
            else if (gameCode == "BPEE" &&
                     gameCopyright.IndexOf("emerald", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pokeFootprintAddress = 5695124;                
                toolStripStatusLabel_ViewIcon.Image = Properties.Resources.EM_icon;
            }
            else if (gameCode == "BPEE" &&
                     gameCopyright.IndexOf("spades", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     gameCopyright.IndexOf("clover", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pokeFootprintAddress = 6688912;
                //pokeIconImageAddress = 6746384;
                toolStripStatusLabel_ViewIcon.Image = Properties.Resources.EM_icon;
            }
            else
            {
                var dialog = new SaveLocationDialog(
                    "",
                    "",
                    Properties.Resources.Rom_rule,
                    "OK",
                     LanguageManager.Get("Error_NotCoverd_Rom1") + "\n" +
                     LanguageManager.Get("Error_NotCoverd_Rom2")
                    );
                dialog.ShowDialog();
                return;
            }


            for (int i = 0; i < iconCount; i++)
            {
                try
                {
                    int palletoffset =pokeIconImageAddress + (i << 2);
                    int iconDataAddr = RomEditExtensions.ReadPointer((IEnumerable<byte>)romData, pokeIconImageAddress + (i << 2), 134217728u);
                    byte paletteId = romData[pokeIconPalAttrAddress + i];
                    int paletteAddr = RomEditExtensions.ReadPointer((IEnumerable<byte>)romData, pokeIconPalAddress + (paletteId << 3), 134217728u);

                    int FootPrinttable = pokeFootprintAddress + (i << 2);
                    int FootPrintAddr = RomEditExtensions.ReadPointer((IEnumerable<byte>)romData, pokeFootprintAddress + (i << 2), 134217728u);

                    byte[] img1 = RomEditExtensions.GetRange(romData, iconDataAddr, 512);       // 手持ちアイコン1
                    byte[] img2 = RomEditExtensions.GetRange(romData, iconDataAddr + 512, 512); // 手持ちアイコン2
                    byte[] FPimg = RomEditExtensions.GetRange(romData, FootPrintAddr, 32);      // 足跡アイコン
                    byte[] palData = RomEditExtensions.GetRange(romData, paletteAddr, 32);      // 手持ちアイコンpallet
                    Color[] palette = GBATileMode0.ToColorMap(palData);

                    Bitmap bitmap1 = GBATileMode0.BuildSprite(img1, 32, 32, palette);
                    Bitmap bitmap2 = GBATileMode0.BuildSprite(img2, 32, 32, palette);
                                        
                    int bestIndex = FindBestPalette(bitmap1, palettes);
                    bitmap1 = CreateIndexedImage(bitmap1, palettes[bestIndex]);
                    bitmap2 = CreateIndexedImage(bitmap2, palettes[bestIndex]);

                    Bitmap image1, image2;

                    if (showBG0)
                    {
                        image1 = ApplyTransparencyFromPalette(bitmap1);
                        image2 = ApplyTransparencyFromPalette(bitmap2);
                    }
                    else
                    {
                        image1 = bitmap1;
                        image2 = bitmap2;
                    }

                    var pb = new SharpPictureBox
                    {
                        Width = iconSize,
                        Height = iconSize,
                        Location = new Point(x, y),
                        Image = image1,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Interpolation = InterpolationMode.NearestNeighbor,
                        Cursor = Cursors.Hand
                    };

                    pb.Click += PictureBox_Click;
                    pb.DoubleClick += PictureBox_DoubleClick;
                    toolTip.SetToolTip(pb, LanguageManager.Get("ToolTips_OpenEditor"));

                    if (showscaleUp) // 図鑑表示モード（拡大モードを流用）
                    {
                        panel = new Panel
                        {
                            Width = iconSize + 2,
                            Height = showFootPrint ? iconSize * 2 + spacing : iconSize + 24 + 4,
                            BorderStyle = BorderStyle.FixedSingle,
                            Location = new Point(x, y),
                            BackColor = Color.White
                        };

                        var label = new Label
                        {
                            Text = $"{i:D3}",
                            Font = customFont,
                            UseCompatibleTextRendering = true,
                            ForeColor = Color.DimGray,
                            AutoSize = true,
                            Location = new Point(2, 2)
                        };
                        panel.Controls.Add(label);
                        label.BringToFront();

                        int labelBottom = label.Bottom;
                        pb.Location = new Point((panel.Width - pb.Width) / 2, labelBottom + 4);
                        panel.Controls.Add(pb);

                        // パレットID表示（オプション）
                        if (showPaletteId)
                        {
                            //var iconPalette = ExtractPaletteColors((Bitmap)bitmap1); // 色取得の処理を変更
                            //int matchedIndex = FindMatchingPaletteId(palette.ToList());
                            if (bestIndex != -1)
                            {
                                int overlaySize = 16 * displayScale;
                                label = new Label
                                {
                                    Text = bestIndex.ToString(),
                                    Font = new Font("Arial", 10 * displayScale, FontStyle.Bold),
                                    ForeColor = Color.Black,
                                    BackColor = Color.FromArgb(180, Color.White),
                                    AutoSize = false,
                                    Width = overlaySize,
                                    Height = overlaySize,
                                    TextAlign = ContentAlignment.MiddleCenter,
                                    Location = new Point(pb.Width - overlaySize - 2, pb.Height - overlaySize - 2)
                                };
                                pb.Controls.Add(label);
                                label.BringToFront();
                            }
                        }

                        if (showFootPrint)
                        {
                            Bitmap footprintBitmap = GBATileMode0.BuildFootprintSprite(FPimg, 16, 16);
                            Bitmap footprintImage = ApplyTransparencyFromPalette(footprintBitmap);
                            fpBox = new SharpPictureBox
                            {
                                Width = iconSize,
                                Height = iconSize,
                                Image = footprintImage,
                                SizeMode = PictureBoxSizeMode.StretchImage,
                                Interpolation = InterpolationMode.NearestNeighbor,
                                Cursor = Cursors.Hand
                            };
                            fpBox.DoubleClick += FootprintBox_DoubleClick;
                            fpBox.Location = new Point((panel.Width - fpBox.Width) / 2, 24 + iconSize + 10);
                            toolTip.SetToolTip(fpBox, LanguageManager.Get("ToolTips_OpenEditor"));
                            panel.Controls.Add(fpBox);

                            panel.Height = fpBox != null ? fpBox.Bottom + 4 : pb.Bottom + 4;
                            scrollPanel.Controls.Add(panel);
                            footprints.Add((fpBox, footprintBitmap, FootPrintAddr, FootPrinttable));
                        }

                        scrollPanel.Controls.Add(panel);
                    }
                    else // 通常表示モード
                    {
                        pb.Location = new Point(x, y);
                        toolTip.SetToolTip(pb, LanguageManager.Get("ToolTips_OpenEditor"));
                        scrollPanel.Controls.Add(pb);

                        // パレットID表示（オプション）
                        if (showPaletteId)
                        {
                            //var iconPalette = ExtractPaletteColors((Bitmap)bitmap1); // 色取得の処理を変更
                            //int matchedIndex = FindMatchingPaletteId(palette.ToList());
                            if (bestIndex != -1)
                            {
                                int overlaySize = 16 * displayScale;
                                var label = new Label
                                {
                                    Text = bestIndex.ToString(),
                                    Font = new Font("Arial", 10 * displayScale, FontStyle.Bold),
                                    ForeColor = Color.Black,
                                    BackColor = Color.FromArgb(180, Color.White),
                                    AutoSize = false,
                                    Width = overlaySize,
                                    Height = overlaySize,
                                    TextAlign = ContentAlignment.MiddleCenter,
                                    Location = new Point(pb.Width - overlaySize - 2, pb.Height - overlaySize - 2)
                                };
                                pb.Controls.Add(label);
                                label.BringToFront();
                            }
                        }

                        if (showFootPrint)
                        {
                            Bitmap footprintBitmap = GBATileMode0.BuildFootprintSprite(FPimg, 16, 16);
                            Bitmap footprintImage = ApplyTransparencyFromPalette(footprintBitmap);
                            fpBox = new SharpPictureBox
                            {
                                Width = iconSize,
                                Height = iconSize,
                                Location = new Point(x, y + iconSize + spacing),
                                Image = footprintImage,
                                SizeMode = PictureBoxSizeMode.StretchImage,
                                Interpolation = InterpolationMode.NearestNeighbor,
                                Cursor = Cursors.Hand
                            };
                            fpBox.DoubleClick += FootprintBox_DoubleClick;
                            toolTip.SetToolTip(fpBox, LanguageManager.Get("ToolTips_OpenEditor"));
                            scrollPanel.Controls.Add(fpBox);
                            footprints.Add((fpBox, footprintBitmap, FootPrintAddr, FootPrinttable));
                        }
                    }

                    icons.Add((pb, image1, image2, $"ROM_Index_{i}", iconDataAddr, iconDataAddr + 512, pokeIconPalAttrAddress + i, palletoffset));

                    x += iconSize + panelSpacing;
                    count++;

                    if (count % displayCount == 0)
                    {
                        x = 10;
                        if (showscaleUp)
                        {
                            y += panel.Height + panelSpacing;
                        }
                        else
                        {
                            y += (showFootPrint ? iconSize * 2 + spacing : iconSize) + panelSpacing;
                        }
                    }


                    toolStripProgressBar_Loading.Value++;
                    if (i % displayCount == 0)
                        Application.DoEvents(); // 10回に一度UI更新                    
                }
                catch
                {
                    // エラーはスキップして続行
                }
            }
            Application.DoEvents(); // 最後にもう一度UIを更新

            scrollPanel.AutoScrollMinSize = new Size(
                x + iconSize + 20,
                y + iconSize + (showFootPrint ? iconSize : 0) + 50
);
            scrollPanel.AutoScroll = true;
            toolStripProgressBar_Loading.Value = 0;
            toolStripStatusLabel_Information.Text = LanguageManager.Get("complete_iconLoad");
            toolStripMenuItem_FootPrint.Visible = true;
        }

        //-------------------------------------------------------------------------------
        // Rom内のアイコンを取得してエクスポート
        //-------------------------------------------------------------------------------
        private void toolStripMenuItem_ExtractIcon_Click(object sender, EventArgs e)
        {
            //ファイルダイアログを生成する
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "gbaファイルを選択してください";
            ofd.Filter = "gbaファイル (*.gba)|*.gba";
            ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ofd.RestoreDirectory = true;
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;

            //ファイルダイアログを表示する
            DialogResult dialog = ofd.ShowDialog();

            //「開く」ボタンが選択された際の処理
            if (dialog == DialogResult.OK)
            {
                string selectedFile = ofd.FileName;
                toolStripStatusLabel_Information.Text = "ROMアイコンを抽出中…";
                toolStripStatusLabel_FilePath.Text = selectedFile;
                SaveIcon(selectedFile);
            }
            //「キャンセル」時の処理
            else if (dialog == DialogResult.Cancel)
            {
                // キャンセル時の任意の処理（必要があれば）
            }
        }

        //-------------------------------------------------------------------------------
        // Rom内のアイコンをbmpでエクスポート
        //-------------------------------------------------------------------------------
        private void SaveIcon(string baseDir)
        {
            string targetDir = AppDomain.CurrentDomain.BaseDirectory;
            string name = $"exported_{DateTime.Now:yyyyMMdd_HHmmss}";
            string path = Path.Combine(targetDir, name);
            Directory.CreateDirectory(path);

            byte[] romData = File.ReadAllBytes(baseDir);

            // アドレスをdllから取得
            int pokeIconImageAddress = GetDataBase.GetPokeIconImageAddress(romData);
            int pokeIconPalAddress = GetDataBase.GetPokeIconPalAddress(romData);
            int pokeIconPalAttrAddress = GetDataBase.GetPokeIconPalAttrAddress(romData);

            //decimal value = numericUpDown1.Value;
            decimal value = totalPokemonNum;
            int length = value.ToString().Length;

            toolStripProgressBar_Loading.Minimum = 0;
            toolStripProgressBar_Loading.Maximum = totalPokemonNum;
            toolStripProgressBar_Loading.Value = 0;

            for (int i = 0; (decimal)i < value; i++)
            {
                try
                {
                    // i番目のアイコン画像のポインタを読み取り、画像データの実アドレスを計算                    
                    int num = RomEditExtensions.ReadPointer((IEnumerable<byte>)romData, pokeIconImageAddress + (i << 2), 134217728u);

                    // アイコンパレットID(1byte)を取得
                    byte b = romData[pokeIconPalAttrAddress + i];
                    // アイコンパレットアドレスを取得
                    int num2 = RomEditExtensions.ReadPointer((IEnumerable<byte>)romData, pokeIconPalAddress + (b << 3), 134217728u);


                    // 画像本体を取得 512byte（8x8タイル × 16タイル = 32×32アイコン）
                    byte[] image_range1 = RomEditExtensions.GetRange(romData, num, 512);
                    byte[] image_range2 = RomEditExtensions.GetRange(romData, num + 512, 512);

                    // パレットを取得 32byte（RGB555形式で16色）
                    byte[] range2 = RomEditExtensions.GetRange(romData, num2, 32);
                    //Console.WriteLine(num2.ToString("X8"));
                    Color[] array = GBATileMode0.ToColorMap(range2);

                    // 画像データとパレットから 32×32 ピクセルの Bitmap を構築
                    Bitmap bitmap1 = GBATileMode0.BuildSprite(image_range1, 32, 32, array);
                    Bitmap bitmap2 = GBATileMode0.BuildSprite(image_range2, 32, 32, array);

                    string text2 = i.ToString($"D{length}");

                    // パレットを使用して減色＆インデックス形式で保存
                    //Bitmap indexed1 = CreateIndexedImage(bitmap1, basePalette);
                    //Bitmap indexed2 = CreateIndexedImage(bitmap2, basePalette);

                    string saveDir = Path.Combine(path, $"{i}.icon");
                    Directory.CreateDirectory(saveDir);
                    LoadPalettes();
                    SaveConverted(bitmap1, $"{i}(1)", saveDir);
                    SaveConverted(bitmap2, $"{i}(2)", saveDir);

                    if (toolStripProgressBar_Loading.Value < toolStripProgressBar_Loading.Maximum)
                    {
                        toolStripProgressBar_Loading.Value++;
                    }

                }
                catch
                {
                }
            }

            MessageBox.Show(LanguageManager.Get("complete_iconLoad"), "完了");
            toolStripProgressBar_Loading.Value = 0;
            toolStripStatusLabel_Information.Text = LanguageManager.Get("complete_iconLoad");

        }

        //-------------------------------------------------------------------------------
        // Footprint Editフォームで呼びだし
        //-------------------------------------------------------------------------------
        private void FootprintBox_DoubleClick(object sender, EventArgs e)
        {
            var pb = sender as PictureBox;
            var footprint = footprints.FirstOrDefault(t => t.box == pb);

            if (footprint.image != null)
            {
                var form = new Form_FootprintEditor(
                    footprint.image,
                    toolStripStatusLabel_FilePath.Text,
                    totalPokemonNum,
                    Binary_Data,
                    footprint.offset,
                    footprint.table,
                    this.Location                    
                );
                form.Show();
            }
 
        }

        //-------------------------------------------------------------------------------
        // フォームの大きさ変更処理
        //-------------------------------------------------------------------------------
        private void UpdateFormSizeBasedOnDisplayCount(int iconTotalCount)
        {
            int iconSize = baseIconSize * displayScale;
            int spacing = 10;
            int panelBorder = 4;

            // 横サイズ（アイコンの列数）
            int onePanelWidth = iconSize + spacing + panelBorder;
            int leftMargin = 0;
            int panelAreaWidth = displayCount * onePanelWidth;
            this.Width = panelAreaWidth + leftMargin - 40;

            // 最大表示行数（ここを制限）
            int maxVisibleRows = 2;

            // パネルの高さ（図鑑モードか通常かで変動）
            int onePanelHeight = showscaleUp ? iconSize + 24 + spacing : iconSize + spacing;

            // 縦サイズ（上限付き）
            int topMargin = palettePanel.Bottom + 20;
            int bottomMargin = 100; // ステータスバーなどの余白

            int visibleRowCount = Math.Min((int)Math.Ceiling((double)iconTotalCount / displayCount), maxVisibleRows);
            int panelAreaHeight = visibleRowCount * onePanelHeight;

            this.Height = topMargin + panelAreaHeight + bottomMargin;

            // スクロールパネルのサイズも調整（オプション）
            scrollPanel.Size = new Size(
                this.ClientSize.Width - 40,  // 余白調整
                panelAreaHeight
            );
        }


        // ====================================================================================

        //-------------------------------------------------------------------------------
        // パレットリソースフォルダを表示
        //-------------------------------------------------------------------------------

        private void ToolStripMenuItem_BasepalletFolder_Click(object sender, EventArgs e)
        {
            string resourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PalletResources");
            if (!Directory.Exists(resourceDir))
            {
                MessageBox.Show(LanguageManager.Get("Error_PalletResource"), "Error");
                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", resourceDir);           

        }

        //-------------------------------------------------------------------------------
        // 読込ポケモン総数を書き換えた際
        //-------------------------------------------------------------------------------
        private void toolStripTextBox_ReadIDCount_TextChanged(object sender, EventArgs e)
        {
            ToolStripTextBox textBox = sender as ToolStripTextBox;
            string currentText = textBox.Text;

            // 数字のみを抽出（0-9のみにフィルタ）
            string filteredText = new string(currentText.Where(char.IsDigit).ToArray());

            try
            {
                if (currentText != filteredText)
                {
                    int selStart = textBox.TextBox.SelectionStart;
                    textBox.Text = filteredText;
                    textBox.TextBox.SelectionStart = Math.Max(0, Math.Min(selStart - 1, filteredText.Length));
                }
                totalPokemonNum = int.Parse(textBox.Text);
            }
            catch
            {
                textBox.Text = "0";
                totalPokemonNum = 0;
            }

        }
    }


}
