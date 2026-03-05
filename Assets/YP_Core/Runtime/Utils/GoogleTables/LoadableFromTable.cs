using System.Collections.Generic;
using UnityEngine;


namespace YP
{
    public abstract class LoadableFromTable : ScriptableObject
    {
        public abstract void LoadData(Dictionary<string, Table> allTables);

}
}


