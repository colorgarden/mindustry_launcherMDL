package com.movtery.zalithlauncher.game.input;

import androidx.annotation.Keep;
import dalvik.annotation.optimization.CriticalNative;

/**
 * Used by native code to detect CriticalNative support.
 */
@Keep
public class CriticalNativeTest {
    @Keep
    @CriticalNative
    public static native void testCriticalNative(int arg0, int arg1);

    @Keep
    public static void invokeTest() {
        testCriticalNative(0, 0);
    }
}
