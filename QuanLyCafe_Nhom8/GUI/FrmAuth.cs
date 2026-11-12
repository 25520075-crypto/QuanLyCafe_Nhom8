using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.GUI
{
    public class FrmAuth : Form
    {
        private readonly UserDAL _userDal = new UserDAL();

        // Trạng thái OTP Quên mật khẩu
        private string _forgotOtp = "";
        private string _forgotEmail = "";
        private DateTime _forgotOtpExpiry;

        // Các Panel 3D chống nháy (Double-Buffered)
        private BufferedPanel pnlLeft3DStage;
        private BufferedPanel pnlLoginCard;
        private BufferedPanel pnlForgotCard;

        // Controls Đăng nhập
        private TextBox txtLoginUser;
        private TextBox txtLoginPass;

        // Controls Quên mật khẩu
        private TextBox txtForgotEmail;
        private TextBox txtOtpCode;
        private TextBox txtNewPass;
        private Control btnSendOtp3D;
        private Control btnConfirmReset3D;

        // Biến đồ họa 3D Realtime & Mouse Parallax
        private Timer _renderTimer;
        private float _animTick = 0f;
        private PointF _mouseNormalized = new PointF(0, 0);
        private bool _isCupHovered = false;
        private int _cupPressDepth = 0;
        private readonly List<FloatingBean3D> _beans3D = new List<FloatingBean3D>();

        // Cho phép kéo thả cửa sổ không viền
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private class FloatingBean3D
        {
            public float X, Y, Z; // Z: Độ sâu 3D (0.4 -> 1.3)
            public float Angle, SpinSpeed, FloatSpeed, Phase;
        }

        // Panel tùy chỉnh bật sẵn DoubleBuffered để vẽ 3D 60FPS không bị nháy hình
        private class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                this.UpdateStyles();
            }
        }

        public FrmAuth()
        {
            Initialize3DBeans();
            Build3DWindowFrame();
            SetupCoffeeBeanCursor();
            Start3DEngine();
        }

        // ==================== 1. KHỞI TẠO HẠT CAFE BAY TRONG KHÔNG GIAN 3D ====================
        private void Initialize3DBeans()
        {
            Random rnd = new Random(88);
            for (int i = 0; i < 11; i++)
            {
                _beans3D.Add(new FloatingBean3D
                {
                    X = rnd.Next(35, 390),
                    Y = rnd.Next(50, 520),
                    Z = 0.45f + (float)rnd.NextDouble() * 0.85f,
                    Angle = rnd.Next(0, 360),
                    SpinSpeed = (float)(rnd.NextDouble() * 2.4 - 1.2),
                    FloatSpeed = 0.6f + (float)rnd.NextDouble() * 1.1f,
                    Phase = (float)(rnd.NextDouble() * Math.PI * 2)
                });
            }
        }

        private void SetupCoffeeBeanCursor()
        {
            try
            {
                Bitmap bmp = new Bitmap(28, 28);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TranslateTransform(14, 14);
                    g.RotateTransform(-30);

                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                        g.FillEllipse(shadow, -7, -9, 18, 24);

                    using (LinearGradientBrush beanBrush = new LinearGradientBrush(
                        new Rectangle(-9, -12, 18, 24),
                        Color.FromArgb(205, 133, 63),
                        Color.FromArgb(55, 28, 12), 45f))
                    {
                        g.FillEllipse(beanBrush, -9, -12, 18, 24);
                    }

                    using (Pen creasePen = new Pen(Color.FromArgb(35, 15, 5), 2.2f))
                    {
                        Point[] curve = { new Point(0, -10), new Point(-3, -3), new Point(3, 3), new Point(0, 10) };
                        g.DrawCurve(creasePen, curve);
                    }
                }
                this.Cursor = new Cursor(bmp.GetHicon());
            }
            catch
            {
                this.Cursor = Cursors.Hand;
            }
        }

        private void Start3DEngine()
        {
            _renderTimer = new Timer { Interval = 25 }; // ~40 FPS mượt mà
            _renderTimer.Tick += (s, e) =>
            {
                _animTick += 0.08f;
                foreach (var b in _beans3D)
                {
                    b.Angle += b.SpinSpeed;
                    b.Y -= b.FloatSpeed * b.Z;
                    if (b.Y < -30) b.Y = 580;
                }
                pnlLeft3DStage.Invalidate();
            };
            _renderTimer.Start();
        }

        // ==================== 2. DỰNG KHUNG CỬA SỐ 3D KHÔNG VIỀN ====================
        private void Build3DWindowFrame()
        {
            this.Text = "Hệ thống Quản lý Cafe 3D - Nhóm 8";
            this.Size = new Size(980, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(28, 18, 14);
            this.DoubleBuffered = true;

            // Bo tròn góc toàn bộ cửa sổ Form
            this.Load += (s, e) =>
            {
                using (GraphicsPath formPath = GetRoundedPath(new Rectangle(0, 0, this.Width, this.Height), 22))
                {
                    this.Region = new Region(formPath);
                }
            };

            // Vẽ nền 3D phía bên phải
            this.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    this.ClientRectangle,
                    Color.FromArgb(42, 27, 21),
                    Color.FromArgb(22, 13, 9), 135f))
                {
                    g.FillRectangle(bgBrush, this.ClientRectangle);
                }

                // Quầng sáng 3D Ambient phía dưới Card bên phải
                using (GraphicsPath glowPath = new GraphicsPath())
                {
                    glowPath.AddEllipse(440, 40, 500, 500);
                    using (PathGradientBrush pgb = new PathGradientBrush(glowPath))
                    {
                        pgb.CenterColor = Color.FromArgb(45, 230, 126, 34);
                        pgb.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(pgb, glowPath);
                    }
                }

                // Viền kim loại 3D bao quanh toàn Form
                using (Pen borderPen = new Pen(Color.FromArgb(110, 211, 84, 0), 2f))
                using (GraphicsPath borderPath = GetRoundedPath(new Rectangle(1, 1, this.Width - 3, this.Height - 3), 22))
                {
                    g.DrawPath(borderPen, borderPath);
                }
            };

            // Cho phép kéo cửa sổ khi giữ chuột trên nền Form
            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            // Sân khấu 3D bên trái (430px)
            pnlLeft3DStage = new BufferedPanel
            {
                Location = new Point(0, 0),
                Size = new Size(430, 580),
                BackColor = Color.Transparent
            };
            pnlLeft3DStage.Paint += PnlLeft3DStage_Paint;
            pnlLeft3DStage.MouseMove += PnlLeft3DStage_MouseMove;
            pnlLeft3DStage.MouseLeave += (s, e) => { _isCupHovered = false; _cupPressDepth = 0; _mouseNormalized = new PointF(0, 0); };
            pnlLeft3DStage.MouseDown += PnlLeft3DStage_MouseDown;
            pnlLeft3DStage.MouseUp += PnlLeft3DStage_MouseUp;

            // Nút Đóng (X) và Thu nhỏ (_) 3D ở góc phải trên cùng
            Control btnClose = CreateWindowControlButton("✕", new Point(932, 14), Color.FromArgb(192, 57, 43), (s, e) => Application.Exit());
            Control btnMin = CreateWindowControlButton("—", new Point(892, 14), Color.FromArgb(93, 64, 55), (s, e) => this.WindowState = FormWindowState.Minimized);

            Build3DLoginCard();
            Build3DForgotCard();

            this.Controls.Add(btnClose);
            this.Controls.Add(btnMin);
            this.Controls.Add(pnlLeft3DStage);
            this.Controls.Add(pnlLoginCard);
            this.Controls.Add(pnlForgotCard);

            ShowCard(pnlLoginCard);
        }

        // ==================== 3. VẼ SÂN KHẤU LY CAFE 3D ẢO & HOLOGRAM ====================
        private void PnlLeft3DStage_MouseMove(object sender, MouseEventArgs e)
        {
            // Tính góc nghiêng 3D Parallax theo tọa độ chuột (-1.0 -> +1.0)
            _mouseNormalized = new PointF(
                (e.X - pnlLeft3DStage.Width / 2f) / (pnlLeft3DStage.Width / 2f),
                (e.Y - pnlLeft3DStage.Height / 2f) / (pnlLeft3DStage.Height / 2f)
            );

            // Kiểm tra chuột có đang chạm vào vùng Ly Cafe 3D không
            Rectangle cupHitBox = new Rectangle(110, 175, 210, 220);
            _isCupHovered = cupHitBox.Contains(e.Location);

            // Cho phép kéo cửa sổ nếu nhấn giữ ngoài vùng ly cafe
            if (e.Button == MouseButtons.Left && !_isCupHovered)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0);
            }
        }

        private void PnlLeft3DStage_MouseDown(object sender, MouseEventArgs e)
        {
            if (_isCupHovered && e.Button == MouseButtons.Left)
            {
                _cupPressDepth = 8;
                pnlLeft3DStage.Invalidate();
            }
        }

        private void PnlLeft3DStage_MouseUp(object sender, MouseEventArgs e)
        {
            if (_cupPressDepth > 0)
            {
                _cupPressDepth = 0;
                pnlLeft3DStage.Invalidate();
                PerformLogin();
            }
        }

        private void PnlLeft3DStage_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = pnlLeft3DStage.Width;
            int h = pnlLeft3DStage.Height;

            // 1. Nền 3D Studio tối sang trọng
            using (LinearGradientBrush studioBg = new LinearGradientBrush(
                new Rectangle(0, 0, w, h),
                Color.FromArgb(52, 31, 23),
                Color.FromArgb(18, 10, 7), 115f))
            {
                g.FillRectangle(studioBg, 0, 0, w, h);
            }

            // Ánh sáng Spotlight 3D chiếu từ đỉnh xuống ly cafe
            float px = _mouseNormalized.X * 18f;
            float py = _mouseNormalized.Y * 14f;

            using (GraphicsPath spotPath = new GraphicsPath())
            {
                spotPath.AddEllipse(30 + px, 85 + py, 370, 390);
                using (PathGradientBrush spotBrush = new PathGradientBrush(spotPath))
                {
                    spotBrush.CenterColor = _isCupHovered
                        ? Color.FromArgb(115, 255, 152, 0)
                        : Color.FromArgb(70, 211, 84, 0);
                    spotBrush.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(spotBrush, spotPath);
                }
            }

            // 2. Vẽ các hạt cafe 3D lớp xa (Z < 0.8)
            foreach (var b in _beans3D)
            {
                if (b.Z < 0.8f) DrawSingle3DBean(g, b, px, py);
            }

            // 3. Bệ phóng Hologram 3D dưới đáy đĩa
            int cx = 215 + (int)px;
            int cy = 340 + (int)py + _cupPressDepth + (_isCupHovered ? -6 : 0);

            for (int r = 0; r < 3; r++)
            {
                float pulse = (float)Math.Sin(_animTick * 1.5f + r) * 6f;
                int rw = 240 + r * 28 + (int)pulse;
                int rh = 44 + r * 7;
                int alpha = _isCupHovered ? (110 - r * 30) : (60 - r * 18);
                using (Pen holoPen = new Pen(Color.FromArgb(Math.Max(15, alpha), 255, 183, 77), 1.8f))
                {
                    g.DrawEllipse(holoPen, cx - rw / 2, 392 + (int)(py * 0.5f) - rh / 2, rw, rh);
                }
            }

            // Bóng đổ 3D sâu dưới đáy đĩa (Ambient Occlusion)
            using (GraphicsPath shadowPath = new GraphicsPath())
            {
                shadowPath.AddEllipse(cx - 120, 372, 240, 48);
                using (PathGradientBrush shadowBrush = new PathGradientBrush(shadowPath))
                {
                    shadowBrush.CenterColor = Color.FromArgb(210, 0, 0, 0);
                    shadowBrush.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(shadowBrush, shadowPath);
                }
            }

            // 4. Đĩa sứ 3D (Saucer) có độ dày và ánh kim
            Rectangle saucerBottom = new Rectangle(cx - 108, cy + 32, 216, 42);
            Rectangle saucerTop = new Rectangle(cx - 112, cy + 25, 224, 42);
            using (LinearGradientBrush sRim = new LinearGradientBrush(saucerBottom, Color.FromArgb(130, 110, 98), Color.FromArgb(65, 48, 40), 90f))
                g.FillEllipse(sRim, saucerBottom);

            using (LinearGradientBrush sSurface = new LinearGradientBrush(saucerTop, Color.FromArgb(252, 248, 244), Color.FromArgb(175, 160, 148), 45f))
                g.FillEllipse(sSurface, saucerTop);

            // Lòng đĩa trũng 3D
            using (LinearGradientBrush sWell = new LinearGradientBrush(new Rectangle(cx - 65, cy + 34, 130, 22), Color.FromArgb(145, 130, 118), Color.FromArgb(235, 228, 220), 90f))
                g.FillEllipse(sWell, cx - 65, cy + 34, 130, 22);

            // 5. Quai cầm 3D (Cup Handle) có bóng khối
            using (Pen handleShadow = new Pen(Color.FromArgb(90, 0, 0, 0), 14f))
                g.DrawArc(handleShadow, cx + 48, cy - 68, 62, 76, -65, 145);

            using (LinearGradientBrush handleBrush = new LinearGradientBrush(new Rectangle(cx + 45, cy - 75, 68, 80),
                _isCupHovered ? Color.FromArgb(255, 213, 79) : Color.FromArgb(245, 238, 230),
                _isCupHovered ? Color.FromArgb(191, 54, 12) : Color.FromArgb(135, 105, 90), 45f))
            using (Pen handlePen = new Pen(handleBrush, 13f))
            {
                handlePen.StartCap = LineCap.Round;
                handlePen.EndCap = LineCap.Round;
                g.DrawArc(handlePen, cx + 45, cy - 72, 62, 76, -68, 145);
            }

            // 6. Thân Ly Cafe 3D (Spherical Cylinder Shading)
            Rectangle cupBounds = new Rectangle(cx - 82, cy - 92, 164, 138);
            using (GraphicsPath cupPath = new GraphicsPath())
            {
                cupPath.AddArc(cx - 82, cy - 22, 164, 72, 0, 180);
                cupPath.AddLine(cx - 82, cy + 14, cx - 86, cy - 88);
                cupPath.AddLine(cx - 86, cy - 88, cx + 86, cy - 88);
                cupPath.AddLine(cx + 86, cy - 88, cx + 82, cy + 14);
                cupPath.CloseFigure();

                using (LinearGradientBrush bodyGrad = new LinearGradientBrush(
                    cupBounds,
                    _isCupHovered ? Color.FromArgb(255, 183, 77) : Color.FromArgb(245, 239, 234),
                    _isCupHovered ? Color.FromArgb(165, 42, 4) : Color.FromArgb(93, 64, 55), 15f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Positions = new float[] { 0f, 0.25f, 0.65f, 1f };
                    if (_isCupHovered)
                    {
                        cb.Colors = new Color[] {
                            Color.FromArgb(255, 224, 130),
                            Color.FromArgb(255, 152, 0),
                            Color.FromArgb(216, 67, 21),
                            Color.FromArgb(110, 28, 0)
                        };
                    }
                    else
                    {
                        cb.Colors = new Color[] {
                            Color.FromArgb(255, 252, 248),
                            Color.FromArgb(228, 216, 206),
                            Color.FromArgb(155, 125, 110),
                            Color.FromArgb(78, 52, 42)
                        };
                    }
                    bodyGrad.InterpolationColors = cb;
                    g.FillPath(bodyGrad, cupPath);
                }

                // Vệt phản quang 3D (Specular Glossy Strip) dọc thân ly
                using (GraphicsPath glossPath = new GraphicsPath())
                {
                    glossPath.AddBezier(
                        cx - 64, cy - 78,
                        cx - 66, cy - 35,
                        cx - 56, cy + 10,
                        cx - 38, cy + 32);
                    using (Pen glossPen = new Pen(Color.FromArgb(115, 255, 255, 255), 9f))
                    {
                        glossPen.StartCap = LineCap.Round;
                        glossPen.EndCap = LineCap.Round;
                        g.DrawPath(glossPen, glossPath);
                    }
                }
            }

            // 7. Vành miệng ly 3D & Nước Cafe Espresso + Latte Art
            Rectangle rimOuter = new Rectangle(cx - 86, cy - 108, 172, 40);
            Rectangle liquidRect = new Rectangle(cx - 78, cy - 103, 156, 30);

            using (LinearGradientBrush rimBrush = new LinearGradientBrush(rimOuter, Color.White, Color.FromArgb(180, 165, 155), 0f))
                g.FillEllipse(rimBrush, rimOuter);

            using (LinearGradientBrush coffeeBrush = new LinearGradientBrush(liquidRect, Color.FromArgb(42, 21, 12), Color.FromArgb(125, 68, 32), 45f))
                g.FillEllipse(coffeeBrush, liquidRect);

            // Lớp bọt Crema & Trái tim Latte Art 3D chuyển động nhẹ
            float cremaWave = (float)Math.Sin(_animTick * 2f) * 3f;
            using (Pen cremaPen = new Pen(Color.FromArgb(180, 230, 176, 115), 3f))
                g.DrawArc(cremaPen, cx - 55 + cremaWave, cy - 96, 110, 18, 15, 150);

            // Vẽ tim Latte Art ở giữa mặt cafe
            using (SolidBrush latteArtBrush = new SolidBrush(Color.FromArgb(215, 248, 235, 215)))
            {
                g.FillEllipse(latteArtBrush, cx - 14 + cremaWave, cy - 93, 15, 10);
                g.FillEllipse(latteArtBrush, cx - 1 + cremaWave, cy - 93, 15, 10);
                PointF[] heartTip = {
                    new PointF(cx - 14 + cremaWave, cy - 88),
                    new PointF(cx + 14 + cremaWave, cy - 88),
                    new PointF(cx + cremaWave, cy - 79)
                };
                g.FillPolygon(latteArtBrush, heartTip);
            }

            // 8. Nhãn 3D phát sáng ngay trên thân ly
            string cupLabel = _isCupHovered ? "⚡ CHẠM ĐỂ VÀO CA!" : "☕ CHẠM ĐỂ LOGIN";
            using (Font cupFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(cupLabel, cupFont);
                RectangleF badgeRect = new RectangleF(cx - sz.Width / 2 - 10, cy - 42, sz.Width + 20, 26);
                using (GraphicsPath badgePath = GetRoundedPath(Rectangle.Round(badgeRect), 12))
                using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(_isCupHovered ? 210 : 150, 38, 20, 14)))
                using (Pen badgeBorder = new Pen(Color.FromArgb(190, 255, 204, 128), 1.2f))
                {
                    g.FillPath(badgeBg, badgePath);
                    g.DrawPath(badgeBorder, badgePath);
                }
                using (SolidBrush txtBrush = new SolidBrush(Color.FromArgb(255, 243, 224)))
                {
                    g.DrawString(cupLabel, cupFont, txtBrush, cx - sz.Width / 2, cy - 38);
                }
            }

            // 9. Khói 3D đa tầng bay lên (Volumetric Steam)
            for (int i = 0; i < 4; i++)
            {
                float sWave = (float)Math.Sin(_animTick * 1.4f + i * 1.3f) * 14f;
                int sx = cx - 38 + i * 25 + (int)sWave;
                int sy = cy - 115 - (int)(Math.Cos(_animTick + i) * 8);

                for (int thick = 3; thick >= 1; thick--)
                {
                    int alpha = (_isCupHovered ? 55 : 32) / thick;
                    using (Pen smokePen = new Pen(Color.FromArgb(alpha, 255, 245, 235), thick * 4.5f))
                    {
                        smokePen.StartCap = LineCap.Round;
                        smokePen.EndCap = LineCap.Round;
                        g.DrawBezier(smokePen,
                            sx, sy + 15,
                            sx - 20, sy - 15,
                            sx + 22, sy - 45,
                            sx - 5, sy - 80);
                    }
                }
            }

            // 10. Vẽ các hạt cafe 3D lớp gần (Z >= 0.8) bay phía trước ly
            foreach (var b in _beans3D)
            {
                if (b.Z >= 0.8f) DrawSingle3DBean(g, b, px, py);
            }

            // 11. Header Thương hiệu 3D Metallic trên đỉnh Sân khấu
            Draw3DStageTypography(g, w, h);

            // Đường phân cách ánh kim 3D giữa Sân khấu trái và Form phải
            using (LinearGradientBrush divBrush = new LinearGradientBrush(
                new Point(w - 2, 0), new Point(w - 2, h),
                Color.Transparent, Color.FromArgb(180, 211, 84, 0)))
            {
                ColorBlend cb = new ColorBlend
                {
                    Positions = new float[] { 0f, 0.5f, 1f },
                    Colors = new Color[] { Color.Transparent, Color.FromArgb(180, 230, 126, 34), Color.Transparent }
                };
                divBrush.InterpolationColors = cb;
                using (Pen divPen = new Pen(divBrush, 2f))
                {
                    g.DrawLine(divPen, w - 1, 20, w - 1, h - 20);
                }
            }
        }

        private void DrawSingle3DBean(Graphics g, FloatingBean3D b, float px, float py)
        {
            GraphicsState state = g.Save();
            float bx = b.X + px * b.Z * 1.4f + (float)Math.Sin(_animTick + b.Phase) * 6f;
            float by = b.Y + py * b.Z * 1.4f;

            g.TranslateTransform(bx, by);
            g.RotateTransform(b.Angle);
            g.ScaleTransform(b.Z, b.Z);

            int alpha = (int)Math.Min(240, 115 * b.Z + 60);
            Rectangle rect = new Rectangle(-9, -13, 18, 26);

            using (SolidBrush dropShadow = new SolidBrush(Color.FromArgb(alpha / 2, 0, 0, 0)))
                g.FillEllipse(dropShadow, -6, -10, 18, 26);

            using (LinearGradientBrush beanBrush = new LinearGradientBrush(
                rect,
                Color.FromArgb(alpha, 210, 140, 75),
                Color.FromArgb(alpha, 58, 30, 14), 45f))
            {
                g.FillEllipse(beanBrush, rect);
            }

            using (Pen splitPen = new Pen(Color.FromArgb(alpha, 30, 14, 6), 2f))
            {
                Point[] sCurve = { new Point(0, -10), new Point(-2, -3), new Point(2, 3), new Point(0, 10) };
                g.DrawCurve(splitPen, sCurve);
            }

            g.Restore(state);
        }

        private void Draw3DStageTypography(Graphics g, int w, int h)
        {
            string title = "COFFEE FEFA 3D";
            using (Font fTitle = new Font("Segoe UI", 22f, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(title, fTitle);
                float tx = (w - sz.Width) / 2f;

                // Đổ bóng chữ 3D 3 lớp
                for (int d = 3; d >= 1; d--)
                {
                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                        g.DrawString(title, fTitle, shadow, tx + d, 34 + d);
                }

                using (LinearGradientBrush goldBrush = new LinearGradientBrush(
                    new RectangleF(tx, 34, sz.Width, sz.Height),
                    Color.FromArgb(255, 248, 225),
                    Color.FromArgb(245, 127, 23), 90f))
                {
                    g.DrawString(title, fTitle, goldBrush, tx, 34);
                }
            }

            string sub = "Tương tác chuột 3D • Chạm Ly Cafe để vào Ca trực";
            using (Font fSub = new Font("Segoe UI", 9.5f, FontStyle.Italic))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(215, 195, 185)))
            {
                SizeF sz = g.MeasureString(sub, fSub);
                g.DrawString(sub, fSub, subBrush, (w - sz.Width) / 2f, 78);
            }

            // Thẻ gợi ý tài khoản 3D Glass ở đáy cột trái
            Rectangle hintBox = new Rectangle(28, h - 82, w - 56, 54);
            using (GraphicsPath hp = GetRoundedPath(hintBox, 14))
            using (SolidBrush glass = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
            using (Pen border = new Pen(Color.FromArgb(80, 255, 204, 128), 1f))
            {
                g.FillPath(glass, hp);
                g.DrawPath(border, hp);
            }

            using (Font fHint = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (SolidBrush hintBrush = new SolidBrush(Color.FromArgb(255, 224, 178)))
            {
                g.DrawString("🔑 TK Quản lý (Admin): admin / admin123", fHint, hintBrush, 46, h - 74);
                g.DrawString("👤 TK Thu ngân (Staff): thungan1 / staff123", fHint, hintBrush, 46, h - 52);
            }
        }

        // ==================== 4. CARD ĐĂNG NHẬP & QUÊN MẬT KHẨU 3D NEUMORPHISM ====================
        private void Build3DLoginCard()
        {
            pnlLoginCard = Create3DFloatingCard();

            Label lblHeader = CreateCardTitle("ĐĂNG NHẬP CA TRỰC", "Hệ thống phân quyền POS & Bảo mật nội bộ", pnlLoginCard);

            pnlLoginCard.Controls.Add(CreateFieldLabel("TÊN ĐĂNG NHẬP", 108));
            Control boxUser = Create3DInputCapsule("👤", 132, false, out txtLoginUser);

            pnlLoginCard.Controls.Add(CreateFieldLabel("MẬT KHẨU HỆ THỐNG", 202));
            Control boxPass = Create3DInputCapsule("🔒", 226, true, out txtLoginPass);

            CheckBox chkShow = new CheckBox
            {
                Text = "Hiển thị mật khẩu",
                Location = new Point(42, 282),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(215, 195, 180),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            chkShow.CheckedChanged += (s, e) => txtLoginPass.UseSystemPasswordChar = !chkShow.Checked;

            Control btnLogin3D = Create3DExtrudedButton(
                "🚀 ĐĂNG NHẬP VÀO HỆ THỐNG POS",
                new Point(40, 315),
                new Size(390, 52),
                Color.FromArgb(230, 126, 34),
                Color.FromArgb(150, 64, 0),
                (s, e) => PerformLogin());

            // Hỗ trợ bấm phím Enter trong ô tài khoản/mật khẩu để đăng nhập ngay
            KeyEventHandler enterKey = (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; PerformLogin(); } };
            txtLoginUser.KeyDown += enterKey;
            txtLoginPass.KeyDown += enterKey;

            LinkLabel lnkForgot = new LinkLabel
            {
                Text = "🔑 Quên mật khẩu? (Khôi phục bằng OTP Gmail)",
                Location = new Point(40, 382),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                LinkColor = Color.FromArgb(255, 183, 77),
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent
            };
            lnkForgot.Click += (s, e) => ShowCard(pnlForgotCard);

            // Hộp thông báo Chính sách phân quyền Admin 3D
            BufferedPanel pnlSecurityBadge = new BufferedPanel
            {
                Location = new Point(40, 415),
                Size = new Size(390, 62),
                BackColor = Color.Transparent
            };
            pnlSecurityBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, pnlSecurityBadge.Width - 1, pnlSecurityBadge.Height - 1);
                using (GraphicsPath p = GetRoundedPath(r, 12))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(75, 0, 0, 0)))
                using (Pen pen = new Pen(Color.FromArgb(90, 230, 126, 34), 1f))
                {
                    e.Graphics.FillPath(b, p);
                    e.Graphics.DrawPath(pen, p);
                }
                using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Italic))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(210, 190, 175)))
                {
                    e.Graphics.DrawString(
                        "🛡️ Bảo mật: Nhân viên mới không tự đăng ký tại đây.\nQuản lý (Admin) sẽ cấp tài khoản & xác thực OTP Gmail trong POS.",
                        f, tb, new PointF(12, 12));
                }
            };

            pnlLoginCard.Controls.AddRange(new Control[]
            {
                lblHeader, boxUser, boxPass, chkShow,
                btnLogin3D, lnkForgot, pnlSecurityBadge
            });
        }

        private void Build3DForgotCard()
        {
            pnlForgotCard = Create3DFloatingCard();

            Label lblHeader = CreateCardTitle("KHÔI PHỤC MẬT KHẨU", "Xác thực bảo mật 2 lớp qua Gmail OTP", pnlForgotCard);

            pnlForgotCard.Controls.Add(CreateFieldLabel("1. GMAIL ĐÃ LIÊN KẾT VỚI TÀI KHOẢN", 102));
            Control boxEmail = Create3DInputCapsule("✉️", 125, false, out txtForgotEmail, width: 265);

            btnSendOtp3D = Create3DExtrudedButton(
                "Gửi OTP",
                new Point(315, 125),
                new Size(115, 44),
                Color.FromArgb(211, 84, 0),
                Color.FromArgb(135, 54, 0),
                BtnSendForgotOtp_Click);

            pnlForgotCard.Controls.Add(CreateFieldLabel("2. MÃ XÁC THỰC OTP (6 CHỮ SỐ TRONG GMAIL)", 185));
            Control boxOtp = Create3DInputCapsule("🛡️", 208, false, out txtOtpCode);
            txtOtpCode.Enabled = false;

            pnlForgotCard.Controls.Add(CreateFieldLabel("3. NHẬP MẬT KHẨU MỚI", 268));
            Control boxNewPass = Create3DInputCapsule("🔑", 291, true, out txtNewPass);
            txtNewPass.Enabled = false;

            btnConfirmReset3D = Create3DExtrudedButton(
                "✔ XÁC NHẬN CẬP NHẬT MẬT KHẨU",
                new Point(40, 355),
                new Size(390, 50),
                Color.FromArgb(39, 174, 96),
                Color.FromArgb(20, 90, 50),
                BtnConfirmReset_Click);
            btnConfirmReset3D.Enabled = false;

            LinkLabel lnkBack = new LinkLabel
            {
                Text = "← Quay về màn hình Đăng nhập",
                Location = new Point(40, 425),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                LinkColor = Color.FromArgb(255, 204, 128),
                BackColor = Color.Transparent
            };
            lnkBack.Click += (s, e) => ShowCard(pnlLoginCard);

            pnlForgotCard.Controls.AddRange(new Control[]
            {
                lblHeader, boxEmail, btnSendOtp3D,
                boxOtp, boxNewPass, btnConfirmReset3D, lnkBack
            });
        }

        // ==================== 5. CÁC THÀNH PHẦN UI 3D TÙY CHỈNH (CAPSULE & TACTILE BUTTON) ====================
        private BufferedPanel Create3DFloatingCard()
        {
            BufferedPanel card = new BufferedPanel
            {
                Location = new Point(468, 52),
                Size = new Size(470, 500),
                BackColor = Color.Transparent
            };

            card.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Đổ bóng 3D sâu phía dưới Card
                for (int i = 5; i >= 1; i--)
                {
                    Rectangle sr = new Rectangle(8 - i, 10 + i * 2, card.Width - 16, card.Height - 20);
                    using (GraphicsPath sp = GetRoundedPath(sr, 22))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                    {
                        g.FillPath(sb, sp);
                    }
                }

                Rectangle mainRect = new Rectangle(6, 6, card.Width - 14, card.Height - 16);
                using (GraphicsPath path = GetRoundedPath(mainRect, 22))
                using (LinearGradientBrush cardGrad = new LinearGradientBrush(
                    mainRect,
                    Color.FromArgb(66, 43, 34),
                    Color.FromArgb(38, 24, 18), 135f))
                {
                    g.FillPath(cardGrad, path);

                    // Viền Bevel 3D phản sáng cạnh trên
                    using (Pen bevelLight = new Pen(Color.FromArgb(120, 255, 204, 128), 1.6f))
                    {
                        g.DrawPath(bevelLight, path);
                    }
                }
            };

            return card;
        }

        private Label CreateCardTitle(string mainTitle, string subTitle, Control parent)
        {
            Label lbl = new Label
            {
                Text = mainTitle,
                Location = new Point(38, 26),
                AutoSize = true,
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 224, 178),
                BackColor = Color.Transparent
            };

            Label sub = new Label
            {
                Text = subTitle,
                Location = new Point(40, 62),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(195, 175, 160),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(sub);
            return lbl;
        }

        private Label CreateFieldLabel(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(42, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 183, 77),
                BackColor = Color.Transparent
            };
        }

        // Ô nhập liệu 3D Sunken Capsule tự phát sáng Neon khi Focus
        private Control Create3DInputCapsule(string icon, int y, bool isPassword, out TextBox innerTextBox, int width = 390)
        {
            BufferedPanel capsule = new BufferedPanel
            {
                Location = new Point(40, y),
                Size = new Size(width, 44),
                BackColor = Color.Transparent,
                Cursor = Cursors.IBeam
            };

            bool isFocused = false;

            TextBox txt = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(28, 17, 13),
                ForeColor = Color.FromArgb(255, 248, 240),
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                Location = new Point(44, 11),
                Width = width - 58,
                UseSystemPasswordChar = isPassword
            };

            txt.GotFocus += (s, e) => { isFocused = true; capsule.Invalidate(); };
            txt.LostFocus += (s, e) => { isFocused = false; capsule.Invalidate(); };
            capsule.Click += (s, e) => txt.Focus();

            capsule.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = new Rectangle(1, 1, capsule.Width - 3, capsule.Height - 3);
                using (GraphicsPath p = GetRoundedPath(r, 12))
                {
                    // Nền lõm 3D (Sunken)
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(28, 17, 13)))
                        g.FillPath(bg, p);

                    // Bóng lõm bên trong cạnh trên
                    using (Pen innerShadow = new Pen(Color.FromArgb(140, 0, 0, 0), 2f))
                        g.DrawLine(innerShadow, 10, 2, capsule.Width - 10, 2);

                    // Viền phát sáng 3D khi người dùng click vào ô nhập
                    Color borderCol = isFocused
                        ? Color.FromArgb(255, 167, 38)
                        : Color.FromArgb(95, 140, 110, 95);
                    using (Pen borderPen = new Pen(borderCol, isFocused ? 2.2f : 1.2f))
                        g.DrawPath(borderPen, p);
                }

                using (Font iconFont = new Font("Segoe UI Emoji", 11f))
                using (SolidBrush iconBrush = new SolidBrush(isFocused ? Color.FromArgb(255, 183, 77) : Color.FromArgb(170, 145, 130)))
                {
                    g.DrawString(icon, iconFont, iconBrush, new PointF(11, 9));
                }
            };

            capsule.Controls.Add(txt);
            innerTextBox = txt;
            return capsule;
        }

        // Nút bấm 3D nổi khối thật (Có đế 3D dày 5px, lún xuống khi nhấn chuột)
        private Control Create3DExtrudedButton(string text, Point loc, Size size, Color topColor, Color bottomExtrudeColor, EventHandler onClick)
        {
            BufferedPanel btn3D = new BufferedPanel
            {
                Location = loc,
                Size = size,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            bool isHover = false;
            bool isPressed = false;

            btn3D.MouseEnter += (s, e) => { isHover = true; btn3D.Invalidate(); };
            btn3D.MouseLeave += (s, e) => { isHover = false; isPressed = false; btn3D.Invalidate(); };
            btn3D.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isPressed = true; btn3D.Invalidate(); } };
            btn3D.MouseUp += (s, e) =>
            {
                if (isPressed)
                {
                    isPressed = false;
                    btn3D.Invalidate();
                    if (btn3D.Enabled && onClick != null) onClick(btn3D, EventArgs.Empty);
                }
            };

            btn3D.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int extrudeDepth = 5;
                int pressOffset = isPressed ? 4 : (isHover ? -1 : 0);

                // 1. Phần đế 3D bên dưới (3D Extrusion Lip)
                Rectangle baseRect = new Rectangle(1, extrudeDepth, btn3D.Width - 3, btn3D.Height - extrudeDepth - 2);
                using (GraphicsPath basePath = GetRoundedPath(baseRect, 12))
                using (SolidBrush baseBrush = new SolidBrush(btn3D.Enabled ? bottomExtrudeColor : Color.FromArgb(60, 60, 60)))
                {
                    g.FillPath(baseBrush, basePath);
                }

                // 2. Mặt trên của nút 3D
                Rectangle faceRect = new Rectangle(1, 1 + Math.Max(0, pressOffset), btn3D.Width - 3, btn3D.Height - extrudeDepth - 2);
                Color cTop = !btn3D.Enabled ? Color.FromArgb(110, 105, 100)
                    : (isHover ? ControlPaint.Light(topColor, 0.18f) : topColor);
                Color cBot = !btn3D.Enabled ? Color.FromArgb(85, 80, 75)
                    : ControlPaint.Dark(topColor, 0.06f);

                using (GraphicsPath facePath = GetRoundedPath(faceRect, 12))
                using (LinearGradientBrush faceBrush = new LinearGradientBrush(faceRect, cTop, cBot, 90f))
                using (Pen glossPen = new Pen(Color.FromArgb(125, 255, 255, 255), 1.2f))
                {
                    g.FillPath(faceBrush, facePath);
                    g.DrawPath(glossPen, facePath);
                }

                // 3. Chữ có bóng nổi 3D
                using (Font f = new Font("Segoe UI", 10f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(text, f);
                    float tx = (btn3D.Width - sz.Width) / 2f;
                    float ty = faceRect.Y + (faceRect.Height - sz.Height) / 2f;

                    using (SolidBrush shadowTxt = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                        g.DrawString(text, f, shadowTxt, tx + 1, ty + 1);

                    using (SolidBrush mainTxt = new SolidBrush(Color.White))
                        g.DrawString(text, f, mainTxt, tx, ty);
                }
            };

            return btn3D;
        }

        private Control CreateWindowControlButton(string symbol, Point loc, Color hoverColor, EventHandler onClick)
        {
            BufferedPanel btn = new BufferedPanel
            {
                Location = loc,
                Size = new Size(32, 28),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };
            btn.Click += onClick;
            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (GraphicsPath p = GetRoundedPath(r, 8))
                using (SolidBrush b = new SolidBrush(hover ? hoverColor : Color.FromArgb(65, 255, 255, 255)))
                {
                    e.Graphics.FillPath(b, p);
                }
                using (Font f = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    SizeF sz = e.Graphics.MeasureString(symbol, f);
                    e.Graphics.DrawString(symbol, f, tb, (btn.Width - sz.Width) / 2f, (btn.Height - sz.Height) / 2f);
                }
            };
            return btn;
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ==================== 6. XỬ LÝ NGHIỆP VỤ ĐĂNG NHẬP, OTP GMAIL & PHÂN QUYỀN POS ====================
        private void PerformLogin()
        {
            string u = txtLoginUser.Text.Trim();
            string p = txtLoginPass.Text.Trim();

            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
            {
                MessageBox.Show("Vui lòng nhập Tên đăng nhập và Mật khẩu trước khi chạm vào Ly Cafe 3D hoặc bấm Đăng nhập!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLoginUser.Focus();
                return;
            }

            try
            {
                UserDTO user = _userDal.CheckLogin(u, p);
                if (user != null)
                {
                    _renderTimer.Stop();
                    this.Hide();

                    FrmMainPOS frmPos = new FrmMainPOS();
                    AttachRoleAndAdminToolsToPOS(frmPos, user);
                    frmPos.Show();
                }
                else
                {
                    MessageBox.Show("Sai tên đăng nhập, mật khẩu hoặc tài khoản đã bị vô hiệu hóa!", "Từ chối truy cập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối CSDL SQL Server:\n" + ex.Message, "Lỗi SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Gắn thanh Phân quyền & Cấp tài khoản Nhân viên (có xác thực OTP Gmail) lên Header của FrmMainPOS
        private void AttachRoleAndAdminToolsToPOS(FrmMainPOS frmPos, UserDTO user)
        {
            bool isAdmin = user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            string roleVi = isAdmin ? "Quản lý (Admin)" : "Thu ngân (Staff)";

            frmPos.Text = $"Hệ thống Quản lý Quán Cafe Thông Minh - Nhóm 8 | Ca trực: {user.FullName} [{roleVi}]";

            Panel pnlTopHeader = null;
            foreach (Control c in frmPos.Controls)
            {
                if (c is Panel p && p.Dock == DockStyle.Top)
                {
                    pnlTopHeader = p;
                    break;
                }
            }

            if (pnlTopHeader != null)
            {
                foreach (Control c in pnlTopHeader.Controls)
                {
                    if (c is Button btn)
                    {
                        if (btn.Text.Contains("Màn hình Pha chế"))
                        {
                            btn.Location = new Point(355, 11);
                            btn.Size = new Size(175, 38);
                        }
                        else if (btn.Text.Contains("Quản lý Thực đơn"))
                        {
                            btn.Location = new Point(540, 11);
                            btn.Size = new Size(175, 38);
                            btn.Enabled = isAdmin;
                            if (!isAdmin) btn.Text = "🔒 Thực đơn (Admin)";
                        }
                        else if (btn.Text.Contains("Thống kê"))
                        {
                            btn.Location = new Point(725, 11);
                            btn.Size = new Size(185, 38);
                            btn.Enabled = isAdmin;
                            if (!isAdmin) btn.Text = "🔒 Thống kê (Admin)";
                        }
                    }
                }

                Button btnManageUsers = new Button
                {
                    Text = isAdmin ? "👥 Cấp TK Nhân viên (Admin)" : "🔒 Cấp TK (Chỉ Admin)",
                    Location = new Point(920, 11),
                    Size = new Size(215, 38),
                    BackColor = isAdmin ? Color.FromArgb(142, 68, 173) : Color.FromArgb(120, 110, 105),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = isAdmin ? Cursors.Hand : Cursors.No,
                    Enabled = isAdmin
                };
                btnManageUsers.FlatAppearance.BorderSize = 0;
                btnManageUsers.Click += (s, e) => ShowAdminUserManagementDialog(frmPos);

                Button btnLogout = new Button
                {
                    Text = $"🚪 Đăng xuất ({user.Username})",
                    Location = new Point(1145, 11),
                    Size = new Size(175, 38),
                    BackColor = Color.FromArgb(192, 57, 43),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnLogout.FlatAppearance.BorderSize = 0;

                bool isLoggingOut = false;
                btnLogout.Click += (s, e) =>
                {
                    if (MessageBox.Show("Bạn có chắc muốn đăng xuất khỏi ca trực hiện tại?", "Xác nhận đổi ca",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        isLoggingOut = true;
                        frmPos.Close();
                        txtLoginPass.Clear();
                        this.Show();
                        _renderTimer.Start();
                    }
                };

                frmPos.FormClosed += (s, e) =>
                {
                    if (!isLoggingOut) Application.Exit();
                };

                pnlTopHeader.Controls.Add(btnManageUsers);
                pnlTopHeader.Controls.Add(btnLogout);
            }
        }

        // Cửa sổ dành riêng cho Admin: Cấp tài khoản mới & Xác thực OTP Gmail
        // Cửa sổ dành riêng cho Admin: Cấp tài khoản mới, Xóa nhân viên & Khóa/Mở khóa
        private void ShowAdminUserManagementDialog(Form parentForm)
        {
            string pendingOtp = "";
            string pendingEmail = "";
            DateTime pendingExpiry = DateTime.MinValue;

            using (Form frmAdmin = new Form())
            {
                frmAdmin.Text = "Quản lý Nhân sự: Cấp mới, Xóa & Phân quyền Tài khoản (Quyền Admin)";
                frmAdmin.Size = new Size(980, 560);
                frmAdmin.StartPosition = FormStartPosition.CenterParent;
                frmAdmin.FormBorderStyle = FormBorderStyle.FixedDialog;
                frmAdmin.MaximizeBox = false;
                frmAdmin.BackColor = Color.FromArgb(38, 24, 18);

                Panel pnlCreate = new Panel
                {
                    Location = new Point(15, 15),
                    Size = new Size(410, 490),
                    BackColor = Color.FromArgb(54, 34, 26)
                };

                Label lblHeader = new Label
                {
                    Text = "✨ CẤP TÀI KHOẢN NHÂN VIÊN MỚI",
                    Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(255, 204, 128),
                    Location = new Point(20, 15),
                    AutoSize = true
                };

                Label MakeDarkLabel(string txt, int x, int y) => new Label
                {
                    Text = txt,
                    Location = new Point(x, y),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(245, 225, 210)
                };

                TextBox MakeDarkInput(int x, int y, int w, bool isPass = false) => new TextBox
                {
                    Location = new Point(x, y),
                    Size = new Size(w, 28),
                    Font = new Font("Segoe UI", 10.5f),
                    BackColor = Color.FromArgb(28, 17, 13),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    UseSystemPasswordChar = isPass
                };

                TextBox txtU = MakeDarkInput(20, 78, 365);
                TextBox txtFn = MakeDarkInput(20, 138, 365);
                TextBox txtPass = MakeDarkInput(20, 198, 215, true);

                ComboBox cboRole = new ComboBox
                {
                    Location = new Point(245, 198),
                    Size = new Size(140, 28),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    BackColor = Color.FromArgb(28, 17, 13),
                    ForeColor = Color.FromArgb(255, 204, 128)
                };
                cboRole.Items.AddRange(new object[] { "Staff", "Admin" });
                cboRole.SelectedIndex = 0;

                TextBox txtMail = MakeDarkInput(20, 262, 245);
                TextBox txtVerifyOtp = MakeDarkInput(20, 328, 365);
                txtVerifyOtp.Enabled = false;

                Button btnSendVerifyOtp = new Button
                {
                    Text = "1. Gửi OTP",
                    Location = new Point(275, 260),
                    Size = new Size(110, 30),
                    BackColor = Color.FromArgb(230, 126, 34),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSendVerifyOtp.FlatAppearance.BorderSize = 0;

                Button btnCreateAccount = new Button
                {
                    Text = "✔ 2. XÁC NHẬN OTP & TẠO TÀI KHOẢN",
                    Location = new Point(20, 378),
                    Size = new Size(365, 46),
                    BackColor = Color.FromArgb(39, 174, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Enabled = false
                };
                btnCreateAccount.FlatAppearance.BorderSize = 0;

                // Cột phải: Bảng danh sách nhân sự + Nút Xóa & Khóa tài khoản
                DataGridView dgvUsers = new DataGridView
                {
                    Location = new Point(440, 15),
                    Size = new Size(510, 430),
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = Color.FromArgb(250, 245, 240),
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    RowTemplate = { Height = 32 }
                };

                Button btnDeleteStaff = new Button
                {
                    Text = "🗑 XÓA NHÂN VIÊN ĐANG CHỌN",
                    Location = new Point(440, 457),
                    Size = new Size(250, 48),
                    BackColor = Color.FromArgb(192, 57, 43),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnDeleteStaff.FlatAppearance.BorderSize = 0;

                Button btnToggleActive = new Button
                {
                    Text = "🔒 KHÓA / MỞ KHÓA TÀI KHOẢN",
                    Location = new Point(700, 457),
                    Size = new Size(250, 48),
                    BackColor = Color.FromArgb(41, 128, 185),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnToggleActive.FlatAppearance.BorderSize = 0;

                Action reloadGrid = () => dgvUsers.DataSource = _userDal.GetAllUsers();
                reloadGrid();

                // SỰ KIỆN XÓA NHÂN VIÊN ĐANG CHỌN
                btnDeleteStaff.Click += (s, e) =>
                {
                    if (dgvUsers.CurrentRow == null)
                    {
                        MessageBox.Show("Vui lòng bấm chọn 1 nhân viên trong bảng bên trên để xóa!", "Chưa chọn nhân viên", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    int userId = Convert.ToInt32(dgvUsers.CurrentRow.Cells["Mã NV"].Value);
                    string username = dgvUsers.CurrentRow.Cells["Tài khoản"].Value.ToString();
                    string fullName = dgvUsers.CurrentRow.Cells["Họ và tên"].Value.ToString();

                    // Chặn xóa tài khoản admin gốc hoặc tài khoản đang đăng nhập hiện tại
                    if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                        username.Equals(txtLoginUser.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show($"Không thể xóa tài khoản Quản trị gốc hoặc tài khoản đang trực ca [{username}]!", "Bảo vệ hệ thống", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }

                    DialogResult confirm = MessageBox.Show(
                        $"Bạn có chắc chắn muốn XÓA VĨNH VIỄN nhân viên:\n- Họ tên: {fullName}\n- Tài khoản: {username}\n\nHành động này không thể hoàn tác!",
                        "Xác nhận xóa nhân viên",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (confirm == DialogResult.Yes)
                    {
                        try
                        {
                            if (_userDal.DeleteUser(userId))
                            {
                                MessageBox.Show($"Đã xóa nhân viên [{fullName} - {username}] khỏi hệ thống!", "Đã xóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                reloadGrid();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Lỗi khi xóa nhân viên: " + ex.Message, "Lỗi SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                };

                // SỰ KIỆN KHÓA / MỞ KHÓA TÀI KHOẢN TẠM THỜI
                btnToggleActive.Click += (s, e) =>
                {
                    if (dgvUsers.CurrentRow == null)
                    {
                        MessageBox.Show("Vui lòng chọn 1 tài khoản trong bảng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    int userId = Convert.ToInt32(dgvUsers.CurrentRow.Cells["Mã NV"].Value);
                    string username = dgvUsers.CurrentRow.Cells["Tài khoản"].Value.ToString();
                    bool isActive = Convert.ToBoolean(dgvUsers.CurrentRow.Cells["Đang hoạt động"].Value);

                    if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                        username.Equals(txtLoginUser.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Không thể tự khóa tài khoản Admin gốc hoặc tài khoản đang trực ca!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (_userDal.ToggleUserActiveStatus(userId, isActive))
                    {
                        string statusText = !isActive ? "MỞ KHÓA (Hoạt động trở lại)" : "KHÓA (Ngừng truy cập)";
                        MessageBox.Show($"Đã chuyển trạng thái tài khoản [{username}] sang: {statusText}!", "Cập nhật thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        reloadGrid();
                    }
                };

                // SỰ KIỆN GỬI OTP TẠO MỚI
                btnSendVerifyOtp.Click += (s, e) =>
                {
                    string u = txtU.Text.Trim();
                    string em = txtMail.Text.Trim();
                    string p = txtPass.Text.Trim();

                    if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(em) || string.IsNullOrEmpty(p))
                    {
                        MessageBox.Show("Vui lòng nhập Tên đăng nhập, Mật khẩu và Gmail trước khi gửi OTP!", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!em.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Vui lòng nhập đúng định dạng @gmail.com!", "Sai định dạng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (_userDal.IsUsernameOrEmailExist(u, em))
                    {
                        MessageBox.Show("Tên đăng nhập hoặc địa chỉ Gmail này đã tồn tại!", "Trùng dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    pendingOtp = AuthService.GenerateOTP();
                    pendingEmail = em;
                    pendingExpiry = DateTime.Now.AddMinutes(5);

                    if (AuthService.SendOtpEmail(em, pendingOtp, $"Xác thực liên kết tài khoản mới [{u}]", out string err))
                    {
                        if (!string.IsNullOrEmpty(err))
                            MessageBox.Show(err, "Thông tin OTP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show($"Đã gửi mã OTP gồm 6 chữ số tới Gmail [{em}]!\nHãy nhập mã OTP để hoàn tất tạo tài khoản.", "Đã gửi OTP", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        txtVerifyOtp.Enabled = true;
                        btnCreateAccount.Enabled = true;
                        txtVerifyOtp.Focus();
                    }
                    else
                    {
                        MessageBox.Show("Lỗi khi gửi mail OTP:\n" + err, "Lỗi SMTP Gmail", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                // SỰ KIỆN XÁC NHẬN OTP & LƯU TÀI KHOẢN MỚI
                btnCreateAccount.Click += (s, e) =>
                {
                    if (DateTime.Now > pendingExpiry)
                    {
                        MessageBox.Show("Mã OTP đã hết hạn (quá 5 phút)! Vui lòng bấm Gửi OTP lại.", "Hết hạn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (txtVerifyOtp.Text.Trim() != pendingOtp || txtMail.Text.Trim() != pendingEmail)
                    {
                        MessageBox.Show("Mã OTP không chính xác hoặc Gmail đã bị thay đổi!", "Sai OTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string role = cboRole.SelectedItem.ToString();
                    if (_userDal.CreateAccountByAdmin(txtU.Text, txtPass.Text, txtFn.Text, role, pendingEmail))
                    {
                        MessageBox.Show($"Đã tạo thành công tài khoản [{txtU.Text.Trim()}] với quyền [{role}]!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        txtU.Clear();
                        txtFn.Clear();
                        txtPass.Clear();
                        txtMail.Clear();
                        txtVerifyOtp.Clear();
                        txtVerifyOtp.Enabled = false;
                        btnCreateAccount.Enabled = false;
                        pendingOtp = "";
                        reloadGrid();
                    }
                };

                pnlCreate.Controls.AddRange(new Control[]
                {
            lblHeader,
            MakeDarkLabel("Tên đăng nhập (*):", 20, 55), txtU,
            MakeDarkLabel("Họ và tên nhân viên (*):", 20, 115), txtFn,
            MakeDarkLabel("Mật khẩu khởi tạo (*):", 20, 175), txtPass,
            MakeDarkLabel("Phân quyền:", 245, 175), cboRole,
            MakeDarkLabel("Gmail nhận OTP xác thực (*):", 20, 238), txtMail, btnSendVerifyOtp,
            MakeDarkLabel("Nhập mã OTP 6 số từ Gmail (*):", 20, 305), txtVerifyOtp,
            btnCreateAccount
                });

                frmAdmin.Controls.AddRange(new Control[] { pnlCreate, dgvUsers, btnDeleteStaff, btnToggleActive });
                frmAdmin.ShowDialog(parentForm);
            }
        }

        private void BtnSendForgotOtp_Click(object sender, EventArgs e)
        {
            string email = txtForgotEmail.Text.Trim();
            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Vui lòng nhập địa chỉ Gmail đã liên kết với tài khoản!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (!_userDal.CheckEmailExist(email))
                {
                    MessageBox.Show("Gmail này chưa được liên kết với tài khoản nào trong hệ thống!", "Không tìm thấy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _forgotOtp = AuthService.GenerateOTP();
                _forgotEmail = email;
                _forgotOtpExpiry = DateTime.Now.AddMinutes(5);

                if (AuthService.SendOtpEmail(email, _forgotOtp, "Khôi phục mật khẩu", out string err))
                {
                    if (!string.IsNullOrEmpty(err))
                        MessageBox.Show(err, "Thông tin OTP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show("Đã gửi mã OTP gồm 6 chữ số tới Gmail của bạn! Vui lòng kiểm tra hộp thư.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    txtOtpCode.Enabled = true;
                    txtNewPass.Enabled = true;
                    btnConfirmReset3D.Enabled = true;
                    btnConfirmReset3D.Invalidate();
                    txtOtpCode.Focus();
                }
                else
                {
                    MessageBox.Show("Không thể gửi email:\n" + err, "Lỗi SMTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnConfirmReset_Click(object sender, EventArgs e)
        {
            string inputOtp = txtOtpCode.Text.Trim();
            string newPass = txtNewPass.Text.Trim();

            if (DateTime.Now > _forgotOtpExpiry)
            {
                MessageBox.Show("Mã OTP đã hết hạn (quá 5 phút)! Vui lòng bấm gửi lại.", "Hết hạn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (inputOtp != _forgotOtp)
            {
                MessageBox.Show("Mã OTP không chính xác! Vui lòng kiểm tra lại.", "Sai OTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(newPass) || newPass.Length < 4)
            {
                MessageBox.Show("Mật khẩu mới phải từ 4 ký tự trở lên!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_userDal.UpdatePasswordByEmail(_forgotEmail, newPass))
            {
                MessageBox.Show("Đổi mật khẩu thành công! Mời bạn đăng nhập lại.", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _forgotOtp = "";
                txtOtpCode.Clear();
                txtNewPass.Clear();
                ShowCard(pnlLoginCard);
            }
        }

        private void ShowCard(Control target)
        {
            pnlLoginCard.Visible = (target == pnlLoginCard);
            pnlForgotCard.Visible = (target == pnlForgotCard);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // FrmAuth
            // 
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.Name = "FrmAuth";
            this.Load += new System.EventHandler(this.FrmAuth_Load);
            this.ResumeLayout(false);

        }

        private void FrmAuth_Load(object sender, EventArgs e)
        {

        }
    }
}