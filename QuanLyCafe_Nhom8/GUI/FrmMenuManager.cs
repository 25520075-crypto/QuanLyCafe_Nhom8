using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.GUI
{
    public class FrmMenuManager : Form
    {
        private readonly MenuDAL _menuDal = new MenuDAL();
        private int _selectedProductId = -1;
        private string _selectedImagePath = "";

        // Controls bên trái (Danh sách & Bộ lọc)
        private ComboBox cboFilterCategory;
        private TextBox txtSearch;
        private DataGridView dgvProducts;
        private Label lblSummary;

        // Controls bên phải (Chi tiết món & Thao tác)
        private TextBox txtProductName;
        private ComboBox cboCategory;
        private NumericUpDown nudPrice;
        private CheckBox chkIsActive;
        private PictureBox picProduct;

        public FrmMenuManager()
        {
            BuildMenuManagerInterface();
            LoadCategoriesToCombos();
            LoadProductGrid();
        }

        private void BuildMenuManagerInterface()
        {
            this.Text = "Quản lý Thực đơn & Danh mục Món - COFFEE FEFA NHÓM 8";
            this.Size = new Size(1180, 680);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(244, 239, 234);
            this.DoubleBuffered = true;

            // 1. HEADER TRÊN CÙNG
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(62, 39, 35)
            };

            Label lblTitle = new Label
            {
                Text = "📋 QUẢN LÝ THỰC ĐƠN",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 224, 178),
                Location = new Point(20, 16),
                AutoSize = true
            };

            lblSummary = new Label
            {
                Text = "Tổng số: 0 món",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(880, 20),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSummary);

            // 2. CỘT TRÁI: BỘ LỌC & BẢNG DANH SÁCH MÓN
            Panel pnlLeft = new Panel
            {
                Location = new Point(15, 75),
                Size = new Size(715, 550),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblFilterCat = new Label
            {
                Text = "Lọc danh mục:",
                Location = new Point(15, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35)
            };

            cboFilterCategory = new ComboBox
            {
                Location = new Point(115, 14),
                Size = new Size(210, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };
            cboFilterCategory.SelectedIndexChanged += (s, e) => LoadProductGrid();

            Label lblSearch = new Label
            {
                Text = "🔍 Tìm tên món:",
                Location = new Point(345, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35)
            };

            txtSearch = new TextBox
            {
                Location = new Point(460, 14),
                Size = new Size(235, 28),
                Font = new Font("Segoe UI", 10f)
            };
            txtSearch.TextChanged += (s, e) => LoadProductGrid();

            dgvProducts = new DataGridView
            {
                Location = new Point(15, 55),
                Size = new Size(683, 475),
                ReadOnly = true,
                AllowUserToAddRows = false,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(252, 250, 248),
                BorderStyle = BorderStyle.FixedSingle,
                RowTemplate = { Height = 36 },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 9.5f)
            };
            dgvProducts.ColumnHeadersHeight = 38;
            dgvProducts.SelectionChanged += DgvProducts_SelectionChanged;

            pnlLeft.Controls.AddRange(new Control[]
            {
                lblFilterCat, cboFilterCategory, lblSearch, txtSearch, dgvProducts
            });

            // 3. CỘT PHẢI: CHI TIẾT MÓN, HÌNH ẢNH & CÁC NÚT CRUD
            Panel pnlRight = new Panel
            {
                Location = new Point(745, 75),
                Size = new Size(405, 550),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblFormTitle = new Label
            {
                Text = "THÔNG TIN CHI TIẾT MÓN",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 39, 35),
                Location = new Point(20, 14),
                AutoSize = true
            };

            Label lblName = CreateLabel("Tên món nước (*):", 20, 52);
            txtProductName = new TextBox
            {
                Location = new Point(20, 75),
                Size = new Size(360, 28),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
            };

            Label lblCat = CreateLabel("Thuộc danh mục (*):", 20, 115);
            cboCategory = new ComboBox
            {
                Location = new Point(20, 138),
                Size = new Size(255, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };

            Button btnAddCategory = CreateButton("+ Danh mục", new Point(282, 137), new Size(98, 29), Color.FromArgb(109, 76, 65), BtnAddCategory_Click);

            Label lblPrice = CreateLabel("Đơn giá bán (VNĐ) (*):", 20, 178);
            nudPrice = new NumericUpDown
            {
                Location = new Point(20, 201),
                Size = new Size(200, 28),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Minimum = 0,
                Maximum = 10000000,
                Increment = 1000,
                ThousandsSeparator = true,
                Value = 25000
            };

            chkIsActive = new CheckBox
            {
                Text = "Đang mở bán trên POS",
                Location = new Point(230, 204),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(39, 174, 96)
            };

            // Khung chọn ảnh minh họa
            Label lblImg = CreateLabel("Ảnh minh họa món:", 20, 242);
            picProduct = new PictureBox
            {
                Location = new Point(20, 266),
                Size = new Size(135, 120),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 245, 240),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            Button btnChooseImg = CreateButton("📁 Chọn ảnh...", new Point(170, 266), new Size(210, 36), Color.FromArgb(41, 128, 185), BtnChooseImg_Click);
            Button btnClearImg = CreateButton("✖ Xóa ảnh", new Point(170, 310), new Size(100, 34), Color.Gray, (s, e) =>
            {
                _selectedImagePath = "";
                picProduct.Image = null;
            });
            Button btnResetForm = CreateButton("🧹 Làm mới", new Point(280, 310), new Size(100, 34), Color.FromArgb(127, 140, 141), (s, e) => ClearInputForm());

            // 4 nút thao tác CRUD chính
            Button btnInsert = CreateButton("➕ THÊM MÓN MỚI", new Point(20, 405), new Size(175, 44), Color.FromArgb(39, 174, 96), BtnInsert_Click);
            Button btnUpdate = CreateButton("💾 LƯU CẬP NHẬT", new Point(205, 405), new Size(175, 44), Color.FromArgb(230, 126, 34), BtnUpdate_Click);
            Button btnToggle = CreateButton("🔄 BẬT / TẮT BÁN", new Point(20, 460), new Size(175, 44), Color.FromArgb(142, 68, 173), BtnToggle_Click);
            Button btnDelete = CreateButton("🗑 XÓA MÓN", new Point(205, 460), new Size(175, 44), Color.FromArgb(192, 57, 43), BtnDelete_Click);

            pnlRight.Controls.AddRange(new Control[]
            {
                lblFormTitle, lblName, txtProductName,
                lblCat, cboCategory, btnAddCategory,
                lblPrice, nudPrice, chkIsActive,
                lblImg, picProduct, btnChooseImg, btnClearImg, btnResetForm,
                btnInsert, btnUpdate, btnToggle, btnDelete
            });

            this.Controls.AddRange(new Control[] { pnlHeader, pnlLeft, pnlRight });
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(93, 64, 55)
            };
        }

        private static Button CreateButton(string text, Point loc, Size size, Color bgColor, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        // ==================== TẢI DỮ LIỆU & ĐỒNG BỘ BẢNG ====================

        private void LoadCategoriesToCombos()
        {
            DataTable dtCat = _menuDal.GetAllCategories();

            // Đổ vào ComboBox chi tiết bên phải
            cboCategory.DataSource = dtCat.Copy();
            cboCategory.DisplayMember = "CategoryName";
            cboCategory.ValueMember = "CategoryId";

            // Đổ vào ComboBox lọc bên trái (Có thêm mục 'Tất cả danh mục')
            DataTable dtFilter = dtCat.Copy();
            DataRow allRow = dtFilter.NewRow();
            allRow["CategoryId"] = 0;
            allRow["CategoryName"] = "--- Tất cả danh mục ---";
            dtFilter.Rows.InsertAt(allRow, 0);

            cboFilterCategory.DataSource = dtFilter;
            cboFilterCategory.DisplayMember = "CategoryName";
            cboFilterCategory.ValueMember = "CategoryId";
        }

        private void LoadProductGrid()
        {
            int filterCatId = 0;
            if (cboFilterCategory.SelectedValue != null && int.TryParse(cboFilterCategory.SelectedValue.ToString(), out int parsed))
            {
                filterCatId = parsed;
            }

            string kw = txtSearch != null ? txtSearch.Text : "";
            DataTable dt = _menuDal.GetProductsForManager(filterCatId, kw);
            dgvProducts.DataSource = dt;

            // Ẩn các cột kỹ thuật, chỉ giữ lại cột hiển thị đẹp cho người dùng
            if (dgvProducts.Columns["ProductId"] != null) dgvProducts.Columns["ProductId"].Visible = false;
            if (dgvProducts.Columns["CategoryId"] != null) dgvProducts.Columns["CategoryId"].Visible = false;
            if (dgvProducts.Columns["ImagePath"] != null) dgvProducts.Columns["ImagePath"].Visible = false;

            if (dgvProducts.Columns["STT"] != null) dgvProducts.Columns["STT"].FillWeight = 35;
            if (dgvProducts.Columns["Tên món nước"] != null) dgvProducts.Columns["Tên món nước"].FillWeight = 150;
            if (dgvProducts.Columns["Danh mục"] != null) dgvProducts.Columns["Danh mục"].FillWeight = 110;
            if (dgvProducts.Columns["Đơn giá (VNĐ)"] != null)
            {
                dgvProducts.Columns["Đơn giá (VNĐ)"].FillWeight = 80;
                dgvProducts.Columns["Đơn giá (VNĐ)"].DefaultCellStyle.Format = "N0";
            }
            if (dgvProducts.Columns["Đang bán"] != null) dgvProducts.Columns["Đang bán"].FillWeight = 55;

            int activeCount = 0;
            foreach (DataRow r in dt.Rows)
            {
                if (Convert.ToBoolean(r["Đang bán"])) activeCount++;
            }
            lblSummary.Text = $"Tổng: {dt.Rows.Count} món (Đang bán: {activeCount} | Ngừng bán: {dt.Rows.Count - activeCount})";
        }

        private void DgvProducts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvProducts.CurrentRow == null) return;

            DataGridViewRow row = dgvProducts.CurrentRow;
            _selectedProductId = Convert.ToInt32(row.Cells["ProductId"].Value);
            txtProductName.Text = row.Cells["Tên món nước"].Value.ToString();

            if (row.Cells["CategoryId"].Value != DBNull.Value)
            {
                cboCategory.SelectedValue = Convert.ToInt32(row.Cells["CategoryId"].Value);
            }

            decimal price = Convert.ToDecimal(row.Cells["Đơn giá (VNĐ)"].Value);
            nudPrice.Value = price >= nudPrice.Minimum && price <= nudPrice.Maximum ? price : 0;

            chkIsActive.Checked = Convert.ToBoolean(row.Cells["Đang bán"].Value);
            _selectedImagePath = row.Cells["ImagePath"].Value.ToString();

            LoadPreviewImage(_selectedImagePath);
        }

        private void LoadPreviewImage(string path)
        {
            picProduct.Image = null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        picProduct.Image = Image.FromStream(fs);
                    }
                }
                catch
                {
                    picProduct.Image = null;
                }
            }
        }

        private void ClearInputForm()
        {
            _selectedProductId = -1;
            txtProductName.Clear();
            nudPrice.Value = 25000;
            chkIsActive.Checked = true;
            _selectedImagePath = "";
            picProduct.Image = null;
            dgvProducts.ClearSelection();
            txtProductName.Focus();
        }

        // ==================== CÁC SỰ KIỆN THÊM / SỬA / XÓA / DANH MỤC ====================

        private void BtnChooseImg_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn hình ảnh món nước";
                ofd.Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _selectedImagePath = ofd.FileName;
                    LoadPreviewImage(_selectedImagePath);
                }
            }
        }

        private void BtnAddCategory_Click(object sender, EventArgs e)
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 380;
                prompt.Height = 175;
                prompt.Text = "Thêm danh mục thực đơn mới";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;

                Label lbl = new Label { Left = 20, Top = 20, Text = "Nhập tên danh mục mới:", AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
                TextBox txt = new TextBox { Left = 20, Top = 48, Width = 320, Font = new Font("Segoe UI", 10.5f) };
                Button btnOk = CreateButton("Lưu danh mục", new Point(190, 88), new Size(150, 34), Color.FromArgb(39, 174, 96), null);
                btnOk.DialogResult = DialogResult.OK;

                prompt.Controls.AddRange(new Control[] { lbl, txt, btnOk });
                prompt.AcceptButton = btnOk;

                if (prompt.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    if (_menuDal.AddCategory(txt.Text))
                    {
                        LoadCategoriesToCombos();
                        MessageBox.Show("Đã thêm danh mục mới thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void BtnInsert_Click(object sender, EventArgs e)
        {
            string name = txtProductName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Vui lòng nhập tên món nước!", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProductName.Focus();
                return;
            }
            if (cboCategory.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn danh mục cho món nước!", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int catId = Convert.ToInt32(cboCategory.SelectedValue);
                if (_menuDal.InsertProduct(catId, name, nudPrice.Value, _selectedImagePath, chkIsActive.Checked))
                {
                    LoadProductGrid();
                    ClearInputForm();
                    MessageBox.Show($"Đã thêm món [{name}] vào thực đơn!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi thêm món: " + ex.Message, "Lỗi SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (_selectedProductId == -1)
            {
                MessageBox.Show("Vui lòng chọn một món trong bảng bên trái để cập nhật!", "Chưa chọn món", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string name = txtProductName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Tên món nước không được để trống!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int catId = Convert.ToInt32(cboCategory.SelectedValue);
                if (_menuDal.UpdateProduct(_selectedProductId, catId, name, nudPrice.Value, _selectedImagePath, chkIsActive.Checked))
                {
                    LoadProductGrid();
                    MessageBox.Show($"Đã cập nhật thông tin món [{name}] thành công!", "Đã lưu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi cập nhật món: " + ex.Message, "Lỗi SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnToggle_Click(object sender, EventArgs e)
        {
            if (_selectedProductId == -1 || dgvProducts.CurrentRow == null)
            {
                MessageBox.Show("Vui lòng chọn một món trong danh sách!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool currentStatus = Convert.ToBoolean(dgvProducts.CurrentRow.Cells["Đang bán"].Value);
            string name = dgvProducts.CurrentRow.Cells["Tên món nước"].Value.ToString();

            if (_menuDal.ToggleProductStatus(_selectedProductId, currentStatus))
            {
                LoadProductGrid();
                string statusMsg = !currentStatus ? "MỞ BÁN TRỞ LẠI" : "TẠM NGỪNG BÁN";
                MessageBox.Show($"Đã chuyển món [{name}] sang trạng thái: {statusMsg}!", "Cập nhật trạng thái", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedProductId == -1 || dgvProducts.CurrentRow == null)
            {
                MessageBox.Show("Vui lòng chọn một món trong bảng để xóa!", "Chưa chọn món", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string name = dgvProducts.CurrentRow.Cells["Tên món nước"].Value.ToString();

            // Kiểm tra an toàn khóa ngoại: Món đã từng bán trong hóa đơn chưa?
            if (_menuDal.IsProductUsedInBills(_selectedProductId))
            {
                DialogResult softDel = MessageBox.Show(
                    $"Món [{name}] đã từng phát sinh trong lịch sử Hóa đơn bán hàng (BillDetails).\n\n" +
                    "Để bảo toàn dữ liệu thống kê doanh thu, bạn có muốn chuyển món này sang trạng thái [NGỪNG BÁN] thay vì xóa vĩnh viễn không?",
                    "Bảo toàn lịch sử Hóa đơn",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (softDel == DialogResult.Yes)
                {
                    _menuDal.ToggleProductStatus(_selectedProductId, true);
                    LoadProductGrid();
                    MessageBox.Show($"Đã ẩn món [{name}] khỏi màn hình bán hàng POS!", "Đã ngừng bán", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn XÓA VĨNH VIỄN món [{name}] khỏi thực đơn?",
                "Xác nhận xóa món",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    if (_menuDal.DeleteProduct(_selectedProductId))
                    {
                        LoadProductGrid();
                        ClearInputForm();
                        MessageBox.Show($"Đã xóa món [{name}] và cập nhật lại số thứ tự!", "Đã xóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xóa món: " + ex.Message, "Lỗi SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // FrmMenuManager
            // 
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.Name = "FrmMenuManager";
            this.Load += new System.EventHandler(this.FrmMenuManager_Load);
            this.ResumeLayout(false);

        }

        private void FrmMenuManager_Load(object sender, EventArgs e)
        {

        }
    }
}