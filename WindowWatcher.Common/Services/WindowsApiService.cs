using System.Runtime.InteropServices;

namespace WindowWatcher.Common.Services;

public static class WindowsApiService
{
    #region Windows API Functions Declarations

    // This Function is used to get Active Window Title
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hwnd, string lpString, int cch);

    // This Function is used to get Handle for Active Window
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetForegroundWindow();

    // This Function is used to get Active process ID
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    #endregion

    #region User-defined Functions

    public static int GetWindowProcessId(IntPtr hwnd)
    {
        // This Function is used to get Active process ID
        GetWindowThreadProcessId(hwnd, out int pid);
        return pid;
    }

    public static IntPtr GetForegroundWindowHandle()
    {
        // This method is used to get Handle for Active Window using GetForegroundWindow() method present in user32.dll
        return GetForegroundWindow();
    }

    public static string GetActiveWindowTitle()
    {
        // This method is used to get active application's title using GetWindowText() method present in user32.dll
        IntPtr hwnd = GetForegroundWindowHandle();
        if (hwnd == IntPtr.Zero) return string.Empty;
        
        string lpText = new string('\0', 100);
        int intLength = GetWindowText(hwnd, lpText, lpText.Length);
        
        if (intLength <= 0 || intLength > lpText.Length) return "unknown";
        return lpText.Trim();
    }

    #endregion
}
