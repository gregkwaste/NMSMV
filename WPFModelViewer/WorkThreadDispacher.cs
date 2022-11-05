using System;
using System.Collections.Generic;
using System.Threading;
using MVCore;
using MVCore.Utils;


namespace WPFModelViewer
{
    class MyTask
    {
        public int task_uid;
        public Thread thread;
        public ThreadRequest thread_request;
    }
    
    class WorkThreadDispacher : System.Timers.Timer
    {
        private List<MyTask> tasks = new List<MyTask>();
        private int taskGUIDCounter = 0;

        public WorkThreadDispacher()
        {
            Interval = 10; //10 ms
            Elapsed += queryTasks;
        }

        public void sendRequest(ThreadRequest tr)
        {
            tasks.Add(createTask(tr));
        }

        private MyTask createTask(ThreadRequest tr)
        {
            MyTask tk = new MyTask();
            tk.task_uid = taskGUIDCounter;
            tk.thread_request = tr;

            //Create and start Thread
            Thread t = null;
            switch (tr.type)
            {
                case THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST:
                    string gameDir = (string) tr.arguments[0];
                    ResourceManager resMgr = (ResourceManager) tr.arguments[1];
                    t = new Thread(() => NMSUtils.loadNMSArchives(gameDir, ref resMgr, ref tk.thread_request.response));
                    break;
                default:
                    MVCore.Common.CallBacks.Log("DISPATCHER : Unsupported Thread Request");
                    break;
            }

            tk.thread = t;
            tk.thread.IsBackground = true;
            tk.thread.Start();
            
            return tk;
        }



        private void queryTasks(object sender, System.Timers.ElapsedEventArgs e)
        {
            int i = 0;
            while(i < tasks.Count)
            {
                MyTask tk = tasks[i];

                //Check if task has finished 
                if (!tk.thread.IsAlive)
                {
                    lock (tk.thread_request)
                    {
                        tk.thread_request.status = THREAD_REQUEST_STATUS.FINISHED;
                    }
                    tasks.RemoveAt(i);
                    continue;
                }
                i++;
            }
        }



    }
}
