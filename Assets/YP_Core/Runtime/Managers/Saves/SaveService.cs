using System;
using YP.Internal;

namespace YP
{
    public abstract class SaveService : Service
    {
        public abstract string GetData();
        public abstract void Commit(string data, Action<bool> onCommited);


    }
}


