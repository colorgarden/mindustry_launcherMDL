/*
 * Preload library: hooks SDL_PollEvent in libsdl-arcarm64.so
 * to inject touch events from /sdcard/sdl_touch.dat.
 */
#include <dlfcn.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <android/log.h>

static FILE* gLogFile = NULL;
#define FLOG(fmt, ...) do { \
    if (!gLogFile) gLogFile = fopen("/sdcard/preload_touch.log", "a"); \
    if (gLogFile) { fprintf(gLogFile, fmt "\n", ##__VA_ARGS__); fflush(gLogFile); } \
    __android_log_print(ANDROID_LOG_INFO, "PreloadTouch", fmt, ##__VA_ARGS__); \
} while(0)

#define SDL_EVENT_FILE "/sdcard/sdl_touch.dat"

typedef uint32_t Uint32; typedef int32_t Sint32; typedef uint8_t Uint8;
typedef struct { Uint32 type; Uint32 ts; Uint32 wid; Uint32 which; Uint32 state; Sint32 x,y,xrel,yrel; } SDL_Motion;
typedef struct { Uint32 type; Uint32 ts; Uint32 wid; Uint32 which; Uint8 btn,state,clicks,pad; Sint32 x,y; } SDL_Button;
typedef union { Uint32 type; SDL_Motion motion; SDL_Button button; } SDL_Event;

static struct { unsigned char action; short x, y; } gBuf[16];
static int gRead=0, gWrite=0, gDbg=0;

static int popEvent(SDL_Event* e) {
    if (gRead == gWrite) {
        FILE* f = fopen(SDL_EVENT_FILE, "rb");
        if (!f) { if (gDbg++<3) FLOG("No touch file"); return 0; }
        fseek(f,0,SEEK_END); long sz=ftell(f);
        if (sz<5) { fclose(f); return 0; }
        if (gDbg++<3) FLOG("Reading %d events", (int)(sz/5));
        fseek(f,0,SEEK_SET); gRead=gWrite=0;
        int n=(int)(sz/5); if(n>16)n=16;
        for(int i=0;i<n;i++){unsigned char r[5];if(fread(r,1,5,f)==5){gBuf[i].action=r[0];gBuf[i].x=(r[1]<<8)|r[2];gBuf[i].y=(r[3]<<8)|r[4];gWrite++;}}
        fclose(f);
        long p=n*5,r=sz-p;
        if(r>0&&n>=16){f=fopen(SDL_EVENT_FILE,"rb");fseek(f,p,SEEK_SET);char*b=malloc(r);fread(b,1,r,f);fclose(f);f=fopen(SDL_EVENT_FILE,"wb");fwrite(b,1,r,f);fclose(f);free(b);}
        else{f=fopen(SDL_EVENT_FILE,"wb");fclose(f);}
        if(!gWrite)return 0;
    }
    typeof(gBuf[0])*t=&gBuf[gRead];gRead=(gRead+1)%16;if(gRead==gWrite)gRead=gWrite=0;
    memset(e,0,sizeof(*e));
    if(t->action==0||t->action==5){e->type=0x401;e->button.btn=1;e->button.state=1;}
    else if(t->action==1||t->action==6){e->type=0x402;e->button.btn=1;e->button.state=0;}
    else if(t->action==2){e->type=0x400;}
    else return 0;
    e->button.x=e->motion.x=t->x;e->button.y=e->motion.y=t->y;
    static int de=0;if(de++<3)FLOG("popEvent: a=%d x=%d y=%d",t->action,t->x,t->y);
    return 1;
}

typedef void* bh_stub_t;
typedef void (*bh_cb_t)(bh_stub_t,int,const char*,const char*,void*,void*,void*);
typedef int (*Poll_t)(void*);
static Poll_t realPoll=NULL;
static int cc=0;

static int myPoll(void* e) {
    if(cc++<5)FLOG("SDL_PollEvent hooked #%d",cc);
    if(popEvent((SDL_Event*)e))return 1;
    return realPoll?realPoll(e):0;
}

static void bh_cb(bh_stub_t s,int code,const char*p,const char*n,void*f,void*prev,void*a){
    (void)s;(void)f;(void)a;
    if(code==0){realPoll=(Poll_t)prev;FLOG("Hook OK: %s in %s",n,p);}
    else FLOG("Hook FAIL[%d]: %s in %s",code,n,p);
}

__attribute__((constructor)) static void init(void) {
    FLOG("PreloadTouch constructor");
    void* bh=dlopen("libbytehook.so",RTLD_NOW);
    if(!bh){FLOG("dlopen libbytehook.so FAILED: %s",dlerror());return;}
    void* (*hook)(const char*,const char*,void*,bh_cb_t,void*)=dlsym(bh,"bytehook_hook_single");
    if(!hook){FLOG("dlsym bytehook_hook_single FAILED");dlclose(bh);return;}
    FLOG("ByteHook loaded, hooking SDL_PollEvent");
    bh_stub_t stub = hook("libsdl-arcarm64.so","SDL_PollEvent",myPoll,bh_cb,NULL);
    FLOG("Hook result: stub=%p", stub);
    if(!stub) {
        FLOG("Hook FAILED immediately, trying other patterns");
        stub = hook("/data/data/*/cache/arc/*/libsdl-arcarm64.so","SDL_PollEvent",myPoll,bh_cb,NULL);
        FLOG("Hook2 result: stub=%p", stub);
    }
}
