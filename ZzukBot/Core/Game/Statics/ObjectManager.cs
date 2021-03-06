﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ZzukBot.Core.Constants;
using ZzukBot.Core.Mem;
using ZzukBot.Core.Game.Objects;
using ZzukBot.Core.Utilities.Extensions;
using ZzukBot.Core.Utilities.Helpers;
using ZzukBot.Settings;

namespace ZzukBot.Core.Game.Statics
{
    /// <summary>
    ///     ObjectManager class
    /// </summary>
    public sealed class ObjectManager
    {
        private static readonly object Locker = new object();

        private static readonly Lazy<ObjectManager> _instance =
            new Lazy<ObjectManager>(() => new ObjectManager());

        private readonly EnumVisibleObjectsCallback _callback;


        /// <summary>
        ///     Objectmanager internal dictionary
        /// </summary>
        private readonly Dictionary<ulong, WoWObject> _objects = new Dictionary<ulong, WoWObject>();

        private readonly IntPtr _ourCallback;
        private readonly MainThread.Updater _updater;

        private volatile List<WoWObject> _finalObjects = new List<WoWObject>();
        private volatile bool _ingame1 = true;
        private volatile bool _ingame2 = true;
        private volatile Dictionary<int, IntPtr> _itemCachePtrs = new Dictionary<int, IntPtr>();
        private volatile Dictionary<int, IntPtr> _questCachePtrs = new Dictionary<int, IntPtr>();

        private ObjectManager()
        {
            _callback = Callback;
            _ourCallback = Marshal.GetFunctionPointerForDelegate(_callback);
            _updater = new MainThread.Updater(_EnumObjects, 50);
            _updater.Start();
            WoWEventHandler.Instance.OnEvent += OnEvent;
        }

        /// <summary>
        ///     Tells if we are ingame
        /// </summary>
        public bool IsIngame => _ingame1 && _ingame2 && Offsets.Player.IsIngame.ReadAs<byte>() == 1;

        /// <summary>
        ///     Access to the instance
        /// </summary>
        public static ObjectManager Instance => _instance.Value;


        /// <summary>
        ///     Access to the local player
        /// </summary>
        public LocalPlayer Player { get; private set; }

        /// <summary>
        ///     Access to the local player's pet
        /// </summary>
        public LocalPet Pet { get; private set; }


        /// <summary>
        ///     Access to the players target
        /// </summary>
        public WoWUnit Target
        {
            get
            {
                if (!IsIngame) return null;
                if (Player.TargetGuid == 0) return null;

                lock (Locker)
                {
                    return (WoWUnit) _finalObjects.FirstOrDefault(i => i.Guid == Player.TargetGuid);
                }
            }
        }

        /// <summary>
        ///     Returns a readonly list with all current objects
        /// </summary>
        public IReadOnlyList<WoWObject> Objects => _finalObjects;

        /// <summary>
        ///     Returns a copy of all units currently in the object list on each access
        /// </summary>
        public List<WoWUnit> Units
        {
            get
            {
                lock (Locker)
                {
                    return _finalObjects.OfType<WoWUnit>().ToList();
                }
            }
        }

        /// <summary>
        ///     Returns a copy of all players currently in the object list on each access
        /// </summary>
        public List<WoWUnit> Players
        {
            get
            {
                lock (Locker)
                {
                    return
                        _finalObjects.OfType<WoWUnit>()
                            .ToList()
                            .Where(i => i.WoWType == Enums.WoWObjectTypes.OT_PLAYER)
                            .ToList();
                }
            }
        }


        /// <summary>
        ///     Returns a copy of all Npcs currently in the object list on each access
        /// </summary>
        public List<WoWUnit> Npcs
        {
            get
            {
                lock (Locker)
                {
                    return
                        _finalObjects.OfType<WoWUnit>().Where(i => i.WoWType == Enums.WoWObjectTypes.OT_UNIT).ToList();
                }
            }
        }


        /// <summary>
        ///     Returns a copy of all gameobjects currently in the object list on each access
        /// </summary>
        public List<WoWGameObject> GameObjects
        {
            get
            {
                lock (Locker)
                {
                    return _finalObjects.OfType<WoWGameObject>()
                        .ToList();
                }
            }
        }

        /// <summary>
        ///     Returns a copy of all items currently in the object list on each access
        /// </summary>
        public List<WoWItem> Items
        {
            get
            {
                lock (Locker)
                {
                    return _finalObjects.OfType<WoWItem>()
                        .ToList();
                }
            }
        }

        /// <summary>
        ///     Access to the party leaders object
        /// </summary>
        public WoWUnit PartyLeader
        {
            get
            {
                var guid = ((int) Offsets.Party.leaderGuid).ReadAs<ulong>();
                if (guid == 0) return null;
                lock (Locker)
                {
                    return (WoWUnit) _finalObjects.FirstOrDefault(i => i.Guid == guid);
                }
            }
        }

        /// <summary>
        ///     Access to the object of party member 1
        /// </summary>
        public WoWUnit Party1
        {
            get
            {
                var guid = ((int) Offsets.Party.party1Guid).ReadAs<ulong>();
                return GetPartyMember(guid);
            }
        }

        /// <summary>
        ///     Access to the object of party member 2
        /// </summary>
        public WoWUnit Party2
        {
            get
            {
                var guid = ((int) Offsets.Party.party2Guid).ReadAs<ulong>();
                return GetPartyMember(guid);
            }
        }

        /// <summary>
        ///     Access to the object of party member 3
        /// </summary>
        public WoWUnit Party3
        {
            get
            {
                var guid = ((int) Offsets.Party.party3Guid).ReadAs<ulong>();
                return GetPartyMember(guid);
            }
        }

        /// <summary>
        ///     Access to the object of party member 4
        /// </summary>
        public WoWUnit Party4
        {
            get
            {
                var guid = ((int) Offsets.Party.party4Guid).ReadAs<ulong>();
                return GetPartyMember(guid);
            }
        }

        private WoWUnit GetPartyMember(ulong guid)
        {
            if (guid == 0) return null;
            lock (Locker)
            {
                return (WoWUnit) _finalObjects.FirstOrDefault(i => i.Guid == guid);
            }
        }

        internal static void Unwrap()
        {
            // ReSharper disable once RedundantJumpStatement
            if (_instance.Value != null) return;
        }

        internal ItemCacheEntry? LookupItemCacheEntry(int parItemId, PrivateEnums.ItemCacheLookupType parLookupType)
        {
            if (_itemCachePtrs.ContainsKey(parItemId))
                return Memory.Reader.Read<ItemCacheEntry>(_itemCachePtrs[parItemId]);
            var res = Functions.ItemCacheGetRow(parItemId, parLookupType);
            if (res == IntPtr.Zero) return null;
            _itemCachePtrs.Add(parItemId, res);
            return Memory.Reader.Read<ItemCacheEntry>(res);
        }

        internal IntPtr LookupItemCachePtr(int parItemId, PrivateEnums.ItemCacheLookupType parLookupType)
        {
            if (_itemCachePtrs.ContainsKey(parItemId))
                return _itemCachePtrs[parItemId];
            var res = Functions.ItemCacheGetRow(parItemId, parLookupType);
            if (res == IntPtr.Zero) return IntPtr.Zero;
            _itemCachePtrs.Add(parItemId, res);
            return _itemCachePtrs[parItemId];
        }

        internal IntPtr LookupQuestCachePtr(int parItemId)
        {
            if (_questCachePtrs.ContainsKey(parItemId))
                return _questCachePtrs[parItemId];
            var res = Functions.QuestCacheGetRow(parItemId);
            if (res == IntPtr.Zero) return IntPtr.Zero;
            _questCachePtrs.Add(parItemId, res);
            return _questCachePtrs[parItemId];
        }

        /// <summary>
        ///     Enumerate through the object manager
        ///     true if ingame
        ///     false if not ingame
        /// </summary>
        private void _EnumObjects()
        {
            if (!IsIngame) return;
            _ingame2 = MainThread.Instance.Invoke(() =>
            {
                // renew playerptr if invalid
                // return if no pointer can be retrieved
                var playerGuid = Functions.GetPlayerGuid();
                if (playerGuid == 0) return false;
                var playerObject = Functions.GetPtrForGuid(playerGuid);
                if (playerObject == IntPtr.Zero) return false;
                if (Player == null || playerObject != Player.Pointer)
                {
                    Player = new LocalPlayer(playerGuid, playerObject, Enums.WoWObjectTypes.OT_PLAYER);
                    WoWEventHandler.Instance.FireOnPlayerInit();
                }

                if (Wait.For("QuestRefresh", 1000))
                    QuestLog.Instance.UpdateQuestLog();
                if (Wait.For("SpellRefreshObjManager", 5000))
                    Player.RefreshSpells();
                

                // set the pointer of all objects to 0
                foreach (var obj in _objects.Values)
                    obj.CanRemove = true;

                Functions.EnumVisibleObjects(_ourCallback, -1);

                // remove the pointer that are stil zero 
                // (pointer not updated from 0 = object not in manager anymore)
                foreach (var pair in _objects.Where(p => p.Value.CanRemove).ToList())
                    _objects.Remove(pair.Key);

                // assign dictionary to list which is viewable from internal
                lock (Locker)
                {
                    _finalObjects = _objects.Values.ToList();
                }
                return true;
            });
        }

        internal void DcKillswitch()
        {
            _ingame1 = false;
            Player = null;
        }

        private void OnEvent(object sender, WoWEventHandler.OnEventArgs args)
        {
            if (args.EventName == "CURSOR_UPDATE")
            {
                var online = Offsets.Player.IsIngame.ReadAs<byte>() == 1;
                if (!online) _ingame1 = false;
            }
            if (args.EventName != "UNIT_MODEL_CHANGED" &&
                args.EventName != "UPDATE_SELECTED_CHARACTER" &&
                args.EventName != "DISCONNECTED_FROM_SERVER" &&
                args.EventName != "VARIABLES_LOADED") return;
            _ingame1 = true;
        }

        /// <summary>
        ///     The callback for EnumVisibleObjects
        /// </summary>
        private int Callback(int filter, ulong guid)
        {
            if (guid == 0) return 0;
            var ptr = Functions.GetPtrForGuid(guid);
            if (ptr == IntPtr.Zero) return 0;
            if (_objects.ContainsKey(guid))
            {
                _objects[guid].Pointer = ptr;
                _objects[guid].CanRemove = false;
            }
            else
            {
                var objType =
                    (Enums.WoWObjectTypes)
                    Memory.Reader.Read<byte>(IntPtr.Add(ptr, (int) Offsets.ObjectManager.ObjType));
                switch (objType)
                {
                    case Enums.WoWObjectTypes.OT_CONTAINER:
                    case Enums.WoWObjectTypes.OT_ITEM:
                        var tmpItem = new WoWItem(guid, ptr, objType);

                        _objects.Add(guid, tmpItem);
                        break;

                    case Enums.WoWObjectTypes.OT_UNIT:

                        var owner = ptr.Add(0x8)
                            .PointsTo()
                            .Add(Offsets.Descriptors.SummonedByGuid)
                            .ReadAs<ulong>();

                        var tmpUnit = new WoWUnit(guid, ptr, objType);
                        _objects.Add(guid, tmpUnit);

                        if (Player != null && owner == Player.Guid)
                        {
                            if (Pet != null && Pet.Pointer == ptr) break;
                            Pet = new LocalPet(guid, ptr, Enums.WoWObjectTypes.OT_UNIT);
                        }

                        break;

                    case Enums.WoWObjectTypes.OT_PLAYER:
                        var tmpPlayer = new WoWUnit(guid, ptr, objType);
                        _objects.Add(guid, tmpPlayer);
                        break;

                    case Enums.WoWObjectTypes.OT_GAMEOBJ:
                        var tmpGameObject = new WoWGameObject(guid, ptr, objType);
                        _objects.Add(guid, tmpGameObject);
                        break;
                }
            }
            return 1;
        }

        /// <summary>
        ///     Delegates for: Enum, Callback, GetPtrByGuid, GetActivePlayer
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int EnumVisibleObjectsCallback(int filter, ulong guid);
    }
}