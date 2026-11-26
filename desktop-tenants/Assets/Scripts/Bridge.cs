using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// Calls into Native/DesktopBridge.m. Outside a macOS player the overlay calls are no-ops
    /// and the desktop is an empty screen, so the scene still runs in the editor.
    /// </summary>
    public static class Bridge
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        const string Lib = "DesktopBridge";
        [DllImport(Lib)] static extern int DB_MakeOverlay(int screen);
        [DllImport(Lib)] static extern void DB_SetClickThrough(int on);
        [DllImport(Lib)] static extern void DB_Screen(float[] out6);
        [DllImport(Lib)] static extern void DB_Mouse(float[] out3);
        [DllImport(Lib)] static extern int DB_Windows(float[] buf, int max);
        [DllImport(Lib)] static extern void DB_Refresh();
        [DllImport(Lib)] static extern string DB_ScreenList();
        [DllImport(Lib)] static extern void DB_Stats(float[] out3);

        /// <summary>[cpu %, memory used GB, memory total GB]; CPU is measured since the last call.</summary>
        public static void Stats(float[] out3) => DB_Stats(out3);

        /// <summary>Covers monitor <paramref name="screen"/> (0 = main, clamped to what is connected).</summary>
        public static bool MakeOverlay(int screen) => DB_MakeOverlay(screen) != 0;
        public static void SetClickThrough(bool on) => DB_SetClickThrough(on ? 1 : 0);
        public static void Screen(float[] out6) => DB_Screen(out6);
        public static void Mouse(float[] out3) => DB_Mouse(out3);
        public static int Windows(float[] buf, int max) => DB_Windows(buf, max);
        public static void Refresh() => DB_Refresh();
        public static string ScreenList() => DB_ScreenList();
#else
        public static bool MakeOverlay(int screen) => true;
        public static void SetClickThrough(bool on) { }
        public static void Screen(float[] o)
        {
            o[0] = UnityEngine.Screen.width; o[1] = UnityEngine.Screen.height;
            o[2] = 0; o[3] = 0; o[4] = o[0]; o[5] = o[1];
        }
        public static void Mouse(float[] o) { o[0] = o[1] = o[2] = 0; }
        public static int Windows(float[] buf, int max) => 0;
        public static void Refresh() { }
        public static string ScreenList() => "editor";
        public static void Stats(float[] o) { o[0] = 0; o[1] = 0; o[2] = 16; }
#endif
    }
}
