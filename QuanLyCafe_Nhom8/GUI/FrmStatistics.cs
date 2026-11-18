using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Protocol;
using QuanLyCafe_Nhom8.Network;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.GUI
{
    public class FrmStatistics : Form
    {
        private DateTimePicker dtpTu = new DateTimePicker();
        private DateTimePicker dtpDen = new DateTimePicker();
        private Label lblTongDon = new Label();
        private Label lblDoanhThu = new Label();
        private DataGridView dgvHoaDon = new DataGridView();

        private TextBox txtKhungChat = new TextBox();
        private TextBox txtNhapTin = new TextBox();

        private int _soDon = 0;
        private decimal _tongTien = 0;

        public FrmStatistics()
        {
            TaoGiaoDien();
            KetNoiChatNoiBo();
            LoadThongKe();
        }

        private void TaoGiaoDien()
        {
            this.Text = "Thống kê Doanh thu & Chat nội bộ - Nhóm 8";
            this.Size = new Size(1050, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(245, 240, 235);
            this.Font = new Font("Segoe UI", 9.5f);

            // --- PHẦN 1: THỐNG KÊ BÊN TRÁI ---
            Label lblTitle = new Label
            {
                Text = "THỐNG KÊ DOANH THU QUÁN CAFE",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(20, 15),
                AutoSize = true
            };

            Label l1 = new Label { Text = "Từ ngày:", Location = new Point(20, 55), AutoSize = true };
            dtpTu.Location = new Point(85, 52);
            dtpTu.Width = 120;
            dtpTu.Format = DateTimePickerFormat.Short;
            dtpTu.Value = DateTime.Now.AddDays(-7);

            Label l2 = new Label { Text = "Đến ngày:", Location = new Point(220, 55), AutoSize = true };
            dtpDen.Location = new Point(290, 52);
            dtpDen.Width = 120;
            dtpDen.Format = DateTimePickerFormat.Short;

            Button btnLoc = new Button
            {
                Text = "Lọc dữ liệu",
                Location = new Point(425, 50),
                Size = new Size(100, 28),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLoc.Click += (s, e) => LoadThongKe();

            lblTongDon.Text = "Tổng số đơn: 0";
            lblTongDon.Location = new Point(20, 90);
            lblTongDon.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblTongDon.AutoSize = true;

            lblDoanhThu.Text = "Tổng doanh thu: 0 VNĐ";
            lblDoanhThu.Location = new Point(220, 90);
            lblDoanhThu.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblDoanhThu.ForeColor = Color.Red;
            lblDoanhThu.AutoSize = true;

            dgvHoaDon.Location = new Point(20, 120);
            dgvHoaDon.Size = new Size(640, 330);
            dgvHoaDon.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvHoaDon.ReadOnly = true;
            dgvHoaDon.AllowUserToAddRows = false;
            dgvHoaDon.BackgroundColor = Color.White;

            Button btnXuatExcel = new Button
            {
                Text = "Xuất file CSV",
                Location = new Point(20, 465),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnXuatExcel.Click += BtnXuatExcel_Click;

            Button btnBaoCaoChat = new Button
            {
                Text = "Gửi báo cáo lên Chat",
                Location = new Point(175, 465),
                Size = new Size(170, 36),
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBaoCaoChat.Click += async (s, e) =>
            {
                string tin = $"[BÁO CÁO] Từ {dtpTu.Value:dd/MM} đến {dtpDen.Value:dd/MM}: {_soDon} đơn - Doanh thu: {_tongTien:N0} VNĐ";
                await InternalChatService.Instance.SendMessageAsync(tin, true);
            };

            // --- PHẦN 2: CHAT NỘI BỘ BÊN PHẢI ---
            Label lblChat = new Label
            {
                Text = "CHAT NỘI BỘ (MẠNG LAN)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(685, 18),
                AutoSize = true
            };

            txtKhungChat.Location = new Point(685, 50);
            txtKhungChat.Size = new Size(330, 400);
            txtKhungChat.Multiline = true;
            txtKhungChat.ReadOnly = true;
            txtKhungChat.ScrollBars = ScrollBars.Vertical;
            txtKhungChat.BackColor = Color.White;

            txtNhapTin.Location = new Point(685, 465);
            txtNhapTin.Size = new Size(245, 36);

            Button btnGuiChat = new Button
            {
                Text = "Gửi",
                Location = new Point(938, 465),
                Size = new Size(77, 36),
                BackColor = Color.FromArgb(62, 39, 35),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnGuiChat.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtNhapTin.Text)) return;
                await InternalChatService.Instance.SendMessageAsync(txtNhapTin.Text);
                txtNhapTin.Clear();
            };

            this.Controls.AddRange(new Control[]
            {
                lblTitle, l1, dtpTu, l2, dtpDen, btnLoc, lblTongDon, lblDoanhThu,
                dgvHoaDon, btnXuatExcel, btnBaoCaoChat, lblChat, txtKhungChat, txtNhapTin, btnGuiChat
            });
        }

        private async void LoadThongKe()
        {
            try
            {
                var dsHoaDon = await CafeNetworkClient.Instance.SendRequestAsync<List<BillStatDTO>>(
                    MsgType.STATS_REVENUE_REQ,
                    new { FromDate = dtpTu.Value.Date, ToDate = dtpDen.Value.Date });

                dgvHoaDon.DataSource = dsHoaDon;

                _soDon = dsHoaDon.Count;
                _tongTien = 0;
                foreach (var hd in dsHoaDon)
                {
                    _tongTien += hd.TotalAmount;
                }

                lblTongDon.Text = "Tổng số đơn: " + _soDon;
                lblDoanhThu.Text = $"Tổng doanh thu: {_tongTien:N0} VNĐ";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải thống kê: " + ex.Message);
            }
        }

        private void BtnXuatExcel_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "ThongKeDoanhThu.csv" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("MaHD,MaBan,GioVao,GioRa,HinhThuc,GiamGia,TongTien");

                foreach (DataGridViewRow row in dgvHoaDon.Rows)
                {
                    sb.AppendLine($"{row.Cells[0].Value},{row.Cells[1].Value},{row.Cells[2].Value},{row.Cells[3].Value},{row.Cells[4].Value},{row.Cells[5].Value},{row.Cells[6].Value}");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Xuất file thành công!");
            }
        }

        private void KetNoiChatNoiBo()
        {
            InternalChatService.Instance.Start();
            foreach (var msg in InternalChatService.Instance.GetHistory())
            {
                HienThiTinNhan(msg);
            }
            InternalChatService.Instance.OnMessageReceived += NhanTinMoi;
        }

        private void NhanTinMoi(InternalChatItem msg)
        {
            if (this.IsDisposed) return;
            this.Invoke(new Action(() => HienThiTinNhan(msg)));
        }

        private void HienThiTinNhan(InternalChatItem msg)
        {
            txtKhungChat.AppendText($"[{msg.SentTime}] {msg.SenderName}: {msg.Content}\r\n");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            InternalChatService.Instance.OnMessageReceived -= NhanTinMoi;
            base.OnFormClosed(e);
        }
    }
}
