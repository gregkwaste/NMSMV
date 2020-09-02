using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore.Engine
{
    public class RequestHandler
    {
        private Queue<ThreadRequest> req_queue;

        public RequestHandler()
        {
            req_queue = new Queue<ThreadRequest>();
        }

        public virtual void issueRequest(ref ThreadRequest req)
        {
            lock (req_queue)
            {
                req_queue.Enqueue(req);
            }
        }

        public bool hasOpenRequests()
        {
            return req_queue.Count > 0;
        }

        public ThreadRequest Fetch()
        {
            lock (req_queue)
            {
                return req_queue.Dequeue();
            }
        }

    }
}
