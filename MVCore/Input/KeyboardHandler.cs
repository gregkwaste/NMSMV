using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Input;


namespace MVCore.Input
{
    public class KeyboardHandler
    {
        //Designed for maintaining KeyStroke Status
        public Dictionary<Key, bool> KeyDown = new Dictionary<Key, bool>();

        //Constructor
        public KeyboardHandler()
        {
            KeyDown[Key.W] = false;
            KeyDown[Key.A] = false;
            KeyDown[Key.S] = false;
            KeyDown[Key.D] = false;
            KeyDown[Key.R] = false;
            KeyDown[Key.F] = false;
            KeyDown[Key.Q] = false;
            KeyDown[Key.E] = false;
            KeyDown[Key.Z] = false;
            KeyDown[Key.C] = false;
        }

        //Update Position
        public void updateState()
        {
            //Update Keyboard State
            KeyboardState state = Keyboard.GetState();

            //Just registers the Movement Keys
            KeyDown[Key.W] = state.IsKeyDown(Key.W);
            KeyDown[Key.A] = state.IsKeyDown(Key.A);
            KeyDown[Key.S] = state.IsKeyDown(Key.S);
            KeyDown[Key.D] = state.IsKeyDown(Key.D);
            KeyDown[Key.R] = state.IsKeyDown(Key.R);
            KeyDown[Key.F] = state.IsKeyDown(Key.F);
            KeyDown[Key.Q] = state.IsKeyDown(Key.Q);
            KeyDown[Key.E] = state.IsKeyDown(Key.E);
            KeyDown[Key.Z] = state.IsKeyDown(Key.Z);
            KeyDown[Key.C] = state.IsKeyDown(Key.C);

        }

        public int getKeyStatus(Key k)
        {

            return KeyDown[k] ? 1 : 0;

        }
    }
}
