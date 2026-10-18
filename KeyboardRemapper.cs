using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Collections.Generic;

class KeyboardRemapper
{
    // --- Windows API Imports ---
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
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // --- Constants ---
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    // Keys to manipulate (Virtual Key Codes)
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;

    private static string toggleKey = "`";

    // --- App State ---
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;
    
    // false = QWERTY (Default), true = AZERTY/Gaming (Alt)
    private static bool isAltProfileActive = false; 

    private static Dictionary<string, int> keyMap = new Dictionary<string, int>();
    private static Dictionary<int, string> reverseKeyMap = new Dictionary<int, string>();

    private static string connectionsFile = "keyboardProfile.txt";

    private static void InitializeKeyMap()
    {
        keyMap["SHIFT"] = 0x10;
        keyMap["CTRL"] = 0x11;
        keyMap["ALT"] = 0x12;
        keyMap["SPACE"] = 0x20;
        keyMap["ENTER"] = 0x0D;
        keyMap["ESC"] = 0x1B;
        keyMap["TAB"] = 0x09;
        keyMap["A"] = 0x41;
        keyMap["B"] = 0x42;
        keyMap["C"] = 0x43;
        keyMap["D"] = 0x44;
        keyMap["E"] = 0x45;
        keyMap["F"] = 0x46;
        keyMap["G"] = 0x47;
        keyMap["H"] = 0x48;
        keyMap["I"] = 0x49;
        keyMap["J"] = 0x4A;
        keyMap["K"] = 0x4B;
        keyMap["L"] = 0x4C;
        keyMap["M"] = 0x4D;
        keyMap["N"] = 0x4E;
        keyMap["O"] = 0x4F;
        keyMap["P"] = 0x50;
        keyMap["Q"] = 0x51;
        keyMap["R"] = 0x52;
        keyMap["S"] = 0x53;
        keyMap["T"] = 0x54;
        keyMap["U"] = 0x55;
        keyMap["V"] = 0x56;
        keyMap["W"] = 0x57;
        keyMap["X"] = 0x58;
        keyMap["Y"] = 0x59;
        keyMap["Z"] = 0x5A;
        keyMap["`"] = 0xC0;
        keyMap["1"] = 0x31;
        keyMap["2"] = 0x32;
        keyMap["3"] = 0x33;
        keyMap["4"] = 0x34;
        keyMap["5"] = 0x35;
        keyMap["6"] = 0x36;
        keyMap["7"] = 0x37;
        keyMap["8"] = 0x38;
        keyMap["9"] = 0x39;
        keyMap["0"] = 0x30;
        foreach (var kvp in keyMap)
        {
            reverseKeyMap[kvp.Value] = kvp.Key;
        }
    }

    private static Dictionary<string, string> keyConnections = new Dictionary<string, string>();

    private static void SaveKeyConnections()
    {
        using (var writer = new System.IO.StreamWriter(connectionsFile))
        {
            writer.WriteLine(toggleKey);
            foreach (var kvp in keyConnections)
            {
                writer.WriteLine(kvp.Key + " to " + kvp.Value);
            }
        }
    }

    private static bool LoadKeyConnections()
    {
        if (!System.IO.File.Exists(connectionsFile))
        {
            return false;
        }

        using (var reader = new System.IO.StreamReader(connectionsFile))
        {
            string firstLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine))
                return false;

            string loadedToggleKey = firstLine.Trim().ToUpperInvariant();
            if (!keyMap.ContainsKey(loadedToggleKey) || loadedToggleKey == "SHIFT" || loadedToggleKey == "CTRL")
            {
                Console.WriteLine("The saved toggle key is invalid.");
                SetToggleKey();
            }
            else
            {
                toggleKey = loadedToggleKey;
                keyConnections.Clear();
            }

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(" to ");
                if (parts.Length == 2)
                {
                    AddKeyConnection(parts[0].Trim().ToUpperInvariant(), parts[1].Trim().ToUpperInvariant());
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    Console.WriteLine("Invalid saved mapping: " + line);
                }
            }
        }

        return true;
    }

    private static void AddKeyConnection(string from, string to)
    {
        if (keyMap.ContainsKey(from) && keyMap.ContainsKey(to) && from != to && from != "`" && to != "`" && from != "SHIFT" && from != "CTRL" && to != "SHIFT" && to != "CTRL") {
            keyConnections[from] = to;
        }
        else
        {
            Console.WriteLine("Invalid key mapping. Please ensure both keys are valid.");
        }
    }

    private static void SetToggleKey()
    {
        Console.WriteLine("Setup a toggle key to press with CTRL and SHIFT. The Suggested key is ` (backtick)");
        do
        {
            Console.WriteLine("Enter your chosen toggle key:");
            string toggleInput = Console.ReadLine();
            if (keyMap.ContainsKey(toggleInput) && toggleInput != "SHIFT" && toggleInput != "CTRL")
            {
                toggleKey = toggleInput;
            }
            else
            {
                Console.WriteLine("Invalid key. Please enter a valid key.");
            }

        } while (toggleKey == null);
    }
    private static void AddKeyRemappings()
    {
        string input;
        Console.WriteLine("Illegal keys are your chosen key, SHIFT, CTRL, and the toggle key " + toggleKey);
        do {
            Console.WriteLine("Enter a key remapping (e.g., Q to A, DONE to end):");
            input = Console.ReadLine();
            if (input != "DONE")
            {
                var parts = input.Split(" to ");
                if (parts.Length == 2)
                {
                    AddKeyConnection(parts[0].Trim().ToUpper(), parts[1].Trim().ToUpper());
                }
                else 
                {
                    Console.WriteLine("Invalid format. Please use 'FROM to TO'.");
                }
            }
        } while (input != "DONE");
    }

    private static bool RemoveKeyConnection(string from)
    {
        if (keyConnections.ContainsKey(from))
        {
            keyConnections.Remove(from);
            Console.WriteLine("Key connection for " + from + " has been removed.");
            return true;
        }
        return false;
    }
    private static void RemoveKeyRemappings()
    {
        string input;
        do {
            Console.WriteLine("Enter a key remapping to remove (e.g. Q, DONE to end):");
            input = Console.ReadLine();
            if (input != "DONE")
            {
                var parts = input.Split(" to ");
                if (parts.Length == 2)
                {
                    if (!RemoveKeyConnection(parts[0].Trim().ToUpper()))
                    {
                        Console.WriteLine("No such key remapping found.");
                    }
                }
                else 
                {
                    Console.WriteLine("Invalid format. Please use 'FROM to TO'.");
                }
            }
        } while (input != "DONE");
    }

    static void Main(string[] args)
    {

        InitializeKeyMap();
        
        if (LoadKeyConnections() == false)
        {
            Console.WriteLine("No saved mappings found.");
            SetToggleKey();
            AddKeyRemappings();
        }
        else 
        {
            Console.WriteLine("Key connections loaded successfully. Would you like change anything? (Y/N)");
            string changeInput = Console.ReadLine();
            if (changeInput != null && changeInput.Trim().ToUpper() == "Y")
            {
                while (true)
                {
                    Console.WriteLine("Would you like to change the \"toggle\" key, \"add\" new mappings, \"remove\" existing mappings or \"reset\" all mappings? Type DONE to exit.");
                    string choice = Console.ReadLine().Trim().ToUpper();
                    if (choice == "TOGGLE")
                    {
                        SetToggleKey();
                    }
                    else if (choice == "ADD")
                    {
                        AddKeyRemappings();
                    }
                    else if (choice == "REMOVE")
                    {
                        RemoveKeyRemappings();
                    }
                    else if (choice == "RESET")
                    {
                        keyConnections.Clear();
                        Console.WriteLine("All mappings have been reset.");
                    }
                    else if (choice == "DONE")
                    {
                        break;
                    }
                    else 
                    {
                        Console.WriteLine("Invalid choice.");
                    }
                }
            }
        }
        SaveKeyConnections();

        Console.WriteLine("⌨️ Keyboard Profile Switcher Initialized.");
        Console.WriteLine("Press [Ctrl + Shift + " + toggleKey + "] to swap profiles.");
        Console.WriteLine("Current Profile: DEFAULT (QWERTY)");

        _hookID = SetHook(_proc);
        
        // This keeps our console app running so the hook stays alive
        // Standard Console.ReadLine() won't process Windows messages correctly, 
        // so we use a basic message loop.
        System.Windows.Forms.Application.Run(); 

        UnhookWindowsHookEx(_hookID);
    }

    private static void LiftAllKeys()
    {
        foreach (var key in keyMap.Values)
        {
            if (key != keyMap[toggleKey] && GetAsyncKeyState(key) != 0) {
                keybd_event((byte)key, 0, KEYEVENTF_KEYUP, 0);
            }
        }
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
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int flags = Marshal.ReadInt32(lParam + 8);

            // 1. IGNORE ARTIFICIAL KEYS SENT BY OUR OWN APP TO PREVENT INFINITE LOOPS
            bool isInjected = (flags & 0x10) != 0; 
            if (isInjected)
            {
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }

            // 2. CHECK FOR PROFILE TOGGLE HOTKEY (Ctrl + Shift + toggleKey)
            if (wParam == (IntPtr)WM_KEYDOWN && vkCode == keyMap[toggleKey])
            {
                bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool shiftPressed = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

                if (ctrlPressed && shiftPressed)
                {
                    LiftAllKeys();
                    isAltProfileActive = !isAltProfileActive;
                    Console.Clear();
                    Console.WriteLine(isAltProfileActive 
                        ? "🔄 Profile Swapped: ALTERNATE ACTIVE" 
                        : "🔄 Profile Swapped: DEFAULT ACTIVE (QWERTY)");
                    
                    return (IntPtr)1; // Suppress the toggle key so it doesn't type on screen
                }
            }

            // 3. REMAP LOGIC IF ALTERNATE PROFILE IS ACTIVE
            if (isAltProfileActive)
            {
                uint eventFlag = (wParam == (IntPtr)WM_KEYUP) ? KEYEVENTF_KEYUP : 0;

                if (reverseKeyMap.ContainsKey(vkCode)) {
                    string vkInput = reverseKeyMap[vkCode];
                // Example: Swap Q into A
                    if (keyConnections.ContainsKey(vkInput))
                    {
                        keybd_event((byte)keyMap[keyConnections[vkInput]], 0, eventFlag, 0);
                        return (IntPtr)1; // Block the original physical key
                    }
                }
            }
        }

        // Pass any unhandled keypresses along to the system/game normally
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }
}