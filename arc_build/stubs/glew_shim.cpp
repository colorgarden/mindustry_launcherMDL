#include "pojav_glue.h"
#include "GL/glew.h"

GLboolean glewIsSupported(const char *name) { (void)name; return GL_TRUE; }
const char* glewGetString(GLenum name) { (void)name; return (const char*)"GLES shim"; }
GLenum glewInit(void) { ensureGLContext(); return GLEW_OK; }
const char* glewGetErrorString(GLenum error) {
    switch(error) {
        case GLEW_OK: return "No error";
        default: return "GLEW shim error";
    }
}

// Stub GL3 functions not in GLES2
extern "C" {
void GL_APIENTRY glTexImage3D(GLenum t, GLint l, GLint ifmt, GLsizei w, GLsizei h, GLsizei d, GLint b, GLenum fmt, GLenum type, const void *p) {}
void GL_APIENTRY glTexSubImage3D(GLenum t, GLint l, GLint xo, GLint yo, GLint zo, GLsizei w, GLsizei h, GLsizei d, GLenum fmt, GLenum type, const void *p) {}
void GL_APIENTRY glCopyTexSubImage3D(GLenum t, GLint l, GLint xo, GLint yo, GLint zo, GLint x, GLint y, GLsizei w, GLsizei h) {}
void GL_APIENTRY glGenQueries(GLsizei n, GLuint *ids) { for(GLsizei i=0;i<n;i++) ids[i]=0; }
void GL_APIENTRY glDeleteQueries(GLsizei n, const GLuint *ids) {}
GLboolean GL_APIENTRY glIsQuery(GLuint id) { return GL_FALSE; }
void GL_APIENTRY glBeginQuery(GLenum target, GLuint id) {}
void GL_APIENTRY glEndQuery(GLenum target) {}
void GL_APIENTRY glGetQueryiv(GLenum target, GLenum pname, GLint *params) { if(params) *params=0; }
void GL_APIENTRY glGetQueryObjectuiv(GLuint id, GLenum pname, GLuint *params) { if(params) *params=0; }
void GL_APIENTRY glGetInteger64v(GLenum pname, GLint64 *params) { if(params) *params=0; }
void GL_APIENTRY glBlitFramebuffer(GLint sx0,GLint sy0,GLint sx1,GLint sy1,GLint dx0,GLint dy0,GLint dx1,GLint dy1,GLbitfield m,GLenum f) {}
void GL_APIENTRY glRenderbufferStorageMultisample(GLenum t, GLsizei s, GLenum ifmt, GLsizei w, GLsizei h) {}
void GL_APIENTRY glTexBuffer(GLenum t, GLenum ifmt, GLuint b) {}
void GL_APIENTRY glTexBufferRange(GLenum t, GLenum ifmt, GLuint b, GLintptr o, GLsizeiptr s) {}
}
