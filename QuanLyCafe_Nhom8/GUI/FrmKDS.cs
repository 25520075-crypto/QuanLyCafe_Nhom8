using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.Services;
using Protocol;
namespace QuanLyCafe_Nhom8.GUI
{
    public partial class FrmKDS : Form
    {
        // Sử dụng NetworkClient TCP Realtime thay thế hoàn toàn cho Timer 3 giây cục bộ
        private readonly NetworkClient _netClient = NetworkClient.Instance;
        private DataGridView dgvPending;
        private Button btnCompleteItem;
        private Button btnRefresh;
        private Label lblCount;
        private Label lblTcpStatus;

        public FrmKDS()
        {
            InitializeComponent();
            BuildKdsInterface();

            _netClient.Start();
            _netClient.OnServerEventReceived += OnTcpEventReceived;
            _netClient.OnConnectionStateChanged += OnTcpConnectionChanged;

            // Tự động đồng bộ danh sách món đang chờ ngay khi mở màn hình KDS
            LoadPendingOrders();
        }

        private void BuildKdsInterface()
        {
            this.Text = "Màn hình Quầy Pha Chế Real-time (TCP Socket Client) - Nhóm 8";
            this.Size = new Size(920, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 245, 241);

            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(62, 39, 35)
            };

            Label lblTitle = new Label
            {
                Text = "QUẦY PHA CHẾ REAL-TIME (TCP SOCKET CLIENT)",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 236, 179),
                Location = new Point(20, 10),
                AutoSize = true
            };

            lblTcpStatus = new Label
            {
                Text = $"🟢 TCP Connected ({_netClient.ServerHost}:{_netClient.ServerPort}) - Chế độ Push Realtime (Không dùng Timer)",
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(129, 199, 132),
                Location = new Point(22, 38),
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
            pnlHeader.Controls.Add(lblTcpStatus);
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
                Text = "✔ HOÀN THÀNH & BÁO VỀ THU NGÂN QUA TCP",
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
                Text = "🔄 Đồng bộ TCP",
                Location = new Point(685, 495),
                Size = new Size(200, 48),
                BackColor = Color.FromArgb(109, 76, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (s, e) =>
            {
                LoadPendingOrders();
            };

            this.Controls.Add(pnlHeader);
            this.Controls.Add(dgvPending);
            this.Controls.Add(btnCompleteItem);
            this.Controls.Add(btnRefresh);
        }

        // Nhận gói tin KDS_NEW_ORDER từ Server đẩy sang ngay khi Thu ngân bấm 'Gửi pha chế'
        private void OnTcpEventReceived(MsgType msgType, string payload)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            if (msgType == MsgType.KDS_NEW_ORDER || msgType == MsgType.KDS_ITEM_DONE)
            {
                this.BeginInvoke(new Action(() =>
                {
                    LoadPendingOrders();
                    if (msgType == MsgType.KDS_NEW_ORDER)
                    {
                        SmartPaymentService.PlaySuccessSound();
                    }
                }));
            }
        }

        // Tự động gửi GET_PENDING_KDS khi kết nối lại mạng thành công
        private void OnTcpConnectionChanged(bool connected, string statusText)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.BeginInvoke(new Action(async () =>
            {
                lblTcpStatus.Text = connected ? $"🟢 {statusText}" : $"🟠 {statusText}";
                lblTcpStatus.ForeColor = connected ? Color.FromArgb(129, 199, 132) : Color.FromArgb(255, 183, 77);
                if (connected)
                {
                    LoadPendingOrders();
                }
            }));
        }

        private void LoadPendingOrders()
        {
            DataTable dt = _netClient.GetPendingKitchenItems();
            dgvPending.DataSource = dt;
            lblCount.Text = $"Đang chờ: {dt.Rows.Count} món";
        }

        private async void btnCompleteItem_Click(object sender, EventArgs e)
        {
            if (dgvPending.CurrentRow == null)
            {
                MessageBox.Show("Vui lòng chọn một món trong danh sách để hoàn thành!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int billDetailId = Convert.ToInt32(dgvPending.CurrentRow.Cells[0].Value);
            string tableName = dgvPending.CurrentRow.Cells[1].Value.ToString();
            string productName = dgvPending.CurrentRow.Cells[2].Value.ToString();

            btnCompleteItem.Enabled = false;
            try
            {
                await _netClient.MarkKitchenItemDoneAsync(billDetailId, tableName, productName);
                SmartPaymentService.PlaySuccessSound();
                LoadPendingOrders();
                MessageBox.Show($"Đã hoàn thành món [{productName}] cho [{tableName}] và báo về máy Thu ngân qua TCP!", "Quầy Bar (TCP)", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                btnCompleteItem.Enabled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _netClient.OnServerEventReceived -= OnTcpEventReceived;
            _netClient.OnConnectionStateChanged -= OnTcpConnectionChanged;
            base.OnFormClosing(e);
        }

        private void FrmKDS_Load(object sender, EventArgs e)
        {

        }
    }
}