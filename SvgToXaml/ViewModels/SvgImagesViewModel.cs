using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using GalaSoft.MvvmLight.Messaging;
using HandyControl.Expression.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using SvgConverter;
using SvgToXaml.Command;
using SvgToXaml.Infrastructure;
using SvgToXaml.Models;
using SvgToXaml.TextViewer;
using static System.Net.Mime.MediaTypeNames;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SvgToXaml.ViewModels
{
    public class SvgImagesViewModel : ViewModelBase
    {
        private string _currentDir;
        private ObservableCollectionSafe<ImageBaseViewModel> _images;
        private ImageBaseViewModel _selectedItem;
        MainWindow view;

        public SvgImagesViewModel(MainWindow view)
        {
            this.view = view;
            _images = new ObservableCollectionSafe<ImageBaseViewModel>();
            OpenFileCommand = new DelegateCommand(OpenFileExecute);
            OpenFolderCommand = new DelegateCommand(OpenFolderExecute);
            ExportDirCommand = new DelegateCommand(ExportDirExecute);
            VC_ExportDirCommand = new DelegateCommand(VC_ExportDirExecute);
            InfoCommand = new DelegateCommand(InfoExecute);
            XamlConvertPngCmd = new DelegateCommand(XamlConvertPng);
            XamlConvertSvgCmd = new DelegateCommand(XamlConvertSvg);

            ContextMenuCommands = new ObservableCollection<Tuple<object, ICommand>>();
            ContextMenuCommands.Add(new Tuple<object, ICommand>("打开资源管理器", new DelegateCommand<string>(OpenExplorerExecute)));

            Messenger.Default.Register<VCSvgModel>(this, OnMessageReceived);

        }

        public SvgImagesViewModel()
        {
        }

        private void OpenFolderExecute()
        {
            //var folderDialog = new FolderBrowserDialog { Description = "打开文件夹", SelectedPath = CurrentDir, ShowNewFolderButton = false };
            //if (folderDialog.ShowDialog() == DialogResult.OK)
            //    CurrentDir = folderDialog.SelectedPath;
            //选择文件夹
            var dialog = new CommonOpenFileDialog("选择文件夹")
            {
                Multiselect = false,
                IsFolderPicker = true,
            };
            if (dialog.ShowDialog(view) == CommonFileDialogResult.Ok)
            {
                CurrentDir = dialog.FileName;
            }

        }

        private void OpenFileExecute()
        {
            var openDlg = new OpenFileDialog { CheckFileExists = true, Filter = "Svg-Files|*.svg*", Multiselect = false };
            if (openDlg.ShowDialog().GetValueOrDefault())
            {
                ImageBaseViewModel.OpenDetailWindow(new SvgImageViewModel(openDlg.FileName));
            }
        }

        private void ExportDirExecute()
        {
            string outFileName = Path.GetFileNameWithoutExtension(CurrentDir) + ".xaml"; 
            var saveDlg = new SaveFileDialog {AddExtension = true, DefaultExt = ".xaml", Filter = "Xaml-File|*.xaml", InitialDirectory = CurrentDir, FileName = outFileName};
            if (saveDlg.ShowDialog() == DialogResult.OK)
            {
                string namePrefix = null;

                bool useComponentResKeys = false;
                string nameSpaceName = null;
                var nameSpace = Microsoft.VisualBasic.Interaction.InputBox("Enter a NameSpace for using static ComponentResKeys (or leave empty to not use it)", "NameSpace");
                if (!string.IsNullOrWhiteSpace(nameSpace))
                {
                    useComponentResKeys = true;
                    nameSpaceName =
                        Microsoft.VisualBasic.Interaction.InputBox(
                            "Enter a Name of NameSpace for using static ComponentResKeys", "NamespaceName");
                }
                else
                {
                    namePrefix = Microsoft.VisualBasic.Interaction.InputBox("Enter a namePrefix (or leave empty to not use it)", "Name Prefix");
                    if (string.IsNullOrWhiteSpace(namePrefix))
                        namePrefix = null;

                }

                outFileName = Path.GetFullPath(saveDlg.FileName);
                var resKeyInfo = new ResKeyInfo
                {
                    XamlName = Path.GetFileNameWithoutExtension(outFileName),
                    Prefix = namePrefix,
                    UseComponentResKeys = useComponentResKeys,
                    NameSpace = nameSpace,
                    NameSpaceName = nameSpaceName,

                };
                File.WriteAllText(outFileName, ConverterLogic.SvgDirToXaml(CurrentDir, resKeyInfo, false));

                BuildBatchFile(outFileName, resKeyInfo);
            }
        }

        private void BuildBatchFile(string outFileName, ResKeyInfo compResKeyInfo)
        {
            if (MessageBox.Show(outFileName + "\n已写\n创建批处理文件以便下次自动化?",
                null, MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
            {
                var outputname = Path.GetFileNameWithoutExtension(outFileName);
                var outputdir = Path.GetDirectoryName(outFileName);
                var relOutputDir = FileUtils.MakeRelativePath(CurrentDir, PathIs.Folder, outputdir, PathIs.Folder);
                var svgToXamlPath =System.Reflection.Assembly.GetEntryAssembly().Location;
                var relSvgToXamlPath = FileUtils.MakeRelativePath(CurrentDir, PathIs.Folder, svgToXamlPath, PathIs.File);
                var batchText = $"{relSvgToXamlPath} BuildDict /inputdir \".\" /outputdir \"{relOutputDir}\" /outputname {outputname}";

                if (compResKeyInfo.UseComponentResKeys)
                {
                    batchText += $" /useComponentResKeys=true /compResKeyNSName={compResKeyInfo.NameSpaceName} /compResKeyNS={compResKeyInfo.NameSpace}";
                    WriteT4Template(outFileName);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(compResKeyInfo.Prefix))
                    {
                        batchText += " /nameprefix \"" + compResKeyInfo.Prefix + "\"";
                    }
                }

                batchText += "\r\npause";

                File.WriteAllText(Path.Combine(CurrentDir, "Update.cmd"), batchText);

                ////Copy ExeFile
                //var srcFile = Environment.GetCommandLineArgs().First();
                //var destFile = Path.Combine(CurrentDir, Path.GetFileName(srcFile));
                ////Console.WriteLine("srcFile:", srcFile);
                ////Console.WriteLine("destFile:", destFile);
                //if (!string.Equals(srcFile, destFile, StringComparison.OrdinalIgnoreCase))
                //{
                //    Console.WriteLine("Copying file...");
                //    File.Copy(srcFile, destFile, true);
                //}
            }
        }

        private void WriteT4Template(string outFileName)
        {
            //BuildAction: "Embedded Resource"
            var appType = typeof(App);
            var assembly = appType.Assembly;
            //assembly.GetName().Name
            var resourceName = appType.Namespace + "." + "Payload.T4Template.tt"; //Achtung: hier Punkt statt Slash
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidDataException($"Error: {resourceName} not found in payload file");
            var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
            var t4FileName = Path.ChangeExtension(outFileName, ".tt");
            File.WriteAllText(t4FileName, text, Encoding.UTF8);
        }

        private void InfoExecute()
        {
            HandyControl.Controls.MessageBox.Info("SvgToXaml © 2015 Bernd Klaiber\n\nPowered by\nsharpvectors.codeplex.com (Svg-Support)\nicsharpcode (AvalonEdit)" +
                "\n\n\n重置 by.vancat" +
                "\n20240316-1505" +
                "\n1.添加批量转化为WPF专用资源,key名为文件名"+
                "\n2.移除剪贴板功能(因为win11有程序占用时会导致该功能闪退,无解)" +
                "\n3.优化UI交互,在主界面添加快速预览"+
                "\n4.修复原版筛选图片的错误,比如也读取名字带有svg字段的文件,压缩包之类的"+
                "\n5.移除Svg文件之外的媒体文件导入"+
                "\n6.使用新版文件选择界面"+
                "\n7.使用HandyContro控件库", "关于");
        }
        private void OpenExplorerExecute(string path)
        {
            Process.Start(path);
        }

        public static SvgImagesViewModel DesignInstance
        {
            get
            {
                var result = new SvgImagesViewModel();
                result.Images.Add(SvgImageViewModel.DesignInstance);
                result.Images.Add(SvgImageViewModel.DesignInstance);
                return result;
            }
        }

        public string CurrentDir
        {
            get { return _currentDir; }
            set
            {
                if (SetProperty(ref _currentDir, value))
                    ReadImagesFromDir(_currentDir);
            }
        }

        public ImageBaseViewModel SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

        public ObservableCollectionSafe<ImageBaseViewModel> Images
        {
            get { return _images; }
            set 
            { 
                SetProperty(ref _images, value); 
            }
        }

        public ICommand OpenFolderCommand { get; set; }
        public ICommand OpenFileCommand { get; set; }
        public ICommand ExportDirCommand { get; set; }
        public ICommand VC_ExportDirCommand { get; set; }
        public ICommand InfoCommand { get; set; }

        public ObservableCollection<Tuple<object, ICommand>> ContextMenuCommands { get; set; }

        private void ReadImagesFromDir(string folder)
        {
            //Images.Clear();
            //var svgFiles = ConverterLogic.SvgFilesFromFolder(folder);
            //var svgImages = svgFiles.Select(f => new SvgImageViewModel(f));

            //var graphicFiles = GetFilesMulti(folder, GraphicImageViewModel.SupportedFormats);
            //var graphicImages = graphicFiles.Select(f => new GraphicImageViewModel(f));

            //var allImages = svgImages.Concat<ImageBaseViewModel>(graphicImages).OrderBy(e=>e.Filepath);

            //Images.AddRange(allImages);

            //只显示Svg类型的,修复原本无法正确过滤svg文件导致出现svg字段的zip包
            Images.Clear();
            var svgFiles = ConverterLogic.SvgFilesFromFolder(folder);
            svgFiles = svgFiles.Where(file => Path.GetExtension(file).Equals(".svg", StringComparison.OrdinalIgnoreCase)).ToArray();
            var svgImages = svgFiles.Select(f => new SvgImageViewModel(f));
            Images.AddRange(svgImages);
        }

        private static string[] GetFilesMulti(string sourceFolder, string filters, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            try
            {
                if (!Directory.Exists(sourceFolder))
                    return new string[0];
                return filters.Split('|').SelectMany(filter => Directory.GetFiles(sourceFolder, filter, searchOption)).ToArray();
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        private void VC_ExportDirExecute()
        {
            var ouput = "";
            foreach (var it in Images)
            {
                var ig = it as SvgImageViewModel;
                //获取文件名(不需要扩展名)
                string keyname = Path.GetFileNameWithoutExtension(ig.SvgData.Filepath);

                var xaml = ig.SvgData.Xaml;
                var pattern = @"x:Key=""[^""]*"""; // 匹配 x:Key 后面的值
                var replacement = $"x:Key=\"{keyname}\""; // 替换为 x:Key="煞笔"
                var regex = new Regex(pattern);
                ouput += regex.Replace(xaml, replacement) + "\r\n\r\n";



            }
            //保存为文件
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Xaml文件|*.xaml";
            saveFileDialog.Title = "保存为Xaml文件";
            saveFileDialog.ShowDialog();
            if (saveFileDialog.FileName != "")
            {
                FileStream fs = (FileStream)saveFileDialog.OpenFile();
                StreamWriter sw = new StreamWriter(fs);
                sw.Write(ouput);
                sw.Close();
                fs.Close();
            }

        }


        
        public ICommand XamlConvertPngCmd { get; set; }
        public ICommand XamlConvertSvgCmd { get; set; }
        private void XamlConvertPng()
        {
            try
            {
                string xamlString = "<DrawingImage>...</DrawingImage>"; // 你的XAML字符串
                xamlString = view.XmlViewer.Text;
                //处理成合法的
                var pattern = @"x:Key=""[^""]*"""; // 匹配 x:Key 后面的值
                var replacement = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""; // 替换为 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                var regex = new Regex(pattern);
                xamlString = regex.Replace(xamlString, replacement) + "\r\n\r\n";

                DrawingImage drawingImage = new DrawingImage();
                drawingImage = (DrawingImage)XamlReader.Parse(xamlString);

                // 创建一个 RenderTargetBitmap 对象
                RenderTargetBitmap rtb = new RenderTargetBitmap((int)drawingImage.Width, (int)drawingImage.Height, 96, 96, PixelFormats.Pbgra32);

                // 创建一个 DrawingVisual 对象，并将 DrawingImage 绘制到其中
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(drawingImage, new Rect(0, 0, drawingImage.Width, drawingImage.Height));
                }

                // 将 DrawingVisual 渲染到 RenderTargetBitmap
                rtb.Render(drawingVisual);

                // 创建一个 PngBitmapEncoder，并添加 BitmapSource
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                //显示窗口保存文件
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "PNG文件|*.png";
                saveFileDialog.FileName = "图片";
                saveFileDialog.Title = "保存为PNG文件";
                saveFileDialog.ShowDialog();
                if (saveFileDialog.FileName != "")
                {
                    FileStream fs = (FileStream)saveFileDialog.OpenFile();
                    encoder.Save(fs);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                // 处理或记录异常
                
            }
        }
        private void XamlConvertSvg()
        {
            //try
            //{
            //    string xamlString = "<DrawingImage>...</DrawingImage>"; // 你的XAML字符串
            //    xamlString = view.XmlViewer.Text;
            //    //处理成合法的
            //    var pattern = @"x:Key=""[^""]*"""; // 匹配 x:Key 后面的值
            //    var replacement = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""; // 替换为 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            //    var regex = new Regex(pattern);
            //    xamlString = regex.Replace(xamlString, replacement) + "\r\n\r\n";

            //    DrawingImage drawingImage = new DrawingImage();
            //    drawingImage = (DrawingImage)XamlReader.Parse(xamlString);

            //    // 将 DrawingImage 转换为 SVG 字符串
            //    string svgString = ConvertDrawingImageToSvg(drawingImage);

            //    // 将 SVG 字符串写入到文件
            //    File.WriteAllText("output.svg", svgString);

            //    //显示窗口保存文件
            //    SaveFileDialog saveFileDialog = new SaveFileDialog();
            //    saveFileDialog.Filter = "PNG文件|*.png";
            //    saveFileDialog.FileName = "图片";
            //    saveFileDialog.Title = "保存为PNG文件";
            //    saveFileDialog.ShowDialog();
            //    if (saveFileDialog.FileName != "")
            //    {
            //        FileStream fs = (FileStream)saveFileDialog.OpenFile();
            //        encoder.Save(fs);
            //        fs.Close();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    // 处理或记录异常

            //}
        }

        private string _文件名;
        public string 文件名
        {
            get { return _文件名; }
            set { SetProperty(ref _文件名, value);  }
        }
        public ImageSource _图片;
        public ImageSource 图片
        {
            get { return _图片; }
            set 
            {
                if (value != null)
                {
                    有图片 = true;
                }
                else
                {
                    有图片 = false;
                }
                SetProperty(ref _图片, value); 
            }
        }
        private string _设计尺寸;
        public string 设计尺寸
        {
            get { return _设计尺寸; }
            set { SetProperty(ref _设计尺寸, value); }
        }
        private bool _有图片=false;
        public bool 有图片
        {
            get { return _有图片; }
            set { SetProperty(ref _有图片, value); }
        }

        private string _文件路径;
        public string 文件路径
        {
            get { return _文件路径; }
            set { SetProperty(ref _文件路径, value); }
        }
        private ConvertedSvgData _Svg解码 = new ConvertedSvgData();
        public ConvertedSvgData Svg解码
        {
            get { return _Svg解码; }
            set { SetProperty(ref _Svg解码, value); }
        }
        public ConvertedSvgData SvgData()
        {
            ConvertedSvgData _convertedSvgData=null;
            if (_convertedSvgData == null)
            {
                try
                {
                    _convertedSvgData = ConverterLogic.ConvertSvg(文件路径, ResultMode.DrawingImage);
                   
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return _convertedSvgData;
        }

        /// <summary>
        /// 通知事件
        /// </summary>
        public event Action<string> MessageSent;
        /// <summary>
        /// 通知处理
        /// </summary>
        /// <param name="pak"></param>
        private void OnMessageReceived(VCSvgModel pak)
        {
            // 处理接收到的消息...
            文件名 = pak.文件名;
            图片= pak.图片;
            设计尺寸 = pak.设计尺寸;
            文件路径 = pak.文件路径;
            Svg解码 = SvgData();
        }
    }
}
