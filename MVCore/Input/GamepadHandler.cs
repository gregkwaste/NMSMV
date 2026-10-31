using OpenTK.Input;
using System.Diagnostics;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MVCore.Input
{
    
    public enum PSGamePadLayout //THE ORDERING AFFECTS THE MAPPING TO THE DEFAULT XBOX LAYOUT
    {
        UP,
        DOWN,
        LEFT,
        RIGHT,
        TRIANGLE,
        SQUARE,
        CIRCLE,
        CROSS,
        L1,
        R1,
        L3,
        R3,
        R_H,
        R2,
        L_H,
        L_V,
        R_V,
        L2
    }

    public enum XBOXGamePadLayout
    {
        UP,
        DOWN,
        LEFT,
        RIGHT,
        Y,
        A,
        X,
        B,
        LB,
        RB,
        LS,
        RS,
        LT,
        RT,
        L_H,
        L_V,
        R_H,
        R_V
    }

    public enum ControllerActions
    {
        ACCELERATE,
        DECELERATE,
        CAMERA_MOVE_H,
        CAMERA_MOVE_V,
        MOVE_Z,
        MOVE_Y,
        MOVE_Y_POS,
        MOVE_Y_NEG,
        MOVE_X,
        MOVE_X_POS,
        MOVE_X_NEG,
        ROTATE_SCENE_POS,
        ROTATE_SCENE_NEG,
        FRAME_SCENE
    }

    public delegate float MapGamepadStatus(float val);

    public abstract class BaseGamepadHandler
    {
        public int ID;
        private List<float> State = new List<float>();
        private Dictionary<ControllerActions, int> ActionMap = new Dictionary<ControllerActions, int>();
        private int slot = -1; //GLFW joystick slot of the pad in use, -1 while none is connected
        private static bool glfwReady = false;
        private const int JoystickLastSlot = 15; //GLFW_JOYSTICK_LAST
    
        public BaseGamepadHandler(int id)
        {
            ID = id;
            for (int i = 0; i < 18; i++)
                State.Add(0.0f);
        }

        //Poll the pad, once per frame. GLFW wants this on the thread that initialized it, the UI thread here
        public virtual void updateState()
        {
            if (!glfwReady)
                glfwReady = GLFW.Init(); //The GL control has usually done it already, then this does nothing
            
            if (glfwReady && (slot < 0 || !GLFW.JoystickIsGamepad(slot)))
                slot = findGamepad(); //Pick up a pad plugged in later, or another one when this one goes

            if (slot < 0 || !GLFW.GetGamepadState(slot, out OpenTK.Windowing.GraphicsLibraryFramework.GamepadState pad_status))
            {
                for (int i = 0; i < State.Count; i++)
                    State[i] = 0.0f;
                return;
            }

            readState(ref pad_status);
        }

        //First joystick slot GLFW has a gamepad mapping for, starting with the preferred one
        private int findGamepad()
        {
            for (int i = 0; i <= JoystickLastSlot; i++)
            {
                int jid = (ID + i) % (JoystickLastSlot + 1);
                if (GLFW.JoystickIsGamepad(jid))
                    return jid;
            }
            return -1;
        }

        //GLFW reports every pad it knows (Xbox, DualShock/DualSense, Switch Pro...) in the Xbox layout.
        //Store it in XBOXGamePadLayout order, triggers in 0..1 and sticks with up positive
        private unsafe void readState(ref OpenTK.Windowing.GraphicsLibraryFramework.GamepadState pad_status)
        {
            //0,1,2,3 - DPAD UP, DOWN, LEFT, RIGHT
            State[0] = pad_status.Buttons[11];
            State[1] = pad_status.Buttons[13];
            State[2] = pad_status.Buttons[14];
            State[3] = pad_status.Buttons[12];

            //4,5,6,7 - BUTTONS Y, A, X, B
            State[4] = pad_status.Buttons[3];
            State[5] = pad_status.Buttons[0];
            State[6] = pad_status.Buttons[2];
            State[7] = pad_status.Buttons[1];

            //8, 9 - BUTTONS (LB), (RB)
            State[8] = pad_status.Buttons[4];
            State[9] = pad_status.Buttons[5];

            //10, 11 - BUTTONS (LS), (RS)
            State[10] = pad_status.Buttons[9];
            State[11] = pad_status.Buttons[10];

            //12, 13 - TRIGGERS (Left), (Right). GLFW gives -1 at rest
            State[12] = 0.5f * (pad_status.Axes[4] + 1.0f);
            State[13] = 0.5f * (pad_status.Axes[5] + 1.0f);

            //14, 15 - STICKS (Left Horizontal), (Left Vertical). GLFW has down positive
            State[14] = pad_status.Axes[0];
            State[15] = -pad_status.Axes[1];

            //16, 17 - STICKS (Right Horizontal), (Right Vertical)
            State[16] = pad_status.Axes[2];
            State[17] = -pad_status.Axes[3];
        }


        public virtual void setActionMap(Dictionary<ControllerActions, int> map)
        {
            ActionMap = map;
        }

        public abstract float getAction(ControllerActions action);

        //-1 when the action has no button
        public int getActionButton(ControllerActions action)
        {
            return ActionMap.TryGetValue(action, out int btn) ? btn : -1;
        }

        public virtual float getState(int id)
        {
            return State[id];
        }

        public virtual void reportButtons()
        {
            string s = "Buttons : ";
            for (int i = 0;i<12; i++)
                s += " " + getState(i).ToString();
            Common.CallBacks.Log(s);
        }

    
        public virtual void reportAxes()
        {
            string s = "Axes: ";
            for (int i = 12; i < 18; i++)
                s += " " + getState(i).ToString();
            Common.CallBacks.Log(s);
        }

        public bool isConnected()
        {
            return slot >= 0;
        }

        public string getName()
        {
            return slot >= 0 ? (GLFW.GetGamepadName(slot) ?? "Gamepad") : "None";
        }


    }


    //Reads any pad GLFW has a mapping for, since GLFW puts them all in the Xbox layout
    public class XBOXGamePadHandler : BaseGamepadHandler
    {
        //Sticks rest a bit off center, worn ones more so
        private const float StickDeadZone = 0.15f;
        private const float TriggerDeadZone = 0.05f;

        public XBOXGamePadHandler(int id) : base(id)
        {
            setDefaultActionMap();
        }

        public override float getAction(ControllerActions action)
        {
            int btn = getActionButton(action);
            if (btn < 0)
                return 0.0f;

            //Cut the dead zone and rescale the rest, so the response starts at 0 instead of jumping.
            //Buttons are 0 or 1 and come through unchanged
            float val = getState(btn);
            float dead = btn >= (int) XBOXGamePadLayout.L_H ? StickDeadZone : TriggerDeadZone;
            float mag = Math.Abs(val);
            if (mag < dead)
                return 0.0f;
            return Math.Sign(val) * Math.Min((mag - dead) / (1.0f - dead), 1.0f);
        }

        //Left stick moves and strafes, the triggers go up (RT) and down (LT), the right stick looks around,
        //the bumpers turn the scene like Q/E and Y frames the scene like Home
        private void setDefaultActionMap()
        {
            Dictionary<ControllerActions, int> actMap = new Dictionary<ControllerActions, int>
            {
                [ControllerActions.MOVE_X] = (int)XBOXGamePadLayout.L_H,
                [ControllerActions.MOVE_Z] = (int)XBOXGamePadLayout.L_V,
                [ControllerActions.MOVE_Y_POS] = (int)XBOXGamePadLayout.RT,
                [ControllerActions.MOVE_Y_NEG] = (int)XBOXGamePadLayout.LT,
                [ControllerActions.CAMERA_MOVE_H] = (int)XBOXGamePadLayout.R_H,
                [ControllerActions.CAMERA_MOVE_V] = (int)XBOXGamePadLayout.R_V,
                [ControllerActions.ROTATE_SCENE_POS] = (int)XBOXGamePadLayout.RB,
                [ControllerActions.ROTATE_SCENE_NEG] = (int)XBOXGamePadLayout.LB,
                [ControllerActions.FRAME_SCENE] = (int)XBOXGamePadLayout.Y
            };
            setActionMap(actMap);
        }
    }


    public class PS4GamePadHandler : BaseGamepadHandler
    {
        Dictionary<PSGamePadLayout, MapGamepadStatus> PS4ButtonFunctionMap = new Dictionary<PSGamePadLayout, MapGamepadStatus>();

        public PS4GamePadHandler(int id) : base(id)
        {
            //Initiallize value mapping functions
            MapGamepadStatus oneToOne = new MapGamepadStatus(map_1_TO_1);
            MapGamepadStatus RV_Map = new MapGamepadStatus(R_V_Map);
            MapGamepadStatus RH_Map = new MapGamepadStatus(R_H_Map);
            MapGamepadStatus R2_Map = new MapGamepadStatus(R_2_Map);
            MapGamepadStatus L2_Map = new MapGamepadStatus(L_2_Map);

            //Initialize the Function map for the DS4 Controller

            //Set 1-1 mapping for buttons
            PS4ButtonFunctionMap[PSGamePadLayout.CIRCLE] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.SQUARE] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.TRIANGLE] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.CROSS] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.UP] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.DOWN] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.LEFT] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.RIGHT] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.R1] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.L1] = oneToOne;

            //Set Mapping for triggers and sticks

            PS4ButtonFunctionMap[PSGamePadLayout.L_H] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.L_V] = oneToOne;
            PS4ButtonFunctionMap[PSGamePadLayout.R_H] = RH_Map;
            PS4ButtonFunctionMap[PSGamePadLayout.R_V] = RV_Map;
            PS4ButtonFunctionMap[PSGamePadLayout.R2] = R2_Map;
            PS4ButtonFunctionMap[PSGamePadLayout.L2] = L2_Map;

        
            setDefaultActionMap();

        }
 
        public override float getAction(ControllerActions action)
        {
            return getState((PSGamePadLayout) getActionButton(action));
        }

        public float getState(PSGamePadLayout btn)
        {
            float base_val = base.getState((int)btn);
            base_val = PS4ButtonFunctionMap[btn](base_val) * 100.0f;
            base_val = (float) Math.Round(base_val, 1);

            if (Math.Abs(base_val) < 10.0f)
                base_val = 0.0f;
        
            return base_val / 100.0f;
        }

        private void setDefaultActionMap()
        {
            //Initialize ActionMap
            Dictionary<ControllerActions, int> actMap = new Dictionary<ControllerActions, int>
            {
                [ControllerActions.ACCELERATE] = (int)PSGamePadLayout.R2,
                [ControllerActions.DECELERATE] = (int)PSGamePadLayout.L2,
                [ControllerActions.CAMERA_MOVE_H] = (int)PSGamePadLayout.R_H,
                [ControllerActions.CAMERA_MOVE_V] = (int)PSGamePadLayout.R_V,
                [ControllerActions.MOVE_Y_POS] = (int)PSGamePadLayout.R1,
                [ControllerActions.MOVE_Y_NEG] = (int)PSGamePadLayout.L1,
                [ControllerActions.MOVE_X] = (int)PSGamePadLayout.L_H
            };
            setActionMap(actMap); //Set this map
        }

        public float map_1_TO_1(float val)
        {
            return val;
        }

        public float L_2_Map(float val)
        {
            return -0.5f * val + 0.5f;
        }

        public float R_2_Map(float val)
        {
            return 1.33f * val;
        }

        public float R_H_Map(float val)
        {
            return 2.0f * val - 1.0f;
        }

        public float R_V_Map(float val)
        {
            return -val;
        }

        public override void reportButtons()
        {
            string s = "PS4 Buttons : ";
            for (int i = 0; i < 12; i++)
                s += " " + getState((PSGamePadLayout) i).ToString();
            Common.CallBacks.Log(s);
        }

        public override void reportAxes()
        {
            string s = "PS4 Axes : ";
            for (int i = 12; i < 18; i++)
                s += " " + getState((PSGamePadLayout)i).ToString();
            Common.CallBacks.Log(s);
        }

    }




}
