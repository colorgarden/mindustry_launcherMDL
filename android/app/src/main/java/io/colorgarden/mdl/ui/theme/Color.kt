package io.colorgarden.mdl.ui.theme

import androidx.compose.ui.graphics.Color

// ZalithLauncher teal palette
val ZalithBg = Color(0xFFBDD9DD)           // 背景 - 柔和青绿
val ZalithTopBar = Color(0xFF2D8B9C)        // 顶栏 - 深青绿
val ZalithCard = Color(0xFFEBF4F3)          // 卡片 - 极浅青绿
val ZalithDialog = Color(0xFFF4F4F4)        // 对话框 - 近白
val ZalithButton = Color(0xFFDDE0E1)        // 按钮 - 浅灰
val ZalithText = Color(0xFF0E0E0E)          // 主文字 - 近黑
val ZalithIcon = Color(0xFF151515)          // 图标 - 深灰
val ZalithStatusBar = Color(0xFFEDEDED)     // 状态栏 - 浅灰
val ZalithErrorBg = Color(0xFFDDBDBD)       // 错误背景 - 浅红
val ZalithFavorite = Color(0xFFFA7C7C)      // 收藏 - 珊瑚红

// Material3 color roles mapped from ZalithLauncher
val Primary = ZalithTopBar
val OnPrimary = Color(0xFFFFFFFF)
val PrimaryContainer = Color(0xFFCEE8ED)
val OnPrimaryContainer = Color(0xFF002026)

val Secondary = Color(0xFF4A6268)
val OnSecondary = Color(0xFFFFFFFF)
val SecondaryContainer = Color(0xFFCDE8EE)
val OnSecondaryContainer = Color(0xFF051F24)

val Tertiary = Color(0xFF54607A)
val OnTertiary = Color(0xFFFFFFFF)

val Error = Color(0xFFBA1A1A)
val OnError = Color(0xFFFFFFFF)
val ErrorContainer = ZalithErrorBg
val OnErrorContainer = Color(0xFF410002)

// Surface & Background
val Surface = ZalithCard
val OnSurface = ZalithText
val SurfaceVariant = ZalithButton
val OnSurfaceVariant = Color(0xFF3F494C)
val SurfaceContainer = Color(0xFFF0F4F3)
val SurfaceContainerHigh = ZalithDialog

val Background = ZalithBg
val OnBackground = ZalithText

val Outline = Color(0xFF6F797C)
val OutlineVariant = Color(0xFFBFC8CA)

// Semantic aliases for screens
val CardBackground = ZalithCard
val PageBackground = ZalithBg
val SuccessGreen = Color(0xFF2E7D32)
val WarningOrange = Color(0xFFF57F17)
