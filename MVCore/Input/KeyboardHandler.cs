using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Windows;
using System.Windows.Input;

namespace MVCore.Input
{
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
            return KeyState[k] ? 1 : 0;
        }


    }
}
