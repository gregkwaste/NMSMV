using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Input;


public class KeyboardHandler
{
    //Designed for maintaining KeyStroke Status
    public Dictionary<Key, bool> KeyDown = new Dictionary<Key, bool>();
    
    //Struct for Stick Positions
    //States are saved as follows : LS_x LS_y RS_x RS_y
    private float[][] LR = new float[][] { new float[] { 0.0f, 0.0f },
                                           new float[] { 0.0f, 0.0f },
                                           new float[] { 0.0f, 0.0f } };

    private float[][] dLR = new float[][] { new float[] { 0.0f, 0.0f },
                                            new float[] { 0.0f, 0.0f },
                                            new float[] { 0.0f, 0.0f } };
    //Calibration coeffs
    private float[][] clibCoeffs = new float[][] { new float[] { 0.0f, 0.0f },
                                           new float[] { 0.0f, 0.0f } };


    //Buttons are : LB, RB, Y, X, B, A
    private float[] buttonStates = new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };


    //Constructor
    public KeyboardHandler()
    {
        KeyDown[Key.W] = false;
        KeyDown[Key.A] = false;
        KeyDown[Key.S] = false;
        KeyDown[Key.D] = false;
        KeyDown[Key.R] = false;
        KeyDown[Key.F] = false;
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
        
    }

    public int getKeyStatus(Key k)
    {

        return KeyDown[k] ? 1: 0;

    }
}