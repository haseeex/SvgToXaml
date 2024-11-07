# 改动
核心代码所有权是  BerndK,只做以下改动:
1.添加批量转化为WPF专用资源,key名为文件名
2.移除剪贴板功能(因为win11有程序占用时会导致该功能闪退,无解)
3.优化UI交互,在主界面添加快速预览
4.修复原版筛选图片的错误,比如也读取名字带有svg字段的文件,压缩包之类
5.移除Svg文件之外的媒体文件导入
6.使用新版文件选择界面
7.使用HandyContro控件库替换原版UI

# SvgToXaml
用于查看 svg 文件并将其转换为 xaml 以供 .NET 使用的智能工具有 3 个主要用例：查看多个 svg 文件、仔细检查单个文件（查看其他信息、svg 源、xaml 代码）将 svg 文件转换为 xaml 批量转换多个 svg 文件

# 预览
![Main View](/Doc/New.png)

# 批量转换
这个想法是，您收集一些 svg 并希望在您的 .net 应用程序中使用它们。因此，只需将它们放在一个文件夹中，然后使用 SvgToXaml 将它们批量转换为一个 xaml 文件。SvgToXaml exe 文件被设计为“混合”应用程序。只需不带参数调用它，它就会作为 WPF 应用程序启动，当您指定参数时，它将更改为控制台应用程序。提供“/？”查看帮助，现在只有一个命令：“BuildDict”
```
>SvgToXaml.exe /? BuildDict
SvgToXaml - Tool to convert SVGs to a Dictionary
(c) 2015 Bernd Klaiber
   BuildDict  Creates a ResourceDictionary with the svg-Images of a folder
              Needed Arguments:
                /inputdir: specify folder of the graphic files to process
                /outputname: Name for the xaml outputfile
              Optional Params:
                /outputdir: folder for the xaml-Output, optional, default: folder of svgs
                /buildhtmlfile: Builds a htmlfile to browse the svgs, optional,default true
```
示例:
`..\..\SvgToXaml\bin\Debug\SvgToXaml.exe BuildDict /inputdir:.\svg /outputname:images /outputdir:.`
这是示例应用程序中包含的 cmd 文件的内容。它将在当前文件夹中生成一个名为“images.xaml”的 xaml 文件，其中包括文件夹“.\svg”中的所有 svg 文件。就是这样，之后，将文件“images.xaml”包含到您的应用程序中，将其合并到 app.xaml 中的资源字典中，然后您就可以在按钮中使用图标，例如：
```
<Button>
    <Image Source="{StaticResource cloud_3_iconDrawingImage}"/>
</Button>
```
添加更多新图标后，只需再次运行该命令，新图标就会出现在更新的 xaml 文件中。对于每个图标，都会创建颜色、路径和绘图图像（使用资源键），以便您可以随意使用它们。您可以一次更改所有图标的颜色（例如为您的应用设置主题），也可以单独更改所有图标的颜色（请参阅源中包含的示例应用）。
