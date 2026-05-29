#ifndef GLEW_STUB_H
#define GLEW_STUB_H

#ifdef __ANDROID__
#include <GLES3/gl31.h>
#include <GLES2/gl2ext.h>
#endif

#ifdef __cplusplus
extern "C" {
#endif

#ifndef GLEW_OK
#define GLEW_OK 0
#endif

#define GLEW_ERROR_NO_GL_VERSION 1
#define GLEW_ERROR_GL_VERSION_10_ONLY 2
#define GLEW_ERROR_GLX_VERSION_11_ONLY 3

#ifndef GLEW_STATIC
#define GLEW_STATIC
#endif

#define glewExperimental GL_TRUE
GLboolean glewIsSupported(const char *name);
const char* glewGetString(GLenum name);
GLenum glewInit(void);
const char* glewGetErrorString(GLenum error);

#define glGenFramebuffersEXT glGenFramebuffers
#define glBindFramebufferEXT glBindFramebuffer
#define glFramebufferTexture2DEXT glFramebufferTexture2D
#define glCheckFramebufferStatusEXT glCheckFramebufferStatus
#define glDeleteFramebuffersEXT glDeleteFramebuffers
#define glRenderbufferStorageEXT glRenderbufferStorage
#define glBindRenderbufferEXT glBindRenderbuffer
#define glDeleteRenderbuffersEXT glDeleteRenderbuffers
#define glGenRenderbuffersEXT glGenRenderbuffers
#define glFramebufferRenderbufferEXT glFramebufferRenderbuffer
#define glGenerateMipmapEXT glGenerateMipmap
#define glGetRenderbufferParameterivEXT glGetRenderbufferParameteriv
#define glGetFramebufferAttachmentParameterivEXT glGetFramebufferAttachmentParameteriv
#define glIsFramebufferEXT glIsFramebuffer
#define glIsRenderbufferEXT glIsRenderbuffer
#define glMapBufferRangeEXT glMapBufferRange
#define glBindVertexArrayOES glBindVertexArray
#define glGenVertexArraysOES glGenVertexArrays
#define glDeleteVertexArraysOES glDeleteVertexArrays
#define glIsVertexArrayOES glIsVertexArray
#define glMapBufferOES glMapBuffer
#define glUnmapBufferOES glUnmapBuffer
#define glDrawBuffersEXT glDrawBuffers

#ifndef GL_COMPRESSED_RGB_S3TC_DXT1_EXT
#define GL_COMPRESSED_RGB_S3TC_DXT1_EXT 0x83F0
#define GL_COMPRESSED_RGBA_S3TC_DXT1_EXT 0x83F1
#define GL_COMPRESSED_RGBA_S3TC_DXT3_EXT 0x83F2
#define GL_COMPRESSED_RGBA_S3TC_DXT5_EXT 0x83F3
#endif
#ifndef GL_ETC1_RGB8_OES
#define GL_ETC1_RGB8_OES 0x8D64
#endif
#ifndef GL_COMPRESSED_RGB_PVRTC_4BPPV1_IMG
#define GL_COMPRESSED_RGB_PVRTC_4BPPV1_IMG 0x8C00
#define GL_COMPRESSED_RGB_PVRTC_2BPPV1_IMG 0x8C01
#define GL_COMPRESSED_RGBA_PVRTC_4BPPV1_IMG 0x8C02
#define GL_COMPRESSED_RGBA_PVRTC_2BPPV1_IMG 0x8C03
#endif
#ifndef GL_COMPRESSED_RGBA_ASTC_4x4_KHR
#define GL_COMPRESSED_RGBA_ASTC_4x4_KHR 0x93B0
#define GL_COMPRESSED_RGBA_ASTC_5x4_KHR 0x93B1
#define GL_COMPRESSED_RGBA_ASTC_5x5_KHR 0x93B2
#define GL_COMPRESSED_RGBA_ASTC_6x5_KHR 0x93B3
#define GL_COMPRESSED_RGBA_ASTC_6x6_KHR 0x93B4
#define GL_COMPRESSED_RGBA_ASTC_8x5_KHR 0x93B5
#define GL_COMPRESSED_RGBA_ASTC_8x6_KHR 0x93B6
#define GL_COMPRESSED_RGBA_ASTC_8x8_KHR 0x93B7
#define GL_COMPRESSED_RGBA_ASTC_10x5_KHR 0x93B8
#define GL_COMPRESSED_RGBA_ASTC_10x6_KHR 0x93B9
#define GL_COMPRESSED_RGBA_ASTC_10x8_KHR 0x93BA
#define GL_COMPRESSED_RGBA_ASTC_10x10_KHR 0x93BB
#define GL_COMPRESSED_RGBA_ASTC_12x10_KHR 0x93BC
#define GL_COMPRESSED_RGBA_ASTC_12x12_KHR 0x93BD
#endif
#ifndef GL_TEXTURE_MAX_ANISOTROPY_EXT
#define GL_TEXTURE_MAX_ANISOTROPY_EXT 0x84FE
#define GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT 0x84FF
#endif
#ifndef GL_BGR
#define GL_BGR 0x80E0
#define GL_BGRA 0x80E1
#endif
#ifndef GL_QUADS
#define GL_QUADS 0x0007
#endif

#ifdef __cplusplus
}
#endif
#endif
