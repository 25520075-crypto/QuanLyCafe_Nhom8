using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.GUI
{
    public partial class FrmKDS : Form
    {
        private readonly PosServiceDAL _posDal = new PosServiceDAL();
        private DataGridView dgvPending;
        private Button btnCompleteItem;
        private Button btnRefresh;
        private Label lblCount;
        private Timer _refreshTimer;

        public FrmKDS()
        {
            InitializeComponent();
            BuildKdsInterface();
            LoadPendingOrders();
            StartAutoRefresh();
        }

        private void BuildKdsInterface()
        {
            this.Text = "Màn hình Quầy Pha Chế Real-time (KDS) - Nhóm 8";
            this.Size = new Size(920, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 245, 241);

            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(62, 39, 35)
            };

            Label lblTitle = new Label
            {
                Text = "QUẦY PHA CHẾ REAL-TIME (KITCHEN DISPLAY SYSTEM)",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 236, 179),
                Location = new Point(20, 18),
                AutoSize = true
            };

            lblCount = new Label
            {
                Text = "Đang chờ: 0 món",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(730, 22),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblCount);

            dgvPending = new DataGridView
            {
                Location = new Point(20, 85),
                Size = new Size(865, 395),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowTemplate = { Height = 38 },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 10)
            };
            dgvPending.ColumnHeadersHeight = 40;

            btnCompleteItem = new Button
            {
                Text = "HOÀN THÀNH & GIAO MÓN ĐANG CHỌN",
                Location = new Point(20, 495),
                Size = new Size(650, 48),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCompleteItem.FlatAppearance.BorderSize = 0;
            btnCompleteItem.Click += btnCompleteItem_Click;

            btnRefresh = new Button
            {
                Text = "Làm mới ngay",
                Location = new Point(685, 495),
                Size = new Size(200, 48),
                BackColor = Color.FromArgb(109, 76, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadPendingOrders();

            this.Controls.Add(pnlHeader);
            this.Controls.Add(dgvPending);
            this.Controls.Add(btnCompleteItem);
            this.Controls.Add(btnRefresh);
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = new Timer { Interval = 3000 };
            _refreshTimer.Tick += (s, e) => LoadPendingOrders();
            _refreshTimer.Start();
        }

        private void LoadPendingOrders()
        {
            DataTable dt = _posDal.GetPendingKitchenItems();
            dgvPending.DataSource = dt;
            lblCount.Text = $"Đang chờ: {dt.Rows.Count} món";
        }

        private void btnCompleteItem_Click(object sender, EventArgs e)
        {
            if (dgvPending.CurrentRow == null)
            {
                MessageBox.Show("Vui lòng chọn một món trong danh sách để hoàn thành!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int billDetailId = Convert.ToInt32(dgvPending.CurrentRow.Cells["Mã"].Value);
            string productName = dgvPending.CurrentRow.Cells["Tên món cần pha"].Value.ToString();
            string tableName = dgvPending.CurrentRow.Cells["Bàn"].Value.ToString();

            _posDal.MarkKitchenItemDone(billDetailId);
            SmartPaymentService.PlaySuccessSound();
            LoadPendingOrders();
            MessageBox.Show($"Đã hoàn thành món [{productName}] cho {tableName}!", "Quầy Bar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnFormClosing(e);
        }
    }
}