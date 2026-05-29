#include <arc_audio_Soloud.h>

//@line:6

    #include "soloud.h"
    #include "soloud_file.h"
    #include "soloud_wav.h"
    #include "soloud_wavstream.h"
    #include "soloud_bus.h"
    #include "soloud_thread.h"
    #include "soloud_filter.h"
    #include "soloud_biquadresonantfilter.h"
    #include "soloud_echofilter.h"
    #include "soloud_lofifilter.h"
    #include "soloud_flangerfilter.h"
    #include "soloud_waveshaperfilter.h"
    #include "soloud_bassboostfilter.h"
    #include "soloud_robotizefilter.h"
    #include "soloud_freeverbfilter.h"
    #include <stdio.h>

    using namespace SoLoud;

    Soloud soloud;

    void throwError(JNIEnv* env, int result){
        jclass excClass = env->FindClass("arc/util/ArcRuntimeException");
        env->ThrowNew(excClass, soloud.getErrorString(result));
    }

    JNIEXPORT void JNICALL Java_arc_audio_Soloud_init(JNIEnv* env, jclass clazz) {


//@line:35

        int result = soloud.init();

        if(result != 0) throwError(env, result);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_deinit(JNIEnv* env, jclass clazz) {


//@line:41

        soloud.deinit();
    

}

JNIEXPORT jstring JNICALL Java_arc_audio_Soloud_backendString(JNIEnv* env, jclass clazz) {


//@line:45

        return env->NewStringUTF(soloud.getBackendString());
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_backendId(JNIEnv* env, jclass clazz) {


//@line:49

        return soloud.getBackendId();
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_backendChannels(JNIEnv* env, jclass clazz) {


//@line:53

        return soloud.getBackendChannels();
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_backendSamplerate(JNIEnv* env, jclass clazz) {


//@line:57

        return soloud.getBackendSamplerate();
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_backendBufferSize(JNIEnv* env, jclass clazz) {


//@line:61

        return soloud.getBackendBufferSize();
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_version(JNIEnv* env, jclass clazz) {


//@line:65

        return soloud.getVersion();
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_activeVoiceCount(JNIEnv* env, jclass clazz) {


//@line:69

        return soloud.getActiveVoiceCount();
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_stopAll(JNIEnv* env, jclass clazz) {


//@line:73

        soloud.stopAll();
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_pauseAll(JNIEnv* env, jclass clazz, jboolean paused) {


//@line:77

        soloud.setPauseAll(paused);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_biquadSet(JNIEnv* env, jclass clazz, jlong handle, jint type, jfloat frequency, jfloat resonance) {


//@line:81

        ((BiquadResonantFilter*)handle)->setParams(type, frequency, resonance);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_echoSet(JNIEnv* env, jclass clazz, jlong handle, jfloat delay, jfloat decay, jfloat filter) {


//@line:85

        ((EchoFilter*)handle)->setParams(delay, decay, filter);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_lofiSet(JNIEnv* env, jclass clazz, jlong handle, jfloat sampleRate, jfloat bitDepth) {


//@line:89

        ((LofiFilter*)handle)->setParams(sampleRate, bitDepth);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_flangerSet(JNIEnv* env, jclass clazz, jlong handle, jfloat delay, jfloat frequency) {


//@line:93

        ((FlangerFilter*)handle)->setParams(delay, frequency);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_waveShaperSet(JNIEnv* env, jclass clazz, jlong handle, jfloat amount) {


//@line:97

        ((WaveShaperFilter*)handle)->setParams(amount);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_bassBoostSet(JNIEnv* env, jclass clazz, jlong handle, jfloat amount) {


//@line:101

        ((BassboostFilter*)handle)->setParams( amount);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_robotizeSet(JNIEnv* env, jclass clazz, jlong handle, jfloat freq, jint waveform) {


//@line:105

        ((RobotizeFilter*)handle)->setParams(freq, waveform);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_freeverbSet(JNIEnv* env, jclass clazz, jlong handle, jfloat mode, jfloat roomSize, jfloat damp, jfloat width) {


//@line:109

        ((FreeverbFilter*)handle)->setParams(mode, roomSize, damp, width);
    

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterBiquad(JNIEnv* env, jclass clazz) {


//@line:113
 return (jlong)(new BiquadResonantFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterEcho(JNIEnv* env, jclass clazz) {


//@line:114
 return (jlong)(new EchoFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterLofi(JNIEnv* env, jclass clazz) {


//@line:115
 return (jlong)(new LofiFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterFlanger(JNIEnv* env, jclass clazz) {


//@line:116
 return (jlong)(new FlangerFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterBassBoost(JNIEnv* env, jclass clazz) {


//@line:117
 return (jlong)(new BassboostFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterWaveShaper(JNIEnv* env, jclass clazz) {


//@line:118
 return (jlong)(new WaveShaperFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterRobotize(JNIEnv* env, jclass clazz) {


//@line:119
 return (jlong)(new RobotizeFilter()); 

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_filterFreeverb(JNIEnv* env, jclass clazz) {


//@line:120
 return (jlong)(new FreeverbFilter()); 

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_setGlobalFilter(JNIEnv* env, jclass clazz, jint index, jlong handle) {


//@line:122

        soloud.setGlobalFilter(index, ((Filter*)handle));
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_filterFade(JNIEnv* env, jclass clazz, jint voice, jint filter, jint attribute, jfloat value, jfloat timeSec) {


//@line:126

        soloud.fadeFilterParameter(voice, filter, attribute, value, timeSec);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_filterSet(JNIEnv* env, jclass clazz, jint voice, jint filter, jint attribute, jfloat value) {


//@line:130

        soloud.setFilterParameter(voice, filter, attribute, value);
    

}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_busNew(JNIEnv* env, jclass clazz) {


//@line:134

        return (jlong)(new Bus());
    

}

static inline jlong wrapped_Java_arc_audio_Soloud_wavLoad
(JNIEnv* env, jclass clazz, jbyteArray obj_bytes, jint length, char* bytes) {

//@line:138

        Wav* wav = new Wav();

        int result = wav->loadMem((unsigned char*)bytes, length, true, true);

        if(result != 0) throwError(env, result);

        return (jlong)wav;
    
}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_wavLoad(JNIEnv* env, jclass clazz, jbyteArray obj_bytes, jint length) {
	char* bytes = (char*)env->GetPrimitiveArrayCritical(obj_bytes, 0);

	jlong JNI_returnValue = wrapped_Java_arc_audio_Soloud_wavLoad(env, clazz, obj_bytes, length, bytes);

	env->ReleasePrimitiveArrayCritical(obj_bytes, bytes, 0);

	return JNI_returnValue;
}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idSeek(JNIEnv* env, jclass clazz, jint id, jfloat seconds) {


//@line:148

        soloud.seek(id, seconds);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idVolume(JNIEnv* env, jclass clazz, jint id, jfloat volume) {


//@line:152

        soloud.setVolume(id, volume);
    

}

JNIEXPORT jfloat JNICALL Java_arc_audio_Soloud_idGetVolume(JNIEnv* env, jclass clazz, jint id) {


//@line:156

        return soloud.getVolume(id);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idPan(JNIEnv* env, jclass clazz, jint id, jfloat pan) {


//@line:160

        soloud.setPan(id, pan);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idPitch(JNIEnv* env, jclass clazz, jint id, jfloat pitch) {


//@line:164

        soloud.setRelativePlaySpeed(id, pitch);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idPause(JNIEnv* env, jclass clazz, jint id, jboolean pause) {


//@line:168

        soloud.setPause(id, pause);
    

}

JNIEXPORT jboolean JNICALL Java_arc_audio_Soloud_idGetPause(JNIEnv* env, jclass clazz, jint voice) {


//@line:172

        return soloud.getPause(voice);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idProtected(JNIEnv* env, jclass clazz, jint id, jboolean protect) {


//@line:176

        soloud.setProtectVoice(id, protect);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idStop(JNIEnv* env, jclass clazz, jint voice) {


//@line:180

        soloud.stop(voice);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_idLooping(JNIEnv* env, jclass clazz, jint voice, jboolean looping) {


//@line:184

        soloud.setLooping(voice, looping);
    

}

JNIEXPORT jboolean JNICALL Java_arc_audio_Soloud_idGetLooping(JNIEnv* env, jclass clazz, jint voice) {


//@line:188

        return soloud.getLooping(voice);
    

}

JNIEXPORT jfloat JNICALL Java_arc_audio_Soloud_idPosition(JNIEnv* env, jclass clazz, jint voice) {


//@line:192

        return (jfloat)soloud.getStreamPosition(voice);
    

}

JNIEXPORT jboolean JNICALL Java_arc_audio_Soloud_idValid(JNIEnv* env, jclass clazz, jint voice) {


//@line:196

        return soloud.isValidVoiceHandle(voice);
    

}

static inline jlong wrapped_Java_arc_audio_Soloud_streamLoad
(JNIEnv* env, jclass clazz, jstring obj_path, char* path) {

//@line:200

        WavStream* stream = new WavStream();

        int result = stream->load(path);

        if(result != 0) throwError(env, result);

        return (jlong)stream;
    
}

JNIEXPORT jlong JNICALL Java_arc_audio_Soloud_streamLoad(JNIEnv* env, jclass clazz, jstring obj_path) {
	char* path = (char*)env->GetStringUTFChars(obj_path, 0);

	jlong JNI_returnValue = wrapped_Java_arc_audio_Soloud_streamLoad(env, clazz, obj_path, path);

	env->ReleaseStringUTFChars(obj_path, path);

	return JNI_returnValue;
}

JNIEXPORT jdouble JNICALL Java_arc_audio_Soloud_streamLength(JNIEnv* env, jclass clazz, jlong handle) {


//@line:210

        WavStream* source = (WavStream*)handle;
        return (jdouble)source->getLength();
    

}

JNIEXPORT jdouble JNICALL Java_arc_audio_Soloud_wavLength(JNIEnv* env, jclass clazz, jlong handle) {


//@line:215

         Wav* source = (Wav*)handle;
         return (jdouble)source->getLength();
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceDestroy(JNIEnv* env, jclass clazz, jlong handle) {


//@line:220

        AudioSource* source = (AudioSource*)handle;
        delete source;
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceInaudible(JNIEnv* env, jclass clazz, jlong handle, jboolean tick, jboolean play) {


//@line:225

        AudioSource* wav = (AudioSource*)handle;
        wav->setInaudibleBehavior(tick, play);
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_sourcePlay__J(JNIEnv* env, jclass clazz, jlong handle) {


//@line:230

        AudioSource* wav = (AudioSource*)handle;
        return soloud.play(*wav);
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_sourceCount(JNIEnv* env, jclass clazz, jlong handle) {


//@line:235

        AudioSource* wav = (AudioSource*)handle;
        return soloud.countAudioSource(*wav);
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_sourcePlay__JFFFZ(JNIEnv* env, jclass clazz, jlong handle, jfloat volume, jfloat pitch, jfloat pan, jboolean loop) {


//@line:240

        AudioSource* wav = (AudioSource*)handle;

        return soloud.play(*wav, volume, pan, pitch, false, loop);
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_sourcePlayBus(JNIEnv* env, jclass clazz, jlong handle, jlong busHandle, jfloat volume, jfloat pitch, jfloat pan, jboolean loop) {


//@line:246

        AudioSource* wav = (AudioSource*)handle;
        Bus* bus = (Bus*)busHandle;

        return bus->play(*wav, volume, pan, pitch, false, loop);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourcePriority(JNIEnv* env, jclass clazz, jlong handle, jfloat priority) {


//@line:253

        AudioSource* source = (AudioSource*)handle;
        source->setPriority(priority);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceMinConcurrentInterrupt(JNIEnv* env, jclass clazz, jlong handle, jfloat value) {


//@line:258

        AudioSource* source = (AudioSource*)handle;
        source->setMinConcurrentInterrupt(value);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceMaxConcurrent(JNIEnv* env, jclass clazz, jlong handle, jint maxConcurrent) {


//@line:263

        AudioSource* source = (AudioSource*)handle;
        source->setMaxConcurrent(maxConcurrent);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceConcurrentGroup(JNIEnv* env, jclass clazz, jlong handle, jint group) {


//@line:268

        AudioSource* source = (AudioSource*)handle;
        source->setConcurrentGroup(group);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceLoop(JNIEnv* env, jclass clazz, jlong handle, jboolean loop) {


//@line:273

        AudioSource* source = (AudioSource*)handle;
        source->setLooping(loop);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceSingleInstance(JNIEnv* env, jclass clazz, jlong handle, jboolean single) {


//@line:278

        AudioSource* source = (AudioSource*)handle;
        source->setSingleInstance(single);
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceStop(JNIEnv* env, jclass clazz, jlong handle) {


//@line:283

        AudioSource* source = (AudioSource*)handle;
        source->stop();
    

}

JNIEXPORT void JNICALL Java_arc_audio_Soloud_sourceFilter(JNIEnv* env, jclass clazz, jlong handle, jint index, jlong filter) {


//@line:288

        ((AudioSource*)handle)->setFilter(index, ((Filter*)filter));
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_pauseDevice(JNIEnv* env, jclass clazz) {


//@line:294

        return soloud.pause();
    

}

JNIEXPORT jint JNICALL Java_arc_audio_Soloud_resumeDevice(JNIEnv* env, jclass clazz) {


//@line:298

        return soloud.resume();
    

}

