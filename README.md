# 窗口透明度控制器

一个轻量的 Windows 桌面工具,用于将任意窗口设置为指定透明度。

## 功能

- 滑动条调节透明度(1%–100%)
- 点击"虚化"后,再点击目标窗口即可将其变为透明
- 拖动滑动条实时更新已虚化窗口的透明度
- 点击"解除虚化"恢复窗口原始样式

## 运行

```bash
dotnet run
```

## 发布

```bash
dotnet publish HideWindow\HideWindow.csproj -c Release -r win-x64 --self-contained false -o bin\publish
```


## 换肤

应用图标位于 `HideWindow\app.ico`

更换ico图标以自定义

皮肤文件位于 `HideWindow\skin\` 下:

- 放置任意一张背景图(jpg/png/bmp/gif/webp)
- 编辑 `background.cfg` 调整暗化和模糊参数
- 重新发布即可生效


## 重新发布-单文件(仍需系统运行时)-推荐

输出 `bin\publish\HideWindow.exe`(依赖系统 .NET 8 运行时)

### 注意:  

1. 运行该程序需要.NET 8 运行时,出现
 > **You must install .NET to run this application.**

​      就说明缺少环境,网上搜一个下载


2. 重新发布时记得关闭程序

```bash
dotnet publish HideWindow\HideWindow.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o bin\publish
```

## 重新发布-单文件(免装运行时)

```bash
dotnet publish HideWindow\HideWindow.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin\publish
```


# 说明

如图双模式

## 点击下一个

点击虚化后点击的下一个窗口将被虚化变为半透明,
可在过程中随时调整窗口的透明度,
再次点击取消虚化.

[![Snipaste_2026-08-18_17-35-24](https://github.com/SCP-GHOST/HideWindow/blob/main/pic/1.png)]

## 预选

点击刷新找到最新窗口,可多选上限十个.
点击虚化按钮虚化窗口,并且可在过程中随时调整每个窗口的透明度

![Snipaste_2026-08-18_17-38-31](https://github.com/SCP-GHOST/HideWindow/blob/main/pic/2.png)





