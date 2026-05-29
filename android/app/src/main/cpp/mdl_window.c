#include <jni.h>
#include <android/native_window_jni.h>
#include <android/native_window.h>
#include <android/log.h>
#include <string.h>
#include <pthread.h>

static ANativeWindow* gWindow = NULL;

// Shared event queue (SDL uses this from libsdl-arcarm64.so via dlsym)
#define MAX_EVENTS 256
typedef struct { int type; int i1,i2,i3,i4; } SdlEvent;
static SdlEvent gEvents[MAX_EVENTS];
static int gRead = 0, gWrite = 0;
static pthread_mutex_t gMutex = PTHREAD_MUTEX_INITIALIZER;

JNIEXPORT void JNICALL
Java_io_colorgarden_mdl_service_PojavRuntime_nSetSurface(JNIEnv* env, jclass clazz, jobject surface) {
    if (gWindow) { ANativeWindow_release(gWindow); gWindow = NULL; }
    if (surface) gWindow = ANativeWindow_fromSurface(env, surface);
}

JNIEXPORT jlong JNICALL
Java_io_colorgarden_mdl_service_PojavRuntime_nGetWindowPtr(JNIEnv* env, jclass clazz) {
    return (jlong)(uintptr_t)gWindow;
}

static int gTouchCount = 0;

JNIEXPORT void JNICALL
Java_io_colorgarden_mdl_service_PojavRuntime_nPushTouchEvent(JNIEnv* env, jclass clazz, jint action, jint x, jint y) {
    pthread_mutex_lock(&gMutex);
    int next = (gWrite + 1) % MAX_EVENTS;
    if (next != gRead) {
        if (action == 0 || action == 5) { // DOWN
            gEvents[gWrite].type = 0x401;
            gEvents[gWrite].i1 = 1; gEvents[gWrite].i2 = x; gEvents[gWrite].i3 = y;
            gWrite = next; next = (gWrite + 1) % MAX_EVENTS;
        } else if (action == 1 || action == 6) { // UP
            gEvents[gWrite].type = 0x402;
            gEvents[gWrite].i1 = 1; gEvents[gWrite].i2 = x; gEvents[gWrite].i3 = y;
            gWrite = next; next = (gWrite + 1) % MAX_EVENTS;
        }
        if (next != gRead) {
            gEvents[gWrite].type = 0x400; // MOTION
            gEvents[gWrite].i1 = x; gEvents[gWrite].i2 = y;
            gWrite = next;
        }
        gTouchCount++;
        if (gTouchCount <= 3) {
            __android_log_print(ANDROID_LOG_ERROR, "mdl_window", "touch: action=%d x=%d y=%d queue=%d",
                                (int)action, (int)x, (int)y, gWrite - gRead);
        }
    }
    pthread_mutex_unlock(&gMutex);
}

// Called from libsdl-arcarm64.so via dlsym
__attribute__((visibility("default")))
ANativeWindow* mdl_get_window(void) { return gWindow; }

__attribute__((visibility("default")))
int mdl_pop_event(int* type, int* i1, int* i2, int* i3, int* i4) {
    pthread_mutex_lock(&gMutex);
    if (gRead == gWrite) { pthread_mutex_unlock(&gMutex); return 0; }
    *type = gEvents[gRead].type;
    *i1 = gEvents[gRead].i1;
    *i2 = gEvents[gRead].i2;
    *i3 = gEvents[gRead].i3;
    *i4 = gEvents[gRead].i4;
    gRead = (gRead + 1) % MAX_EVENTS;
    pthread_mutex_unlock(&gMutex);
    return 1;
}
