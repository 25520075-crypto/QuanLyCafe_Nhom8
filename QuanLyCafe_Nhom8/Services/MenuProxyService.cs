using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Newtonsoft.Json;
using Protocol;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.Services
{
    public class MenuProxyService
    {
        private readonly MenuDAL _localDal = new MenuDAL();
        public bool UseNetworkClient { get; set; } = true;

        public DataTable GetAllCategories()
        {
            if (!UseNetworkClient) return _localDal.GetAllCategories();

            var res = NetworkClient.Instance.SendRequestAsync(MsgType.GET_CATEGORIES_REQ).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);

            var list = JsonConvert.DeserializeObject<List<CategoryDTO>>(res.PayloadJson) ?? new List<CategoryDTO>();
            DataTable dt = new DataTable();
            dt.Columns.Add("CategoryId", typeof(int));
            dt.Columns.Add("CategoryName", typeof(string));

            foreach (var c in list)
                dt.Rows.Add(c.CategoryId, c.CategoryName);
            return dt;
        }

        public DataTable GetProductsForManager(int categoryId, string keyword)
        {
            if (!UseNetworkClient) return _localDal.GetProductsForManager(categoryId, keyword);

            var res = NetworkClient.Instance.SendRequestAsync(MsgType.GET_ALL_PRODUCTS_REQ).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);

            var list = JsonConvert.DeserializeObject<List<ProductDTO>>(res.PayloadJson) ?? new List<ProductDTO>();
            if (categoryId > 0)
                list = list.Where(p => p.CategoryId == categoryId).ToList();
            if (!string.IsNullOrWhiteSpace(keyword))
                list = list.Where(p => (p.ProductName ?? "").ToLower().Contains(keyword.Trim().ToLower())).ToList();

            DataTable dt = new DataTable();
            dt.Columns.Add("ProductId", typeof(int));
            dt.Columns.Add("CategoryId", typeof(int));
            dt.Columns.Add("CategoryName", typeof(string));
            dt.Columns.Add("ProductName", typeof(string));
            dt.Columns.Add("Price", typeof(decimal));
            dt.Columns.Add("ImagePath", typeof(string));
            dt.Columns.Add("IsActive", typeof(bool));

            foreach (var p in list)
                dt.Rows.Add(p.ProductId, p.CategoryId, p.CategoryName, p.ProductName, p.Price, p.ImagePath, p.IsActive);
            return dt;
        }

        public bool AddCategory(string categoryName)
        {
            if (!UseNetworkClient) return _localDal.AddCategory(categoryName);
            var res = NetworkClient.Instance.SendRequestAsync(MsgType.ADD_CATEGORY_REQ, new { CategoryName = categoryName }).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);
            return true;
        }

        public bool InsertProduct(int categoryId, string name, decimal price, string imagePath, bool isActive)
        {
            if (!UseNetworkClient) return _localDal.InsertProduct(categoryId, name, price, imagePath, isActive);
            var res = NetworkClient.Instance.SendRequestAsync(MsgType.INSERT_PRODUCT_REQ, new
            {
                CategoryId = categoryId,
                ProductName = name,
                Price = price,
                ImagePath = imagePath,
                IsActive = isActive
            }).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);
            return true;
        }

        public bool UpdateProduct(int productId, int categoryId, string name, decimal price, string imagePath, bool isActive)
        {
            if (!UseNetworkClient) return _localDal.UpdateProduct(productId, categoryId, name, price, imagePath, isActive);
            var res = NetworkClient.Instance.SendRequestAsync(MsgType.UPDATE_PRODUCT_REQ, new
            {
                ProductId = productId,
                CategoryId = categoryId,
                ProductName = name,
                Price = price,
                ImagePath = imagePath,
                IsActive = isActive
            }).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);
            return true;
        }

        public bool ToggleProductStatus(int productId, bool isActive)
        {
            if (!UseNetworkClient) return _localDal.ToggleProductStatus(productId, isActive);
            var res = NetworkClient.Instance.SendRequestAsync(MsgType.TOGGLE_PRODUCT_REQ, new { ProductId = productId, IsActive = isActive }).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);
            return true;
        }

        public bool DeleteProduct(int productId)
        {
            if (!UseNetworkClient) return _localDal.DeleteProduct(productId);
            var res = NetworkClient.Instance.SendRequestAsync(MsgType.DELETE_PRODUCT_REQ, new { ProductId = productId }).GetAwaiter().GetResult();
            if (!res.Success) throw new Exception(res.Message);
            return true;
        }
    }
}
