import java.io.*;
import java.lang.instrument.Instrumentation;
import java.lang.reflect.*;

public class SdlHookAgent {
    private static volatile boolean running = true;
    private static BufferedWriter logWriter;

    static void log(String msg) {
        try {
            if (logWriter == null) logWriter = new BufferedWriter(new FileWriter("/sdcard/touch_agent.log", true));
            logWriter.write(msg + "\n"); logWriter.flush();
        } catch (Exception e) {}
    }

    public static void premain(String args, Instrumentation inst) {
        Thread t = new Thread(() -> {
            try { Thread.sleep(3000); } catch (Exception e) {}
            log("TouchAgent starting");
            injectLoop();
        }, "TouchAgent");
        t.setDaemon(true); t.start();
        Runtime.getRuntime().addShutdownHook(new Thread(() -> running = false));
    }

    // ----- Touch/mouse injection -----
    static Object inputObj;
    static Method handleInput;
    static Object appObj;
    static Method postMethod;
    static int injectedCount = 0;

    static void injectBatch(byte[] data, int total, int recordSize) {
        initInjection();
        if (!initDone || handleInput == null) return;
        for (int i = 0; i + recordSize - 1 < total; i += recordSize) {
            int action = data[i] & 0xFF;
            int x = ((data[i+1]&0xFF)<<8) | (data[i+2]&0xFF);
            int y = ((data[i+3]&0xFF)<<8) | (data[i+4]&0xFF);
            try {
                boolean pressed = (action == 0);
                // MOVE first (cursor follows)
                handleInput.invoke(inputObj, (Object) new int[]{2, x, y});
                if (action != 2) {
                    // Short delay so game's render can update preview position
                    try { Thread.sleep(32); } catch (Exception e3) {}
                    // Then the click
                    handleInput.invoke(inputObj, (Object) new int[]{3, pressed ? 1 : 0, x, y, 1});
                }
            } catch (Exception e) {}
        }
        if (++injectedCount <= 5) log("Batch: " + (total/recordSize) + " events");
    }
    static boolean initDone, initFailed;

    static void initInjection() {
        if (initDone || initFailed) return;
        try {
            Class<?> core = Class.forName("arc.Core");
            Field inF = core.getDeclaredField("input"); inF.setAccessible(true);
            inputObj = inF.get(null);
            Field appF = core.getDeclaredField("app"); appF.setAccessible(true);
            appObj = appF.get(null);
            if (inputObj != null) {
                for (Method m : inputObj.getClass().getDeclaredMethods()) {
                    if (m.getName().equals("handleInput")) { handleInput = m; handleInput.setAccessible(true); break; }
                }
            }
            if (appObj != null) {
                for (Method m : appObj.getClass().getMethods()) {
                    if (m.getName().equals("post") && m.getParameterCount() == 1) { postMethod = m; break; }
                }
            }
            log("Init: hi=" + (handleInput != null) + " post=" + (postMethod != null));
            initDone = true;
        } catch (Exception e) {
            log("Init FAILED: " + e);
            initFailed = true;
        }
    }

    static void inject(int action, int x, int y) {
        initInjection();
        if (!initDone || handleInput == null) return;
        final int fa = action, fx = x, fy = y;
        Runnable task = () -> {
            try {
                if (fa == 2) handleInput.invoke(inputObj, (Object) new int[]{2, fx, fy});
                else if (fa == 0) handleInput.invoke(inputObj, (Object) new int[]{3, 1, fx, fy, 1});
                else if (fa == 1) handleInput.invoke(inputObj, (Object) new int[]{3, 0, fx, fy, 1});
            } catch (Exception e) {}
        };
        if (postMethod != null && appObj != null) {
            try { postMethod.invoke(appObj, task); } catch (Exception e) { task.run(); }
        } else { task.run(); }
        if (++injectedCount <= 5) log("Event a=" + action + " x=" + x + " y=" + y);
    }

    // ----- Keyboard injection -----
    static int keyInjCount = 0;
    static void injectKey(int action, int scancode) {
        initInjection();
        if (!initDone || handleInput == null) return;
        final int fa = action, fs = scancode;
        Runnable task = () -> {
            try {
                handleInput.invoke(inputObj, (Object) new int[]{5, fa == 0 ? 1 : 0, 0, 0, fs});
            } catch (Exception e) {}
        };
        if (postMethod != null && appObj != null) {
            try { postMethod.invoke(appObj, task); } catch (Exception e) { task.run(); }
        } else { task.run(); }
        if (++keyInjCount <= 5) log("KeyEvent: sc=" + scancode + " " + (action == 0 ? "DOWN" : "UP"));
    }

    static void injectBatchButtons(byte[] data, int total) {
        initInjection();
        if (!initDone || handleInput == null) return;
        final byte[] d = data.clone();
        final int t = total;
        Runnable task = () -> {
            for (int i = 0; i + 5 < t; i += 6) {
                int a = d[i] & 0xFF, x = ((d[i+1]&0xFF)<<8)|(d[i+2]&0xFF), y = ((d[i+3]&0xFF)<<8)|(d[i+4]&0xFF), b = d[i+5] & 0xFF;
                try { handleInput.invoke(inputObj, (Object) new int[]{3, a == 0 ? 1 : 0, x, y, b}); } catch (Exception e) {}
            }
        };
        if (postMethod != null && appObj != null) {
            try { postMethod.invoke(appObj, task); } catch (Exception e) { task.run(); }
        } else { task.run(); }
    }

    // ----- Game state detection -----
    static boolean isInGame = false;
    static Object stateObj;
    static Method isGameMethod, isMenuMethod;
    static int stateCheckCount = 0;

    static boolean checkGameState() {
        try {
            if (stateObj == null) {
                Class<?> vc = Class.forName("mindustry.Vars");
                Field sf = vc.getDeclaredField("state"); sf.setAccessible(true);
                stateObj = sf.get(null);
                if (stateObj != null) {
                    isGameMethod = stateObj.getClass().getMethod("isGame");
                    isMenuMethod = stateObj.getClass().getMethod("isMenu");
                }
            }
            if (stateObj != null && isGameMethod != null) {
                try {
                    Method m = stateObj.getClass().getMethod("isPlaying");
                    boolean r = (Boolean) m.invoke(stateObj);
                    if (++stateCheckCount % 10 == 0) log("State#"+stateCheckCount+": isPlaying=" + r);
                    return r;
                } catch (Exception e) {
                    boolean g = (Boolean) isGameMethod.invoke(stateObj);
                    boolean m2 = isMenuMethod != null ? (Boolean) isMenuMethod.invoke(stateObj) : true;
                    if (++stateCheckCount % 10 == 0) log("State: isGame=" + g + " isMenu=" + m2);
                    return g || !m2;
                }
            }
        } catch (Exception e) {}
        return true;
    }

    static void writeJoystickState(boolean enabled) {
        try {
            FileWriter fw = new FileWriter("/sdcard/joystick_enabled.txt");
            fw.write(enabled ? "1" : "0"); fw.close();
        } catch (Exception e) {}
        if (enabled != isInGame) {
            isInGame = enabled;
            log("Joystick " + (enabled ? "ON" : "OFF"));
        }
    }

    // ----- Main loop -----
    static void injectLoop() {
        File f = new File("/sdcard/sdl_touch.dat");
        File kf = new File("/sdcard/sdl_keys.dat");
        File bf = new File("/sdcard/sdl_button.dat");
        int counter = 0;
        while (running) {
            try {
                if (++counter % 50 == 0) writeJoystickState(checkGameState());
                if (f.length() >= 5) {
                    byte[] d = new byte[(int)f.length()];
                    int t = (new FileInputStream(f)).read(d);
                    new FileOutputStream(f).close();
                    injectBatch(d, t, 5);
                }
                if (kf.length() >= 2) {
                    byte[] kd = new byte[(int)kf.length()];
                    int kt = (new FileInputStream(kf)).read(kd);
                    new FileOutputStream(kf).close();
                    for (int i = 0; i + 1 < kt; i += 2) injectKey(kd[i] & 0xFF, kd[i+1] & 0xFF);
                }
                if (bf.length() >= 6) {
                    byte[] bd = new byte[(int)bf.length()];
                    int bt = (new FileInputStream(bf)).read(bd);
                    new FileOutputStream(bf).close();
                    injectBatchButtons(bd, bt);
                }
                Thread.sleep(16);
            } catch (Exception e) { try { Thread.sleep(32); } catch (Exception ex) {} }
        }
    }
}
