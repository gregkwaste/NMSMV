using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics.Contracts;
using OpenTK.Mathematics;

namespace MVCore.Input
{
    public class MVMouseState
    {
        public Dictionary<System.Windows.Input.MouseButton, bool> ButtonState = new();
        public Vector2 Position;
        public Vector2 Delta;
        public Vector2 PrevPosition;
        
        public MVMouseState()
        {
            foreach (System.Windows.Input.MouseButton k in Enum.GetValues(typeof(System.Windows.Input.MouseButton)))
            {
                ButtonState[k] = false;
            }
        }

        public void SetButtonState(System.Windows.Input.MouseButton btn, bool value)
        {
            lock (ButtonState)
                ButtonState[btn] = value;
        }

        public void Clear()
        {
            lock (ButtonState)
                foreach (System.Windows.Input.MouseButton btn in new List<System.Windows.Input.MouseButton>(ButtonState.Keys))
                    ButtonState[btn] = false;
        }


    }

    public class MVKeyboardState
    {
        //Designed for maintaining KeyStroke Status
        public Dictionary<Key, bool> KeyState = new Dictionary<Key, bool>();
        
        //Constructor
        public MVKeyboardState()
        {
            foreach (Key k in Enum.GetValues(typeof(Key)))
            {
                KeyState[k] = false;
            }
        }

        public void SetKeyState(Key key, bool value)
        {
            lock (KeyState)
                KeyState[key] = value;
        }

        public void updateState(MVKeyboardState ref_state)
        {
            foreach (Key k in ref_state.KeyState.Keys)
            {
                KeyState[k] = ref_state.KeyState[k];
            }
        }

        public int getKeyStatus(Key k)
        {
            lock (KeyState)
                return KeyState[k] ? 1 : 0;
        }

        public void Clear()
        {
            lock (KeyState)
                foreach (Key k in new List<Key>(KeyState.Keys))
                    KeyState[k] = false;
        }

    }
}
