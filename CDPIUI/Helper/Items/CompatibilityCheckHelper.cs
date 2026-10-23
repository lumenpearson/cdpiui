using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Items
{
    public class CompatibilityCheckHelper
    {
        private static CompatibilityCheckHelper _instance;
        private static readonly object _lock = new();
        public static CompatibilityCheckHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new CompatibilityCheckHelper();
                    return _instance;
                }
            }
        }
        public CompatibilityCheckHelper() { }

        public bool isCheckActive = false;
        public async Task BeginCheck()
        {
            if (isCheckActive) return;
            isCheckActive = true;
            try
            {
                List<string> outdatedComponents = [];
                var components = ComponentItemsLoaderHelper.Instance.GetComponentHelpers();

                foreach (var component in components)
                {
                    ConfigurationService configHelper = component.GetConfigHelper();

                    foreach (var config in configHelper.GetConfigItems())
                    {
                        if (config.target != null && config.target.Count == 2)
                        {
                            var databaseItem = DatabaseHelper.Instance.GetItemById(config.target[0]);
                            if (databaseItem == null) continue;

                            string requiredVersion = config.target[1];
                            string installedVersion = databaseItem.CurrentVersion;

                            if (VersionHelper.CompareVersionStrings(requiredVersion, installedVersion) == 1)
                            {
                                if (!outdatedComponents.Contains(config.target[0]))
                                {
                                    outdatedComponents.Add(config.target[0]);
                                }
                            }
                        }
                    }
                }

                foreach (var component in outdatedComponents)
                {
                    await PipeHelper.SendNotificationPacket(Shared.Pipe.Models.NotificationsMessageIds.CompatibilityCheckAssistant,
                        new()
                        {
                            { "componentName", HardcodedItemIds.ComponentIds.TryGetKeyByValue(component, out var knownComponent)
                                ? knownComponent.ToString()
                                : DatabaseHelper.Instance.GetItemById(component)?.ShortName is { Length: > 0 } shortName ? shortName : component }
                        });
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(CompatibilityCheckHelper), $"Cannot begin check: {ex.Message}");
            }
            isCheckActive = false;
        }
    }

}
