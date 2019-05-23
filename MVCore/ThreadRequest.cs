using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
   public enum THREAD_REQUEST_TYPE
    {
        NEW_SCENE_REQUEST,
        UPDATE_SCENE_REQUEST,
        RESIZE_REQUEST,
        TERMINATE_REQUEST,
        COMPILE_SHADER_REQUEST,
        MODIFY_SHADER_REQUEST,
        RESUME_RENDER_REQUEST,
        PAUSE_RENDER_REQUEST,
        NULL
    };

    public enum THREAD_REQUEST_STATUS
    {
        ACTIVE,
        FINISHED,
        NULL
    };

    public class ThreadRequest
    {
        public List<object> arguments;
        public THREAD_REQUEST_TYPE type;
        public THREAD_REQUEST_STATUS status;
        public ThreadRequest()
        {
            type = THREAD_REQUEST_TYPE.NULL;
            status = THREAD_REQUEST_STATUS.ACTIVE;
            arguments = new List<object>();
        }
    }
}
