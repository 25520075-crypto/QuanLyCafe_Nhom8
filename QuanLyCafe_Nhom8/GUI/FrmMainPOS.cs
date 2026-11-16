using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;
using QuanLyCafe_Nhom8.GUI;
using QuanLyCafe_Nhom8.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace QuanLyCafe_Nhom8.GUI
{
    public partial class FrmMainPOS : Form
    {
        private enum Channel { DineIn, TakeAway, ShopeeXanh, Grab }
        private const string StatusEmpty = "Trống";

        // Tầng dữ liệu và trạng thái màn hình POS
        private readonly PosServiceDAL _posDal = new PosServiceDAL();
        private List<ProductDTO> _allProducts = new List<ProductDTO>();
        private List<TableDTO> _cachedTables = new List<TableDTO>();
        private readonly Dictionary<int, int> _tableDiscounts = new Dictionary<int, int>();

        private int _selectedTableId = -1;
        private string _selectedTableName = "";
        private Channel _currentChannel = Channel.DineIn;
        private decimal _currentRawTotal = 0;
        private int _approvedDiscountPercent = 0;

        // Tính tổng tiền sau giảm giá tại một nơi duy nhất
        private decimal FinalTotal => _currentRawTotal - (_currentRawTotal * _approvedDiscountPercent / 100m);

        public FrmMainPOS()
        {
            InitializeComponent();
            BuildModernPosInterface();
            LoadAllProducts();
            SwitchChannelTab(Channel.DineIn, reloadFromDb: true);

            // Tự động bật luồng lắng nghe lệnh Telegram 2 chiều (/homnay, /homqua, /ban, /huymon)
            new TelegramBotService().StartTwoWayListener();
        }

        // ==================== HÀM KIỂM TRA & LÀM MỚI DỮ LIỆU ====================

        private static void Warn(string msg) =>
            MessageBox.Show(msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private static void Info(string msg, string title = "Thông báo") =>
            MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        private static void ShowError(string msg, string title = "Lỗi") =>
            MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        private bool EnsureHasBill(string warningMessage)
        {
            if (_selectedTableId == -1 || _currentRawTotal <= 0)
            {
                Warn(warningMessage);
                return false;
            }
            return true;
        }

        private void RefreshAll(bool reloadTablesFromDb = true)
        {
            LoadTablesToBoard(reloadTablesFromDb);
            LoadBillForSelectedTable();
        }

        private static Channel GetChannel(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return Channel.DineIn;
            bool Starts(string prefix) => tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            if (Starts("Mang về")) return Channel.TakeAway;
            if (Starts("Shopee") || Starts("XanhSM")) return Channel.ShopeeXanh;
            if (Starts("Grab")) return Channel.Grab;
            return Channel.DineIn;
        }

        // Kiểm tra món trên dòng hiện tại đã gửi xuống quầy pha chế hay chưa
        private bool IsItemSentToKitchen(DataGridViewRow row)
        {
            if (row == null) return false;

            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell.Value == null) continue;

                if (cell.Value is bool isSentBool)
                {
                    return isSentBool;
                }

                string val = cell.Value.ToString().Trim().ToLowerInvariant();

                // Nếu trạng thái đang là nháp / chưa gửi -> chưa gửi pha chế
                if (val.Contains("nháp") || val.Contains("chưa gửi") || val.Contains("mới gọi") || val.Contains("chờ xác nhận"))
                {
                    return false;
                }

                // Nếu trạng thái đã chuyển xuống bếp / pha chế / hoàn thành -> đã gửi pha chế
                if (val.Contains("đã gửi") || val.Contains("chờ pha") || val.Contains("đang pha") ||
                    val.Contains("đang làm") || val.Contains("hoàn thành") || val.Contains("đã xong") ||
                    val.Contains("pha chế") || val.Contains("đã phục vụ"))
                {
                    return true;
                }
            }

            return false;
        }

        // ==================== XỬ LÝ SƠ ĐỒ BÀN & THỰC ĐƠN ====================

        private void SwitchChannelTab(Channel channel, bool reloadFromDb = false)
        {
            _currentChannel = channel;
            HighlightTabs(_mainTabButtons, channel);

            if (cboPaymentMethod != null && cboPaymentMethod.Items.Count >= 4)
            {
                if (channel == Channel.ShopeeXanh) cboPaymentMethod.SelectedIndex = 2;
                else if (channel == Channel.Grab) cboPaymentMethod.SelectedIndex = 3;
                else cboPaymentMethod.SelectedIndex = 0;
            }

            LoadTablesToBoard(reloadFromDb);
        }

        private void LoadTablesToBoard(bool reloadFromDb = false)
        {
            try
            {
                if (reloadFromDb || _cachedTables.Count == 0)
                {
                    _cachedTables = _posDal.GetAllTables();
                }

                ClearAndDispose(flpTables);
                flpTables.SuspendLayout();

                foreach (TableDTO table in _cachedTables)
                {
                    if (GetChannel(table.TableName) != _currentChannel) continue;

                    bool isEmpty = table.Status == StatusEmpty;
                    bool isSelected = table.TableId == _selectedTableId;
                    string title = isSelected
                        ? $"✔ {table.TableName}\n({table.Status})"
                        : $"{table.TableName}\n({table.Status})";

                    Button btn = MakeButton(
                        title,
                        Point.Empty,
                        new Size(128, 96),
                        isEmpty ? Theme.Green : Theme.Orange,
                        TableButton_Click);

                    btn.Margin = new Padding(6);
                    btn.Tag = table;
                    btn.FlatAppearance.BorderSize = isSelected ? 4 : 0;
                    btn.FlatAppearance.BorderColor = Theme.Brown;

                    flpTables.Controls.Add(btn);
                }

                flpTables.ResumeLayout();
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tải sơ đồ bàn: " + ex.Message);
            }
        }

        private void LoadAllProducts()
        {
            try
            {
                _allProducts = _posDal.GetActiveProducts();
                BindProductList(_allProducts);
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tải danh sách thực đơn: " + ex.Message);
            }
        }

        private void BindProductList(List<ProductDTO> list)
        {
            cboProducts.DataSource = null;
            cboProducts.DataSource = list;
            cboProducts.DisplayMember = "ProductName";
            cboProducts.ValueMember = "ProductId";
        }

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
            if ((sender as Button)?.Tag is TableDTO table)
            {
                _selectedTableId = table.TableId;
                _selectedTableName = table.TableName;

                // Khôi phục % giảm giá đã duyệt của riêng bàn này
                _approvedDiscountPercent = _tableDiscounts.TryGetValue(_selectedTableId, out int savedDiscount) ? savedDiscount : 0;
                nudDiscount.Value = _approvedDiscountPercent;

                lblSelectedTable.Text = $"ĐANG PHỤC VỤ: {_selectedTableName.ToUpper()} ({table.Status})";
                picVietQR.Image = null;

                // Chỉ vẽ lại viền chọn bàn từ bộ nhớ đệm, không gọi lại DB
                LoadTablesToBoard(reloadFromDb: false);
                LoadBillForSelectedTable();
            }
        }

        private void LoadBillForSelectedTable()
        {
            if (_selectedTableId == -1) return;

            try
            {
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

                if (_currentRawTotal <= 0)
                {
                    _approvedDiscountPercent = 0;
                    _tableDiscounts.Remove(_selectedTableId);
                    nudDiscount.Value = 0;
                }

                UpdateTotalDisplay();
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tải chi tiết hóa đơn: " + ex.Message);
            }
        }

        private void UpdateTotalDisplay()
        {
            lblTotalAmount.Text = _approvedDiscountPercent > 0
                ? $"TỔNG THANH TOÁN (Giảm {_approvedDiscountPercent}%): {FinalTotal:N0} VNĐ"
                : $"TỔNG THANH TOÁN: {FinalTotal:N0} VNĐ";
        }

        // ==================== SỰ KIỆN NGHIỆP VỤ POS ====================

        private void btnAddItem_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1)
            {
                Warn("Vui lòng chọn một bàn / đơn trước khi gọi món!");
                return;
            }
            if (cboProducts.SelectedValue == null)
            {
                Warn("Không tìm thấy món phù hợp!");
                return;
            }

            try
            {
                int productId = Convert.ToInt32(cboProducts.SelectedValue);
                int quantity = Convert.ToInt32(nudQuantity.Value);

                _posDal.AddItemToTable(_selectedTableId, productId, quantity);
                RefreshAll();
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi thêm món: " + ex.Message);
            }
        }

        private void btnSendKitchen_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Chưa có món nào trong bàn để gửi pha chế!")) return;

            try
            {
                int sentCount = _posDal.SendTableOrderToKitchen(_selectedTableId);
                if (sentCount > 0)
                {
                    SmartPaymentService.PlaySuccessSound();
                    LoadBillForSelectedTable();
                    Info($"Đã gửi {sentCount} món của [{_selectedTableName}] xuống quầy pha chế!", "Đã gửi pha chế");
                }
                else
                {
                    Info("Tất cả các món của bàn này đều đã được gửi pha chế trước đó!");
                }
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi gửi pha chế: " + ex.Message);
            }
        }

        private void btnRemoveItem_Click(object sender, EventArgs e)
        {
            if (_selectedTableId == -1 || dgvBillDetails.CurrentRow == null)
            {
                Warn("Vui lòng chọn một món trong bảng hóa đơn để hủy!");
                return;
            }

            int billDetailId = Convert.ToInt32(dgvBillDetails.CurrentRow.Cells["BillDetailId"].Value);
            string itemName = dgvBillDetails.CurrentRow.Cells["Tên món"].Value.ToString();
            bool isSentToKitchen = IsItemSentToKitchen(dgvBillDetails.CurrentRow);
            string managerName = "";

            // Chỉ món ĐÃ GỬI PHA CHẾ mới bắt buộc nhập tài khoản Quản lý/Admin để duyệt hủy
            if (isSentToKitchen)
            {
                if (!RequestManagerApprovalDialog($"Hủy món ĐÃ GỬI PHA CHẾ [{itemName}] tại {_selectedTableName}", out managerName))
                {
                    return;
                }
            }

            try
            {
                _posDal.RemoveBillDetailItem(billDetailId, _selectedTableId);
                RefreshAll();

                if (isSentToKitchen)
                {
                    try
                    {
                        new TelegramBotService().GuiTin(
                            $"⚠️ CẢNH BÁO HỦY MÓN ĐÃ PHA CHẾ:\n" +
                            $"- Bàn/Đơn: {_selectedTableName}\n" +
                            $"- Món hủy: {itemName}\n" +
                            $"- Duyệt bởi: {managerName}\n" +
                            $"- Thời gian: {DateTime.Now:HH:mm dd/MM/yyyy}");
                    }
                    catch { }

                    Info($"Đã hủy món [{itemName}] (Duyệt bởi Quản lý: {managerName}).", "Đã hủy món");
                }
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi hủy món: " + ex.Message);
            }
        }

        private void btnApplyDiscount_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Bàn chưa có hóa đơn để áp dụng giảm giá!")) return;

            int requestedDiscount = Convert.ToInt32(nudDiscount.Value);
            if (requestedDiscount == 0)
            {
                _approvedDiscountPercent = 0;
                _tableDiscounts.Remove(_selectedTableId);
                UpdateTotalDisplay();
                return;
            }

            if (!RequestManagerApprovalDialog($"Giảm giá {requestedDiscount}% cho {_selectedTableName}", out string managerName))
            {
                nudDiscount.Value = _approvedDiscountPercent;
                return;
            }

            _approvedDiscountPercent = requestedDiscount;
            _tableDiscounts[_selectedTableId] = requestedDiscount;
            UpdateTotalDisplay();
            Info($"Đã duyệt giảm giá {_approvedDiscountPercent}% bởi Quản lý: {managerName}!", "Thành công");
        }

        private void btnMoveTable_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Vui lòng chọn bàn đang có khách cần chuyển đi!")) return;

            TableDTO targetTable = ShowVisualTableSelectorDialog(isMoveToEmptyMode: true);
            if (targetTable == null) return;

            try
            {
                int oldTableId = _selectedTableId;
                _posDal.MoveToEmptyTable(oldTableId, targetTable.TableId);

                if (_tableDiscounts.TryGetValue(oldTableId, out int discount))
                {
                    _tableDiscounts.Remove(oldTableId);
                    _tableDiscounts[targetTable.TableId] = discount;
                }

                _selectedTableId = targetTable.TableId;
                _selectedTableName = targetTable.TableName;
                RefreshAll();
                Info($"Đã chuyển sang [{_selectedTableName}] thành công!", "Chuyển bàn hoàn tất");
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi chuyển bàn: " + ex.Message);
            }
        }

        private void btnMergeTable_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Vui lòng chọn bàn đang có khách cần gộp!")) return;

            TableDTO targetTable = ShowVisualTableSelectorDialog(isMoveToEmptyMode: false);
            if (targetTable == null) return;

            try
            {
                int sourceTableId = _selectedTableId;
                _posDal.MergeOccupiedTables(sourceTableId, targetTable.TableId);

                _tableDiscounts.Remove(sourceTableId);
                _tableDiscounts.Remove(targetTable.TableId);
                _approvedDiscountPercent = 0;
                nudDiscount.Value = 0;

                _selectedTableId = targetTable.TableId;
                _selectedTableName = targetTable.TableName;
                RefreshAll();
                Info($"Đã gộp chung hóa đơn vào [{_selectedTableName}] thành công!", "Gộp bàn hoàn tất");
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi gộp bàn: " + ex.Message);
            }
        }

        private void btnGenerateQR_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Bàn / Đơn này chưa có hóa đơn cần thanh toán!")) return;

            cboPaymentMethod.SelectedIndex = 1;
            string qrUrl = SmartPaymentService.BuildVietQrUrl(FinalTotal, _selectedTableName);
            picVietQR.LoadAsync(qrUrl);
        }

        private void btnPrintBill_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Chưa có hóa đơn để in!")) return;

            try
            {
                string method = cboPaymentMethod.SelectedItem?.ToString() ?? "Tiền mặt";
                DataTable dtItems = _posDal.GetBillDetailsByTable(_selectedTableId);
                string receiptContent = BuildReceiptText(dtItems, method);
                ShowReceiptPreviewDialog(receiptContent);
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi tạo phiếu in: " + ex.Message);
            }
        }

        private void btnConfirmPayment_Click(object sender, EventArgs e)
        {
            if (!EnsureHasBill("Không có hóa đơn để thanh toán!")) return;

            string paymentMethod = cboPaymentMethod.SelectedItem?.ToString() ?? "Tiền mặt";
            decimal expectedTotal = FinalTotal;

            DialogResult confirm = MessageBox.Show(
                $"Xác nhận đã nhận đủ [{expectedTotal:N0} VNĐ] cho [{_selectedTableName}]?\n- Hình thức: {paymentMethod}\n- Bàn/Đơn sẽ tự động chuyển về trạng thái Trống.",
                "Xác nhận thanh toán",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                if (_posDal.CheckoutTable(_selectedTableId, _approvedDiscountPercent, paymentMethod, out decimal finalPaid))
                {
                    string paidTableName = _selectedTableName;
                    _tableDiscounts.Remove(_selectedTableId);
                    picVietQR.Image = null;
                    _approvedDiscountPercent = 0;
                    nudDiscount.Value = 0;
                    lblSelectedTable.Text = $"ĐÃ HOÀN TẤT: {paidTableName.ToUpper()} ({StatusEmpty})";

                    RefreshAll();
                    SmartPaymentService.PlaySuccessSound();
                    Info($"Đã thanh toán thành công {paidTableName}!\n- Số tiền: {finalPaid:N0} VNĐ\n- Nguồn tiền: {paymentMethod}\n- Trạng thái: Bàn đã Trống.", "Hoàn tất");
                }
                else
                {
                    Warn("Không thể hoàn tất thanh toán. Vui lòng kiểm tra lại trạng thái hóa đơn!");
                }
            }
            catch (Exception ex)
            {
                ShowError("Lỗi khi thanh toán: " + ex.Message);
            }
        }

        private void FrmMainPOS_Load(object sender, EventArgs e)
        {

        }
    }
}