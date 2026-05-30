LOCAL_PATH := $(call my-dir)
include $(CLEAR_VARS)
LOCAL_MODULE := preload_touch
LOCAL_LDLIBS := -ldl -llog
LOCAL_SRC_FILES := preload_touch.c
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := sdl_hook_jvm
LOCAL_LDLIBS := -ldl -llog
LOCAL_SRC_FILES := sdl_hook_jvm.c
include $(BUILD_SHARED_LIBRARY)
