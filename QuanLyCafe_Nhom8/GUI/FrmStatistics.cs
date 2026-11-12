using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.GUI
{
    public class FrmStatistics : Form
    {
        DateTimePicker dtpTu = new DateTimePicker();
        DateTimePicker dtpDen = new DateTimePicker();
        Button btnLoc = new Button();
        Button btnXuat = new Button();
        Button btnTele = new Button();
        Button btnHuy = new Button();
        Label lblDon = new Label();
        Label lblTien = new Label();
        DataGridView dgv = new DataGridView();

        TelegramBotService bot = new TelegramBotService();
        int soDon = 0;
        decimal tongTien = 0;

        public FrmStatistics()
        {
            InitUI();
            LoadData();
        }

        private void InitUI()
        {
            this.Text = "Thong ke";
            this.Size = new Size(850, 550);
            this.StartPosition = FormStartPosition.CenterScreen;

            Label l1 = new Label() { Text = "Tu:", Location = new Point(20, 23), AutoSize = true };
            dtpTu.Location = new Point(55, 20);
            dtpTu.Width = 130;
            dtpTu.Format = DateTimePickerFormat.Short;
            dtpTu.Value = DateTime.Now.AddDays(-7);

            Label l2 = new Label() { Text = "Den:", Location = new Point(205, 23), AutoSize = true };
            dtpDen.Location = new Point(245, 20);
            dtpDen.Width = 130;
            dtpDen.Format = DateTimePickerFormat.Short;

            btnLoc.Text = "Loc";
            btnLoc.Location = new Point(395, 18);
            btnLoc.Size = new Size(80, 27);
            btnLoc.Click += (s, e) => LoadData();

            lblDon.Text = "So don: 0";
            lblDon.Location = new Point(20, 65);
            lblDon.Font = new Font("Arial", 10, FontStyle.Bold);
            lblDon.AutoSize = true;

            lblTien.Text = "Doanh thu: 0 VND";
            lblTien.Location = new Point(180, 65);
            lblTien.Font = new Font("Arial", 10, FontStyle.Bold);
            lblTien.ForeColor = Color.Red;
            lblTien.AutoSize = true;

            dgv.Location = new Point(20, 100);
            dgv.Size = new Size(790, 340);
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;

            btnXuat.Text = "Xuat CSV";
            btnXuat.Location = new Point(20, 460);
            btnXuat.Size = new Size(120, 32);
            btnXuat.Click += BtnXuat_Click;

            btnTele.Text = "Gui Telegram";
            btnTele.Location = new Point(160, 460);
            btnTele.Size = new Size(130, 32);
            btnTele.Click += BtnTele_Click;

            btnHuy.Text = "Bao huy mon";
            btnHuy.Location = new Point(310, 460);
            btnHuy.Size = new Size(130, 32);
            btnHuy.Click += BtnHuy_Click;
            this.Controls.AddRange(new Control[] { l1, dtpTu, l2, dtpDen, btnLoc, lblDon, lblTien, dgv, btnXuat, btnTele, btnHuy });
        }

        private void LoadData()
        {
            try
            {
                string sql = @"SELECT BillId, TableId, CheckInTime, CheckOutTime, PaymentMethod, DiscountPercent, TotalAmount 
                               FROM Bills 
                               WHERE Status = 1 AND CAST(CheckOutTime AS DATE) BETWEEN @tu AND @den";

                SqlParameter[] p = {
                    new SqlParameter("@tu", dtpTu.Value.Date),
                    new SqlParameter("@den", dtpDen.Value.Date)
                };

                DataTable dt = DatabaseHelper.ExecuteQuery(sql, p);
                dgv.DataSource = dt;

                soDon = dt.Rows.Count;
                tongTien = 0;

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (dt.Rows[i]["TotalAmount"] != DBNull.Value)
                        tongTien += Convert.ToDecimal(dt.Rows[i]["TotalAmount"]);
                }

                lblDon.Text = "So don: " + soDon;
                lblTien.Text = "Doanh thu: " + tongTien.ToString("N0") + " VND";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Loi: " + ex.Message);
            }
        }

        private void BtnXuat_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV (*.csv)|*.csv";
            sfd.FileName = "BaoCao.csv";

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

                sb.AppendLine("So don:," + soDon);
                sb.AppendLine("Doanh thu:," + tongTien);

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Xuat xong!");
            }
        }

        private void BtnTele_Click(object sender, EventArgs e)
        {
            bool ok = bot.GuiDoanhThu(dtpTu.Value, dtpDen.Value, soDon, tongTien);
            MessageBox.Show(ok ? "Da gui!" : "Gui loi!");
        }

        private void BtnHuy_Click(object sender, EventArgs e)
        {
            MessageBox.Show(bot.BaoHuyMon());
        }
    }
}
