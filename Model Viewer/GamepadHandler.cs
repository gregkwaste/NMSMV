using OpenTK.Input;
using System.Diagnostics;
using System;
class GamepadHandler
{
    //Designed for proper GamePads - XBOX Controllers and stuff
    //Controller ID
    private int ID = -1;
    //Struct for Stick Positions
    //States are saved as follows : LS_x LS_y RS_x RS_y
    private float[][] LR = new float[][] { new float[] { 0.0f, 0.0f },
                                           new float[] { 0.0f, 0.0f } };

    private float[][] dLR = new float[][] { new float[] { 0.0f, 0.0f },
                                           new float[] { 0.0f, 0.0f } };
    //Calibration coeffs
    private float[][] clibCoeffs = new float[][] { new float[] { 0.0f, 0.0f },
                                           new float[] { 0.0f, 0.0f } };


    //Buttons are : LB, RB, Y, X, B, A
    private float[] buttonStates = new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    

    //Constructor
    public GamepadHandler(int index)
    {
        //Set ID
        ID = index;
        calibrateAxes();
    }

    public void calibrateAxes()
    {
        GamePadState state = GamePad.GetState(ID);
        clibCoeffs[0][0] = state.ThumbSticks.Left.X;
        clibCoeffs[0][1] = state.ThumbSticks.Left.Y;
        clibCoeffs[1][0] = state.ThumbSticks.Right.X;
        clibCoeffs[1][1] = state.ThumbSticks.Right.Y;
    }

    //Update Position
    public void updateState()
    {
        //Update Sticks
        GamePadState state = GamePad.GetState(ID);
        float l_x = state.ThumbSticks.Left.X;
        float l_y = state.ThumbSticks.Left.Y;
        float r_x = state.ThumbSticks.Right.X;
        float r_y = state.ThumbSticks.Right.Y;

        //Update differences
        //dLR[0][0] = l_x -  LR[0][0];
        //dLR[0][1] = l_y -  LR[0][1]; 
        //dLR[1][0] = r_x - LR[1][0];
        //dLR[1][1] = r_y - LR[1][1];

        //Store the new values
        LR[0][0] = l_x;
        LR[0][1] = l_y;
        LR[1][0] = r_x;
        LR[1][1] = r_y;

        //Update Buttons
        buttonStates[0] = (float)state.Buttons.LeftShoulder;
        buttonStates[1] = (float)state.Buttons.RightShoulder;
        buttonStates[2] = (float)state.Buttons.Y;
        buttonStates[3] =  (float) state.Buttons.X;
        buttonStates[4] = (float)state.Buttons.B;
        buttonStates[5] = (float)state.Buttons.A;


    }

    public float getDisp(int stick, int axis)
    {
        return dLR[stick][axis];
    }

    public float getAxsState(int stick, int axis)
    {

        float length = (float) Math.Abs(LR[stick][axis]);
        if (length >= 0.25)
        {
            float dir = (float) Math.Round(LR[stick][axis]) / length;
            return dir * length;
        }
        else
        {
            return 0.0f;
        }
        
    }
    public void reportButtons()
    {
        Debug.WriteLine(getBtnState(0) + " " + getBtnState(1) + " " + getBtnState(2) + " " + getBtnState(3) + " " + getBtnState(4) + " " + getBtnState(5) + " ");
    }

    public float getBtnState(int btnId)
    {
        return buttonStates[btnId];
    }




}



