using System;
using System.Data;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.Services
{
    public class MenuProxyService
    {
        private readonly MenuDAL _localDal = new MenuDAL();
        public bool UseNetworkClient { get; set; } = false;

        public DataTable GetAllCategories()
        {
            return _localDal.GetAllCategories();
        }

        public DataTable GetProductsForManager(int categoryId, string keyword)
        {
            return _localDal.GetProductsForManager(categoryId, keyword);
        }

        public bool AddCategory(string categoryName)
        {
            return _localDal.AddCategory(categoryName);
        }

        public bool InsertProduct(int categoryId, string name, decimal price, string imagePath, bool isActive)
        {
            return _localDal.InsertProduct(categoryId, name, price, imagePath, isActive);
        }

        public bool UpdateProduct(int productId, int categoryId, string name, decimal price, string imagePath, bool isActive)
        {
            return _localDal.UpdateProduct(productId, categoryId, name, price, imagePath, isActive);
        }

        public bool ToggleProductStatus(int productId, bool isActive)
        {
            return _localDal.ToggleProductStatus(productId, isActive);
        }

        public bool DeleteProduct(int productId)
        {
            return _localDal.DeleteProduct(productId);
        }
    }
}
