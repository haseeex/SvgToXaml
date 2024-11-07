using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GalaSoft.MvvmLight.Messaging;
using SvgToXaml.Command;
using SvgToXaml.Models;

namespace SvgToXaml.ViewModels
{
    public abstract class ImageBaseViewModel : ViewModelBase
    {
        protected ImageBaseViewModel(string filepath)
        {
            Filepath = filepath;
            OpenDetailCommand = new DelegateCommand(OpenDetailExecute);
            ReadSvgCmd = new DelegateCommand(ReadSvg);
            OpenFileCommand = new DelegateCommand(OpenFileExecute);
        }

        public string Filepath { get; }
        public string Filename => Path.GetFileName(Filepath);
        public ImageSource PreviewSource => GetImageSource();
        public ICommand OpenDetailCommand { get; set; }
        public ICommand ReadSvgCmd { get; set; }
        public ICommand OpenFileCommand { get; set; }
        protected abstract ImageSource GetImageSource();
        public abstract bool HasXaml { get; }
        public abstract bool HasSvg { get; }
        public string SvgDesignInfo => GetSvgDesignInfo();

        private void OpenDetailExecute()
        {
            OpenDetailWindow(this);
        }

        public static void OpenDetailWindow(ImageBaseViewModel imageBaseViewModel)
        {
            new DetailWindow { DataContext = imageBaseViewModel }.Show();
        }

        private void OpenFileExecute()
        {
            Process.Start(Filepath);
        }

        protected abstract string GetSvgDesignInfo();


        private void ReadSvg()
        {
            Messenger.Default.Send(new VCSvgModel() 
            {
                文件名 = Filename,
                图片 = PreviewSource,
                设计尺寸 = SvgDesignInfo,
                文件路径 = Filepath
            });
        }
    }
}