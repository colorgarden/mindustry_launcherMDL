#pragma once
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef int SDL_bool;
#define SDL_TRUE 1
#define SDL_FALSE 0

#define SDL_WINDOWPOS_UNDEFINED 0x1FFF0000

typedef struct SDL_Window { int dummy; } SDL_Window;

typedef struct SDL_Surface { int w, h; } SDL_Surface;

typedef struct SDL_Cursor { int dummy; } SDL_Cursor;

typedef enum {
    SDL_SYSTEM_CURSOR_ARROW,
    SDL_SYSTEM_CURSOR_IBEAM,
    SDL_SYSTEM_CURSOR_WAIT,
    SDL_SYSTEM_CURSOR_CROSSHAIR,
    SDL_SYSTEM_CURSOR_WAITARROW,
    SDL_SYSTEM_CURSOR_SIZENWSE,
    SDL_SYSTEM_CURSOR_SIZENESW,
    SDL_SYSTEM_CURSOR_SIZEWE,
    SDL_SYSTEM_CURSOR_SIZENS,
    SDL_SYSTEM_CURSOR_SIZEALL,
    SDL_SYSTEM_CURSOR_NO,
    SDL_SYSTEM_CURSOR_HAND,
    SDL_NUM_SYSTEM_CURSORS
} SDL_SystemCursor;

typedef struct { int x, y, w, h; } SDL_Rect;

typedef enum {
    SDL_GL_RED_SIZE, SDL_GL_GREEN_SIZE, SDL_GL_BLUE_SIZE,
    SDL_GL_ALPHA_SIZE, SDL_GL_BUFFER_SIZE,
    SDL_GL_DOUBLEBUFFER, SDL_GL_DEPTH_SIZE, SDL_GL_STENCIL_SIZE,
    SDL_GL_ACCUM_RED_SIZE, SDL_GL_ACCUM_GREEN_SIZE, SDL_GL_ACCUM_BLUE_SIZE, SDL_GL_ACCUM_ALPHA_SIZE,
    SDL_GL_STEREO, SDL_GL_MULTISAMPLEBUFFERS, SDL_GL_MULTISAMPLESAMPLES,
    SDL_GL_ACCELERATED_VISUAL, SDL_GL_RETAINED_BACKING,
    SDL_GL_CONTEXT_MAJOR_VERSION, SDL_GL_CONTEXT_MINOR_VERSION,
    SDL_GL_CONTEXT_PROFILE_MASK
} SDL_GLattr;

typedef struct { uint32_t format; int w, h, refresh_rate; void *driverdata; } SDL_DisplayMode;

typedef struct { uint8_t major, minor, patch; } SDL_version;
#define SDL_VERSION(x) ((x)->major=2,(x)->minor=32,(x)->patch=8)

typedef struct {
    uint32_t type, timestamp;
    uint32_t windowID;
    union {
        struct { uint8_t event; int32_t data1, data2; } window;
        struct { int32_t x, y; } motion;
        struct { uint8_t type, button, state; int32_t x, y; } button;
        struct { int32_t x, y; } wheel;
        struct { uint8_t type, state, repeat; struct { int32_t sym; uint16_t mod; uint32_t scancode; } keysym; uint32_t timestamp; } key;
        struct { char text[32]; } text;
        struct { int32_t start, length; char text[32]; } edit;
    };
} SDL_Event;

#define SDL_QUIT           0x100
#define SDL_WINDOWEVENT    0x200
#define SDL_MOUSEMOTION    0x400
#define SDL_MOUSEBUTTONDOWN 0x401
#define SDL_MOUSEBUTTONUP  0x402
#define SDL_MOUSEWHEEL     0x403
#define SDL_KEYDOWN        0x300
#define SDL_KEYUP          0x301
#define SDL_TEXTINPUT      0x302
#define SDL_TEXTEDITING    0x303

#define SDL_WINDOW_OPENGL  0x00000002
#define SDL_WINDOW_SHOWN   0x00000004
#define SDL_INIT_VIDEO     0x00000020

int  SDL_Init(uint32_t flags);
int  SDL_InitSubSystem(uint32_t flags);
void SDL_QuitSubSystem(uint32_t flags);
int  SDL_WasInit(uint32_t flags);
void SDL_Quit(void);
SDL_bool SDL_SetHint(const char *name, const char *value);
void SDL_GetVersion(SDL_version *ver);
const char* SDL_GetError(void);
int  SDL_SetClipboardText(const char *text);
const char* SDL_GetClipboardText(void);
SDL_Window* SDL_CreateWindow(const char *title, int x, int y, int w, int h, uint32_t flags);
void SDL_DestroyWindow(SDL_Window *window);
void SDL_SetWindowIcon(SDL_Window *window, SDL_Surface *icon);
void SDL_RestoreWindow(SDL_Window *window);
void SDL_MaximizeWindow(SDL_Window *window);
void SDL_MinimizeWindow(SDL_Window *window);
int  SDL_SetWindowFullscreen(SDL_Window *window, uint32_t flags);
void SDL_SetWindowBordered(SDL_Window *window, SDL_bool bordered);
void SDL_SetWindowSize(SDL_Window *window, int w, int h);
void SDL_SetWindowPosition(SDL_Window *window, int x, int y);
int  SDL_GetWindowDisplayIndex(SDL_Window *window);
int  SDL_GetDisplayUsableBounds(int displayIndex, SDL_Rect *rect);
int  SDL_GetDisplayBounds(int displayIndex, SDL_Rect *rect);
int  SDL_GetCurrentDisplayMode(int displayIndex, SDL_DisplayMode *mode);
int  SDL_GetDesktopDisplayMode(int displayIndex, SDL_DisplayMode *mode);
void SDL_SetWindowAlwaysOnTop(SDL_Window *window, SDL_bool on_top);
int  SDL_GetNumVideoDisplays(void);
uint32_t SDL_GetWindowFlags(SDL_Window *window);
void SDL_SetWindowTitle(SDL_Window *window, const char *title);
SDL_Surface* SDL_CreateRGBSurfaceFrom(void *pixels, int width, int height, int depth, int pitch, uint32_t Rmask, uint32_t Gmask, uint32_t Bmask, uint32_t Amask);
SDL_Cursor* SDL_CreateColorCursor(SDL_Surface *surface, int hot_x, int hot_y);
SDL_Cursor* SDL_CreateSystemCursor(SDL_SystemCursor id);
void SDL_SetCursor(SDL_Cursor *cursor);
void SDL_FreeCursor(SDL_Cursor *cursor);
void SDL_FreeSurface(SDL_Surface *surface);
int  SDL_ShowSimpleMessageBox(uint32_t flags, const char *title, const char *message, SDL_Window *window);
void SDL_StartTextInput(void);
void SDL_StopTextInput(void);
void SDL_SetTextInputRect(SDL_Rect *rect);
SDL_bool SDL_IsTextInputActive(void);
int  SDL_PollEvent(SDL_Event *event);
int  SDL_GL_SetAttribute(int attr, int value);
SDL_bool SDL_GL_ExtensionSupported(const char *extension);
void* SDL_GL_CreateContext(SDL_Window *window);
int  SDL_GL_SetSwapInterval(int interval);
void SDL_GL_SwapWindow(SDL_Window *window);
void SDL_GL_GetDrawableSize(SDL_Window *window, int *w, int *h);

#ifdef __cplusplus
}
#endif
