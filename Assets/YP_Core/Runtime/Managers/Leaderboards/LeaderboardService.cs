using System;
using System.Collections.Generic;
using YP.Internal;

namespace YP
{
    public abstract class LeaderboardService : Service
    {

        public abstract bool availableForThisPlayer { get; }

        public abstract void SetScore(string leaderboardKey, int score);

        public abstract void GetEntries
            (string leaderboardKey, Action<List<LeaderboardEntry>> onReceived);

        public abstract void GetPlayerEntry
            (string leaderboardKey, Action<LeaderboardEntry> onReceived);


    }
}


