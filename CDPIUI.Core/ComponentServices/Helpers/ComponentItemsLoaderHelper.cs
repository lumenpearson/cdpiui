using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers
{
    public class ComponentItemsLoaderHelper
    {

        // Replaced as a whole, never changed in place: Init runs on the startup path and after every
        // Store change while other threads look components up (audit 11.9).
        private volatile List<ComponentHelper> Components = [];

        public Action? InitRequested;

        private static ComponentItemsLoaderHelper? _instance;
        private static readonly object _lock = new();
        public static ComponentItemsLoaderHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ComponentItemsLoaderHelper();
                    return _instance;
                }
            }
        }

        private ComponentItemsLoaderHelper()
        {
            Init();
        }

        public void Init(bool forse = true)
        {
            List<ComponentHelper> current = Components;
            if (!forse && current.Count != 0) return;

            List<DatabaseStoreItem> configItems = 
                DatabaseHelper.Instance.GetItemsByType("component");

            List<ComponentHelper> components = [];
            foreach (DatabaseStoreItem item in configItems)
            {
                if (!Path.Exists(item.Directory) || 
                    !File.Exists(Path.Combine(item.Directory!, item.Executable + ".exe")))
                    continue;

                // New helpers: an installed or updated pack changes the preset lists they cache.
                components.Add(new ComponentHelper(item.Id!));
            }
            Components = components;
            InitRequested?.Invoke();
        }

        public List<ComponentHelper> GetComponentHelpers()
        {
            return [.. Components];
        }

        public ComponentHelper? GetComponentHelperFromId(string id)
        {
            return Components.FirstOrDefault(componentHelper => componentHelper.Id == id);
        }
    }
}
