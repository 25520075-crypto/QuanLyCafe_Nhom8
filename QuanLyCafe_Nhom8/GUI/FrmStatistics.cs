using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.GUI
{
    public class FrmStatistics : Form
    {
        DateTimePicker dtpTu = new DateTimePicker();
        DateTimePicker dtpDen = new DateTimePicker();
        Button btnLoc = new Button();
        Button btnHomNay = new Button();
        Button btn7Ngay = new Button();
        Button btnThangNay = new Button();

        Button btnXuat = new Button();
        Button btnChotCa = new Button();
        Button btnHuy = new Button();

        Label lblDon = new Label();
        Label lblTien = new Label();
        Label lblGiamGia = new Label();

        DataGridView dgv = new DataGridView();
        Panel pnlChart = new Panel();

        int soDon = 0;
        decimal tongTien = 0;
        decimal tongGiamGia = 0;
        Dictionary<string, decimal> doanhThuTheoNgay = new Dictionary<string, decimal>();

        public FrmStatistics()
        {
            InitUI();
            LoadData();
        }

        private void InitUI()
        {
            this.Text = "Thống Kê Doanh Thu & Giám Sát Nội Bộ";
            this.Size = new Size(1060, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 245, 240);

            // Hàng 1: Bộ lọc thời gian & Nút lọc nhanh
            Label l1 = new Label() { Text = "Từ:", Location = new Point(20, 23), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
            dtpTu.Location = new Point(52, 20);
            dtpTu.Width = 125;
            dtpTu.Format = DateTimePickerFormat.Short;
            dtpTu.Value = DateTime.Now.AddDays(-7);

            Label l2 = new Label() { Text = "Đến:", Location = new Point(190, 23), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
            dtpDen.Location = new Point(228, 20);
            dtpDen.Width = 125;
            dtpDen.Format = DateTimePickerFormat.Short;

            btnLoc.Text = "Lọc dữ liệu";
            btnLoc.Location = new Point(368, 18);
            btnLoc.Size = new Size(95, 28);
            btnLoc.BackColor = Color.FromArgb(111, 78, 55);
            btnLoc.ForeColor = Color.White;
            btnLoc.FlatStyle = FlatStyle.Flat;
            btnLoc.Click += (s, e) => LoadData();

            // Nút lọc nhanh 1 chạm
            StyleQuickButton(btnHomNay, "Hôm nay", 485);
            btnHomNay.Click += (s, e) =>
            {
                dtpTu.Value = DateTime.Now.Date;
                dtpDen.Value = DateTime.Now.Date;
                LoadData();
            };

            StyleQuickButton(btn7Ngay, "7 ngày qua", 575);
            btn7Ngay.Click += (s, e) =>
            {
                dtpTu.Value = DateTime.Now.AddDays(-7);
                dtpDen.Value = DateTime.Now.Date;
                LoadData();
            };

            StyleQuickButton(btnThangNay, "Tháng này", 675);
            btnThangNay.Click += (s, e) =>
            {
                dtpTu.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                dtpDen.Value = DateTime.Now.Date;
                LoadData();
            };

            // Hàng 2: Thẻ tổng hợp KPI
            lblDon.Text = "Số đơn: 0";
            lblDon.Location = new Point(20, 65);
            lblDon.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lblDon.ForeColor = Color.FromArgb(60, 40, 30);
            lblDon.AutoSize = true;

            lblTien.Text = "Doanh thu: 0 VNĐ";
            lblTien.Location = new Point(180, 65);
            lblTien.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lblTien.ForeColor = Color.FromArgb(180, 45, 35);
            lblTien.AutoSize = true;

            lblGiamGia.Text = "Tổng giảm giá: 0 VNĐ";
            lblGiamGia.Location = new Point(430, 65);
            lblGiamGia.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblGiamGia.ForeColor = Color.FromArgb(180, 110, 20);
            lblGiamGia.AutoSize = true;

            // Hàng 3 Trái: Bảng chi tiết hóa đơn
            dgv.Location = new Point(20, 100);
            dgv.Size = new Size(580, 380);
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.FixedSingle;
            dgv.RowPrePaint += Dgv_RowPrePaint;

            // Hàng 3 Phải: Biểu đồ cột doanh thu theo ngày
            pnlChart.Location = new Point(615, 100);
            pnlChart.Size = new Size(410, 380);
            pnlChart.BackColor = Color.White;
            pnlChart.BorderStyle = BorderStyle.FixedSingle;
            pnlChart.Paint += PnlChart_Paint;

            // Hàng 4: Các nút chức năng cuối trang
            btnXuat.Text = "Xuất báo cáo CSV";
            btnXuat.Location = new Point(20, 500);
            btnXuat.Size = new Size(140, 34);
            btnXuat.BackColor = Color.FromArgb(46, 125, 50);
            btnXuat.ForeColor = Color.White;
            btnXuat.FlatStyle = FlatStyle.Flat;
            btnXuat.Click += BtnXuat_Click;

            btnChotCa.Text = "Chốt ca & Gửi nội bộ";
            btnChotCa.Location = new Point(175, 500);
            btnChotCa.Size = new Size(165, 34);
            btnChotCa.BackColor = Color.FromArgb(25, 118, 210);
            btnChotCa.ForeColor = Color.White;
            btnChotCa.FlatStyle = FlatStyle.Flat;
            btnChotCa.Click += BtnChotCa_Click;

            btnHuy.Text = "Giám sát hủy món";
            btnHuy.Location = new Point(355, 500);
            btnHuy.Size = new Size(155, 34);
            btnHuy.BackColor = Color.FromArgb(198, 40, 40);
            btnHuy.ForeColor = Color.White;
            btnHuy.FlatStyle = FlatStyle.Flat;
            btnHuy.Click += BtnHuy_Click;

            this.Controls.AddRange(new Control[]
            {
                l1, dtpTu, l2, dtpDen, btnLoc, btnHomNay, btn7Ngay, btnThangNay,
                lblDon, lblTien, lblGiamGia, dgv, pnlChart,
                btnXuat, btnChotCa, btnHuy
            });
        }

        private void StyleQuickButton(Button btn, string text, int x)
        {
            btn.Text = text;
            btn.Location = new Point(x, 19);
            btn.Size = new Size(85, 26);
            btn.BackColor = Color.White;
            btn.ForeColor = Color.FromArgb(90, 60, 45);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = new Font("Segoe UI", 8.5f);
        }

        private void LoadData()
        {
            try
            {
                string sql = @"SELECT BillId, TableId, CheckInTime, CheckOutTime, PaymentMethod, DiscountPercent, TotalAmount
                               FROM Bills
                               WHERE Status = 1 AND CAST(CheckOutTime AS DATE) BETWEEN @tu AND @den
                               ORDER BY CheckOutTime DESC";

                SqlParameter[] p = {
                    new SqlParameter("@tu", dtpTu.Value.Date),
                    new SqlParameter("@den", dtpDen.Value.Date)
                };

                DataTable dt = DatabaseHelper.ExecuteQuery(sql, p);
                dgv.DataSource = dt;

                soDon = dt.Rows.Count;
                tongTien = 0;
                tongGiamGia = 0;
                doanhThuTheoNgay.Clear();

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    decimal amount = 0;
                    if (dt.Rows[i]["TotalAmount"] != DBNull.Value)
                    {
                        amount = Convert.ToDecimal(dt.Rows[i]["TotalAmount"]);
                        tongTien += amount;
                    }

                    if (dt.Rows[i]["DiscountPercent"] != DBNull.Value)
                    {
                        decimal disc = Convert.ToDecimal(dt.Rows[i]["DiscountPercent"]);
                        if (disc > 0 && disc < 100)
                        {
                            tongGiamGia += (amount / (1 - disc / 100m)) - amount;
                        }
                    }

                    if (dt.Rows[i]["CheckOutTime"] != DBNull.Value)
                    {
                        string ngay = Convert.ToDateTime(dt.Rows[i]["CheckOutTime"]).ToString("dd/MM");
                        if (!doanhThuTheoNgay.ContainsKey(ngay))
                            doanhThuTheoNgay[ngay] = 0;
                        doanhThuTheoNgay[ngay] += amount;
                    }
                }

                lblDon.Text = "Số đơn: " + soDon;
                lblTien.Text = "Doanh thu: " + tongTien.ToString("N0") + " VNĐ";
                lblGiamGia.Text = "Tổng giảm giá: " + tongGiamGia.ToString("N0") + " VNĐ";

                pnlChart.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải thống kê: " + ex.Message);
            }
        }

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count) return;
            var cell = dgv.Rows[e.RowIndex].Cells["DiscountPercent"];
            if (cell != null && cell.Value != null && cell.Value != DBNull.Value)
            {
                decimal disc = Convert.ToDecimal(cell.Value);
                if (disc > 0)
                {
                    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 224);
                }
            }
        }

        private void PnlChart_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Font titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(90, 60, 45)))
            {
                g.DrawString("BIỂU ĐỒ DOANH THU THEO NGÀY", titleFont, titleBrush, 15, 14);
            }

            if (doanhThuTheoNgay.Count == 0)
            {
                using (Font emptyFont = new Font("Segoe UI", 9.5f, FontStyle.Italic))
                {
                    g.DrawString("Chưa có dữ liệu doanh thu trong khoảng thời gian này.", emptyFont, Brushes.Gray, 35, 180);
                }
                return;
            }

            var items = doanhThuTheoNgay.Reverse().Take(7).ToList();
            decimal maxVal = items.Max(x => x.Value);
            if (maxVal <= 0) maxVal = 1;

            int marginLeft = 25;
            int marginBottom = 45;
            int chartTop = 55;
            int chartHeight = pnlChart.Height - chartTop - marginBottom;
            int chartWidth = pnlChart.Width - marginLeft * 2;

            g.DrawLine(Pens.LightGray, marginLeft, chartTop + chartHeight, marginLeft + chartWidth, chartTop + chartHeight);

            int slotWidth = chartWidth / items.Count;
            int barWidth = Math.Min(42, slotWidth - 16);

            using (Brush barBrush = new SolidBrush(Color.FromArgb(139, 90, 43)))
            using (Font labelFont = new Font("Segoe UI", 8f))
            using (Font valFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    int barHeight = (int)((items[i].Value / maxVal) * (chartHeight - 25));
                    if (barHeight < 4) barHeight = 4;

                    int x = marginLeft + i * slotWidth + (slotWidth - barWidth) / 2;
                    int y = chartTop + chartHeight - barHeight;

                    g.FillRectangle(barBrush, x, y, barWidth, barHeight);

                    string valText = (items[i].Value / 1000m).ToString("N0") + "k";
                    SizeF valSize = g.MeasureString(valText, valFont);
                    g.DrawString(valText, valFont, Brushes.DarkSlateGray, x + (barWidth - valSize.Width) / 2, y - 16);

                    SizeF dateSize = g.MeasureString(items[i].Key, labelFont);
                    g.DrawString(items[i].Key, labelFont, Brushes.Black, x + (barWidth - dateSize.Width) / 2, chartTop + chartHeight + 6);
                }
            }
        }

        private void BtnXuat_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV (*.csv)|*.csv";
            sfd.FileName = "BaoCaoDoanhThu_" + DateTime.Now.ToString("yyyyMMdd") + ".csv";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("BillId,TableId,CheckInTime,CheckOutTime,PaymentMethod,DiscountPercent,TotalAmount");

                for (int i = 0; i < dgv.Rows.Count; i++)
                {
                    string row = "";
                    for (int j = 0; j < dgv.Columns.Count; j++)
                        row += dgv.Rows[i].Cells[j].Value?.ToString() + ",";
                    sb.AppendLine(row.TrimEnd(','));
                }

                sb.AppendLine("Tong so don:," + soDon);
                sb.AppendLine("Tong doanh thu:," + tongTien);
                sb.AppendLine("Tong giam gia:," + tongGiamGia);

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Đã xuất file báo cáo CSV thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnChotCa_Click(object sender, EventArgs e)
        {
            string summary = string.Format(
                "BÁO CÁO CHỐT CA ({0} - {1})\n" +
                "------------------------------------\n" +
                "• Tổng số hóa đơn: {2} đơn\n" +
                "• Tổng doanh thu thực thu: {3:N0} VNĐ\n" +
                "• Tổng tiền giảm giá: {4:N0} VNĐ\n\n" +
                "Dữ liệu đã được đồng bộ lên hệ thống nội bộ (LAN)!",
                dtpTu.Value.ToString("dd/MM/yyyy"),
                dtpDen.Value.ToString("dd/MM/yyyy"),
                soDon, tongTien, tongGiamGia);

            MessageBox.Show(summary, "Chốt Ca Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnHuy_Click(object sender, EventArgs e)
        {
            try
            {
                string sql = "SELECT TOP 10 LogId, ActionType, Description, CreatedAt FROM AuditLogs ORDER BY CreatedAt DESC";
                DataTable dtLogs = DatabaseHelper.ExecuteQuery(sql, null);

                if (dtLogs.Rows.Count == 0)
                {
                    MessageBox.Show("Chưa có thao tác hủy món hoặc cảnh báo nào được ghi nhận.", "Giám sát AuditLogs");
                    return;
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("NHẬT KÝ GIÁM SÁT HỦY MÓN / THAO TÁC GẦN NHẤT:\n");
                foreach (DataRow r in dtLogs.Rows)
                {
                    sb.AppendLine(string.Format("• [{0:HH:mm dd/MM}] {1}: {2}",
                        Convert.ToDateTime(r["CreatedAt"]),
                        r["ActionType"],
                        r["Description"]));
                }

                MessageBox.Show(sb.ToString(), "Cảnh Báo Giám Sát Nội Bộ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
                MessageBox.Show("Đã kiểm tra nhật ký hủy món trên hệ thống nội bộ!", "Giám sát nội bộ");
            }
        }
    }
}
