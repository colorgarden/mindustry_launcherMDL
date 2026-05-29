#ifndef SDL_STUB_H
#define SDL_STUB_H
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif

typedef struct SDL_Window { int dummy; } SDL_Window;
typedef struct SDL_Surface { int w, h; } SDL_Surface;
typedef struct SDL_Cursor { int dummy; } SDL_Cursor;
typedef struct SDL_version { uint8_t major, minor, patch; } SDL_version;
typedef struct SDL_Rect { int x, y, w, h; } SDL_Rect;
typedef struct SDL_DisplayMode { int w, h; } SDL_DisplayMode;
typedef struct SDL_Keysym { int sym; int scancode; int mod; } SDL_Keysym;
typedef struct SDL_KeyboardEvent { uint32_t timestamp; SDL_Keysym keysym; uint8_t repeat; } SDL_KeyboardEvent;
typedef struct SDL_WindowEvent { uint8_t event; int data1; int data2; } SDL_WindowEvent;
typedef struct SDL_MouseMotionEvent { int x; int y; } SDL_MouseMotionEvent;
typedef struct SDL_MouseButtonEvent { uint8_t button; int x; int y; } SDL_MouseButtonEvent;
typedef struct SDL_MouseWheelEvent { int x; int y; } SDL_MouseWheelEvent;
typedef struct SDL_TextInputEvent { char text[32]; } SDL_TextInputEvent;
typedef struct SDL_TextEditingEvent { char text[32]; int start; int length; } SDL_TextEditingEvent;
typedef union SDL_Event {
    uint32_t type;
    SDL_WindowEvent window;
    SDL_MouseMotionEvent motion;
    SDL_MouseButtonEvent button;
    SDL_MouseWheelEvent wheel;
    SDL_KeyboardEvent key;
    SDL_TextInputEvent text;
    SDL_TextEditingEvent edit;
} SDL_Event;

typedef enum {
    SDL_SYSTEM_CURSOR_ARROW = 0,
    SDL_SYSTEM_CURSOR_HAND = 11
} SDL_SystemCursor;

typedef int SDL_GLattr;

#define SDL_INIT_TIMER          0x00000001
#define SDL_INIT_AUDIO          0x00000010
#define SDL_INIT_VIDEO          0x00000020
#define SDL_INIT_JOYSTICK       0x00000200
#define SDL_INIT_HAPTIC         0x00001000
#define SDL_INIT_GAMECONTROLLER 0x00002000
#define SDL_INIT_EVENTS         0x00004000
#define SDL_INIT_EVERYTHING     0x00107231

#define SDL_WINDOWPOS_UNDEFINED 0x1FFF0000
#define SDL_WINDOW_OPENGL       0x00000002
#define SDL_WINDOW_SHOWN        0x00000004
#define SDL_WINDOW_FULLSCREEN   0x00000001
#define SDL_WINDOW_RESIZABLE    0x00000020
#define SDL_WINDOW_BORDERLESS   0x00000010
#define SDL_WINDOW_MAXIMIZED    0x00000080

#define SDL_TRUE  1
#define SDL_FALSE 0
typedef int SDL_bool;

#define SDL_MESSAGEBOX_ERROR       0x00000010
#define SDL_MESSAGEBOX_WARNING     0x00000020
#define SDL_MESSAGEBOX_INFORMATION 0x00000040

#define SDL_QUIT            0x100
#define SDL_WINDOWEVENT     0x200
#define SDL_KEYDOWN         0x300
#define SDL_KEYUP           0x301
#define SDL_TEXTEDITING     0x302
#define SDL_TEXTINPUT       0x303
#define SDL_MOUSEMOTION     0x400
#define SDL_MOUSEBUTTONDOWN 0x401
#define SDL_MOUSEBUTTONUP   0x402
#define SDL_MOUSEWHEEL      0x403

#define SDL_VERSION(x) do { (x)->major = 2; (x)->minor = 32; (x)->patch = 8; } while(0)

int SDL_Init(uint32_t flags);
int SDL_InitSubSystem(uint32_t flags);
void SDL_QuitSubSystem(uint32_t flags);
int SDL_WasInit(uint32_t flags);
void SDL_Quit(void);
SDL_bool SDL_SetHint(const char *name, const char *value);
void SDL_GetVersion(SDL_version *ver);
const char* SDL_GetError(void);
int SDL_SetClipboardText(const char *text);
const char* SDL_GetClipboardText(void);
SDL_Window* SDL_CreateWindow(const char *title, int x, int y, int w, int h, uint32_t flags);
void SDL_DestroyWindow(SDL_Window *window);
void SDL_SetWindowIcon(SDL_Window *window, SDL_Surface *icon);
void SDL_RestoreWindow(SDL_Window *window);
void SDL_MaximizeWindow(SDL_Window *window);
void SDL_MinimizeWindow(SDL_Window *window);
int SDL_SetWindowFullscreen(SDL_Window *window, uint32_t flags);
void SDL_SetWindowBordered(SDL_Window *window, SDL_bool bordered);
void SDL_SetWindowSize(SDL_Window *window, int w, int h);
void SDL_SetWindowPosition(SDL_Window *window, int x, int y);
int SDL_GetWindowDisplayIndex(SDL_Window *window);
int SDL_GetDisplayUsableBounds(int displayIndex, SDL_Rect *rect);
int SDL_GetDisplayBounds(int displayIndex, SDL_Rect *rect);
int SDL_GetCurrentDisplayMode(int displayIndex, SDL_DisplayMode *mode);
int SDL_GetDesktopDisplayMode(int displayIndex, SDL_DisplayMode *mode);
void SDL_SetWindowAlwaysOnTop(SDL_Window *window, SDL_bool on_top);
int SDL_GetNumVideoDisplays(void);
uint32_t SDL_GetWindowFlags(SDL_Window *window);
void SDL_SetWindowTitle(SDL_Window *window, const char *title);
SDL_Surface* SDL_CreateRGBSurfaceFrom(void *pixels, int width, int height, int depth, int pitch, uint32_t Rmask, uint32_t Gmask, uint32_t Bmask, uint32_t Amask);
SDL_Cursor* SDL_CreateColorCursor(SDL_Surface *surface, int hot_x, int hot_y);
SDL_Cursor* SDL_CreateSystemCursor(SDL_SystemCursor id);
void SDL_SetCursor(SDL_Cursor *cursor);
void SDL_FreeCursor(SDL_Cursor *cursor);
void SDL_FreeSurface(SDL_Surface *surface);
int SDL_ShowSimpleMessageBox(uint32_t flags, const char *title, const char *message, SDL_Window *window);
void SDL_StartTextInput(void);
void SDL_StopTextInput(void);
void SDL_SetTextInputRect(SDL_Rect *rect);
SDL_bool SDL_IsTextInputActive(void);
int SDL_PollEvent(SDL_Event *event);
int SDL_GL_SetAttribute(SDL_GLattr attr, int value);
SDL_bool SDL_GL_ExtensionSupported(const char *extension);
void* SDL_GL_CreateContext(SDL_Window *window);
int SDL_GL_SetSwapInterval(int interval);
void SDL_GL_SwapWindow(SDL_Window *window);
void SDL_GL_GetDrawableSize(SDL_Window *window, int *w, int *h);

#ifdef __cplusplus
}
#endif
#endif
