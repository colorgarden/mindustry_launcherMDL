#include <GL/glew.h>

// Provide the function pointer globals that glew.c normally fills
// These are declared as "extern" in glew.h, defined weak so linker is happy
#define GLEW_STUB_VAR(type, name) type name = 0

// All __glew* function pointer variables referenced by Arc backend
GLEW_STUB_VAR(PFNGLACTIVETEXTUREPROC, __glewActiveTexture);
GLEW_STUB_VAR(PFNGLBINDBUFFERPROC, __glewBindBuffer);
GLEW_STUB_VAR(PFNGLBUFFERDATAPROC, __glewBufferData);
GLEW_STUB_VAR(PFNGLBUFFERSUBDATAPROC, __glewBufferSubData);
GLEW_STUB_VAR(PFNGLDELETEBUFFERSPROC, __glewDeleteBuffers);
GLEW_STUB_VAR(PFNGLGENBUFFERSPROC, __glewGenBuffers);
GLEW_STUB_VAR(PFNGLMAPBUFFERPROC, __glewMapBuffer);
GLEW_STUB_VAR(PFNGLUNMAPBUFFERPROC, __glewUnmapBuffer);
GLEW_STUB_VAR(PFNGLGETBUFFERPARAMETERIVPROC, __glewGetBufferParameteriv);
GLEW_STUB_VAR(PFNGLGETBUFFERPOINTERVPROC, __glewGetBufferPointerv);

// Functions Arc backend calls
GLenum glewInit(void) { return 0; /* GLEW_OK */ }
GLboolean glewIsSupported(const char *name) { (void)name; return 1; }
const char* glewGetErrorString(GLenum error) { (void)error; return "No error"; }
const char* glewGetString(GLenum name) {
    switch(name) {
        case 0x1001: return "2.1.0"; // GLEW_VERSION
        case 0x1002: return "2.1";   // GLEW_VERSION_MAJOR_MINOR
        default: return "";
    }
}
