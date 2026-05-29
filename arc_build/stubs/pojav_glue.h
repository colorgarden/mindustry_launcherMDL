#pragma once
#include <jni.h>
#include <android/native_window.h>
#include <android/native_window_jni.h>
#include <android/log.h>
#include <dlfcn.h>
#include <stdarg.h>
#include <cstdio>
#include <EGL/egl.h>
#include <GLES2/gl2.h>
#ifndef EGL_OPENGL_ES3_BIT
#define EGL_OPENGL_ES3_BIT 0x0040
#endif

static void sdl_log(const char* fmt, ...) {
    char buf[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(buf, sizeof(buf), fmt, ap);
    va_end(ap);
    __android_log_print(ANDROID_LOG_ERROR, "libsdl-arcarm64", "%s", buf);
    FILE* f = fopen("/sdcard/sdl_egl.log", "a");
    if (f) { fprintf(f, "%s\n", buf); fclose(f); }
}
#define LOGI(...) sdl_log(__VA_ARGS__)
#define LOGE(...) sdl_log(__VA_ARGS__)

static int gWidth = 1920;
static int gHeight = 1080;

static ANativeWindow* getSavedWindow() {
    void* lib = dlopen("libmdl_window.so", RTLD_NOW);
    if (lib) {
        typedef ANativeWindow* (*fn_t)(void);
        fn_t fn = (fn_t)dlsym(lib, "mdl_get_window");
        if (fn) { ANativeWindow* w = fn(); if (w) return w; }
    }
    return nullptr;
}

static void ensureGLContext() {
    static bool ready = false;
    if (ready) return;
    if (eglGetCurrentContext() != EGL_NO_CONTEXT) { ready = true; return; }

    EGLDisplay dpy = eglGetDisplay(EGL_DEFAULT_DISPLAY);
    if (dpy == EGL_NO_DISPLAY) { LOGE("eglGetDisplay failed"); return; }
    EGLint major, minor;
    eglInitialize(dpy, &major, &minor);
    LOGI("EGL v%d.%d", (int)major, (int)minor);

    ANativeWindow* win = getSavedWindow();
    bool hasWindow = (win != nullptr);
    if (hasWindow) {
        // gWidth/gHeight already set from ANativeWindow in SDL_CreateWindow
        // Set buffer to same resolution - no scaling, no letterboxing
        ANativeWindow_setBuffersGeometry(win, gWidth, gHeight, AHARDWAREBUFFER_FORMAT_R8G8B8A8_UNORM);
        LOGI("ANativeWindow %p buffer %dx%d", (void*)win, gWidth, gHeight);
    } else {
        LOGE("no native window, using PBuffer");
    }

    EGLint cfgAttrs[] = {
        EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT | EGL_OPENGL_ES3_BIT,
        EGL_SURFACE_TYPE, hasWindow ? EGL_WINDOW_BIT : EGL_PBUFFER_BIT,
        EGL_RED_SIZE,8,EGL_GREEN_SIZE,8,EGL_BLUE_SIZE,8,EGL_ALPHA_SIZE,8,
        EGL_DEPTH_SIZE,24,EGL_STENCIL_SIZE,8,EGL_NONE
    };
    EGLConfig cfg; EGLint n;
    if (!eglChooseConfig(dpy, cfgAttrs, &cfg, 1, &n) || n == 0) {
        hasWindow = false;
        EGLint fb[]={EGL_RENDERABLE_TYPE,EGL_OPENGL_ES2_BIT | EGL_OPENGL_ES3_BIT,EGL_SURFACE_TYPE,EGL_PBUFFER_BIT,EGL_RED_SIZE,8,EGL_GREEN_SIZE,8,EGL_BLUE_SIZE,8,EGL_ALPHA_SIZE,8,EGL_NONE};
        eglChooseConfig(dpy, fb, &cfg, 1, &n);
    }

    EGLContext ctx = eglCreateContext(dpy, cfg, EGL_NO_CONTEXT, (EGLint[]){EGL_CONTEXT_CLIENT_VERSION,3,EGL_NONE});
    if (ctx == EGL_NO_CONTEXT) { LOGE("eglCreateContext failed: %x", eglGetError()); return; }

    EGLSurface surf = EGL_NO_SURFACE;
    if (hasWindow) {
        surf = eglCreateWindowSurface(dpy, cfg, win, nullptr);
        if (surf == EGL_NO_SURFACE) { LOGE("eglCreateWindowSurface failed: %x", eglGetError()); hasWindow = false; }
    }
    if (!hasWindow)
        surf = eglCreatePbufferSurface(dpy, cfg, (EGLint[]){EGL_WIDTH,gWidth,EGL_HEIGHT,gHeight,EGL_NONE});
    if (surf == EGL_NO_SURFACE) { LOGE("no surface"); return; }

    if (!eglMakeCurrent(dpy, surf, surf, ctx)) { LOGE("eglMakeCurrent failed: %x", eglGetError()); return; }
    LOGI("EGL ready: %s %dx%d ctx=%p", hasWindow?"WINDOW":"PBUFFER", gWidth, gHeight, (void*)ctx);
    ready = true;
}
