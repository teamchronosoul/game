using System.Collections.Generic;
using YP.Internal;

namespace YP
{
    public abstract class AnalyticsService : Service
    {

        public abstract void SendEvent(string eventKey, Dictionary<string, object> parameters);

    }
}



