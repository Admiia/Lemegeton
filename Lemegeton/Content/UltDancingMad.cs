using System;
using Lemegeton.Core;
using System.Collections.Generic;
using System.Linq;
using static Lemegeton.Core.State;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Network;

namespace Lemegeton.Content
{

    internal class UltDancingMad : Core.Content
    {

        public override FeaturesEnum Features => FeaturesEnum.None;

        private const uint umadZoneId = 1363;
        private const uint StatusConfetti = 5078;
        private const uint StatusArrowUp = 5079;
        private const uint StatusArrowDown = 5080;
        private const uint StatusArrowRight = 5081;
        private const uint StatusArrowLeft = 5082;
        // 731 to 733
        // 822 to 824
        private const uint HeadmarkerForsakenStack = 731;
        private const uint HeadmarkerForsakenCircle = 732;
        private const uint HeadmarkerForsakenCone = 733;
        private const uint AbilityForsaken = 47804;
        private const uint AbilityAllThingsEnding = 47836;
        // idk why theres 2 abilities called all things ending with different ids
        private const uint AbilityAllThingsEnding2 = 47837;
        
        
        private bool ZoneOk = false;
        private bool _subbed = false;

        private ForsakenAm _forsakenAm;

        private enum PhaseEnum
        {
            Start,
            Forsaken,

        }

        private PhaseEnum _CurrentPhase = PhaseEnum.Start;
        private PhaseEnum CurrentPhase
        {
            get
            {
                return _CurrentPhase;
            }
            set
            {
                if (_CurrentPhase != value)
                {
                    Log(State.LogLevelEnum.Debug, null, "Moving to phase {0}", value);
                    _CurrentPhase = value;
                }
            }
        }

        #region ForsakenAm
        public class ForsakenAm : Automarker
        {

            [AttributeOrderNumber(1000)]
            public AutomarkerSigns Signs1 { get; set; }
            [AttributeOrderNumber(1010)]
            public AutomarkerSigns Signs2 { get; set; }

            [AttributeOrderNumber(2000)]
            public AutomarkerPrio Prio { get; set; }

            [DebugOption]
            [AttributeOrderNumber(2500)]
            public AutomarkerTiming Timing { get; set; }

            [DebugOption]
            [AttributeOrderNumber(3000)]
            public System.Action Test { get; set; }

            private bool isFirstAssign = true;
            private Dictionary<uint, uint> current_roles = new Dictionary<uint, uint>{};
            private List<uint> _groupA = new List<uint>();
            private List<uint> _groupB = new List<uint>();
            private uint n_assigned_roles = 0;
            private uint tower_set = 1;


            public ForsakenAm(State state) : base(state)
            {
                Enabled = false;
                AsSoftmarker = true; // Client-side only marker by default.
                Timing = new AutomarkerTiming() { TimingType = AutomarkerTiming.TimingTypeEnum.Inherit, Parent = state.cfg.DefaultAutomarkerTiming };
                Signs1 = new AutomarkerSigns();
                Signs1.SetRole("OddTowerLeftStack", AutomarkerSigns.SignEnum.Bind1);
                Signs1.SetRole("OddTowerRightStack", AutomarkerSigns.SignEnum.Bind2);
                Signs1.SetRole("OddTowerCircle", AutomarkerSigns.SignEnum.Circle);
                Signs1.SetRole("OddTowerCone", AutomarkerSigns.SignEnum.Triangle);

                Signs2 = new AutomarkerSigns();
                Signs2.SetRole("EvenTowerLeftCone", AutomarkerSigns.SignEnum.Attack1);
                Signs2.SetRole("EvenTowerLeftCircle", AutomarkerSigns.SignEnum.Ignore1);
                Signs2.SetRole("EvenTowerRightCone", AutomarkerSigns.SignEnum.Attack2);
                Signs2.SetRole("EvenTowerRightCircle", AutomarkerSigns.SignEnum.Ignore2);
                Prio = new AutomarkerPrio();
                Prio.Priority = AutomarkerPrio.PrioTypeEnum.PartyListCustom;
                Test = new System.Action(() => Signs1.TestFunctionality(state, null, Timing, SelfMarkOnly, AsSoftmarker));
            }

            public override void Reset()
            {
                Log(State.LogLevelEnum.Debug, null, "Reset");
                n_assigned_roles = 0;
                isFirstAssign = true;
                current_roles.Clear();
                tower_set = 1;
            }

            internal void FeedHeadmarker(uint actorId, uint headMarkerId)
            {
                if (Active == false)
                {
                    return;
                }
                Log(State.LogLevelEnum.Debug, null, "Admia: Registered headMarkerId {0} on {1:X}", headMarkerId, actorId);
                if (actorId == 0)
                {
                    return;
                }
                switch (headMarkerId)
                {
                    case HeadmarkerForsakenStack:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken (set {0}): Player {1} Gained Stack", tower_set, actorId);
                        current_roles[actorId] = headMarkerId;
                        break;
                    case HeadmarkerForsakenCircle:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken (set {0}): Player {1} Gained Circle", tower_set, actorId);
                        current_roles[actorId] = headMarkerId;
                        break;
                    case HeadmarkerForsakenCone:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken (set {0}): Player {1} Gained Cone", tower_set, actorId);
                        current_roles[actorId] = headMarkerId;
                        break;
                    default:
                        return;
                }
                n_assigned_roles += 1;
                Log(State.LogLevelEnum.Debug, null, "Admia: Assigned roles {0}", n_assigned_roles);
                if (isFirstAssign && n_assigned_roles == 8)
                {
                    DecideGroupAB();
                    ProcessTowerSet();
                    n_assigned_roles = 0;
                    isFirstAssign = false;
                    tower_set += 1;
                }
                else if (!isFirstAssign && n_assigned_roles == 4)
                {
                    ProcessTowerSet();
                    n_assigned_roles = 0;
                    tower_set += 1;
                }
            }
            internal void FeedAction(uint actorId, uint actionId)
            {
                if (actionId == AbilityAllThingsEnding || actionId == AbilityAllThingsEnding2)
                {
                    if (tower_set > 8)
                    {
                        _state.ClearAutoMarkers();
                    }
                }
            }
            internal void ProcessTowerSet()
            {
                Log(State.LogLevelEnum.Debug, null, "Processing tower set {0}", tower_set);
                _state.ClearAutoMarkers();
                switch (tower_set)
                {
                    case 1:
                    case 3:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken set {0} (odd), Group A resolving", tower_set);
                        DecideOddTowerMarkers(_groupA);
                        break;
                    case 2: 
                    case 8:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken set {0} (even), Group A resolving", tower_set);
                        DecideEvenTowerMarkers(_groupA);
                        break;
                    case 5:
                    case 7:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken set {0} (odd), Group B resolving", tower_set);
                        DecideOddTowerMarkers(_groupB);
                        break;
                    case 4:
                    case 6:
                        Log(State.LogLevelEnum.Debug, null, "Admia: Forsaken set {0} (even), Group B resolving", tower_set);
                        DecideEvenTowerMarkers(_groupB);
                        break;
                    default:
                        Log(State.LogLevelEnum.Debug, null, "Tower set {0} not recognised", tower_set);
                        break;
                }
            }
            internal void DecideGroupAB()
            {
                Log(State.LogLevelEnum.Debug, null, "Admia: Adding Players to groups");
                Party pty = _state.GetPartyMembers();
                List<Party.PartyMember> sorted_party = pty.Members;
                Prio.SortByPriority(sorted_party);

                List<Party.PartyMember> support_pair1 = sorted_party.GetRange(0, 2);
                List<Party.PartyMember> support_pair2 = sorted_party.GetRange(2, 2);
                List<Party.PartyMember> dps_pair1 = sorted_party.GetRange(4, 2);
                List<Party.PartyMember> dps_pair2 = sorted_party.GetRange(6, 2);

                if (current_roles[(uint) support_pair1[0].ObjectId] != current_roles[(uint) support_pair1[1].ObjectId])
                {
                    Log(State.LogLevelEnum.Debug, null, "Admia: admia in group A");
                    _groupA.Append((uint) support_pair1[0].ObjectId);
                    _groupA.Append((uint) support_pair1[1].ObjectId);
                    _groupB.Append((uint) support_pair2[0].ObjectId);
                    _groupB.Append((uint) support_pair2[1].ObjectId);
                }
                else
                {
                    Log(State.LogLevelEnum.Debug, null, "Admia: admia in group B");
                    _groupB.Append((uint) support_pair1[0].ObjectId);
                    _groupB.Append((uint) support_pair1[1].ObjectId);
                    _groupA.Append((uint) support_pair2[0].ObjectId);
                    _groupA.Append((uint) support_pair2[1].ObjectId);
                }

                if (current_roles[(uint) dps_pair1[0].ObjectId] != current_roles[(uint) dps_pair1[1].ObjectId])
                {
                    _groupA.Append((uint) dps_pair1[0].ObjectId);
                    _groupA.Append((uint) dps_pair1[1].ObjectId);
                    _groupB.Append((uint) dps_pair2[0].ObjectId);
                    _groupB.Append((uint) dps_pair2[1].ObjectId);
                }
                else
                {
                    _groupB.Append((uint) dps_pair1[0].ObjectId);
                    _groupB.Append((uint) dps_pair1[1].ObjectId);
                    _groupA.Append((uint) dps_pair2[0].ObjectId);
                    _groupA.Append((uint) dps_pair2[1].ObjectId);
                }

                return;
            }
            internal void DecideOddTowerMarkers(List<uint> resolving_group)
            {
                AutomarkerPayload ap = new AutomarkerPayload(_state, SelfMarkOnly, AsSoftmarker);

                Party pty = _state.GetPartyMembers();
                List<Party.PartyMember> resolving_party = pty.GetByActorIds(resolving_group);
                Prio.SortByPriority(resolving_party);
                bool left_stack_assigned = false;
                foreach (Party.PartyMember mbr in resolving_party)
                {
                    switch ((uint)mbr.ObjectId)
                    {
                        case HeadmarkerForsakenCircle:
                            ap.Assign(Signs1.Roles["OddTowerCircle"], mbr.GameObject);
                            break;
                        case HeadmarkerForsakenCone:
                            ap.Assign(Signs1.Roles["OddTowerCone"], mbr.GameObject);
                            break;
                        case HeadmarkerForsakenStack:
                            if (left_stack_assigned)
                            {
                                ap.Assign(Signs1.Roles["OddTowerLeftStack"], mbr.GameObject);
                                left_stack_assigned = true;
                            }
                            else
                            {
                                ap.Assign(Signs1.Roles["OddTowerRightStack"], mbr.GameObject);
                            }
                            break;
                    }
                }
                _state.ExecuteAutomarkers(ap, Timing);
            }
            internal void DecideEvenTowerMarkers(List<uint> resolving_group)
            {
                AutomarkerPayload ap = new AutomarkerPayload(_state, SelfMarkOnly, AsSoftmarker);

                Party pty = _state.GetPartyMembers();
                List<Party.PartyMember> resolving_party = pty.GetByActorIds(resolving_group);
                Prio.SortByPriority(resolving_party);

                bool left_cone_assigned = false;
                bool left_circle_assigned = false;

                foreach (Party.PartyMember mbr in resolving_party)
                {
                    switch ((uint)mbr.ObjectId)
                    {
                        case HeadmarkerForsakenCircle:
                            if (left_circle_assigned)
                            {
                                ap.Assign(Signs2.Roles["EvenTowerLeftCircle"], mbr.GameObject);
                                left_cone_assigned = true;
                            }
                            else
                            {
                                ap.Assign(Signs2.Roles["EvenTowerRightCircle"], mbr.GameObject);
                            }
                            break;
                        case HeadmarkerForsakenCone:
                            if (left_cone_assigned)
                            {
                                ap.Assign(Signs2.Roles["EvenTowerLeftCone"], mbr.GameObject);
                                left_cone_assigned = true;
                            }
                            else
                            {
                                ap.Assign(Signs2.Roles["EvenTowerRightCone"], mbr.GameObject);
                            }
                            break;
                    }
                }
                _state.ExecuteAutomarkers(ap, Timing);
            }

        }
        #endregion

        public UltDancingMad(State st) : base(st)
        {
            st.OnZoneChange += OnZoneChange;
        }

        private void SubscribeToEvents()
        {
            lock (this)
            {
                if (_subbed == true)
                {
                    return;
                }
                _subbed = true;
                Log(LogLevelEnum.Debug, null, "Subscribing to events");
                _state.OnStatusChange += OnStatusChange;
                _state.OnAction += OnAction;
                _state.OnHeadMarker += OnHeadMarker;
            }
        }

        private void UnsubscribeFromEvents()
        {
            lock (this)
            {
                if (_subbed == false)
                {
                    return;
                }
                Log(LogLevelEnum.Debug, null, "Unsubscribing from events");
                _state.OnStatusChange -= OnStatusChange;
                _state.OnAction -= OnAction;
                _state.OnHeadMarker -= OnHeadMarker;
                _subbed = false;
            }
        }

        protected override bool ExecutionImplementation()
        {
            if (ZoneOk == true)
            {
                return base.ExecutionImplementation();
            }
            return false;
        }

        private void OnTether(uint src, uint dest, uint tetherId)
        {

        }

        private void OnHeadMarker(uint dest, uint markerId)
        {
            if (CurrentPhase == PhaseEnum.Forsaken)
            {
                _forsakenAm.FeedHeadmarker(dest, markerId);
            }
        }

        private void OnStatusChange(uint src, uint dest, uint statusId, bool gained, float duration, int stacks)
        {
            
        }

        private void OnAction(uint src, uint dest, ushort actionId)
        {
            if (actionId == AbilityForsaken)
            {
                Log(State.LogLevelEnum.Info, null, "Admia: Forsaken cast captured");
                CurrentPhase = PhaseEnum.Forsaken;
            }
            if (actionId == AbilityAllThingsEnding || actionId == AbilityAllThingsEnding2)
            {
                Log(State.LogLevelEnum.Info, null, "Admia: Allthingsending cast captured");
                if (CurrentPhase == PhaseEnum.Forsaken)
                {
                    _forsakenAm.FeedAction(dest, actionId);
                }
            }
        }

        private void OnCombatChange(bool inCombat)
        {
            Reset();
            CurrentPhase = PhaseEnum.Start;
        }

        private void OnZoneChange(uint newZone)
        {
            bool newZoneOk = (newZone == umadZoneId);
            if (newZoneOk == true && ZoneOk == false)
            {
                Log(State.LogLevelEnum.Info, null, "UMAD Content available");
                _forsakenAm = (ForsakenAm)Items["ForsakenAm"];
                SubscribeToEvents();
                LogItems();
            }
            else if (newZoneOk == false && ZoneOk == true)
            {
                Log(State.LogLevelEnum.Info, null, "Content unavailable");
                UnsubscribeFromEvents();
            }
            ZoneOk = newZoneOk;
        }

    }

}
