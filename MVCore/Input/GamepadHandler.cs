using OpenTK.Input;
using System.Diagnostics;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;


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
        MOVE_X_NEG
    }

    public enum ControllerType
    {
        PS4_v2,
        XBOX
    }

    public delegate float MapGamepadStatus(float val);

    public abstract class BaseGamepadHandler
    {
        public int ID;
        private List<float> State = new List<float>();
        private Dictionary<ControllerActions, int> ActionMap = new Dictionary<ControllerActions, int>();
    
        public BaseGamepadHandler(int id)
        {
            ID = id;
            for (int i = 0; i < 18; i++)
                State.Add(0.0f);
        }

        public virtual void updateState()
        {
            GamePadCapabilities caps = GamePad.GetCapabilities(ID);
        
            if (!caps.IsConnected)
            {
                //Console.WriteLine("COntroller not connected");
                return;
            }
            
            GamePadState pad_status = GamePad.GetState(ID);

            //0,1,2,3 - DPAD UP, DOWN, LEFT, RIGHT
            State[0] = (float)pad_status.DPad.Up;
            State[1] = (float)pad_status.DPad.Down;
            State[2] = (float)pad_status.DPad.Left;
            State[3] = (float)pad_status.DPad.Right;

            //4,5,6,7 - BUTTONS (UP), (DOWN), (LEFT), (RIGHT)
            State[4] = (float)pad_status.Buttons.Y;
            State[5] = (float)pad_status.Buttons.A;
            State[6] = (float)pad_status.Buttons.X;
            State[7] = (float)pad_status.Buttons.B;

            //8, 9 - BUTTONS (LB), (RB)
            State[8] = (float)pad_status.Buttons.LeftShoulder;
            State[9] = (float)pad_status.Buttons.RightShoulder;

            //10, 11 - BUTTONS (LS), (RS)
            State[10] = (float)pad_status.Buttons.LeftStick;
            State[11] = (float)pad_status.Buttons.RightStick;

            //12, 13 - TRIGGERS (Left), (Right) 
            State[12] = pad_status.Triggers.Left;
            State[13] = pad_status.Triggers.Right;

            //14, 15 - STICKS (Left Horizontal), (Left Vertical)
            State[14] = pad_status.ThumbSticks.Left.X;
            State[15] = pad_status.ThumbSticks.Left.Y;

            //14, 15 - STICKS (Right Horizontal), (Right Vertical)
            State[16] = pad_status.ThumbSticks.Right.X;
            State[17] = pad_status.ThumbSticks.Right.Y;

        }


        public virtual void setActionMap(Dictionary<ControllerActions, int> map)
        {
            ActionMap = map;
        }

        public abstract float getAction(ControllerActions action);

        public int getActionButton(ControllerActions action)
        {
            return ActionMap[action];
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
            Console.WriteLine(s);
        }

    
        public virtual void reportAxes()
        {
            string s = "Axes: ";
            for (int i = 12; i < 18; i++)
                s += " " + getState(i).ToString();
            Console.WriteLine(s);
        }

        public bool isConnected()
        {
            return GamePad.GetCapabilities(ID).IsConnected;
        }

        public string getName()
        {
            return GamePad.GetName(ID);
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
            Console.WriteLine(s);
        }

        public override void reportAxes()
        {
            string s = "PS4 Axes : ";
            for (int i = 12; i < 18; i++)
                s += " " + getState((PSGamePadLayout)i).ToString();
            Console.WriteLine(s);
        }

    }




}