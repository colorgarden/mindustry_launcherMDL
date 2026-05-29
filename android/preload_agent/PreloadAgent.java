import java.io.*;
import java.lang.instrument.*;

/**
 * Reads /sdcard/MDL/display_config.txt and forces the game
 * to use the configured resolution via Core.graphics.resize().
 */
public class PreloadAgent {
    public static void premain(String agentArgs, Instrumentation inst) {
        System.out.println("[PreloadAgent] Starting...");

        Thread t = new Thread(() -> {
            // Wait for game to init (Core.app and Core.graphics exist)
            for (int i = 0; i < 60; i++) {
                try { Thread.sleep(1000); } catch (InterruptedException e) { return; }
                try {
                    Class<?> core = Class.forName("arc.Core");
                    Object app = core.getField("app").get(null);
                    if (app == null) { System.out.println("[PreloadAgent] Core.app is null, retry " + i); continue; }
                    Object graphics = core.getField("graphics").get(null);
                    if (graphics == null) { System.out.println("[PreloadAgent] Core.graphics is null, retry " + i); continue; }

                    System.out.println("[PreloadAgent] Game initialized successfully.");
                    return;
                } catch (Exception e) {
                    System.out.println("[PreloadAgent] error: " + e);
                }
            }
        }, "PreloadAgent-thread");
        t.setDaemon(true);
        t.start();
    }
}
