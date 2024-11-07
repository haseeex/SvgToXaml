using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using BKLib.CommandLineParser;

namespace SvgConverter
{
    public class CmdLineTarget : SimpleBaseTarget
    {
        [ArgumentCommand(LongDesc = "使用文件夹的 svg-Images 创建资源字典")]
        public int BuildDict(
            [ArgumentParam(Aliases = "i", Desc = "dir to the SVGs", LongDesc = "指定要处理的图形文件的文件夹")]
            string inputdir,
            [ArgumentParam(Aliases = "o", LongDesc = "xaml 输出文件的名称")]
            string outputname,
            [ArgumentParam(DefaultValue = null, ExplicitNeeded = false, LongDesc = "xaml 输出的文件夹，可选，默认：svgs 文件夹")]
            string outputdir = null,
            [ArgumentParam(LongDesc = "构建一个html文件来浏览svgs，可选，默认true")]
            bool buildhtmlfile = true,
            [ArgumentParam(DefaultValue = null, ExplicitNeeded = false, LongDesc = "命名该文件所有项目的前缀，可选，默认：无前缀")]
            string nameprefix = null,
            [ArgumentParam(DefaultValue = false, ExplicitNeeded = false, LongDesc = "如果为 true，则创建显式资源密钥文件，默认值： false", ExplicitWantedArguments = "resKeyNS,resKeyNSName")]
            bool useComponentResKeys = false,
            [ArgumentParam(DefaultValue = null, ExplicitNeeded = false, LongDesc = "与 Use Res Key 一起使用的命名空间")]
            string compResKeyNS = null,
            [ArgumentParam(DefaultValue = null, ExplicitNeeded = false, LongDesc = "与 Use Res Key 一起使用的命名空间的名称" )]
            string compResKeyNSName = null,
            [ArgumentParam(DefaultValue = false, ExplicitNeeded = false, LongDesc = "如果为 true，则过滤 Pixels Per Dip 以确保与 < 4.6.2 的兼容性，默认值： false")]
            bool filterPixelsPerDip = false,
            [ArgumentParam(DefaultValue = false, ExplicitNeeded = false, LongDesc = "递归遍历 inputdir 子文件夹")]
            bool handleSubFolders = false
            )
        {
            Console.WriteLine("构建资源字典...");
            var outFileName = Path.Combine(outputdir ?? inputdir, outputname);
            if (!Path.HasExtension(outFileName))
                outFileName = Path.ChangeExtension(outFileName, ".xaml");

            var resKeyInfo = new ResKeyInfo
            {
                Name = null,
                XamlName = Path.GetFileNameWithoutExtension(outputname),
                Prefix = nameprefix,
                UseComponentResKeys = useComponentResKeys,
                NameSpace = compResKeyNS,
                NameSpaceName = compResKeyNSName,
            };

            File.WriteAllText(outFileName, ConverterLogic.SvgDirToXaml(inputdir, resKeyInfo, null, filterPixelsPerDip, handleSubFolders));
            Console.WriteLine("xaml 写入到: {0}", Path.GetFullPath(outFileName));

            if (buildhtmlfile)
            {
                var htmlFilePath = Path.Combine(inputdir,
                    Path.GetFileNameWithoutExtension(outputname));
                var files = ConverterLogic.SvgFilesFromFolder(inputdir);
                BuildHtmlBrowseFile(files, htmlFilePath);
            }
            return 0; //no Error
        }

        private static void BuildHtmlBrowseFile(IEnumerable<string> files, string outputFilename, int size = 128)
        {
            //<html>
            //    <head>
            //        <title>Browse Images</title>
            //    </head>
            //    <body>
            //        Images in file xyz<br>
            //        <img src="cloud-17-icon.svg" title="Title" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //        <img src="cloud-17-icon.svg" height="128" width="128">
            //    </body>
            //</html>            
            var doc = new XDocument(
            new XElement("html",
                new XElement("head",
                    new XElement("title", "Browse svg images")),
                new XElement("body", $"Images in file: {outputFilename}",
                    new XElement("br"),
                    files.Select(
                    f => new XElement("img",
                        new XAttribute("src", Path.GetFileName(f) ?? ""),
                        new XAttribute("title", Path.GetFileNameWithoutExtension(f) ?? ""),
                        new XAttribute("height", size),
                        new XAttribute("width", size)
                        )
                    )
                )
            ));
            var filename = Path.ChangeExtension(outputFilename, ".html");
            doc.Save(filename);
            Console.WriteLine("Html 覆盖到 {0}", filename);
        }
    }
}
