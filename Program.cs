using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace BinaryIndices
{
    static class Program
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static Dictionary<char, string> superscripts = new Dictionary<char, string>
        {
            {'0',"⁰"}, {'1',"¹"}, {'2',"²"}, {'3',"³"}, {'4',"⁴"}, {'5',"⁵"}, {'6',"⁶"}, {'7',"⁷"}, {'8',"⁸"}, {'9',"⁹"}
        };
        private static Dictionary<char, string> subscripts = new Dictionary<char, string>
        {
            {'0',"₀"}, {'1',"₁"}, {'2',"₂"}, {'3',"₃"}, {'4',"₄"}, {'5',"₅"}, {'6',"₆"}, {'7',"₇"}, {'8',"₈"}, {'9',"₉"}
        };

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [STAThread]
        static void Main()
        {
            // Закрываем старые процессы Python, если они зависли
            try {
                Process.Start(new ProcessStartInfo("taskkill", "/f /im pythonw.exe") { CreateNoWindow = true, UseShellExecute = false });
                Process.Start(new ProcessStartInfo("taskkill", "/f /im python.exe") { CreateNoWindow = true, UseShellExecute = false });
            } catch {}

            ApplicationConfiguration.Initialize();
            _hookID = SetHook(_proc);
            Application.Run(); // Запуск фонового цикла Windows
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // Клавиши 0-9 на основной клавиатуре (48-57)
                if (vkCode >= 48 && vkCode <= 57)
                {
                    bool isCapsOn = (GetKeyState(0x14) & 1) == 1; // VK_CAPITAL
                    bool isShiftPressed = (GetKeyState(0x10) & 0x8000) != 0; // VK_SHIFT

                    if (isCapsOn)
                    {
                        char digit = (char)('0' + (vkCode - 48));
                        string textToSend = isShiftPressed ? subscripts[digit] : superscripts[digit];

                        // Синхронно отправляем символ
                        SendKeys.SendWait(textToSend);

                        // Возвращаем 1 — полная блокировка оригинальной цифры системой Windows
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}
