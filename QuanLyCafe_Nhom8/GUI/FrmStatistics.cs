using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Protocol;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.GUI
{
    public class FrmStatistics : Form
    {
        DateTimePicker dtpTu = new DateTimePicker { Location = new Point(52, 20), Width = 125, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(-7) };
        DateTimePicker dtpDen = new DateTimePicker { Location = new Point(228, 20), Width = 125, Format = DateTimePickerFormat.Short };
        Button btnLoc = new Button { Text = "Lọc dữ liệu", Location = new Point(368, 18), Size = new Size(95, 28), BackColor = Color.FromArgb(111, 78, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        Button btnHomNay = new Button { Text = "Hôm nay", Location = new Point(485, 19), Size = new Size(85, 26), BackColor = Color.White, FlatStyle = FlatStyle.Flat };
        Button btn7Ngay = new Button { Text = "7 ngày qua", Location = new Point(575, 19), Size = new Size(85, 26), BackColor = Color.White, FlatStyle = FlatStyle.Flat };
        Button btnThangNay = new Button { Text = "Tháng này", Location = new Point(675, 19), Size = new Size(85, 26), BackColor = Color.White, FlatStyle = FlatStyle.Flat };

        Label lblDon = new Label { Text = "Số đơn: 0", Location = new Point(20, 65), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        Label lblTien = new Label { Text = "Doanh thu: 0 VNĐ", Location = new Point(180, 65), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.Firebrick };
        Label lblGiamGia = new Label { Text = "Tổng giảm giá: 0 VNĐ", Location = new Point(430, 65), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.DarkGoldenrod };

        DataGridView dgv = new DataGridView { Location = new Point(20, 100), Size = new Size(580, 380), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, BackgroundColor = Color.White };
        Panel pnlChart = new Panel { Location = new Point(615, 100), Size = new Size(410, 380), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        Button btnXuat = new Button { Text = "Xuất báo cáo CSV", Location = new Point(20, 500), Size = new Size(140, 34), BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        Button btnChotCa = new Button { Text = "Chốt ca & Gửi nội bộ", Location = new Point(175, 500), Size = new Size(165, 34), BackColor = Color.RoyalBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        Button btnHuy = new Button { Text = "Giám sát hủy món", Location = new Point(355, 500), Size = new Size(155, 34), BackColor = Color.Crimson, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

        int soDon = 0;
        decimal tongTien = 0, tongGiamGia = 0;
        Dictionary<string, decimal> doanhThuTheoNgay = new Dictionary<string, decimal>();

        public FrmStatistics()
        {
            this.Text = "Thống Kê Doanh Thu";
            this.Size = new Size(1060, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 245, 240);

            Label l1 = new Label { Text = "Từ:", Location = new Point(20, 23), AutoSize = true };
            Label l2 = new Label { Text = "Đến:", Location = new Point(190, 23), AutoSize = true };

            this.Controls.AddRange(new Control[] { l1, dtpTu, l2, dtpDen, btnLoc, btnHomNay, btn7Ngay, btnThangNay, lblDon, lblTien, lblGiamGia, dgv, pnlChart, btnXuat, btnChotCa, btnHuy });

            btnLoc.Click += async (s, e) => await LoadDataAsync();
            btnHomNay.Click += async (s, e) => { dtpTu.Value = DateTime.Now.Date; dtpDen.Value = DateTime.Now.Date; await LoadDataAsync(); };
            btn7Ngay.Click += async (s, e) => { dtpTu.Value = DateTime.Now.AddDays(-7); dtpDen.Value = DateTime.Now.Date; await LoadDataAsync(); };
            btnThangNay.Click += async (s, e) => { dtpTu.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1); dtpDen.Value = DateTime.Now.Date; await LoadDataAsync(); };

            pnlChart.Paint += PnlChart_Paint;
            btnXuat.Click += BtnXuat_Click;
            btnChotCa.Click += BtnChotCa_Click;
            btnHuy.Click += BtnHuy_Click;

            this.Load += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var res = await NetworkClient.Instance.SendRequestAsync(MsgType.STATS_REVENUE_REQ, new
                {
                    FromDate = dtpTu.Value.Date,
                    ToDate = dtpDen.Value.Date
                });

                if (!res.Success) { MessageBox.Show(res.Message); return; }

                DataTable dt = new DataTable();
                dt.Columns.Add("BillId", typeof(int));
                dt.Columns.Add("TableId", typeof(int));
                dt.Columns.Add("CheckInTime", typeof(DateTime));
                dt.Columns.Add("CheckOutTime", typeof(DateTime));
                dt.Columns.Add("PaymentMethod", typeof(string));
                dt.Columns.Add("DiscountPercent", typeof(decimal));
                dt.Columns.Add("TotalAmount", typeof(decimal));

                JToken token = JToken.Parse(string.IsNullOrWhiteSpace(res.PayloadJson) ? "[]" : res.PayloadJson);
                JArray arr = token as JArray ?? (token["Bills"] as JArray) ?? new JArray();

                soDon = 0; tongTien = 0; tongGiamGia = 0;
                doanhThuTheoNgay.Clear();

                foreach (var item in arr)
                {
                    decimal amount = item["TotalAmount"]?.Value<decimal>() ?? 0m;
                    decimal disc = item["DiscountPercent"]?.Value<decimal>() ?? 0m;
                    DateTime checkOut = item["CheckOutTime"]?.Value<DateTime>() ?? DateTime.Now;

                    dt.Rows.Add(
                        item["BillId"]?.Value<int>() ?? 0,
                        item["TableId"]?.Value<int>() ?? 0,
                        item["CheckInTime"]?.Value<DateTime>() ?? DateTime.Now,
                        checkOut,
                        item["PaymentMethod"]?.ToString() ?? "Tiền mặt",
                        disc, amount);

                    soDon++;
                    tongTien += amount;
                    if (disc > 0 && disc < 100) tongGiamGia += (amount / (1 - disc / 100m)) - amount;

                    string ngay = checkOut.ToString("dd/MM");
                    if (!doanhThuTheoNgay.ContainsKey(ngay)) doanhThuTheoNgay[ngay] = 0;
                    doanhThuTheoNgay[ngay] += amount;
                }

                dgv.DataSource = dt;
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

        private void PnlChart_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawString("BIỂU ĐỒ DOANH THU THEO NGÀY", new Font("Segoe UI", 10f, FontStyle.Bold), Brushes.SaddleBrown, 15, 14);
            if (doanhThuTheoNgay.Count == 0) return;

            var items = doanhThuTheoNgay.Reverse().Take(7).ToList();
            decimal maxVal = Math.Max(1, items.Max(x => x.Value));
            int slotW = (pnlChart.Width - 50) / items.Count;
            int barW = Math.Min(40, slotW - 15);

            for (int i = 0; i < items.Count; i++)
            {
                int barH = Math.Max(5, (int)((items[i].Value / maxVal) * 260));
                int x = 25 + i * slotW + (slotW - barW) / 2;
                int y = 330 - barH;
                g.FillRectangle(Brushes.Sienna, x, y, barW, barH);
                g.DrawString((items[i].Value / 1000m).ToString("N0") + "k", new Font("Segoe UI", 7.5f), Brushes.Black, x, y - 15);
                g.DrawString(items[i].Key, new Font("Segoe UI", 8f), Brushes.Black, x, 336);
            }
        }

        private void BtnXuat_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "BaoCao_" + DateTime.Now.ToString("yyyyMMdd") + ".csv" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("BillId,TableId,CheckInTime,CheckOutTime,PaymentMethod,DiscountPercent,TotalAmount");
            foreach (DataGridViewRow r in dgv.Rows)
                sb.AppendLine(string.Join(",", r.Cells.Cast<DataGridViewCell>().Select(c => c.Value)));

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Xuất CSV thành công!");
        }

        private async void BtnChotCa_Click(object sender, EventArgs e)
        {
            await NetworkClient.Instance.SendChatAsync($"[CHỐT CA] Đơn: {soDon} | Doanh thu: {tongTien:N0} VNĐ | Giảm giá: {tongGiamGia:N0} VNĐ");
            MessageBox.Show("Đã gửi báo cáo chốt ca lên kênh nội bộ!");
        }

        private void BtnHuy_Click(object sender, EventArgs e)
        {
            List<string> logs;
            lock (NetworkClient.Instance.ChatHistory)
                logs = NetworkClient.Instance.ChatHistory.Where(m => m.Contains("CẢNH BÁO HỦY MÓN")).Take(10).ToList();

            MessageBox.Show(logs.Count == 0 ? "Chưa có nhật ký hủy món nào." : string.Join("\n", logs), "Giám sát hủy món");
        }
    }
}
