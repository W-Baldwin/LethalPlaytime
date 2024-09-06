using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig.ConfigItems.Options;
using LethalConfig.ConfigItems;
using LethalConfig;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace LethalPlaytime
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    [BepInDependency("ainavt.lc.lethalconfig")]
    public class LethalPlaytime : BaseUnityPlugin
    {
        public static LethalPlaytime Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        internal static Harmony? Harmony { get; set; }

        private void Awake()
        {
            Logger = base.Logger;
            Instance = this;

            NetcodePatcher();
            Patch();

            PlaytimeConfig.ConfigureAndRegisterAssets(Instance);

            

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        }

        internal static void Patch()
        {
            Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

            Logger.LogDebug("Patching...");

            Harmony.PatchAll();

            Logger.LogDebug("Finished patching!");
        }

        internal static void Unpatch()
        {
            Logger.LogDebug("Unpatching...");

            Harmony?.UnpatchSelf();

            Logger.LogDebug("Finished unpatching!");
        }

        private void NetcodePatcher()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        //Makes a integer slider configuration entry with LethalConfig with limits 0-100 for rarity of item spawned.
        internal ConfigEntry<int> CreateIntSliderConfig(string itemName, int defaultRarity, string description, int sliderMin, int sliderMax, string configCategory)
        {
            var rarityEntry = Config.Bind(configCategory, itemName, defaultRarity, description + " [" + sliderMin + "-" + sliderMax + "]");
            var slider = new IntSliderConfigItem(rarityEntry, new IntSliderOptions
            {
                RequiresRestart = true,
                Min = sliderMin,
                Max = sliderMax
            });
            LethalConfigManager.AddConfigItem(slider);
            return rarityEntry;
        }
    }
}