#include <arc_graphics_Pixmap.h>

//@line:830


    #include <stdlib.h>
    #include <stdint.h>

    #include <stdlib.h>
    #define STB_IMAGE_IMPLEMENTATION
    #define STBI_FAILURE_USERMSG
    #define STBI_NO_STDIO
    #ifdef __APPLE__
    #define STBI_NO_THREAD_LOCALS
    #endif
    #include "stb_image.h"

    JNIEXPORT jobject JNICALL Java_arc_graphics_Pixmap_loadJni(JNIEnv* env, jclass clazz, jlongArray nativeData, jbyteArray buffer, jint offset, jint len) {

//@line:847

        const unsigned char* p_buffer = (const unsigned char*)env->GetPrimitiveArrayCritical(buffer, 0);

        int32_t width, height, format;

        //always use STBI_rgb_alpha (4) as the format, since that's the only thing pixmaps support
        //RGB images are generally uncommon and the memory savings don't really matter; formats have to be converted to RGBA for drawing anyway
        const unsigned char* pixels = stbi_load_from_memory(p_buffer + offset, len, &width, &height, &format, STBI_rgb_alpha);

        env->ReleasePrimitiveArrayCritical(buffer, (char*)p_buffer, 0);

        if(pixels == NULL) return NULL;

        jobject pixel_buffer = env->NewDirectByteBuffer((void*)pixels, width * height * 4);
        jlong* p_native_data = (jlong*)env->GetPrimitiveArrayCritical(nativeData, 0);
        p_native_data[0] = (jlong)pixels;
        p_native_data[1] = width;
        p_native_data[2] = height;
        env->ReleasePrimitiveArrayCritical(nativeData, p_native_data, 0);

        return pixel_buffer;
     
}

JNIEXPORT jobject JNICALL Java_arc_graphics_Pixmap_createJni(JNIEnv* env, jclass clazz, jlongArray nativeData, jint width, jint height) {

//@line:871

        const unsigned char* pixels = (unsigned char*)malloc(width * height * 4);

        if(!pixels) return 0;

        //fill pixel array with 0s
        //TODO use calloc insted?
        memset((void*)pixels, 0, width * height * 4);

        jobject pixel_buffer = env->NewDirectByteBuffer((void*)pixels, width * height * 4);
        jlong* p_native_data = (jlong*)env->GetPrimitiveArrayCritical(nativeData, 0);
        p_native_data[0] = (jlong)pixels;
        p_native_data[1] = width;
        p_native_data[2] = height;
        env->ReleasePrimitiveArrayCritical(nativeData, p_native_data, 0);

        return pixel_buffer;
     
}

JNIEXPORT void JNICALL Java_arc_graphics_Pixmap_free(JNIEnv* env, jclass clazz, jlong buffer) {


//@line:890

        free((void*)buffer);
     

}

JNIEXPORT jstring JNICALL Java_arc_graphics_Pixmap_getFailureReason(JNIEnv* env, jclass clazz) {


//@line:894

        return env->NewStringUTF(stbi_failure_reason());
     

}

