using UnityEngine;
using UnityEngine.Events;
using YP.Internal;


namespace YP
{
    public class LangDefiner : Manager
    {
        [SerializeField] private UnityEvent<Language> _onLanguageDefined;



        private static LangDefiner instance;
        private static LangDefinerService service => instance.supportedService as LangDefinerService;



        protected override string managerName => "YP System Language";

        protected override void OnInitialized()
        {
            instance = this;

            var definedLanguage = service.GetLanguage();
            _onLanguageDefined.Invoke(definedLanguage);

            Log("Language defined: " + definedLanguage);
            Log(Core.Message.Initialized(managerName));
        }



    }
}


