#include "pojav_glue.h"
#include "SDL.h"
#include <cstring>
#include <cstdlib>
#include <cstdio>
#include <android/log.h>

// Touch event queue (reads from /sdcard/sdl_touch.dat written by PojavRuntime.kt)
#define SDL_EVENT_FILE "/sdcard/sdl_touch.dat"

struct TouchEvent { unsigned char action; short x, y; };

static TouchEvent gTouchBuf[16];
static int gTouchRead = 0, gTouchWrite = 0;
static int dbg_pop_cnt = 0;

static int popEvent(SDL_Event* event) {
    if (gTouchRead == gTouchWrite) {
        FILE* f = fopen(SDL_EVENT_FILE, "rb");
        if (!f) {
            if (dbg_pop_cnt++ < 3) __android_log_print(ANDROID_LOG_INFO, "SDLShim", "popEvent: file not found %s", SDL_EVENT_FILE);
            return 0;
        }
        fseek(f, 0, SEEK_END);
        long size = ftell(f);
        if (size < 5) { fclose(f); return 0; }
        if (dbg_pop_cnt++ < 3) __android_log_print(ANDROID_LOG_INFO, "SDLShim", "popEvent: file found, size=%ld bytes (%d events)", size, (int)(size/5));
        fseek(f, 0, SEEK_SET);

        gTouchRead = 0; gTouchWrite = 0;
        int count = (int)(size / 5);
        if (count > 16) count = 16;
        for (int i = 0; i < count; i++) {
            unsigned char raw[5];
            if (fread(raw, 1, 5, f) == 5) {
                gTouchBuf[i].action = raw[0];
                gTouchBuf[i].x = (raw[1] << 8) | raw[2];
                gTouchBuf[i].y = (raw[3] << 8) | raw[4];
                gTouchWrite++;
            }
        }
        fclose(f);

        // Truncate processed events from file
        long processed = count * 5;
        long remain = size - processed;
        if (remain > 0 && count >= 16) {
            f = fopen(SDL_EVENT_FILE, "rb");
            fseek(f, processed, SEEK_SET);
            char* buf = (char*)malloc(remain);
            fread(buf, 1, remain, f);
            fclose(f);
            f = fopen(SDL_EVENT_FILE, "wb");
            fwrite(buf, 1, remain, f);
            fclose(f);
            free(buf);
        } else {
            f = fopen(SDL_EVENT_FILE, "wb"); fclose(f);
        }

        if (gTouchWrite == 0) return 0;
    }

    TouchEvent* te = &gTouchBuf[gTouchRead];
    gTouchRead = (gTouchRead + 1) % 16;
    if (gTouchRead == gTouchWrite) { gTouchRead = gTouchWrite = 0; }

    static int dbg_evt_cnt = 0;
    memset(event, 0, sizeof(*event));
    if (te->action == 0 || te->action == 5) {
        event->type = 0x401; // SDL_MOUSEBUTTONDOWN
        event->button.button = 1; // SDL_BUTTON_LEFT
    } else if (te->action == 1 || te->action == 6) {
        event->type = 0x402; // SDL_MOUSEBUTTONUP
        event->button.button = 1; // SDL_BUTTON_LEFT
    } else if (te->action == 2) {
        event->type = 0x400; // SDL_MOUSEMOTION
    } else {
        return 0;
    }
    if (dbg_evt_cnt++ < 5) __android_log_print(ANDROID_LOG_INFO, "SDLShim", "popEvent: dispatching type=0x%x pos=%d,%d", event->type, te->x, te->y);

    event->button.x = te->x; event->button.y = te->y;
    event->motion.x = te->x; event->motion.y = te->y;
    return 1;
}

static SDL_Window gDummyWindow{0};

int SDL_Init(uint32_t flags) {
    (void)flags;
    return 0;
}

int SDL_InitSubSystem(uint32_t flags) { (void)flags; return 0; }
void SDL_QuitSubSystem(uint32_t flags) { (void)flags; }
int SDL_WasInit(uint32_t flags) { (void)flags; return 1; }
void SDL_Quit(void) {}
SDL_bool SDL_SetHint(const char *name, const char *value) { (void)name; (void)value; return SDL_TRUE; }
void SDL_GetVersion(SDL_version *ver) { if(ver){ ver->major=2; ver->minor=32; ver->patch=8; } }
const char* SDL_GetError(void) { return "stub"; }
int SDL_SetClipboardText(const char *text) { (void)text; return 0; }
const char* SDL_GetClipboardText(void) { return ""; }

SDL_Window* SDL_CreateWindow(const char *title, int x, int y, int w, int h, uint32_t flags) {
    (void)title; (void)x; (void)y; (void)flags;
    // Read user display config: width=X, height=Y
    FILE* cfg = fopen("/sdcard/MDL/display_config.txt", "r");
    int cfgW = 0, cfgH = 0;
    if (cfg) {
        char line[64];
        while (fgets(line, sizeof(line), cfg)) {
            int val;
            if (sscanf(line, "width=%d", &val) == 1) cfgW = val;
            if (sscanf(line, "height=%d", &val) == 1) cfgH = val;
        }
        fclose(cfg);
    }
    if (cfgW > 0 && cfgH > 0) { gWidth = cfgW; gHeight = cfgH; }
    else { if (w > 0) gWidth = w; if (h > 0) gHeight = h; }
    LOGI("SDL_CreateWindow %dx%d", gWidth, gHeight);
    return &gDummyWindow;
}
void SDL_DestroyWindow(SDL_Window *window) { (void)window; }
void SDL_SetWindowIcon(SDL_Window *window, SDL_Surface *icon) { (void)window; (void)icon; }
void SDL_RestoreWindow(SDL_Window *window) { (void)window; }
void SDL_MaximizeWindow(SDL_Window *window) { (void)window; }
void SDL_MinimizeWindow(SDL_Window *window) { (void)window; }
int SDL_SetWindowFullscreen(SDL_Window *window, uint32_t flags) { (void)window; (void)flags; return 0; }
void SDL_SetWindowBordered(SDL_Window *window, SDL_bool bordered) { (void)window; (void)bordered; }
void SDL_SetWindowSize(SDL_Window *window, int w, int h) { (void)window; if (w>0) gWidth=w; if (h>0) gHeight=h; }
void SDL_SetWindowPosition(SDL_Window *window, int x, int y) { (void)window; (void)x; (void)y; }
int SDL_GetWindowDisplayIndex(SDL_Window *window) { (void)window; return 0; }
int SDL_GetDisplayUsableBounds(int displayIndex, SDL_Rect *rect) {
    (void)displayIndex;
    if (rect) { rect->x = 0; rect->y = 0; rect->w = gWidth; rect->h = gHeight; }
    return 0;
}
int SDL_GetDisplayBounds(int displayIndex, SDL_Rect *rect) { return SDL_GetDisplayUsableBounds(displayIndex, rect); }
int SDL_GetCurrentDisplayMode(int displayIndex, SDL_DisplayMode *mode) {
    (void)displayIndex;
    if (mode) { mode->w = gWidth; mode->h = gHeight; }
    return 0;
}
int SDL_GetDesktopDisplayMode(int displayIndex, SDL_DisplayMode *mode) { return SDL_GetCurrentDisplayMode(displayIndex, mode); }
void SDL_SetWindowAlwaysOnTop(SDL_Window *window, SDL_bool on_top) { (void)window; (void)on_top; }
int SDL_GetNumVideoDisplays(void) { return 1; }
uint32_t SDL_GetWindowFlags(SDL_Window *window) { (void)window; return SDL_WINDOW_OPENGL | SDL_WINDOW_SHOWN; }
void SDL_SetWindowTitle(SDL_Window *window, const char *title) { (void)window; (void)title; }

SDL_Surface* SDL_CreateRGBSurfaceFrom(void *pixels, int width, int height, int depth, int pitch, uint32_t Rmask, uint32_t Gmask, uint32_t Bmask, uint32_t Amask) {
    (void)pixels; (void)width; (void)height; (void)depth; (void)pitch; (void)Rmask; (void)Gmask; (void)Bmask; (void)Amask;
    static SDL_Surface s; s.w = width; s.h = height; return &s;
}
SDL_Cursor* SDL_CreateColorCursor(SDL_Surface *surface, int hot_x, int hot_y) { (void)surface; (void)hot_x; (void)hot_y; return nullptr; }
SDL_Cursor* SDL_CreateSystemCursor(SDL_SystemCursor id) { (void)id; return nullptr; }
void SDL_SetCursor(SDL_Cursor *cursor) { (void)cursor; }
void SDL_FreeCursor(SDL_Cursor *cursor) { (void)cursor; }
void SDL_FreeSurface(SDL_Surface *surface) { (void)surface; }
int SDL_ShowSimpleMessageBox(uint32_t flags, const char *title, const char *message, SDL_Window *window) { (void)flags; (void)title; (void)message; (void)window; return 0; }
void SDL_StartTextInput(void) {}
void SDL_StopTextInput(void) {}
void SDL_SetTextInputRect(SDL_Rect *rect) { (void)rect; }
SDL_bool SDL_IsTextInputActive(void) { return SDL_FALSE; }
int SDL_PollEvent(SDL_Event *event) { return popEvent(event); }

int SDL_GL_SetAttribute(int attr, int value) { (void)attr; (void)value; return 0; }
SDL_bool SDL_GL_ExtensionSupported(const char *extension) { (void)extension; return SDL_TRUE; }
void* SDL_GL_CreateContext(SDL_Window *window) { (void)window; ensureGLContext(); return (void*)1; }
int SDL_GL_SetSwapInterval(int interval) { (void)interval; return 0; }
void SDL_GL_SwapWindow(SDL_Window *window) {
    (void)window;
    eglSwapBuffers(eglGetCurrentDisplay(), eglGetCurrentSurface(EGL_DRAW));
}
void SDL_GL_GetDrawableSize(SDL_Window *window, int *w, int *h) { (void)window; if(w) *w=gWidth; if(h) *h=gHeight; }
