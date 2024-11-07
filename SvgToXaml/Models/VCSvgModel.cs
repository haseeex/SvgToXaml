using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using SvgToXaml.ViewModels;

namespace SvgToXaml.Models
{
    public class VCSvgModel:ViewModelBase
    {
        public string 文件名 { get; set; }
        public ImageSource 图片 { get; set; }
        public string 设计尺寸 { get; set; }
        public string 文件路径 { get; set; }
    }
}
