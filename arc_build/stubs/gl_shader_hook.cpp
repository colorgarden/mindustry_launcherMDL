#include <dlfcn.h>
#include <GLES2/gl2.h>
#include <cstring>
#include <cstdlib>
#include <cstdio>
#include <android/log.h>

#define LOGI(...) __android_log_print(ANDROID_LOG_ERROR, "gl_hook", __VA_ARGS__)
#define LOGF(fmt, ...) do { \
    FILE* _f = fopen("/sdcard/gl_hook.log", "a"); \
    if (_f) { fprintf(_f, fmt "\n", ##__VA_ARGS__); fclose(_f); } \
} while(0)

__attribute__((constructor))
static void hook_init() {
    LOGF("=== gl_hook loaded ===");
    LOGI("gl_hook loaded");
}

static PFNGLSHADERSOURCEPROC real_glShaderSource = nullptr;
static int callCount = 0;

static PFNGLSHADERSOURCEPROC getReal() {
    if (!real_glShaderSource) {
        real_glShaderSource = (PFNGLSHADERSOURCEPROC)dlsym(RTLD_NEXT, "glShaderSource");
        LOGF("dlsym(RTLD_NEXT, glShaderSource) = %p", (void*)real_glShaderSource);
    }
    return real_glShaderSource;
}

static const char* matchVersion(const char* s) {
    if (!s) return nullptr;
    while (*s == ' ' || *s == '\t') s++;
    if (s[0] != '#' || s[1] != 'v' || s[2] != 'e' || s[3] != 'r' || s[4] != 's' || s[5] != 'i' || s[6] != 'o' || s[7] != 'n')
        return nullptr;
    return s + 8;
}

static char versionBuf[32];

extern "C" void glShaderSource(GLuint shader, GLsizei count, const GLchar* const* string, const GLint* length) {
    PFNGLSHADERSOURCEPROC real = getReal();
    if (!real) {
        LOGF("glShaderSource #%d: NO REAL FUNC, abort", ++callCount);
        return;
    }

    if (count > 0 && matchVersion(string[0])) {
        const char* ver = matchVersion(string[0]);
        int verNum = atoi(ver);
        LOGF("glShaderSource #%d: count=%d version=%d", ++callCount, (int)count, verNum);
        if (verNum >= 110 && verNum < 200) {
            LOGF("  -> rewriting #version %d to #version 300 es", verNum);
            strcpy(versionBuf, "#version 300 es\n");
            const GLchar* newStrings[16];
            newStrings[0] = versionBuf;
            for (int i = 1; i < count && i < 15; i++) {
                newStrings[i] = string[i];
            }
            GLint newLengths[16];
            const GLint* lenPtr = nullptr;
            if (length) {
                newLengths[0] = (GLint)strlen(versionBuf);
                for (int i = 1; i < count && i < 15; i++) {
                    newLengths[i] = length[i];
                }
                lenPtr = newLengths;
            }
            real(shader, count, newStrings, lenPtr);
            return;
        }
        LOGF("  -> version %d outside range, pass through", verNum);
    } else {
        if (callCount < 5) {
            LOGF("glShaderSource #%d: count=%d no version line, first=[%.60s]",
                 ++callCount, (int)count, count > 0 ? string[0] : "(null)");
        } else {
            callCount++;
        }
    }

    real(shader, count, string, length);
}
