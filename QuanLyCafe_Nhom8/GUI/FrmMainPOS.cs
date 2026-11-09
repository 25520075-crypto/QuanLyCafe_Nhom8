using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;
using QuanLyCafe_Nhom8.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace QuanLyCafe_Nhom8.GUI
{
    public partial class FrmMainPOS : Form
    {
        // Khai báo tầng xử lý dữ liệu và các biến trạng thái của bàn đang chọn
        private readonly PosServiceDAL _posDal = new PosServiceDAL();
        private List<ProductDTO> _allProducts = new List<ProductDTO>();
        private int _selectedTableId = -1;
        private string _selectedTableName = "";
        private string _currentChannelTab = "DINE_IN";
        private decimal _currentRawTotal = 0;
        private int _approvedDiscountPercent = 0;

        // Các điều khiển giao diện chính trên màn hình POS
        private FlowLayoutPanel flpTables;
        private Button btnTabDineIn;
        private Button btnTabTakeAway;
        private Button btnTabShopeeXanh;
        private Button btnTabGrab;
        private TextBox txtSearchProduct;
        private ComboBox cboProducts;
        private ComboBox cboPaymentMethod;
        private NumericUpDown nudQuantity;
        private NumericUpDown nudDiscount;
        private Button btnApplyDiscount;
        private Button btnAddItem;
        private Button btnSendKitchen;
        private Button btnRemoveItem;
        private Button btnMoveTable;
        private Button btnMergeTable;
        private Button btnGenerateQR;
        private Button btnPrintBill;
        private Button btnConfirmPayment;
        private DataGridView dgvBillDetails;
        private Label lblSelectedTable;
        private Label lblTotalAmount;
        private PictureBox picVietQR;

        public FrmMainPOS()
        {
            InitializeComponent();
            BuildModernPosInterface();
            LoadAllProducts();
            SwitchChannelTab("DINE_IN");
        }

        // Hàm dựng toàn bộ bố cục giao diện 3 cột theo tông màu Quán Cafe Hiện Đại
        private void BuildModernPosInterface()
        {
            this.Text = "Hệ thống POS Quản lý Quán Cafe Thông Minh - Nhóm 8";
            this.Size = new Size(1350, 805);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(244, 239, 234);
            this.DoubleBuffered = true;

            // 1. THANH HEADER ĐIỀU HƯỚNG TRÊN CÙNG
            Panel pnlTopHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(62, 39, 35)
            };

            Label lblBrand = new Label
            {
                Text = "☕ HIGHLAND COFFEE POS - NHÓM 8",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 224, 178),
                Location = new Point(18, 15),
                AutoSize = true
            };

            Button btnOpenKDS = CreateHeaderButton("🔔 Màn hình Pha chế (KDS)", new Point(630, 11), Color.FromArgb(211, 84, 0));
            btnOpenKDS.Click += (s, e) =>
            {
                FrmKDS kdsForm = new FrmKDS();
                kdsForm.ShowDialog();
                LoadBillForSelectedTable();
            };

            Button btnOpenMenu = CreateHeaderButton("📋 Quản lý Thực đơn", new Point(855, 11), Color.FromArgb(109, 76, 65));
            btnOpenMenu.Click += (s, e) =>
            {
                MessageBox.Show("Module Quản lý Thực đơn (FrmMenuManager) do Tuấn Anh phụ trách sẽ được gắn vào nút này khi Merge nhánh!", "Thông tin Module", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnOpenStats = CreateHeaderButton("📊 Thống kê & Telegram", new Point(1070, 11), Color.FromArgb(109, 76, 65));
            btnOpenStats.Click += (s, e) =>
            {
                MessageBox.Show("Module Thống kê & Telegram Bot (FrmStatistics) do Ngô Việt Hưng phụ trách sẽ được gắn vào nút này khi Merge nhánh!", "Thông tin Module", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnlTopHeader.Controls.Add(lblBrand);
            pnlTopHeader.Controls.Add(btnOpenKDS);
            pnlTopHeader.Controls.Add(btnOpenMenu);
            pnlTopHeader.Controls.Add(btnOpenStats);

            // 2. CỘT TRÁI: 4 TAB KÊNH BÁN HÀNG + SƠ ĐỒ BÀN + NÚT MỞ BẢNG CHUYỂN/GỘP BÀN
            Panel pnlLeft = new Panel
            {
                Location = new Point(15, 72),
                Size = new Size(455, 680),
                BackColor = Color.White
            };

            Label lblAreaTitle = new Label
            {
                Text = "KÊNH BÁN HÀNG & SƠ ĐỒ ĐƠN",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(12, 10),
                AutoSize = true
            };

            Label lblLegend = new Label
            {
                Text = "● Xanh: Trống  ● Cam: Có đơn",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.DimGray,
                Location = new Point(275, 13),
                AutoSize = true
            };

            btnTabDineIn = CreateTabButton("Tại quán", new Point(12, 38), 98, () => SwitchChannelTab("DINE_IN"));
            btnTabTakeAway = CreateTabButton("Mang về", new Point(114, 38), 98, () => SwitchChannelTab("TAKE_AWAY"));
            btnTabShopeeXanh = CreateTabButton("Shopee & XanhSM", new Point(216, 38), 130, () => SwitchChannelTab("SHOPEE_XANH"));
            btnTabGrab = CreateTabButton("GrabFood", new Point(350, 38), 93, () => SwitchChannelTab("GRAB"));

            flpTables = new FlowLayoutPanel
            {
                Location = new Point(12, 78),
                Size = new Size(431, 515),
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 248, 245)
            };

            btnMoveTable = new Button
            {
                Text = "⇄ Chuyển bàn (Mở bảng chọn bàn Trống)",
                Location = new Point(12, 608),
                Size = new Size(210, 52),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMoveTable.FlatAppearance.BorderSize = 0;
            btnMoveTable.Click += btnMoveTable_Click;

            btnMergeTable = new Button
            {
                Text = "⊕ Gộp bàn (Mở bảng chọn bàn Có khách)",
                Location = new Point(233, 608),
                Size = new Size(210, 52),
                BackColor = Color.FromArgb(142, 68, 173),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMergeTable.FlatAppearance.BorderSize = 0;
            btnMergeTable.Click += btnMergeTable_Click;

            pnlLeft.Controls.Add(lblAreaTitle);
            pnlLeft.Controls.Add(lblLegend);
            pnlLeft.Controls.Add(btnTabDineIn);
            pnlLeft.Controls.Add(btnTabTakeAway);
            pnlLeft.Controls.Add(btnTabShopeeXanh);
            pnlLeft.Controls.Add(btnTabGrab);
            pnlLeft.Controls.Add(flpTables);
            pnlLeft.Controls.Add(btnMoveTable);
            pnlLeft.Controls.Add(btnMergeTable);

            // 3. CỘT GIỮA: TÌM KIẾM MÓN + GỌI MÓN NHÁP + GỬI PHA CHẾ + HỦY MÓN / GIẢM GIÁ (CẦN QUẢN LÝ)
            Panel pnlCenter = new Panel
            {
                Location = new Point(482, 72),
                Size = new Size(515, 680),
                BackColor = Color.White
            };

            lblSelectedTable = new Label
            {
                Text = "CHƯA CHỌN BÀN / ĐƠN",
                Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(192, 57, 43),
                Location = new Point(15, 10),
                AutoSize = true
            };

            Label lblSearch = new Label
            {
                Text = "🔍 Tìm món:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(15, 44),
                AutoSize = true
            };

            txtSearchProduct = new TextBox
            {
                Location = new Point(105, 41),
                Size = new Size(395, 27),
                Font = new Font("Segoe UI", 10f)
            };
            txtSearchProduct.TextChanged += txtSearchProduct_TextChanged;

            cboProducts = new ComboBox
            {
                Location = new Point(15, 76),
                Size = new Size(290, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5f)
            };

            nudQuantity = new NumericUpDown
            {
                Location = new Point(315, 76),
                Size = new Size(65, 32),
                Minimum = 1,
                Maximum = 50,
                Value = 1,
                Font = new Font("Segoe UI", 10.5f)
            };

            btnAddItem = new Button
            {
                Text = "+ Thêm nháp",
                Location = new Point(390, 74),
                Size = new Size(110, 33),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddItem.FlatAppearance.BorderSize = 0;
            btnAddItem.Click += btnAddItem_Click;

            dgvBillDetails = new DataGridView
            {
                Location = new Point(15, 118),
                Size = new Size(485, 385),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(252, 250, 248),
                BorderStyle = BorderStyle.FixedSingle,
                RowTemplate = { Height = 34 },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 9.5f)
            };

            btnSendKitchen = new Button
            {
                Text = "📨 GỬI PHA CHẾ (Sau khi đã đọc lại order cho khách xác nhận)",
                Location = new Point(15, 512),
                Size = new Size(485, 38),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSendKitchen.FlatAppearance.BorderSize = 0;
            btnSendKitchen.Click += btnSendKitchen_Click;

            btnRemoveItem = new Button
            {
                Text = "🗑 Hủy món (Cần Quản lý)",
                Location = new Point(15, 558),
                Size = new Size(205, 36),
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRemoveItem.FlatAppearance.BorderSize = 0;
            btnRemoveItem.Click += btnRemoveItem_Click;

            Label lblDiscountTitle = new Label
            {
                Text = "Giảm (%):",
                Location = new Point(228, 566),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35)
            };

            nudDiscount = new NumericUpDown
            {
                Location = new Point(302, 563),
                Size = new Size(62, 28),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Font = new Font("Segoe UI", 10)
            };

            btnApplyDiscount = new Button
            {
                Text = "Duyệt Giảm giá",
                Location = new Point(372, 558),
                Size = new Size(128, 36),
                BackColor = Color.FromArgb(109, 76, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnApplyDiscount.FlatAppearance.BorderSize = 0;
            btnApplyDiscount.Click += btnApplyDiscount_Click;

            Panel pnlTotalBox = new Panel
            {
                Location = new Point(15, 604),
                Size = new Size(485, 60),
                BackColor = Color.FromArgb(62, 39, 35)
            };

            lblTotalAmount = new Label
            {
                Text = "TỔNG THANH TOÁN: 0 VNĐ",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 224, 178),
                Location = new Point(15, 16),
                AutoSize = true
            };
            pnlTotalBox.Controls.Add(lblTotalAmount);

            pnlCenter.Controls.Add(lblSelectedTable);
            pnlCenter.Controls.Add(lblSearch);
            pnlCenter.Controls.Add(txtSearchProduct);
            pnlCenter.Controls.Add(cboProducts);
            pnlCenter.Controls.Add(nudQuantity);
            pnlCenter.Controls.Add(btnAddItem);
            pnlCenter.Controls.Add(dgvBillDetails);
            pnlCenter.Controls.Add(btnSendKitchen);
            pnlCenter.Controls.Add(btnRemoveItem);
            pnlCenter.Controls.Add(lblDiscountTitle);
            pnlCenter.Controls.Add(nudDiscount);
            pnlCenter.Controls.Add(btnApplyDiscount);
            pnlCenter.Controls.Add(pnlTotalBox);

            // 4. CỘT PHẢI: VIETQR + IN BILL KÈM QR + CHỌN LOẠI TIỀN & XÁC NHẬN ĐÃ NHẬN TIỀN
            Panel pnlRight = new Panel
            {
                Location = new Point(1008, 72),
                Size = new Size(312, 680),
                BackColor = Color.White
            };

            Label lblPayTitle = new Label
            {
                Text = "THANH TOÁN & IN HÓA ĐƠN",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(18, 10),
                AutoSize = true
            };

            btnGenerateQR = new Button
            {
                Text = "1. Tạo mã VietQR theo số tiền",
                Location = new Point(18, 38),
                Size = new Size(276, 38),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGenerateQR.FlatAppearance.BorderSize = 0;
            btnGenerateQR.Click += btnGenerateQR_Click;

            picVietQR = new PictureBox
            {
                Location = new Point(18, 84),
                Size = new Size(276, 340),
                BackColor = Color.FromArgb(250, 248, 245),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            btnPrintBill = new Button
            {
                Text = "🖨 Xem & In Bill Tính Tiền (Kèm QR)",
                Location = new Point(18, 434),
                Size = new Size(276, 40),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPrintBill.FlatAppearance.BorderSize = 0;
            btnPrintBill.Click += btnPrintBill_Click;

            Label lblMethod = new Label
            {
                Text = "Phân loại nguồn tiền thu vào:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(18, 486),
                AutoSize = true
            };

            cboPaymentMethod = new ComboBox
            {
                Location = new Point(18, 510),
                Size = new Size(276, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            cboPaymentMethod.Items.AddRange(new object[]
            {
                "Tiền mặt",
                "Chuyển khoản QR",
                "Ví ShopeeFood / XanhSM",
                "Ví GrabFood"
            });
            cboPaymentMethod.SelectedIndex = 0;

            btnConfirmPayment = new Button
            {
                Text = "2. XÁC NHẬN ĐÃ NHẬN TIỀN\n(Làm trống bàn / Hoàn tất đơn)",
                Location = new Point(18, 552),
                Size = new Size(276, 60),
                BackColor = Color.FromArgb(211, 84, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnConfirmPayment.FlatAppearance.BorderSize = 0;
            btnConfirmPayment.Click += btnConfirmPayment_Click;

            pnlRight.Controls.Add(lblPayTitle);
            pnlRight.Controls.Add(btnGenerateQR);
            pnlRight.Controls.Add(picVietQR);
            pnlRight.Controls.Add(btnPrintBill);
            pnlRight.Controls.Add(lblMethod);
            pnlRight.Controls.Add(cboPaymentMethod);
            pnlRight.Controls.Add(btnConfirmPayment);

            this.Controls.Add(pnlTopHeader);
            this.Controls.Add(pnlLeft);
            this.Controls.Add(pnlCenter);
            this.Controls.Add(pnlRight);
        }

        private Button CreateHeaderButton(string text, Point location, Color backColor)
        {
            Button btn = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(205, 38),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CreateTabButton(string text, Point location, int width, Action onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 34),
                BackColor = Color.FromArgb(224, 214, 205),
                ForeColor = Color.FromArgb(62, 39, 35),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        // Chuyển đổi giữa 4 Tab kênh bán hàng: Tại quán, Mang về, Shopee & XanhSM, GrabFood
        private void SwitchChannelTab(string tabKey)
        {
            _currentChannelTab = tabKey;

            Color activeBg = Color.FromArgb(62, 39, 35);
            Color inactiveBg = Color.FromArgb(224, 214, 205);

            btnTabDineIn.BackColor = tabKey == "DINE_IN" ? activeBg : inactiveBg;
            btnTabDineIn.ForeColor = tabKey == "DINE_IN" ? Color.White : activeBg;

            btnTabTakeAway.BackColor = tabKey == "TAKE_AWAY" ? activeBg : inactiveBg;
            btnTabTakeAway.ForeColor = tabKey == "TAKE_AWAY" ? Color.White : activeBg;

            btnTabShopeeXanh.BackColor = tabKey == "SHOPEE_XANH" ? activeBg : inactiveBg;
            btnTabShopeeXanh.ForeColor = tabKey == "SHOPEE_XANH" ? Color.White : activeBg;

            btnTabGrab.BackColor = tabKey == "GRAB" ? activeBg : inactiveBg;
            btnTabGrab.ForeColor = tabKey == "GRAB" ? Color.White : activeBg;

            // Tự động gợi ý hình thức thanh toán theo Tab đang mở
            if (cboPaymentMethod != null && cboPaymentMethod.Items.Count >= 4)
            {
                if (tabKey == "SHOPEE_XANH") cboPaymentMethod.SelectedIndex = 2;
                else if (tabKey == "GRAB") cboPaymentMethod.SelectedIndex = 3;
                else cboPaymentMethod.SelectedIndex = 0;
            }

            LoadTablesToBoard();
        }

        // Kiểm tra xem tên Bàn/Đơn thuộc về Tab nào
        private bool IsTableMatchingTab(string tableName, string tabKey)
        {
            if (tabKey == "TAKE_AWAY")
                return tableName.StartsWith("Mang về", StringComparison.OrdinalIgnoreCase);
            if (tabKey == "SHOPEE_XANH")
                return tableName.StartsWith("Shopee", StringComparison.OrdinalIgnoreCase) ||
                       tableName.StartsWith("XanhSM", StringComparison.OrdinalIgnoreCase);
            if (tabKey == "GRAB")
                return tableName.StartsWith("Grab", StringComparison.OrdinalIgnoreCase);

            return !tableName.StartsWith("Mang về", StringComparison.OrdinalIgnoreCase) &&
                   !tableName.StartsWith("Shopee", StringComparison.OrdinalIgnoreCase) &&
                   !tableName.StartsWith("XanhSM", StringComparison.OrdinalIgnoreCase) &&
                   !tableName.StartsWith("Grab", StringComparison.OrdinalIgnoreCase);
        }

        // Hiển thị các nút bàn lên sơ đồ theo Tab đang chọn
        private void LoadTablesToBoard()
        {
            flpTables.Controls.Clear();
            List<TableDTO> allTables = _posDal.GetAllTables();

            foreach (TableDTO table in allTables)
            {
                if (!IsTableMatchingTab(table.TableName, _currentChannelTab)) continue;

                bool isEmpty = table.Status == "Trống";
                bool isSelected = table.TableId == _selectedTableId;
                Button btn = new Button
                {
                    Width = 128,
                    Height = 96,
                    Margin = new Padding(6),
                    Text = isSelected
                        ? $"✔ {table.TableName}\n({table.Status})"
                        : $"{table.TableName}\n({table.Status})",
                    Tag = table,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand,
                    BackColor = isEmpty
                        ? Color.FromArgb(39, 174, 96)
                        : Color.FromArgb(211, 84, 0)
                };
                btn.FlatAppearance.BorderSize = isSelected ? 4 : 0;
                btn.FlatAppearance.BorderColor = Color.FromArgb(62, 39, 35);
                btn.Click += TableButton_Click;
                flpTables.Controls.Add(btn);
            }
        }

        private void LoadAllProducts()
        {
            _allProducts = _posDal.GetActiveProducts();
            BindProductList(_allProducts);
        }

        private void BindProductList(List<ProductDTO> list)
        {
            cboProducts.DataSource = null;
            cboProducts.DataSource = list;
            cboProducts.DisplayMember = "ProductName";
            cboProducts.ValueMember = "ProductId";
        }

        // Lọc danh sách món ngay khi gõ chữ vào ô tìm kiếm nhanh
        private void txtSearchProduct_TextChanged(object sender, EventArgs e)
        {
            string keyword = txtSearchProduct.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(keyword))
            {
                BindProductList(_allProducts);
                return;
            }

            List<ProductDTO> filtered = _allProducts
                .Where(p => p.ProductName.ToLowerInvariant().Contains(keyword))
                .ToList();
            BindProductList(filtered);
        }

        private void TableButton_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn?.Tag is TableDTO table)
            {
                _selectedTableId = table.TableId;
                _selectedTableName = table.TableName;
                _approvedDiscountPercent = 0;
                nudDiscount.Value = 0;
                lblSelectedTable.Text = $"ĐANG PHỤC VỤ: {_selectedTableName.ToUpper()} ({table.Status})";
                picVietQR.Image = null;
                LoadTablesToBoard();
                LoadBillForSelectedTable();
            }
        }

        private void LoadBillForSelectedTable()
        {
            if (_selectedTableId == -1) return;
            DataTable dt = _posDal.GetBillDetailsByTable(_selectedTableId);
            dgvBillDetails.DataSource = dt;
            if (dgvBillDetails.Columns["BillDetailId"] != null)
            {
                dgvBillDetails.Columns["BillDetailId"].Visible = false;
            }

            _currentRawTotal = 0;
            foreach (DataRow row in dt.Rows)
            {
                _currentRawTotal += Convert.ToDecimal(row["Thành tiền"]);
            }
            UpdateTotalDisplay();
        }

        private void UpdateTotalDisplay()
        {
            decimal finalTotal = _currentRawTotal - (_currentRawTotal * _approvedDiscountPercent / 100m);
            lblTotalAmount.Text = _approvedDiscountPercent > 0
                ? $"TỔNG THANH TOÁN (Giảm {_approvedDiscountPercent}%): {finalTotal:N0} VNĐ"
                : $"TỔNG THANH TOÁN: {finalTotal:N0} VNĐ";
        }

        // Hộp thoại yêu cầu đăng nhập tài khoản Quản lý (Admin) khi Hủy món hoặc Giảm giá
        private bool RequestManagerApprovalDialog(string actionTitle, out string approvedBy)
        {
            approvedBy = "";
            using (Form authForm = new Form())
            {
                authForm.Text = "Yêu cầu Quản lý (Admin) phê duyệt";
                authForm.Size = new Size(420, 250);
                authForm.StartPosition = FormStartPosition.CenterParent;
                authForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                authForm.MaximizeBox = false;
                authForm.MinimizeBox = false;
                authForm.BackColor = Color.White;

                Label lblHeader = new Label
                {
                    Text = $"Thao tác: {actionTitle}\n(Chỉ tài khoản Quản lý mới được phép thực hiện)",
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(192, 57, 43),
                    Location = new Point(20, 15),
                    AutoSize = true
                };

                Label lblUser = new Label
                {
                    Text = "TK Quản lý:",
                    Location = new Point(20, 72),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10)
                };

                TextBox txtUser = new TextBox
                {
                    Location = new Point(125, 69),
                    Size = new Size(250, 28),
                    Font = new Font("Segoe UI", 10),
                    Text = "admin"
                };

                Label lblPass = new Label
                {
                    Text = "Mật khẩu:",
                    Location = new Point(20, 112),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10)
                };

                TextBox txtPass = new TextBox
                {
                    Location = new Point(125, 109),
                    Size = new Size(250, 28),
                    Font = new Font("Segoe UI", 10),
                    UseSystemPasswordChar = true
                };

                Button btnOk = new Button
                {
                    Text = "Duyệt thao tác",
                    Location = new Point(125, 155),
                    Size = new Size(140, 36),
                    BackColor = Color.FromArgb(39, 174, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    DialogResult = DialogResult.OK
                };

                Button btnCancel = new Button
                {
                    Text = "Hủy bỏ",
                    Location = new Point(275, 155),
                    Size = new Size(100, 36),
                    BackColor = Color.Gray,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5f),
                    DialogResult = DialogResult.Cancel
                };

                authForm.Controls.Add(lblHeader);
                authForm.Controls.Add(lblUser);
                authForm.Controls.Add(txtUser);
                authForm.Controls.Add(lblPass);
                authForm.Controls.Add(txtPass);
                authForm.Controls.Add(btnOk);
                authForm.Controls.Add(btnCancel);
                authForm.AcceptButton = btnOk;

                if (authForm.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                if (_posDal.VerifyManagerAccount(txtUser.Text, txtPass.Text, out approvedBy))
                {
                    return true;
                }

                MessageBox.Show(
                    "Sai mật khẩu hoặc tài khoản không phải Quản lý (Admin)!\n(Gợi ý khi chạy Demo: TK 'admin' / MK 'admin123')",
                    "Từ chối phê duyệt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        // BẢNG CHỌN BÀN TRỰC QUAN CÓ 4 TAB VÀ DẤU TÍCH XANH LÁ (✔) ĐỂ CHỐNG CHUYỂN/GỘP NHẦM BÀN
        private TableDTO ShowVisualTableSelectorDialog(bool isMoveToEmptyMode)
        {
            TableDTO chosenTable = null;
            string modalTab = _currentChannelTab;
            List<TableDTO> allTables = _posDal.GetAllTables();

            using (Form pickerForm = new Form())
            {
                pickerForm.Text = isMoveToEmptyMode
                    ? $"CHUYỂN BÀN: Chọn bàn TRỐNG để chuyển [{_selectedTableName}] sang"
                    : $"GỘP BÀN: Chọn bàn ĐANG CÓ KHÁCH để gộp [{_selectedTableName}] vào";
                pickerForm.Size = new Size(680, 560);
                pickerForm.StartPosition = FormStartPosition.CenterParent;
                pickerForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                pickerForm.MaximizeBox = false;
                pickerForm.MinimizeBox = false;
                pickerForm.BackColor = Color.FromArgb(244, 239, 234);

                Label lblInstruction = new Label
                {
                    Text = isMoveToEmptyMode
                        ? $"Đang chuyển từ [{_selectedTableName}] -> Hãy bấm vào 1 bàn TRỐNG bên dưới (Sẽ hiện dấu tích xanh ✔):"
                        : $"Đang gộp từ [{_selectedTableName}] -> Hãy bấm vào 1 bàn ĐANG CÓ KHÁCH bên dưới (Sẽ hiện dấu tích xanh ✔):",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(62, 39, 35),
                    Location = new Point(15, 12),
                    AutoSize = true
                };

                FlowLayoutPanel flpModalTables = new FlowLayoutPanel
                {
                    Location = new Point(15, 82),
                    Size = new Size(635, 365),
                    AutoScroll = true,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblPickedStatus = new Label
                {
                    Text = "Chưa tích chọn bàn đích nào",
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(192, 57, 43),
                    Location = new Point(15, 465),
                    AutoSize = true
                };

                Button btnConfirmPick = new Button
                {
                    Text = "✔ XÁC NHẬN THỰC HIỆN",
                    Location = new Point(380, 458),
                    Size = new Size(270, 44),
                    BackColor = Color.FromArgb(39, 174, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    Enabled = false,
                    Cursor = Cursors.Hand
                };
                btnConfirmPick.FlatAppearance.BorderSize = 0;
                btnConfirmPick.Click += (s, e) =>
                {
                    pickerForm.DialogResult = DialogResult.OK;
                    pickerForm.Close();
                };

                Button btnMTab1 = null, btnMTab2 = null, btnMTab3 = null, btnMTab4 = null;

                Action renderModalCards = null;
                renderModalCards = () =>
                {
                    Color activeColor = Color.FromArgb(62, 39, 35);
                    Color inactiveColor = Color.FromArgb(215, 204, 200);

                    btnMTab1.BackColor = modalTab == "DINE_IN" ? activeColor : inactiveColor;
                    btnMTab1.ForeColor = modalTab == "DINE_IN" ? Color.White : activeColor;
                    btnMTab2.BackColor = modalTab == "TAKE_AWAY" ? activeColor : inactiveColor;
                    btnMTab2.ForeColor = modalTab == "TAKE_AWAY" ? Color.White : activeColor;
                    btnMTab3.BackColor = modalTab == "SHOPEE_XANH" ? activeColor : inactiveColor;
                    btnMTab3.ForeColor = modalTab == "SHOPEE_XANH" ? Color.White : activeColor;
                    btnMTab4.BackColor = modalTab == "GRAB" ? activeColor : inactiveColor;
                    btnMTab4.ForeColor = modalTab == "GRAB" ? Color.White : activeColor;

                    flpModalTables.Controls.Clear();
                    foreach (TableDTO t in allTables)
                    {
                        if (!IsTableMatchingTab(t.TableName, modalTab)) continue;
                        if (t.TableId == _selectedTableId) continue;

                        bool isEmpty = t.Status == "Trống";
                        // Nếu là Chuyển bàn thì chỉ cho chọn bàn Trống; nếu là Gộp bàn thì chỉ cho chọn bàn Có khách
                        bool isSelectable = isMoveToEmptyMode ? isEmpty : !isEmpty;
                        bool isTicked = chosenTable != null && chosenTable.TableId == t.TableId;

                        Button card = new Button
                        {
                            Width = 142,
                            Height = 98,
                            Margin = new Padding(7),
                            FlatStyle = FlatStyle.Flat,
                            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                            Enabled = isSelectable,
                            Cursor = isSelectable ? Cursors.Hand : Cursors.No,
                            Text = isTicked
                                ? $"✅ [ĐÃ CHỌN]\n{t.TableName}\n({t.Status})"
                                : $"{t.TableName}\n({t.Status})"
                        };

                        if (isTicked)
                        {
                            card.BackColor = Color.FromArgb(46, 204, 113);
                            card.ForeColor = Color.White;
                            card.FlatAppearance.BorderSize = 4;
                            card.FlatAppearance.BorderColor = Color.FromArgb(20, 90, 50);
                        }
                        else if (isSelectable)
                        {
                            card.BackColor = isEmpty ? Color.FromArgb(232, 248, 245) : Color.FromArgb(253, 237, 236);
                            card.ForeColor = Color.FromArgb(44, 62, 80);
                            card.FlatAppearance.BorderSize = 2;
                            card.FlatAppearance.BorderColor = isEmpty ? Color.FromArgb(39, 174, 96) : Color.FromArgb(211, 84, 0);
                        }
                        else
                        {
                            card.BackColor = Color.FromArgb(235, 235, 235);
                            card.ForeColor = Color.DarkGray;
                            card.FlatAppearance.BorderSize = 1;
                            card.FlatAppearance.BorderColor = Color.Silver;
                        }

                        card.Click += (s, e) =>
                        {
                            chosenTable = t;
                            lblPickedStatus.Text = $"✔ Đã tích chọn: {t.TableName} ({t.Status})";
                            lblPickedStatus.ForeColor = Color.FromArgb(39, 174, 96);
                            btnConfirmPick.Enabled = true;
                            renderModalCards();
                        };

                        flpModalTables.Controls.Add(card);
                    }
                };

                btnMTab1 = CreateTabButton("1. Tại quán", new Point(15, 42), 140, () => { modalTab = "DINE_IN"; renderModalCards(); });
                btnMTab2 = CreateTabButton("2. Mua mang về", new Point(162, 42), 145, () => { modalTab = "TAKE_AWAY"; renderModalCards(); });
                btnMTab3 = CreateTabButton("3. Shopee & XanhSM", new Point(314, 42), 175, () => { modalTab = "SHOPEE_XANH"; renderModalCards(); });
                btnMTab4 = CreateTabButton("4. GrabFood", new Point(496, 42), 154, () => { modalTab = "GRAB"; renderModalCards(); });

                pickerForm.Controls.Add(lblInstruction);
                pickerForm.Controls.Add(btnMTab1);
                pickerForm.Controls.Add(btnMTab2);
                pickerForm.Controls.Add(btnMTab3);
                pickerForm.Controls.Add(btnMTab4);
                pickerForm.Controls.Add(flpModalTables);
                pickerForm.Controls.Add(lblPickedStatus);
                pickerForm.Controls.Add(btnConfirmPick);

                renderModalCards();

                return pickerForm.ShowDialog(this) == DialogResult.OK ? chosenTable : null;
            }
        }

        private void btnAddItem_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1)
            {
                MessageBox.Show("Vui lòng chọn một bàn / đơn trước khi gọi món!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboProducts.SelectedValue == null)
            {
                MessageBox.Show("Không tìm thấy món phù hợp!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int productId = Convert.ToInt32(cboProducts.SelectedValue);
            int quantity = Convert.ToInt32(nudQuantity.Value);

            _posDal.AddItemToTable(_selectedTableId, productId, quantity);
            LoadTablesToBoard();
            LoadBillForSelectedTable();
        }

        private void btnSendKitchen_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Chưa có món nào trong bàn để gửi pha chế!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int sentCount = _posDal.SendTableOrderToKitchen(_selectedTableId);
            if (sentCount > 0)
            {
                SmartPaymentService.PlaySuccessSound();
                LoadBillForSelectedTable();
                MessageBox.Show($"Đã gửi {sentCount} dòng món của [{_selectedTableName}] xuống màn hình Quầy Pha Chế (KDS)!", "Đã gửi pha chế", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Tất cả các món của bàn này đều đã được gửi pha chế trước đó!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnRemoveItem_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || dgvBillDetails.CurrentRow == null)
            {
                MessageBox.Show("Vui lòng chọn một món trong bảng hóa đơn để hủy!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int billDetailId = Convert.ToInt32(dgvBillDetails.CurrentRow.Cells["BillDetailId"].Value);
            string itemName = dgvBillDetails.CurrentRow.Cells["Tên món"].Value.ToString();

            if (!RequestManagerApprovalDialog($"Hủy món [{itemName}] tại {_selectedTableName}", out string managerName))
            {
                return;
            }

            _posDal.RemoveBillDetailItem(billDetailId, _selectedTableId);
            LoadTablesToBoard();
            LoadBillForSelectedTable();
            MessageBox.Show($"Đã hủy món [{itemName}] (Duyệt bởi Quản lý: {managerName}).\nLịch sử hủy đã được Trigger tự động ghi vào bảng AuditLogs!", "Đã hủy món", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnApplyDiscount_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Bàn chưa có hóa đơn để áp dụng giảm giá!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int requestedDiscount = Convert.ToInt32(nudDiscount.Value);
            if (requestedDiscount == 0)
            {
                _approvedDiscountPercent = 0;
                UpdateTotalDisplay();
                return;
            }

            if (!RequestManagerApprovalDialog($"Giảm giá {requestedDiscount}% cho {_selectedTableName}", out string managerName))
            {
                nudDiscount.Value = _approvedDiscountPercent;
                return;
            }

            _approvedDiscountPercent = requestedDiscount;
            UpdateTotalDisplay();
            MessageBox.Show($"Đã duyệt giảm giá {_approvedDiscountPercent}% bởi Quản lý: {managerName}!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Khi bấm nút Chuyển bàn -> Mở bảng tích chọn bàn Trống
        private void btnMoveTable_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Vui lòng chọn bàn đang có khách cần chuyển đi!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TableDTO targetTable = ShowVisualTableSelectorDialog(true);
            if (targetTable == null) return;

            _posDal.MoveToEmptyTable(_selectedTableId, targetTable.TableId);
            _selectedTableId = targetTable.TableId;
            _selectedTableName = targetTable.TableName;
            LoadTablesToBoard();
            LoadBillForSelectedTable();
            MessageBox.Show($"Đã chuyển sang [{_selectedTableName}] thành công!", "Chuyển bàn hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Khi bấm nút Gộp bàn -> Mở bảng tích chọn bàn Đang có khách
        private void btnMergeTable_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Vui lòng chọn bàn đang có khách cần gộp!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TableDTO targetTable = ShowVisualTableSelectorDialog(false);
            if (targetTable == null) return;

            _posDal.MergeOccupiedTables(_selectedTableId, targetTable.TableId);
            _selectedTableId = targetTable.TableId;
            _selectedTableName = targetTable.TableName;
            LoadTablesToBoard();
            LoadBillForSelectedTable();
            MessageBox.Show($"Đã gộp chung hóa đơn vào [{_selectedTableName}] thành công!", "Gộp bàn hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnGenerateQR_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Bàn / Đơn này chưa có hóa đơn cần thanh toán!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            decimal finalTotal = _currentRawTotal - (_currentRawTotal * _approvedDiscountPercent / 100m);

            try
            {
                cboPaymentMethod.SelectedIndex = 1;
                string qrUrl = SmartPaymentService.BuildVietQrUrl(finalTotal, _selectedTableName);
                picVietQR.LoadAsync(qrUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tải ảnh VietQR: " + ex.Message, "Lỗi mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Nút Xem & In Bill Tính Tiền (Hiển thị tờ hóa đơn kèm mã VietQR ngay trên màn hình, không cần mua máy in thật)
        private void btnPrintBill_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Chưa có hóa đơn để in!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal finalTotal = _currentRawTotal - (_currentRawTotal * _approvedDiscountPercent / 100m);
            string method = cboPaymentMethod.SelectedItem?.ToString() ?? "Tiền mặt";
            DataTable dtItems = _posDal.GetBillDetailsByTable(_selectedTableId);

            // Tự động nạp mã QR nếu chưa bấm Tạo mã QR trước đó
            if (picVietQR.Image == null)
            {
                try
                {
                    string qrUrl = SmartPaymentService.BuildVietQrUrl(finalTotal, _selectedTableName);
                    picVietQR.LoadAsync(qrUrl);
                }
                catch { }
            }

            using (Form billForm = new Form())
            {
                billForm.Text = $"Phiếu Tính Tiền - {_selectedTableName}";
                billForm.Size = new Size(440, 720);
                billForm.StartPosition = FormStartPosition.CenterParent;
                billForm.BackColor = Color.White;

                Panel pnlReceipt = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.White,
                    Padding = new Padding(20)
                };

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("       HIGHLAND COFFEE POS - NHÓM 8");
                sb.AppendLine("     ĐC: Khu Công Nghệ Cao, Đồng Nai");
                sb.AppendLine("==========================================");
                sb.AppendLine($"        PHIẾU TẠM TÍNH / HÓA ĐƠN");
                sb.AppendLine($"Bàn / Đơn : {_selectedTableName}");
                sb.AppendLine($"Thời gian : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Hình thức : {method}");
                sb.AppendLine("------------------------------------------");
                sb.AppendLine(string.Format("{0,-18} {1,3} {2,9} {3,9}", "Tên món", "SL", "Đ.Giá", "T.Tiền"));
                sb.AppendLine("------------------------------------------");

                foreach (DataRow row in dtItems.Rows)
                {
                    string name = row["Tên món"].ToString();
                    if (name.Length > 17) name = name.Substring(0, 17);
                    int qty = Convert.ToInt32(row["SL"]);
                    decimal price = Convert.ToDecimal(row["Đơn giá"]);
                    decimal sub = Convert.ToDecimal(row["Thành tiền"]);
                    sb.AppendLine(string.Format("{0,-18} {1,3} {2,9:N0} {3,9:N0}", name, qty, price, sub));
                }

                sb.AppendLine("------------------------------------------");
                sb.AppendLine($"Tạm tính          : {_currentRawTotal:N0} VNĐ");
                sb.AppendLine($"Giảm giá          : {_approvedDiscountPercent}%");
                sb.AppendLine($"TỔNG THANH TOÁN   : {finalTotal:N0} VNĐ");
                sb.AppendLine("==========================================");
                sb.AppendLine("   Quét mã VietQR bên dưới để thanh toán:");

                Label lblReceiptText = new Label
                {
                    Text = sb.ToString(),
                    Font = new Font("Consolas", 9.5f, FontStyle.Regular),
                    Location = new Point(20, 15),
                    AutoSize = true
                };

                PictureBox picBillQR = new PictureBox
                {
                    Location = new Point(85, lblReceiptText.PreferredHeight + 25),
                    Size = new Size(240, 270),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle
                };

                if (picVietQR.Image != null)
                {
                    picBillQR.Image = picVietQR.Image;
                }
                else
                {
                    picBillQR.LoadAsync(SmartPaymentService.BuildVietQrUrl(finalTotal, _selectedTableName));
                }

                Button btnPrintPdf = new Button
                {
                    Text = "🖨 Xuất Máy In / Lưu File PDF",
                    Location = new Point(65, lblReceiptText.PreferredHeight + 310),
                    Size = new Size(280, 42),
                    BackColor = Color.FromArgb(41, 128, 185),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold)
                };
                btnPrintPdf.Click += (s2, e2) =>
                {
                    PrintDocument pd = new PrintDocument();
                    pd.PrintPage += (senderPrint, args) =>
                    {
                        args.Graphics.DrawString(sb.ToString(), new Font("Consolas", 10), Brushes.Black, new PointF(20, 20));
                        if (picBillQR.Image != null)
                        {
                            args.Graphics.DrawImage(picBillQR.Image, new Rectangle(60, 340, 220, 250));
                        }
                    };
                    PrintDialog pDialog = new PrintDialog { Document = pd };
                    if (pDialog.ShowDialog() == DialogResult.OK)
                    {
                        pd.Print();
                    }
                };

                pnlReceipt.Controls.Add(lblReceiptText);
                pnlReceipt.Controls.Add(picBillQR);
                pnlReceipt.Controls.Add(btnPrintPdf);
                billForm.Controls.Add(pnlReceipt);
                billForm.ShowDialog(this);
            }
        }

        // Xác nhận đã nhận tiền trực tiếp tại máy Thu ngân (Không cần đăng nhập lại), lưu loại tiền và làm trống bàn
        private void btnConfirmPayment_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Không có hóa đơn để thanh toán!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string paymentMethod = cboPaymentMethod.SelectedItem?.ToString() ?? "Tiền mặt";
            decimal expectedTotal = _currentRawTotal - (_currentRawTotal * _approvedDiscountPercent / 100m);

            DialogResult confirm = MessageBox.Show(
                $"Xác nhận đã nhận đủ [{expectedTotal:N0} VNĐ] cho [{_selectedTableName}]?\n- Hình thức: {paymentMethod}\n- Bàn/Đơn sẽ tự động chuyển về trạng thái Trống.",
                "Xác nhận thanh toán",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            if (_posDal.CheckoutTable(_selectedTableId, _approvedDiscountPercent, paymentMethod, out decimal finalPaid))
            {
                string paidTableName = _selectedTableName;
                picVietQR.Image = null;
                _approvedDiscountPercent = 0;
                nudDiscount.Value = 0;
                lblSelectedTable.Text = $"ĐÃ HOÀN TẤT: {paidTableName.ToUpper()} (Trống)";
                LoadTablesToBoard();
                LoadBillForSelectedTable();

                SmartPaymentService.PlaySuccessSound();
                MessageBox.Show($"Đã thanh toán thành công {paidTableName}!\n- Số tiền: {finalPaid:N0} VNĐ\n- Nguồn tiền: {paymentMethod}\n- Trạng thái: Bàn đã Trống.", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}