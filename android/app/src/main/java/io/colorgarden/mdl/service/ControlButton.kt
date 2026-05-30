package io.colorgarden.mdl.service

data class ControlButtonConfig(
    val joystickEnabled: Boolean = true,
    val joystickCenterX: Float = 0.13f,
    val joystickCenterY: Float = 0.68f,
    val joystickRadius: Float = 0.12f,
    val buttons: List<ControlButton> = listOf(
        ControlButton("RMB", 0.82f, 0.75f, action = ControlAction.MouseRight, color = 0x44FF4444.toInt()),
        ControlButton("Q", 0.82f, 0.55f, action = ControlAction.KeyPress(20)),
        ControlButton("E", 0.82f, 0.65f, action = ControlAction.KeyPress(8))
    )
)

data class ControlButton(
    val label: String,
    val x: Float, val y: Float,
    val width: Float = 0.08f, val height: Float = 0.08f,
    val action: ControlAction = ControlAction.MouseLeft,
    val color: Int = 0x44666666
)

sealed class ControlAction {
    data class KeyPress(val sdlScancode: Int) : ControlAction()
    data object MouseLeft : ControlAction()
    data object MouseRight : ControlAction()
}
