/*
 * JVM LD_PRELOAD hook for SDL_PollEvent.
 * Loaded via LD_PRELOAD env var before JVM starts.
 * Must call popEvent() first, then original SDL_PollEvent via dlsym(RTLD_NEXT).
 */
#include <dlfcn.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <android/log.h>

#define LOG_TAG "SDLHookJVM"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define FLOG(fmt,...) do{static FILE*f;if(!f)f=fopen("/sdcard/sdl_hook.log","a");if(f){fprintf(f,fmt"\n",##__VA_ARGS__);fflush(f);}LOGI(fmt,##__VA_ARGS__);}while(0)

#define SDL_EVENT_FILE "/sdcard/sdl_touch.dat"

typedef uint32_t U32; typedef int32_t S32; typedef uint8_t U8;
typedef struct{U32 t,ts,wid,which,state;S32 x,y,xr,yr;}SM;
typedef struct{U32 t,ts,wid,which;U8 b,s,c,p;S32 x,y;}SB;
typedef union{U32 type;SM motion;SB button;}SDLEvent;

static struct{unsigned char a;short x,y;}gB[16];
static int gR,gW,gD;

static int popEvent(SDLEvent*e){
    if(gR==gW){FILE*f=fopen(SDL_EVENT_FILE,"rb");if(!f){if(gD++<3)FLOG("No file");return 0;}
    fseek(f,0,2);long s=ftell(f);if(s<5){fclose(f);return 0;}
    if(gD++<3)FLOG("Reading %d events",(int)(s/5));fseek(f,0,0);gR=gW=0;
    int n=(int)(s/5);if(n>16)n=16;
    for(int i=0;i<n;i++){unsigned char r[5];if(fread(r,1,5,f)==5){gB[i].a=r[0];gB[i].x=(r[1]<<8)|r[2];gB[i].y=(r[3]<<8)|r[4];gW++;}}
    fclose(f);long p=n*5,r=s-p;
    if(r>0&&n>=16){f=fopen(SDL_EVENT_FILE,"rb");fseek(f,p,0);char*b=malloc(r);fread(b,1,r,f);fclose(f);f=fopen(SDL_EVENT_FILE,"wb");fwrite(b,1,r,f);fclose(f);free(b);}
    else{f=fopen(SDL_EVENT_FILE,"wb");fclose(f);}
    if(!gW)return 0;}
    typeof(gB[0])*t=&gB[gR];gR=(gR+1)%16;if(gR==gW)gR=gW=0;memset(e,0,sizeof(*e));
    if(t->a==0||t->a==5){e->type=0x401;e->button.b=1;e->button.s=1;}
    else if(t->a==1||t->a==6){e->type=0x402;e->button.b=1;e->button.s=0;}
    else if(t->a==2)e->type=0x400;else return 0;
    e->button.x=e->motion.x=t->x;e->button.y=e->motion.y=t->y;
    static int de=0;if(de++<3)FLOG("pop: a=%d %d,%d",t->a,t->x,t->y);
    return 1;
}

typedef int(*PF)(void*);
static PF orig; static int cc;

int SDL_PollEvent(void*e){
    if(cc++<5)FLOG("SDL_PollEvent hooked #%d",cc);
    if(popEvent((SDLEvent*)e))return 1;
    if(!orig){orig=(PF)dlsym(RTLD_NEXT,"SDL_PollEvent");FLOG("orig=%p",orig);}
    return orig?orig(e):0;
}
