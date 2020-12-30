using ProtoBuf;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRageMath;

namespace SE_Mod_ADP_Reworked
{
    /// <summary>
    /// Singleton for saving and loading all block terminal settings.
    /// </summary>
    public class DrillSettings
    {
        static bool _initialized = false;
        static DrillSettings _instance = new DrillSettings();
        public static DrillSettings Instance
        {
            get
            {
                if (!_initialized)
                    Init();
                return _instance;
            }
        }

        private DrillSettings()
        {
        }

        private static void Init()
        {
            try
            {
                _instance.LoadAllTerminalValues();
                _initialized = true;
            }
            catch
            {
                // ignore
            }
        }
        List<DrillPlatformSetting> m_platformSettings = new List<DrillPlatformSetting>();

        // This is to save data to world file
        public void SaveAllTerminalValues()
        {
            Logger.Instance.LogDebug("SaveAllTerminalValues");
            try
            {
                var strdata = MyAPIGateway.Utilities.SerializeToXML<List<DrillPlatformSetting>>(m_platformSettings);
                MyAPIGateway.Utilities.SetVariable<string>("DrillPlatform", strdata);
            }
            catch(Exception ex)
            {
                // If an old save game is loaded, it seems it might try to resave to upgrade.
                // If this happens, the ModAPI may not be initialized
                // NEVER prevent someone from saving their game.
                // It's better to lose terminal information than a player to lose hours of work.
                Logger.Instance.LogMessage("WARNING: There was an error saving terminal settings. Values may be lost.");
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }
        }

        public void LoadAllTerminalValues()
        {
            Logger.Instance.LogDebug("LoadAllTerminalValues");

            string strdata;

            MyAPIGateway.Utilities.GetVariable<string>("DrillPlatform", out strdata);
            if (!string.IsNullOrEmpty(strdata))
            {
                Logger.Instance.LogDebug("Success!");
                m_platformSettings = MyAPIGateway.Utilities.SerializeFromXML<List<DrillPlatformSetting>>(strdata);
            }
        }

        #region Turret drill
        public DrillPlatformSetting RetrieveTerminalValues(IMyAssembler drill)
        {
            Logger.Instance.LogDebug("RetrieveTerminalValues");
            var settings = m_platformSettings.FirstOrDefault((x) => x.EntityId == drill.EntityId);
            if (settings != null)
            {
                Logger.Instance.LogDebug("Found settings for block: " + drill.CustomName);
            }
            return settings;
        }

        public void StoreTerminalValues(IMyAssembler drill)
        {
            var settings = m_platformSettings.FirstOrDefault((x) => x.EntityId == drill.EntityId);

            if (settings == null)
            {
                settings = new DrillPlatformSetting();
                settings.EntityId = drill.EntityId;
                m_platformSettings.Add(settings);
            }
            settings.StoneEnabled = drill.GameLogic.GetAs<Rig>().StoneEnabled;
        }

        public void DeleteTerminalValues(IMyAssembler drill)
        {
            Logger.Instance.LogDebug("DeleteTerminalValues");
            var settings = m_platformSettings.FirstOrDefault((x) => x.EntityId == drill.EntityId);

            if (settings != null)
            {
                DrillSettings.Instance.m_platformSettings.Remove(settings);
            }
        }
        #endregion
    }

    public static class DrillSettingExtensions
    {
        #region Platform
        public static DrillPlatformSetting RetrieveTerminalValues(this IMyAssembler drill)
        {
            return DrillSettings.Instance.RetrieveTerminalValues(drill);
        }

        public static void StoreTerminalValues(this IMyAssembler drill)
        {
            DrillSettings.Instance.StoreTerminalValues(drill);
        }

        public static void DeleteTerminalValues(this IMyAssembler drill)
        {
            DrillSettings.Instance.DeleteTerminalValues(drill);
        }
        #endregion
    }

    [ProtoContract]
    public class DrillPlatformSetting
    {
        [ProtoMember(1)]
        public long EntityId = 0;

        [ProtoMember(2)]
        public bool StoneEnabled = false;
    }

    /// <summary>
    /// Left for legacy purposes
    /// </summary>
}
