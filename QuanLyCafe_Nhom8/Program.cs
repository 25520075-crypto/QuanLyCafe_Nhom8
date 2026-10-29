using System;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.GUI;

namespace QuanLyCafe_Nhom8
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FrmMainPOS());
        }
    }
}