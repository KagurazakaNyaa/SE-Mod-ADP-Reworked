using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.Entity;
using VRage.Voxels;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace P3DResourceRig
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Assembler), true, "DrillPlatform")]
    public class Rig : MyGameLogicComponent
    {
        // Builder is nessassary for GetObjectBuilder method as far as I know.
        private MyObjectBuilder_EntityBase builder;
        private Sandbox.ModAPI.IMyAssembler m_generator;
        private IMyCubeBlock m_parent;

        bool m_bInit = false;

        #region Terminal Controls
        static bool _ControlsInited = false;

        static IMyTerminalControlCheckbox m_enableStone;
        #region Control Values
        public bool StoneEnabled
        {
            get { return m_stoneEnabled; }
            set { m_stoneEnabled = value; }
        }
        bool m_stoneEnabled = false;
        #endregion
        private void CreateTerminalControls()
        {
            if (_ControlsInited)
                return;

            _ControlsInited = true;
            Func<IMyTerminalBlock, bool> enabledCheck = delegate (IMyTerminalBlock b) { return b.BlockDefinition.SubtypeId == "DrillPlatform"; };

            m_enableStone = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyAssembler>("EnableStone");
            // Separator
            var sep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyAssembler>(string.Empty);
            if (sep != null)
            {
                sep.Visible = enabledCheck;
                sep.Enabled = enabledCheck;
                MyAPIGateway.TerminalControls.AddControl<IMyAssembler>(sep);
            }
            // EnableStone checkbox
            if (m_enableStone != null)
            {
                m_enableStone.Title = MyStringId.GetOrCompute("开采石头");
                m_enableStone.Tooltip = MyStringId.GetOrCompute("启用此选项将会在设备中产生石头。");
                m_enableStone.Getter = (b) => enabledCheck(b) && b.GameLogic.GetAs<Rig>().StoneEnabled;
                m_enableStone.Setter = (b, v) => { if (enabledCheck(b)) MessageUtils.SendMessageToAll(new MessageToggleStoneEnable() { EntityId = b.EntityId, EnableStone = v }); };
                m_enableStone.Visible = enabledCheck;
                m_enableStone.Enabled = enabledCheck;
                m_enableStone.OnText = MyStringId.GetOrCompute("On");
                m_enableStone.OffText = MyStringId.GetOrCompute("Off");
                MyAPIGateway.TerminalControls.AddControl<IMyAssembler>(m_enableStone);

                var action = MyAPIGateway.TerminalControls.CreateAction<IMyAssembler>("EnableStone");
                if (action != null)
                {
                    StringBuilder actionname = new StringBuilder();
                    actionname.Append(m_enableStone.Title).Append(" ").Append(m_enableStone.OnText).Append("/").Append(m_enableStone.OffText);

                    action.Name = actionname;
                    action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
                    action.ValidForGroups = true;
                    action.Action = (b) => m_enableStone.Setter(b, !m_enableStone.Getter(b));
                    action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<Rig>().StoneEnabled ? m_enableStone.OnText : m_enableStone.OffText);

                    MyAPIGateway.TerminalControls.AddAction<IMyAssembler>(action);
                }
            }
        }
        public void LoadTerminalValues()
        {
            // New way
            var settings = (m_generator as IMyAssembler).RetrieveTerminalValues();
            if (settings != null)
            {
                Logger.Instance.LogMessage("Init");
                m_stoneEnabled = settings.StoneEnabled;
            }
        }
        #endregion

        #region IsInVoxel definition
        private bool IsInVoxel(Sandbox.ModAPI.IMyTerminalBlock block)
        {
            BoundingBoxD blockWorldAABB = block.PositionComp.WorldAABB;
            List<MyVoxelBase> voxelList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInBox(ref blockWorldAABB, voxelList);
            var cubeSize = block.CubeGrid.GridSize;
            BoundingBoxD localAAABB = new BoundingBoxD(cubeSize * ((Vector3D)block.Min - 1), cubeSize * ((Vector3D)block.Max + 1));
            var gridWorldMatrix = block.CubeGrid.WorldMatrix;
            foreach (var map in voxelList)
            {
                if (map.IsAnyAabbCornerInside(ref gridWorldMatrix, localAAABB))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
        #region colors
        private Color m_primaryColor = Color.OrangeRed;
        private Color m_secondaryColor = Color.LemonChiffon;
        public Color PrimaryBeamColor
        {
            get { return m_primaryColor; }
            set
            {
                m_primaryColor = value;               
            }
        }

        public Color SecondaryBeamColor
        {
            get { return m_secondaryColor; }
            set
            {
                m_secondaryColor = value;        
            }
        }
        #endregion
        Sandbox.ModAPI.IMyTerminalBlock terminalBlock;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            m_generator = Entity as Sandbox.ModAPI.IMyAssembler;
            m_parent = Entity as IMyCubeBlock;
            builder = objectBuilder;

            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

            terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;
            if (!m_bInit)
            {
                m_bInit = true;
                LoadTerminalValues();
                CreateTerminalControls();
            }
        }
        #region UpdateBeforeSimulation
        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();

            //if (m_generator.IsWorking && !m_generator.CustomData.Contains("No Drill"))
            if (m_generator.IsWorking && m_stoneEnabled)
            {
                if (IsInVoxel(m_generator as Sandbox.ModAPI.IMyTerminalBlock))
                {
                    IMyInventory inventory = ((Sandbox.ModAPI.IMyTerminalBlock)Entity).GetInventory(1) as IMyInventory;
                    VRage.MyFixedPoint amount = (VRage.MyFixedPoint)(500 * (1 + (0.4 * m_generator.UpgradeValues["Productivity"])));
                    inventory.AddItems(amount, new MyObjectBuilder_Ore() { SubtypeName = "Stone" });
                    terminalBlock.RefreshCustomInfo();
                }
            }
        }
        #endregion
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return builder;
        }

        private ulong m_counter = 0;

        //draw counter
        public override void UpdateBeforeSimulation()
        {
            m_counter++;
            if (m_generator.IsWorking && m_stoneEnabled)
            {
                if (IsInVoxel(m_generator as Sandbox.ModAPI.IMyTerminalBlock))
                {
                    if (MyAPIGateway.Session?.Player == null)
					{
						return;
					}
					else
					{
						DrawBeams();
					}				
                }
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (MyAPIGateway.Multiplayer == null || MyAPIGateway.Session == null)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                return;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

            // Should always be enabled
            if (!(m_generator as IMyFunctionalBlock).Enabled)
                (m_generator as IMyFunctionalBlock).Enabled = true;
        }

        private void DrawBeams()
        {
            var maincolor = PrimaryBeamColor.ToVector4();
            var auxcolor = SecondaryBeamColor.ToVector4();

            VRage.Game.MySimpleObjectDraw.DrawLine(m_generator.WorldAABB.Center - (m_generator.WorldMatrix.Down * 2.5), m_generator.WorldAABB.Center + (m_generator.WorldMatrix.Down * 2.5 * 4), VRage.Utils.MyStringId.GetOrCompute("WeaponLaser"), ref auxcolor, 0.33f);
            VRage.Game.MySimpleObjectDraw.DrawLine(m_generator.WorldAABB.Center - (m_generator.WorldMatrix.Down * 2.5), m_generator.WorldAABB.Center + (m_generator.WorldMatrix.Down * 2.5 * 4), VRage.Utils.MyStringId.GetOrCompute("WeaponLaser"), ref maincolor, 1.02f);

            // Draw 'pulsing' beam
            if (m_counter % 2 == 0)
            {
                VRage.Game.MySimpleObjectDraw.DrawLine(m_generator.WorldAABB.Center - (m_generator.WorldMatrix.Down * 2.5), m_generator.WorldAABB.Center + (m_generator.WorldMatrix.Down * 2.5 * 4), VRage.Utils.MyStringId.GetOrCompute("WeaponLaser"), ref maincolor, 1.12f);
            }        
        }
    }
}