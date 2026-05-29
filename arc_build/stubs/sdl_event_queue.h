#pragma once
#include "SDL.h"
#include <cstring>
#include <cstdio>
#include <cstdlib>

#define SDL_EVENT_FILE "/sdcard/sdl_touch.dat"

struct TouchEvent { unsigned char action; short x, y; };

// Simple queue - buffer up to 16 events in memory, read from file when empty
static TouchEvent gBuf[16];
static int gBufRead = 0, gBufWrite = 0;

static int popEvent(SDL_Event* event) {
    // Try to refill from file if buffer is empty
    if (gBufRead == gBufWrite) {
        FILE* f = fopen(SDL_EVENT_FILE, "rb");
        if (!f) return 0;
        fseek(f, 0, SEEK_END);
        long size = ftell(f);
        if (size < 5) { fclose(f); return 0; }
        fseek(f, 0, SEEK_SET);

        gBufRead = 0; gBufWrite = 0;
        int count = (int)(size / 5);
        if (count > 16) count = 16;
        for (int i = 0; i < count; i++) {
            unsigned char raw[5];
            if (fread(raw, 1, 5, f) == 5) {
                gBuf[i].action = raw[0];
                gBuf[i].x = (raw[1] << 8) | raw[2];
                gBuf[i].y = (raw[3] << 8) | raw[4];
                gBufWrite++;
            }
        }
        fclose(f);

        // Truncate file - keep remaining events for next batch
        long processed = count * 5;
        long remain = size - processed;
        if (remain > 0 && count >= 16) {
            // More events remain, shift them
            f = fopen(SDL_EVENT_FILE, "rb");
            fseek(f, processed, SEEK_SET);
            char* buf = (char*)malloc(remain);
            fread(buf, 1, remain, f);
            fclose(f);
            f = fopen(SDL_EVENT_FILE, "wb");
            fwrite(buf, 1, remain, f);
            fclose(f);
            free(buf);
        } else {
            // All consumed or small enough, just clear
            f = fopen(SDL_EVENT_FILE, "wb"); fclose(f);
        }

        if (gBufWrite == 0) return 0;
    }

    TouchEvent* te = &gBuf[gBufRead];
    gBufRead = (gBufRead + 1) % 16;
    if (gBufRead == gBufWrite) { gBufRead = gBufWrite = 0; }

    memset(event, 0, sizeof(*event));
    if (te->action == 0 || te->action == 5) { event->type = 0x401; event->button.button = 1; }
    else if (te->action == 1 || te->action == 6) { event->type = 0x402; event->button.button = 1; }
    else if (te->action == 2) { event->type = 0x400; }
    else return 0;

    event->button.x = te->x; event->button.y = te->y;
    event->motion.x = te->x; event->motion.y = te->y;
    return 1;
}
