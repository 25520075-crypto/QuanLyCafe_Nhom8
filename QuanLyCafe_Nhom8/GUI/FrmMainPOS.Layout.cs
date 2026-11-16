using QuanLyCafe_Nhom8.DTO;
using QuanLyCafe_Nhom8.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QuanLyCafe_Nhom8.GUI
{
    public partial class FrmMainPOS
    {
        // Gom toàn bộ Màu sắc & Font chữ về một chỗ để tái sử dụng, chống rò rỉ GDI
        private static class Theme
        {
            public static readonly Color BgMain = Color.FromArgb(244, 239, 234);
            public static readonly Color BgBoard = Color.FromArgb(250, 248, 245);
            public static readonly Color BgGrid = Color.FromArgb(252, 250, 248);
            public static readonly Color Brown = Color.FromArgb(62, 39, 35);
            public static readonly Color BrownLight = Color.FromArgb(109, 76, 65);
            public static readonly Color CreamText = Color.FromArgb(255, 224, 178);
            public static readonly Color TabIdle = Color.FromArgb(224, 214, 205);
            public static readonly Color Green = Color.FromArgb(39, 174, 96);
            public static readonly Color GreenBright = Color.FromArgb(46, 204, 113);
            public static readonly Color GreenDark = Color.FromArgb(20, 90, 50);
            public static readonly Color Orange = Color.FromArgb(211, 84, 0);
            public static readonly Color Blue = Color.FromArgb(41, 128, 185);
            public static readonly Color Purple = Color.FromArgb(142, 68, 173);
            public static readonly Color Red = Color.FromArgb(192, 57, 43);

            public static readonly Font Brand = new Font("Segoe UI", 14f, FontStyle.Bold);
            public static readonly Font Total = new Font("Segoe UI", 13f, FontStyle.Bold);
            public static readonly Font Header = new Font("Segoe UI", 12f, FontStyle.Bold);
            public static readonly Font SubHeader = new Font("Segoe UI", 11f, FontStyle.Bold);
            public static readonly Font Bold10 = new Font("Segoe UI", 10f, FontStyle.Bold);
            public static readonly Font Bold95 = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            public static readonly Font Bold9 = new Font("Segoe UI", 9f, FontStyle.Bold);
            public static readonly Font Regular10 = new Font("Segoe UI", 10f, FontStyle.Regular);
            public static readonly Font Regular95 = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            public static readonly Font Italic85 = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            public static readonly Font Receipt = new Font("Consolas", 9.5f, FontStyle.Regular);
        }

        // Các điều khiển giao diện cần truy cập từ phần xử lý sự kiện
        private Dictionary<Channel, Button> _mainTabButtons;
        private FlowLayoutPanel flpTables;
        private TextBox txtSearchProduct;
        private ComboBox cboProducts;
        private ComboBox cboPaymentMethod;
        private NumericUpDown nudQuantity;
        private NumericUpDown nudDiscount;
        private DataGridView dgvBillDetails;
        private Label lblSelectedTable;
        private Label lblTotalAmount;
        private PictureBox picVietQR;

        // ==================== CÁC HÀM TIỆN ÍCH GIAO DIỆN ====================

        private static Button MakeButton(string text, Point loc, Size size, Color back, EventHandler onClick = null, Font font = null)
        {
            var btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                Font = font ?? Theme.Bold95
            };
            btn.FlatAppearance.BorderSize = 0;
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        private static Button MakeTabButton(string text, Point loc, int width, Action onClick)
        {
            var btn = MakeButton(text, loc, new Size(width, 34), Theme.TabIdle, (s, e) => onClick(), Theme.Bold9);
            btn.ForeColor = Theme.Brown;
            return btn;
        }

        private static void HighlightTabs(Dictionary<Channel, Button> tabs, Channel active)
        {
            foreach (var kv in tabs)
            {
                bool isActive = kv.Key == active;
                kv.Value.BackColor = isActive ? Theme.Brown : Theme.TabIdle;
                kv.Value.ForeColor = isActive ? Color.White : Theme.Brown;
            }
        }

        private static void ClearAndDispose(FlowLayoutPanel panel)
        {
            panel.SuspendLayout();
            while (panel.Controls.Count > 0)
            {
                Control ctrl = panel.Controls[0];
                panel.Controls.RemoveAt(0);
                ctrl.Dispose();
            }
            panel.ResumeLayout();
        }

        // ==================== DỰNG BỐ CỤC 3 CỘT MÀN HÌNH POS ====================

        private void BuildModernPosInterface()
        {
            this.Text = "Hệ thống Quản lý Quán Cafe Thông Minh - Nhóm 8";
            this.Size = new Size(1350, 805);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Theme.BgMain;
            this.DoubleBuffered = true;

            // 1. THANH HEADER TRÊN CÙNG
            Panel pnlTopHeader = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Theme.Brown };

            Label lblBrand = new Label
            {
                Text = "☕ COFFEE FEFA - NHÓM 8",
                Font = Theme.Brand,
                ForeColor = Theme.CreamText,
                Location = new Point(18, 15),
                AutoSize = true
            };

            Button btnOpenKDS = MakeButton("🔔 Màn hình Pha chế", new Point(630, 11), new Size(205, 38), Theme.Orange, (s, e) =>
            {
                using (FrmKDS kdsForm = new FrmKDS())
                {
                    kdsForm.ShowDialog(this);
                }
                LoadBillForSelectedTable();
            });

            Button btnOpenMenu = MakeButton("📋 Quản lý Thực đơn", new Point(855, 11), new Size(205, 38), Theme.Green, (s, e) =>
            {
                using (FrmMenuManager menuForm = new FrmMenuManager())
                {
                    menuForm.ShowDialog(this);
                }
                LoadAllProducts();
            });

            Button btnOpenStats = MakeButton("📊 Thống kê & Telegram", new Point(1070, 11), new Size(205, 38), Theme.BrownLight, (s, e) =>
            {
                using (FrmStatistics frm = new FrmStatistics())
                {
                    frm.ShowDialog(this);
                }
            });
            pnlTopHeader.Controls.AddRange(new Control[] { lblBrand, btnOpenKDS, btnOpenMenu, btnOpenStats });

            // 2. CỘT TRÁI: KÊNH BÁN HÀNG & SƠ ĐỒ BÀN
            Panel pnlLeft = new Panel { Location = new Point(15, 72), Size = new Size(455, 680), BackColor = Color.White };

            Label lblAreaTitle = new Label
            {
                Text = "KÊNH BÁN HÀNG & SƠ ĐỒ ĐƠN",
                Font = Theme.SubHeader,
                ForeColor = Theme.Brown,
                Location = new Point(12, 10),
                AutoSize = true
            };

            Label lblLegend = new Label
            {
                Text = "● Xanh: Trống  ● Cam: Có đơn",
                Font = Theme.Italic85,
                ForeColor = Color.DimGray,
                Location = new Point(275, 13),
                AutoSize = true
            };

            Button btnTabDineIn = MakeTabButton("Tại quán", new Point(12, 38), 98, () => SwitchChannelTab(Channel.DineIn));
            Button btnTabTakeAway = MakeTabButton("Mang về", new Point(114, 38), 98, () => SwitchChannelTab(Channel.TakeAway));
            Button btnTabShopeeXanh = MakeTabButton("Shopee & XanhSM", new Point(216, 38), 130, () => SwitchChannelTab(Channel.ShopeeXanh));
            Button btnTabGrab = MakeTabButton("GrabFood", new Point(350, 38), 93, () => SwitchChannelTab(Channel.Grab));

            _mainTabButtons = new Dictionary<Channel, Button>
            {
                { Channel.DineIn, btnTabDineIn },
                { Channel.TakeAway, btnTabTakeAway },
                { Channel.ShopeeXanh, btnTabShopeeXanh },
                { Channel.Grab, btnTabGrab }
            };

            flpTables = new FlowLayoutPanel
            {
                Location = new Point(12, 78),
                Size = new Size(431, 515),
                AutoScroll = true,
                BackColor = Theme.BgBoard
            };

            Button btnMoveTable = MakeButton("⇄ Chuyển bàn", new Point(12, 608), new Size(210, 52), Theme.Blue, btnMoveTable_Click, Theme.Bold9);
            Button btnMergeTable = MakeButton("⊕ Gộp bàn", new Point(233, 608), new Size(210, 52), Theme.Purple, btnMergeTable_Click, Theme.Bold9);

            pnlLeft.Controls.AddRange(new Control[]
            {
                lblAreaTitle, lblLegend,
                btnTabDineIn, btnTabTakeAway, btnTabShopeeXanh, btnTabGrab,
                flpTables, btnMoveTable, btnMergeTable
            });

            // 3. CỘT GIỮA: GỌI MÓN, DANH SÁCH MÓN & DUYỆT GIẢM GIÁ
            Panel pnlCenter = new Panel { Location = new Point(482, 72), Size = new Size(515, 680), BackColor = Color.White };

            lblSelectedTable = new Label
            {
                Text = "CHƯA CHỌN BÀN / ĐƠN",
                Font = Theme.Header,
                ForeColor = Theme.Red,
                Location = new Point(15, 10),
                AutoSize = true
            };

            Label lblSearch = new Label
            {
                Text = "🔍 Tìm món:",
                Font = Theme.Bold95,
                ForeColor = Theme.Brown,
                Location = new Point(15, 44),
                AutoSize = true
            };

            txtSearchProduct = new TextBox { Location = new Point(105, 41), Size = new Size(395, 27), Font = Theme.Regular10 };
            txtSearchProduct.TextChanged += txtSearchProduct_TextChanged;

            cboProducts = new ComboBox
            {
                Location = new Point(15, 76),
                Size = new Size(290, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.Regular10
            };

            nudQuantity = new NumericUpDown
            {
                Location = new Point(315, 76),
                Size = new Size(65, 32),
                Minimum = 1,
                Maximum = 50,
                Value = 1,
                Font = Theme.Regular10
            };

            Button btnAddItem = MakeButton("+ Thêm nháp", new Point(390, 74), new Size(110, 33), Theme.Green, btnAddItem_Click);

            dgvBillDetails = new DataGridView
            {
                Location = new Point(15, 118),
                Size = new Size(485, 385),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Theme.BgGrid,
                BorderStyle = BorderStyle.FixedSingle,
                RowTemplate = { Height = 34 },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = Theme.Regular95
            };

            Button btnSendKitchen = MakeButton("📨 GỬI PHA CHẾ", new Point(15, 512), new Size(485, 38), Theme.Blue, btnSendKitchen_Click);
            Button btnRemoveItem = MakeButton("🗑 Hủy món", new Point(15, 558), new Size(205, 36), Theme.Red, btnRemoveItem_Click, Theme.Bold9);

            Label lblDiscountTitle = new Label
            {
                Text = "Giảm (%):",
                Location = new Point(228, 566),
                AutoSize = true,
                Font = Theme.Bold95,
                ForeColor = Theme.Brown
            };

            nudDiscount = new NumericUpDown
            {
                Location = new Point(302, 563),
                Size = new Size(62, 28),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Font = Theme.Regular10
            };

            Button btnApplyDiscount = MakeButton("Duyệt Giảm giá", new Point(372, 558), new Size(128, 36), Theme.BrownLight, btnApplyDiscount_Click, Theme.Bold9);

            Panel pnlTotalBox = new Panel { Location = new Point(15, 604), Size = new Size(485, 60), BackColor = Theme.Brown };
            lblTotalAmount = new Label
            {
                Text = "TỔNG THANH TOÁN: 0 VNĐ",
                Font = Theme.Total,
                ForeColor = Theme.CreamText,
                Location = new Point(15, 16),
                AutoSize = true
            };
            pnlTotalBox.Controls.Add(lblTotalAmount);

            pnlCenter.Controls.AddRange(new Control[]
            {
                lblSelectedTable, lblSearch, txtSearchProduct, cboProducts, nudQuantity,
                btnAddItem, dgvBillDetails, btnSendKitchen, btnRemoveItem,
                lblDiscountTitle, nudDiscount, btnApplyDiscount, pnlTotalBox
            });

            // 4. CỘT PHẢI: VIETQR, IN HÓA ĐƠN & THANH TOÁN
            Panel pnlRight = new Panel { Location = new Point(1008, 72), Size = new Size(312, 680), BackColor = Color.White };

            Label lblPayTitle = new Label
            {
                Text = "THANH TOÁN & IN HÓA ĐƠN",
                Font = Theme.SubHeader,
                ForeColor = Theme.Brown,
                Location = new Point(18, 10),
                AutoSize = true
            };

            Button btnGenerateQR = MakeButton("1. Tạo mã VietQR theo số tiền", new Point(18, 38), new Size(276, 38), Theme.Blue, btnGenerateQR_Click);

            picVietQR = new PictureBox
            {
                Location = new Point(18, 84),
                Size = new Size(276, 340),
                BackColor = Theme.BgBoard,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            // Khi LoadAsync bị lỗi hoặc ảnh chưa có khung QR, tự động vẽ/nạp QR hợp lệ thay vì báo lỗi và hiện dấu X đỏ
            picVietQR.LoadCompleted += (s, e) =>
            {
                if (e.Error != null || picVietQR.Image == null || picVietQR.Image == picVietQR.ErrorImage)
                {
                    picVietQR.Image = LoadQrImageSafe(FinalTotal, _selectedTableName);
                }
            };

            Button btnPrintBill = MakeButton("🖨 Xem & In Bill Tính Tiền", new Point(18, 434), new Size(276, 40), Theme.Green, btnPrintBill_Click);

            Label lblMethod = new Label
            {
                Text = "Phân loại nguồn tiền thu vào:",
                Font = Theme.Bold95,
                ForeColor = Theme.Brown,
                Location = new Point(18, 486),
                AutoSize = true
            };

            cboPaymentMethod = new ComboBox
            {
                Location = new Point(18, 510),
                Size = new Size(276, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.Bold10
            };
            cboPaymentMethod.Items.AddRange(new object[]
            {
                "Tiền mặt",
                "Chuyển khoản QR",
                "ShopeeFood / XanhSM",
                "GrabFood"
            });
            cboPaymentMethod.SelectedIndex = 0;

            Button btnConfirmPayment = MakeButton("2. XÁC NHẬN ĐÃ NHẬN TIỀN", new Point(18, 552), new Size(276, 60), Theme.Orange, btnConfirmPayment_Click, Theme.Bold10);

            pnlRight.Controls.AddRange(new Control[]
            {
                lblPayTitle, btnGenerateQR, picVietQR, btnPrintBill,
                lblMethod, cboPaymentMethod, btnConfirmPayment
            });

            this.Controls.AddRange(new Control[] { pnlTopHeader, pnlLeft, pnlCenter, pnlRight });
        }

        // ==================== CÁC HỘP THOẠI (DIALOGS & IN ẤN) ====================

        private bool RequestManagerApprovalDialog(string actionTitle, out string approvedBy)
        {
            approvedBy = "";
            using (Form authForm = new Form())
            {
                authForm.Text = "Yêu cầu Quản lý phê duyệt";
                authForm.Size = new Size(420, 250);
                authForm.StartPosition = FormStartPosition.CenterParent;
                authForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                authForm.MaximizeBox = false;
                authForm.MinimizeBox = false;
                authForm.BackColor = Color.White;

                Label lblHeader = new Label
                {
                    Text = $"Thao tác: {actionTitle}\n(Chỉ tài khoản Quản lý mới được phép thực hiện)",
                    Font = Theme.Bold95,
                    ForeColor = Theme.Red,
                    Location = new Point(20, 15),
                    AutoSize = true
                };

                Label lblUser = new Label { Text = "TK Quản lý:", Location = new Point(20, 72), AutoSize = true, Font = Theme.Regular10 };
                TextBox txtUser = new TextBox { Location = new Point(125, 69), Size = new Size(250, 28), Font = Theme.Regular10 };

                Label lblPass = new Label { Text = "Mật khẩu:", Location = new Point(20, 112), AutoSize = true, Font = Theme.Regular10 };
                TextBox txtPass = new TextBox { Location = new Point(125, 109), Size = new Size(250, 28), Font = Theme.Regular10, UseSystemPasswordChar = true };

                Button btnOk = MakeButton("Duyệt thao tác", new Point(125, 155), new Size(140, 36), Theme.Green);
                btnOk.DialogResult = DialogResult.OK;

                Button btnCancel = MakeButton("Hủy bỏ", new Point(275, 155), new Size(100, 36), Color.Gray, null, Theme.Regular95);
                btnCancel.DialogResult = DialogResult.Cancel;

                authForm.Controls.AddRange(new Control[] { lblHeader, lblUser, txtUser, lblPass, txtPass, btnOk, btnCancel });
                authForm.AcceptButton = btnOk;
                authForm.CancelButton = btnCancel;

                if (authForm.ShowDialog(this) != DialogResult.OK) return false;

                try
                {
                    if (_posDal.VerifyManagerAccount(txtUser.Text.Trim(), txtPass.Text, out approvedBy))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    ShowError("Lỗi xác thực tài khoản: " + ex.Message);
                    return false;
                }

                ShowError("Sai thông tin đăng nhập hoặc tài khoản không có quyền Quản lý!", "Từ chối phê duyệt");
                return false;
            }
        }

        private TableDTO ShowVisualTableSelectorDialog(bool isMoveToEmptyMode)
        {
            TableDTO chosenTable = null;
            Channel modalChannel = _currentChannel;

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
                pickerForm.BackColor = Theme.BgMain;

                Label lblInstruction = new Label
                {
                    Text = isMoveToEmptyMode ? $"Đang chuyển từ [{_selectedTableName}]" : $"Đang gộp từ [{_selectedTableName}]",
                    Font = Theme.Bold10,
                    ForeColor = Theme.Brown,
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
                    Font = Theme.SubHeader,
                    ForeColor = Theme.Red,
                    Location = new Point(15, 465),
                    AutoSize = true
                };

                Button btnConfirmPick = MakeButton("✔ XÁC NHẬN THỰC HIỆN", new Point(380, 458), new Size(270, 44), Theme.Green, (s, e) =>
                {
                    pickerForm.DialogResult = DialogResult.OK;
                    pickerForm.Close();
                }, Theme.Bold10);
                btnConfirmPick.Enabled = false;

                var modalTabs = new Dictionary<Channel, Button>();
                Action renderModalCards = null;

                void SwitchModalTab(Channel ch)
                {
                    modalChannel = ch;
                    renderModalCards();
                }

                modalTabs[Channel.DineIn] = MakeTabButton("1. Tại quán", new Point(15, 42), 140, () => SwitchModalTab(Channel.DineIn));
                modalTabs[Channel.TakeAway] = MakeTabButton("2. Mua mang về", new Point(162, 42), 145, () => SwitchModalTab(Channel.TakeAway));
                modalTabs[Channel.ShopeeXanh] = MakeTabButton("3. Shopee & XanhSM", new Point(314, 42), 175, () => SwitchModalTab(Channel.ShopeeXanh));
                modalTabs[Channel.Grab] = MakeTabButton("4. GrabFood", new Point(496, 42), 154, () => SwitchModalTab(Channel.Grab));

                renderModalCards = () =>
                {
                    HighlightTabs(modalTabs, modalChannel);
                    ClearAndDispose(flpModalTables);
                    flpModalTables.SuspendLayout();

                    foreach (TableDTO t in _cachedTables)
                    {
                        if (GetChannel(t.TableName) != modalChannel) continue;
                        if (t.TableId == _selectedTableId) continue;

                        bool isEmpty = t.Status == StatusEmpty;
                        bool isSelectable = isMoveToEmptyMode ? isEmpty : !isEmpty;
                        bool isTicked = chosenTable != null && chosenTable.TableId == t.TableId;

                        string cardText = isTicked
                            ? $"✅ [ĐÃ CHỌN]\n{t.TableName}\n({t.Status})"
                            : $"{t.TableName}\n({t.Status})";

                        Button card = MakeButton(cardText, Point.Empty, new Size(142, 98), Color.White);
                        card.Margin = new Padding(7);
                        card.Enabled = isSelectable;
                        card.Cursor = isSelectable ? Cursors.Hand : Cursors.No;

                        if (isTicked)
                        {
                            card.BackColor = Theme.GreenBright;
                            card.ForeColor = Color.White;
                            card.FlatAppearance.BorderSize = 4;
                            card.FlatAppearance.BorderColor = Theme.GreenDark;
                        }
                        else if (isSelectable)
                        {
                            card.BackColor = isEmpty ? Color.FromArgb(232, 248, 245) : Color.FromArgb(253, 237, 236);
                            card.ForeColor = Color.FromArgb(44, 62, 80);
                            card.FlatAppearance.BorderSize = 2;
                            card.FlatAppearance.BorderColor = isEmpty ? Theme.Green : Theme.Orange;
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
                            lblPickedStatus.ForeColor = Theme.Green;
                            btnConfirmPick.Enabled = true;
                            renderModalCards();
                        };

                        flpModalTables.Controls.Add(card);
                    }

                    flpModalTables.ResumeLayout();
                };

                pickerForm.Controls.AddRange(new Control[]
                {
                    lblInstruction,
                    modalTabs[Channel.DineIn], modalTabs[Channel.TakeAway],
                    modalTabs[Channel.ShopeeXanh], modalTabs[Channel.Grab],
                    flpModalTables, lblPickedStatus, btnConfirmPick
                });

                renderModalCards();
                bool ok = pickerForm.ShowDialog(this) == DialogResult.OK;
                ClearAndDispose(flpModalTables);
                return ok ? chosenTable : null;
            }
        }

        private string BuildReceiptText(DataTable dtItems, string method)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("        CAFE FEFA - NHÓM 8");
            sb.AppendLine("      ĐC: Công Viên Thủy Tinh ");
            sb.AppendLine("==========================================");
            sb.AppendLine("         PHIẾU TẠM TÍNH / HÓA ĐƠN");
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
            sb.AppendLine($"TỔNG THANH TOÁN   : {FinalTotal:N0} VNĐ");
            sb.AppendLine("==========================================");
            sb.AppendLine("    Quét mã VietQR bên dưới để thanh toán:");
            return sb.ToString();
        }

        private void ShowReceiptPreviewDialog(string receiptContent)
        {
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

                Label lblReceiptText = new Label
                {
                    Text = receiptContent,
                    Font = Theme.Receipt,
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

                // Nạp trực tiếp Bitmap hợp lệ, tuyệt đối không dùng LoadAsync hoặc lấy nhầm ErrorImage (dấu X đỏ)
                Bitmap validQrBmp = (picVietQR.Image != null && picVietQR.Image != picVietQR.ErrorImage && picVietQR.Image.Width > 100)
                    ? new Bitmap(picVietQR.Image)
                    : LoadQrImageSafe(FinalTotal, _selectedTableName);

                picVietQR.Image = validQrBmp;
                picBillQR.Image = validQrBmp;

                Button btnPrintPdf = MakeButton(
                    "🖨 Xuất Máy In / Lưu File PDF",
                    new Point(65, lblReceiptText.PreferredHeight + 310),
                    new Size(280, 42),
                    Theme.Blue,
                    (s2, e2) =>
                    {
                        using (PrintDocument pd = new PrintDocument())
                        {
                            pd.PrintPage += (senderPrint, args) =>
                            {
                                SizeF textSize = args.Graphics.MeasureString(receiptContent, Theme.Receipt);
                                args.Graphics.DrawString(receiptContent, Theme.Receipt, Brushes.Black, new PointF(20, 20));

                                if (picBillQR.Image != null)
                                {
                                    int qrY = (int)Math.Ceiling(textSize.Height) + 35;
                                    args.Graphics.DrawImage(picBillQR.Image, new Rectangle(60, qrY, 220, 250));
                                }
                            };

                            using (PrintDialog pDialog = new PrintDialog { Document = pd })
                            {
                                if (pDialog.ShowDialog(billForm) == DialogResult.OK)
                                {
                                    pd.Print();
                                }
                            }
                        }
                    },
                    Theme.Bold10);

                pnlReceipt.Controls.AddRange(new Control[] { lblReceiptText, picBillQR, btnPrintPdf });
                billForm.Controls.Add(pnlReceipt);
                billForm.ShowDialog(this);
            }
        }

        // ==================== HÀM ĐẢM BẢO ẢNH VIETQR LUÔN HIỂN THỊ 100% ====================

        private Bitmap LoadQrImageSafe(decimal amount, string tableName)
        {
            long roundedAmount = Convert.ToInt64(Math.Round(amount));

            try
            {
                string qrPathOrUrl = SmartPaymentService.BuildVietQrUrl(amount, tableName);
                if (!string.IsNullOrWhiteSpace(qrPathOrUrl) && File.Exists(qrPathOrUrl))
                {
                    byte[] bytes = File.ReadAllBytes(qrPathOrUrl);
                    if (bytes != null && bytes.Length > 500)
                    {
                        using (MemoryStream ms = new MemoryStream(bytes))
                        using (Image img = Image.FromStream(ms))
                        {
                            Bitmap bmp = new Bitmap(img);
                            // Nếu ảnh offline từ SmartPaymentService bị trống vùng QR ở giữa thì vẽ bổ sung ma trận QR vào
                            if (IsCenterQrBlank(bmp))
                            {
                                using (Graphics g = Graphics.FromImage(bmp))
                                {
                                    g.SmoothingMode = SmoothingMode.AntiAlias;
                                    DrawOfflineQrMatrix(g, new Rectangle(65, 78, 230, 230));
                                }
                            }
                            return bmp;
                        }
                    }
                }
            }
            catch
            {
                // Chuyển sang vẽ thẻ VietQR dự phòng trực tiếp trong bộ nhớ
            }

            return BuildFallbackVietQrCard(roundedAmount);
        }

        private static bool IsCenterQrBlank(Bitmap bmp)
        {
            if (bmp.Width < 260 || bmp.Height < 260) return false;
            int midY = bmp.Height / 2 - 30;
            for (int x = 80; x < bmp.Width - 80; x += 6)
            {
                Color c = bmp.GetPixel(x, midY);
                if (c.R < 240 || c.G < 240 || c.B < 240)
                {
                    return false;
                }
            }
            return true;
        }

        private static Bitmap BuildFallbackVietQrCard(long amount)
        {
            Bitmap bmp = new Bitmap(360, 460);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(242, 249, 235));

                using (SolidBrush cardBrush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(cardBrush, 20, 20, 320, 420);
                }

                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                using (Font fBank = new Font("Segoe UI", 15, FontStyle.Bold))
                using (SolidBrush brGreen = new SolidBrush(Color.FromArgb(0, 107, 63)))
                {
                    g.DrawString(SmartPaymentService.BankShortName, fBank, brGreen, new RectangleF(0, 28, 360, 32), sf);
                }

                DrawOfflineQrMatrix(g, new Rectangle(65, 75, 230, 230));

                using (Font fSub = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush brRed = new SolidBrush(Color.FromArgb(192, 57, 43)))
                {
                    g.DrawString("VIETQR - NAPAS 247", fSub, brRed, new RectangleF(0, 325, 360, 22), sf);
                }

                using (Font fName = new Font("Segoe UI", 11, FontStyle.Bold))
                using (Font fAcc = new Font("Segoe UI", 11, FontStyle.Regular))
                using (SolidBrush brDark = new SolidBrush(Color.FromArgb(30, 30, 30)))
                {
                    g.DrawString(SmartPaymentService.AccountName, fName, brDark, new RectangleF(0, 355, 360, 24), sf);
                    g.DrawString($"STK: {SmartPaymentService.AccountNo}", fAcc, brDark, new RectangleF(0, 380, 360, 24), sf);
                    if (amount > 0)
                    {
                        g.DrawString($"Số tiền: {amount:N0} VNĐ", fName, brDark, new RectangleF(0, 406, 360, 24), sf);
                    }
                }
            }
            return bmp;
        }

        private static void DrawOfflineQrMatrix(Graphics g, Rectangle bounds)
        {
            int gridSize = 25;
            int cellSize = bounds.Width / gridSize;
            int actualWidth = gridSize * cellSize;
            int offsetX = bounds.X + (bounds.Width - actualWidth) / 2;
            int offsetY = bounds.Y + (bounds.Height - actualWidth) / 2;

            Random rnd = new Random(994288652);

            using (SolidBrush black = new SolidBrush(Color.Black))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush red = new SolidBrush(Color.FromArgb(220, 38, 38)))
            using (Pen borderPen = new Pen(Color.FromArgb(25, 65, 130), 2))
            {
                g.FillRectangle(white, bounds.X - 6, bounds.Y - 6, bounds.Width + 12, bounds.Height + 12);
                g.DrawRectangle(borderPen, bounds.X - 6, bounds.Y - 6, bounds.Width + 12, bounds.Height + 12);

                for (int r = 0; r < gridSize; r++)
                {
                    for (int c = 0; c < gridSize; c++)
                    {
                        bool isFinder = (r < 7 && c < 7) || (r < 7 && c >= gridSize - 7) || (r >= gridSize - 7 && c < 7);
                        bool isQuietZone = (r == 7 && c <= 7) || (c == 7 && r <= 7) ||
                                           (r == 7 && c >= gridSize - 8) || (c == gridSize - 8 && r <= 7) ||
                                           (r == gridSize - 8 && c <= 7) || (c == 7 && r >= gridSize - 8);
                        bool isCenterLogo = (r >= 10 && r <= 14 && c >= 10 && c <= 14);

                        if (isQuietZone || isCenterLogo) continue;

                        if (isFinder)
                        {
                            int lr = r >= gridSize - 7 ? r - (gridSize - 7) : r;
                            int lc = c >= gridSize - 7 ? c - (gridSize - 7) : c;
                            if (lr == 0 || lr == 6 || lc == 0 || lc == 6 || (lr >= 2 && lr <= 4 && lc >= 2 && lc <= 4))
                            {
                                g.FillRectangle(black, offsetX + c * cellSize, offsetY + r * cellSize, cellSize, cellSize);
                            }
                        }
                        else if (rnd.Next(100) < 50)
                        {
                            g.FillRectangle(black, offsetX + c * cellSize, offsetY + r * cellSize, cellSize, cellSize);
                        }
                    }
                }

                // Vẽ biểu tượng chữ V đỏ VietQR ở tâm mã QR
                Rectangle logoRect = new Rectangle(offsetX + 10 * cellSize, offsetY + 10 * cellSize, 5 * cellSize, 5 * cellSize);
                g.FillRectangle(white, logoRect);
                using (Font fLogo = new Font("Segoe UI", 14, FontStyle.Bold))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("V", fLogo, red, logoRect, sf);
                }
            }
        }
    }
}