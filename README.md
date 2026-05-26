# MDL - Mindustry Launcher

一个运行在 Windows 7+ 系统上的 Mindustry 启动器，支持多版本管理、模组浏览、蓝图下载和联机功能。[:us: English](README.en.md)

> 注意：本项目大部分内容由 AI 生成。

## 截图

![主界面](mindustry_luancher/Assets/screenshot.png)

## 系统要求

- Windows 7 或更高版本
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## 功能

### 多版本管理
- 创建、导入、重命名和删除 Mindustry 游戏实例
- 每个版本可独立配置隔离模式（数据文件夹是否独立）
- 支持多实例同时运行

### Java 配置
- 自动扫描系统中已安装的 Java（注册表、JAVA_HOME、PATH、常见目录）
- 支持全局 Java 路径和单版本自定义 Java 路径
- 支持自定义 JVM 参数
- 智能内存分配：根据系统物理内存自动计算，也可手动设置

### 模组浏览器
- 浏览 GitHub 上的 Mindustry 模组仓库列表
- 搜索过滤、按星数排序
- 查看模组详情（作者、描述、版本历史）
- 一键下载安装模组到当前实例
- 支持卸载已安装的模组

### 蓝图下载
- 从社区蓝图仓库获取 `.msch` 蓝图文件
- 多源切换
- 本地缓存，无需重复下载
- 一键安装蓝图到游戏 schematics 目录

### 联机大厅
- 基于 EasyTier 的 P2P 虚拟组网联机
- 创建房间（6 位数字房号）/ 加入房间
- 玩家列表实时显示
- 一键启动联机并加入游戏

### 存档管理
- 扫描本地 Mindustry 存档文件（`.msav`）
- 解析地图名、波次、作者、描述、游玩时间
- 支持删除存档

### Settings 编辑器
- 解析和编辑 Mindustry 的 `settings.bin` 二进制配置文件
- 表格化展示所有设置项
- 支持搜索过滤
- 自动备份原文件

### 其他
- **GitHub 加速** — 内置 6 种代理节点，解决国内网络问题
- **下载管理** — 支持从 GitHub Release 下载各版本 Mindustry
- **崩溃分析** — 启动失败时自动分析日志，导出错误报告
- **窗口记忆** — 自动保存和恢复窗口位置与大小

## 构建

```bash
git clone https://github.com/colorgarden/mindustry_luancherMDL.git
cd mindustry_luancherMDL
dotnet build
```

或使用 Visual Studio 2022+ 打开解决方案。

## 技术栈

- .NET 10 / C# 13
- WPF (Windows Presentation Foundation)
- 自定义 WindowChrome 无边框窗口
- 内置字体图标 (fontello)

## 许可证

AGPL-3.0 License
