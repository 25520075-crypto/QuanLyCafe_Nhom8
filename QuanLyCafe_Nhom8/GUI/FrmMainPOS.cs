using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;
using QuanLyCafe_Nhom8.Services;

namespace QuanLyCafe_Nhom8.GUI
{
    public partial class FrmMainPOS : Form
    {
        private readonly PosServiceDAL _posDal = new PosServiceDAL();
        private int _selectedTableId = -1;
        private string _selectedTableName = "";
        private decimal _currentRawTotal = 0;

        private FlowLayoutPanel flpTables;
        private ComboBox cboProducts;
        private NumericUpDown nudQuantity;
        private NumericUpDown nudDiscount;
        private Button btnAddItem;
        private Button btnGenerateQR;
        private Button btnConfirmPayment;
        private DataGridView dgvBillDetails;
        private Label lblSelectedTable;
        private Label lblTotalAmount;
        private PictureBox picVietQR;

        public FrmMainPOS()
        {
            InitializeComponent();
            BuildPosInterface();
            LoadTablesToBoard();
            LoadProductsToCombo();
        }

        private void BuildPosInterface()
        {
            this.Text = "Hệ thống POS Quản lý Quán Cafe - Nhóm 8";
            this.Size = new Size(1250, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 246, 250);
            this.DoubleBuffered = true;

            flpTables = new FlowLayoutPanel
            {
                Location = new Point(15, 15),
                Size = new Size(430, 645),
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Panel pnlCenter = new Panel
            {
                Location = new Point(460, 15),
                Size = new Size(430, 645),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblSelectedTable = new Label
            {
                Text = "Chưa chọn bàn",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(15, 15),
                AutoSize = true
            };

            cboProducts = new ComboBox
            {
                Location = new Point(15, 55),
                Size = new Size(240, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10)
            };

            nudQuantity = new NumericUpDown
            {
                Location = new Point(265, 55),
                Size = new Size(60, 32),
                Minimum = 1,
                Maximum = 50,
                Value = 1,
                Font = new Font("Segoe UI", 10)
            };

            btnAddItem = new Button
            {
                Text = "Thêm món",
                Location = new Point(335, 53),
                Size = new Size(80, 32),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnAddItem.Click += btnAddItem_Click;

            dgvBillDetails = new DataGridView
            {
                Location = new Point(15, 100),
                Size = new Size(400, 440),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            Label lblDiscountTitle = new Label
            {
                Text = "Giảm giá (%):",
                Location = new Point(15, 558),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            nudDiscount = new NumericUpDown
            {
                Location = new Point(115, 555),
                Size = new Size(70, 28),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Font = new Font("Segoe UI", 10)
            };
            nudDiscount.ValueChanged += (s, e) => UpdateTotalDisplay();

            lblTotalAmount = new Label
            {
                Text = "Tổng tiền: 0 VNĐ",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(192, 57, 43),
                Location = new Point(15, 595),
                AutoSize = true
            };

            pnlCenter.Controls.Add(lblSelectedTable);
            pnlCenter.Controls.Add(cboProducts);
            pnlCenter.Controls.Add(nudQuantity);
            pnlCenter.Controls.Add(btnAddItem);
            pnlCenter.Controls.Add(dgvBillDetails);
            pnlCenter.Controls.Add(lblDiscountTitle);
            pnlCenter.Controls.Add(nudDiscount);
            pnlCenter.Controls.Add(lblTotalAmount);

            Panel pnlRight = new Panel
            {
                Location = new Point(905, 15),
                Size = new Size(315, 645),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnGenerateQR = new Button
            {
                Text = "1. Tạo mã thanh toán VietQR",
                Location = new Point(20, 20),
                Size = new Size(275, 42),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnGenerateQR.Click += btnGenerateQR_Click;

            picVietQR = new PictureBox
            {
                Location = new Point(20, 80),
                Size = new Size(275, 360),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            btnConfirmPayment = new Button
            {
                Text = "2. Xác nhận Đã nhận tiền",
                Location = new Point(20, 460),
                Size = new Size(275, 48),
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnConfirmPayment.Click += btnConfirmPayment_Click;

            pnlRight.Controls.Add(btnGenerateQR);
            pnlRight.Controls.Add(picVietQR);
            pnlRight.Controls.Add(btnConfirmPayment);

            this.Controls.Add(flpTables);
            this.Controls.Add(pnlCenter);
            this.Controls.Add(pnlRight);
        }

        private void LoadTablesToBoard()
        {
            flpTables.Controls.Clear();
            List<TableDTO> tables = _posDal.GetAllTables();
            foreach (TableDTO table in tables)
            {
                Button btn = new Button
                {
                    Width = 125,
                    Height = 95,
                    Margin = new Padding(8),
                    Text = $"{table.TableName}\n({table.Status})",
                    Tag = table,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = table.Status == "Trống"
                        ? Color.FromArgb(46, 204, 113)
                        : Color.FromArgb(230, 126, 34)
                };
                btn.Click += TableButton_Click;
                flpTables.Controls.Add(btn);
            }
        }

        private void LoadProductsToCombo()
        {
            List<ProductDTO> products = _posDal.GetActiveProducts();
            cboProducts.DataSource = products;
            cboProducts.DisplayMember = "ProductName";
            cboProducts.ValueMember = "ProductId";
        }

        private void TableButton_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn?.Tag is TableDTO table)
            {
                _selectedTableId = table.TableId;
                _selectedTableName = table.TableName;
                lblSelectedTable.Text = $"Đang chọn: {_selectedTableName} ({table.Status})";
                picVietQR.Image = null;
                LoadBillForSelectedTable();
            }
        }

        private void LoadBillForSelectedTable()
        {
            if (_selectedTableId == -1) return;
            DataTable dt = _posDal.GetBillDetailsByTable(_selectedTableId);
            dgvBillDetails.DataSource = dt;

            _currentRawTotal = 0;
            foreach (DataRow row in dt.Rows)
            {
                _currentRawTotal += Convert.ToDecimal(row["SubTotal"]);
            }
            UpdateTotalDisplay();
        }

        private void UpdateTotalDisplay()
        {
            decimal discount = nudDiscount.Value;
            decimal finalTotal = _currentRawTotal - (_currentRawTotal * discount / 100m);
            lblTotalAmount.Text = $"Tổng tiền: {finalTotal:N0} VNĐ";
        }

        private void btnAddItem_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1)
            {
                MessageBox.Show("Vui lòng chọn một bàn trước khi gọi món!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboProducts.SelectedValue == null) return;

            int productId = Convert.ToInt32(cboProducts.SelectedValue);
            int quantity = Convert.ToInt32(nudQuantity.Value);

            _posDal.AddItemToTable(_selectedTableId, productId, quantity);
            LoadTablesToBoard();
            LoadBillForSelectedTable();
        }

        private void btnGenerateQR_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Bàn này chưa có hóa đơn cần thanh toán!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            decimal discount = nudDiscount.Value;
            decimal finalTotal = _currentRawTotal - (_currentRawTotal * discount / 100m);

            try
            {
                string qrUrl = SmartPaymentService.BuildVietQrUrl(finalTotal, _selectedTableName);
                picVietQR.LoadAsync(qrUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tải ảnh VietQR: " + ex.Message, "Lỗi mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnConfirmPayment_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                MessageBox.Show("Không có hóa đơn để thanh toán!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int discount = Convert.ToInt32(nudDiscount.Value);
            if (_posDal.CheckoutTable(_selectedTableId, discount, out decimal finalPaid))
            {
                string paidTableName = _selectedTableName;
                picVietQR.Image = null;
                nudDiscount.Value = 0;
                LoadTablesToBoard();
                LoadBillForSelectedTable();

                SmartPaymentService.PlaySuccessSound();
                MessageBox.Show($"Đã thanh toán thành công {paidTableName} với số tiền {finalPaid:N0} VNĐ!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}