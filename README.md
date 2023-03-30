# DesktopAppDoctor



## 一、简介

`DesktopAppDoctor` 是一款用来帮助开发者诊断 Windows 桌面应用程序未响应和崩溃问题的辅助工具。

此工具可以在后台运行，周期性地检测特定应用程序的工作状态，若应用程序未响应或者崩溃退出，则会自动导出当前系统和应用的相关信息和 Dump 文件并打包到指定位置。

此工具收集的相关信息如下：

- 系统 CPU 使用率
- 系统内存使用率
- 硬盘使用率
- 网卡使用率
- Windows 应用程序事件
- 应用程序 CPU 使用率
- 应用程序内存使用率

## 二、使用方法

启动软件后，切换到`诊断助手`Tab页，在关联进程一栏中填入待诊断进程名称（不包含.exe），然后点击保存即可。

可以单独配置是否检测崩溃、是否检测未响应。

## 三、截图

![image-20230329180606810](./imgs/image-20230329180606810.png)

![image-20230329180822004](./imgs/image-20230329180822004.png)
