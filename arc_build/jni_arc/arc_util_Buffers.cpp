#include <arc_util_Buffers.h>

//@line:236

	#include <stdio.h>
	#include <stdlib.h>
	#include <string.h>
	JNIEXPORT void JNICALL Java_arc_util_Buffers_freeMemory(JNIEnv* env, jclass clazz, jobject obj_buffer) {
	char* buffer = (char*)(obj_buffer?env->GetDirectBufferAddress(obj_buffer):0);


//@line:246

		free(buffer);
	 

}

JNIEXPORT jobject JNICALL Java_arc_util_Buffers_newDisposableByteBuffer(JNIEnv* env, jclass clazz, jint numBytes) {


//@line:250

		return env->NewDirectByteBuffer((char*)malloc(numBytes), numBytes);
	

}

static inline jlong wrapped_Java_arc_util_Buffers_getBufferAddress
(JNIEnv* env, jclass clazz, jobject obj_buffer, unsigned char* buffer) {

//@line:254

	    return (jlong) buffer;
	
}

JNIEXPORT jlong JNICALL Java_arc_util_Buffers_getBufferAddress(JNIEnv* env, jclass clazz, jobject obj_buffer) {
	unsigned char* buffer = (unsigned char*)(obj_buffer?env->GetDirectBufferAddress(obj_buffer):0);

	jlong JNI_returnValue = wrapped_Java_arc_util_Buffers_getBufferAddress(env, clazz, obj_buffer, buffer);


	return JNI_returnValue;
}

JNIEXPORT void JNICALL Java_arc_util_Buffers_clear(JNIEnv* env, jclass clazz, jobject obj_buffer, jint numBytes) {
	char* buffer = (char*)(obj_buffer?env->GetDirectBufferAddress(obj_buffer):0);


//@line:259

		memset(buffer, 0, numBytes);
	

}

JNIEXPORT void JNICALL Java_arc_util_Buffers_copyJni___3FLjava_nio_Buffer_2II(JNIEnv* env, jclass clazz, jfloatArray obj_src, jobject obj_dst, jint numFloats, jint offset) {
	unsigned char* dst = (unsigned char*)(obj_dst?env->GetDirectBufferAddress(obj_dst):0);
	float* src = (float*)env->GetPrimitiveArrayCritical(obj_src, 0);


//@line:263

		memcpy(dst, src + offset, numFloats << 2 );
	
	env->ReleasePrimitiveArrayCritical(obj_src, src, 0);

}

JNIEXPORT void JNICALL Java_arc_util_Buffers_copyJni___3BILjava_nio_Buffer_2II(JNIEnv* env, jclass clazz, jbyteArray obj_src, jint srcOffset, jobject obj_dst, jint dstOffset, jint numBytes) {
	unsigned char* dst = (unsigned char*)(obj_dst?env->GetDirectBufferAddress(obj_dst):0);
	char* src = (char*)env->GetPrimitiveArrayCritical(obj_src, 0);


//@line:267

		memcpy(dst + dstOffset, src + srcOffset, numBytes);
	
	env->ReleasePrimitiveArrayCritical(obj_src, src, 0);

}

JNIEXPORT void JNICALL Java_arc_util_Buffers_copyJni___3SILjava_nio_Buffer_2II(JNIEnv* env, jclass clazz, jshortArray obj_src, jint srcOffset, jobject obj_dst, jint dstOffset, jint numBytes) {
	unsigned char* dst = (unsigned char*)(obj_dst?env->GetDirectBufferAddress(obj_dst):0);
	short* src = (short*)env->GetPrimitiveArrayCritical(obj_src, 0);


//@line:271

		memcpy(dst + dstOffset, src + srcOffset, numBytes);
	 
	env->ReleasePrimitiveArrayCritical(obj_src, src, 0);

}

JNIEXPORT void JNICALL Java_arc_util_Buffers_copyJni___3IILjava_nio_Buffer_2II(JNIEnv* env, jclass clazz, jintArray obj_src, jint srcOffset, jobject obj_dst, jint dstOffset, jint numBytes) {
	unsigned char* dst = (unsigned char*)(obj_dst?env->GetDirectBufferAddress(obj_dst):0);
	int* src = (int*)env->GetPrimitiveArrayCritical(obj_src, 0);


//@line:275

		memcpy(dst + dstOffset, src + srcOffset, numBytes);
	
	env->ReleasePrimitiveArrayCritical(obj_src, src, 0);

}

JNIEXPORT void JNICALL Java_arc_util_Buffers_copyJni___3FILjava_nio_Buffer_2II(JNIEnv* env, jclass clazz, jfloatArray obj_src, jint srcOffset, jobject obj_dst, jint dstOffset, jint numBytes) {
	unsigned char* dst = (unsigned char*)(obj_dst?env->GetDirectBufferAddress(obj_dst):0);
	float* src = (float*)env->GetPrimitiveArrayCritical(obj_src, 0);


//@line:279

		memcpy(dst + dstOffset, src + srcOffset, numBytes);
	
	env->ReleasePrimitiveArrayCritical(obj_src, src, 0);

}

JNIEXPORT void JNICALL Java_arc_util_Buffers_copyJni__Ljava_nio_Buffer_2ILjava_nio_Buffer_2II(JNIEnv* env, jclass clazz, jobject obj_src, jint srcOffset, jobject obj_dst, jint dstOffset, jint numBytes) {
	unsigned char* src = (unsigned char*)(obj_src?env->GetDirectBufferAddress(obj_src):0);
	unsigned char* dst = (unsigned char*)(obj_dst?env->GetDirectBufferAddress(obj_dst):0);


//@line:283

		memcpy(dst + dstOffset, src + srcOffset, numBytes);
	

}

