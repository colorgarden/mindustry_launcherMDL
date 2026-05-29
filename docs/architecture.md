# Mindustry Mobile Launcher — 架构设计

## 项目结构

```
mindustry_mobile_launcher/
├── src/
│   ├── MindustryShared/        # 共享 C# 服务层 (类库)
│   ├── MindustryLauncher/      # MAUI 启动器 UI
│   └── MindustryRuntime/       # Android 原生游戏运行时
└── docs/
```

## 关键决策：为什么用 backend-android 而不是 backend-sdl

Arc 有两条渲染后端线：

| | backend-sdl (桌面) | backend-android (移动) |
|---|---|---|
| 渲染 | SDL_CreateWindow → OpenGL | GLSurfaceView → EGL/GLES |
| 平台检测 | `getType()` → `desktop` | `getType()` → `android` |
| 触屏 | SDL 翻译鼠标事件 | 原生 MotionEvent 处理 |
| 移动 UI | ❌ 不触发 | ✅ `isMobile()` → 大按钮、手势 |

**结论：** 通过 SDL → Android 跑桌面版会让游戏误判为桌面端，触屏体验极差。
直接复用 Arc 已有的 `backend-android`，多版本 ClassLoader 隔离即可。

## 分层架构

```
┌─────────────────────────────────────────────┐
│        MindustryLauncher (MAUI)             │
│  版本管理 │ 模组浏览 │ 蓝图 │ 设置 │ 联机   │
├─────────────────────────────────────────────┤
│        MindustryShared (C# 类库)            │
│  ConfigService │ RemoteDownloadService      │
│  ModService    │ SchematicService           │
│  MultiplayerService                         │
├─────────────────────────────────────────────┤
│        MindustryRuntime (Android Native)    │
│  ┌──────────────────────────────────────┐   │
│  │   AndroidLauncher Activity (Kotlin)  │   │
│  │   ├─ Custom ClassLoader (per ver)    │   │
│  │   ├─ Arc backend-android            │   │
│  │   │   ├─ AndroidApplication (Activity)│  │
│  │   │   ├─ GLSurfaceView + EGL/GLES   │   │
│  │   │   └─ AndroidInput (触屏/手势)    │   │
│  │   ├─ Mindustry core.jar (vX.Y)      │   │
│  │   └─ Arc natives (.so, ARM64)       │   │
│  └──────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

## 关键技术点

### 1. 多版本 ClassLoader 隔离
- 桌面版 .jar 是 JVM 字节码（依赖 backend-sdl），**无法直接在 Android 上加载**
- 每个 Mindustry 版本的 `:core` 模块需预先编译为 DEX（目标：Android ARM64）
- 运行时通过 `DexClassLoader` 加载版本特定 DEX，反射获取 `ApplicationListener`
- 共享 Arc `backend-android`（EGL/GLES + 触屏），整个 launcher 只编译一次

### 编译管线
```
Mindustry 源码 (vX.Y)
  → git checkout v146 / v147 / foo-client 分支
    → gradlew core:assembleAndroid  (每个版本输出 core.dex)
      → 存放于 instances/vXXX/core.dex
        → DexClassLoader 动态加载
```

### 2. 游戏运行流程
```
MAUI 点击"启动"
  → Intent 启动 AndroidLauncherActivity
    → 加载对应版本的 DEX + natives
    → 实例化 Arc AndroidApplication
    → setContentView(GLSurfaceView)
    → 游戏开始
  ← onDestroy() / exit()
  ← Broadcast 通知 MAUI 游戏已退出
```

### 3. MAUI ↔ Android Runtime 通信
- **启动**: MAUI 通过 Platform Intent 启动 AndroidLauncherActivity
- **状态**: Android ForegroundService 管理游戏进程生命周期
- **回调**: 游戏退出/崩溃通过 LocalBroadcast 回传 MAUI
- **数据路径**: 共享 `/data/data/io.colorgarden.mdl/` 下的文件系统

### 4. Native 层
- Arc 的 `natives-android` 和 `natives-freetype-android` 已编译 ARM64 .so
- 无需额外编译 SDL
- 使用 Android 系统的 EGL + GLES 实现

## 数据隔离

```
/data/data/io.colorgarden.mdl/
├── instances/
│   ├── v146/               # 版本 146 独立数据
│   │   ├── core.dex
│   │   ├── mods/
│   │   ├── saves/
│   │   ├── schematics/
│   │   └── settings.bin
│   ├── v147/
│   └── foo-client/
├── shared/
│   ├── arc/                # 共享 Arc framework (DEX)
│   └── natives/            # 共享 native .so
└── launcher_config.json
```
