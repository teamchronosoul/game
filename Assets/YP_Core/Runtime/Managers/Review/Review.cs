using System;
using YP.Internal;

namespace YP
{
    public class Review : Manager
    {
        private static Review instance;
        private static ReviewService service => instance.supportedService as ReviewService;


        protected override string managerName => "YP Review";

        protected override void OnInitialized()
        {
            instance = this;
            Log(Core.Message.Initialized(managerName));
        }

        public static void Request(Action onOpened = null, Action onClosed = null)
        {
            service.Request(onOpened, onClosed);
            instance.Log("Requested.");
        }


    }
}


