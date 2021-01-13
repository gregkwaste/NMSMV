using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
   public enum THREAD_REQUEST_TYPE
    {
        GL_RESIZE_REQUEST,
        GL_COMPILE_SHADER_REQUEST,
        GL_COMPILE_ALL_SHADERS_REQUEST,
        GL_MODIFY_SHADER_REQUEST,
        GL_RESUME_RENDER_REQUEST,
        GL_PAUSE_RENDER_REQUEST,
        QUERY_GLCONTROL_STATUS_REQUEST,
        MOUSEPOSITION_INFO_REQUEST,
        NEW_SCENE_REQUEST,
        NEW_TEST_SCENE_REQUEST,
        UPDATE_SCENE_REQUEST,
        CHANGE_MODEL_PARENT_REQUEST,
        TERMINATE_REQUEST,  
        LOAD_NMS_ARCHIVES_REQUEST,
        OPEN_FILE_REQUEST,
        GIZMO_PICKING_REQUEST,
        INIT_RESOURCE_MANAGER,
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
        public int response;
        public ThreadRequest()
        {
            type = THREAD_REQUEST_TYPE.NULL;
            status = THREAD_REQUEST_STATUS.ACTIVE;
            arguments = new List<object>();
            response = 0;
        }
    }
}
