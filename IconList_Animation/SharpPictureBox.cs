using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace IconList_Animation
{
    public class SharpPictureBox : PictureBox
    {
        // 滲みを防ぐ補間モード（NearestNeighborでピクセルアート向け）
        public InterpolationMode Interpolation { get; set; } = InterpolationMode.NearestNeighbor;

        protected override void OnPaint(PaintEventArgs pe)
        {
            if (Image != null)
            {
                pe.Graphics.InterpolationMode = this.Interpolation;
                pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                pe.Graphics.DrawImage(Image, ClientRectangle);
            }
            else
            {
                base.OnPaint(pe);
            }
        }
    }
}
