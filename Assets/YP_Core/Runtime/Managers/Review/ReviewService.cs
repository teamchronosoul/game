using System;
using YP.Internal;


namespace YP
{
    public abstract class ReviewService : Service
    {
        public abstract void Request(Action onOpened, Action onClosed);

    }
}

