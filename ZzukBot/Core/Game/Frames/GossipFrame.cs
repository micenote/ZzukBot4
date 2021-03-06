﻿using System;
using System.Collections.Generic;
using ZzukBot.Core.Constants;
using ZzukBot.Core.Game.Frames.FrameObjects;
using ZzukBot.Core.Game.Statics;
using ZzukBot.Core.Mem;
using ZzukBot.Core.Utilities.Extensions;

namespace ZzukBot.Core.Game.Frames
{
    /// <summary>
    ///     Representing the currently open Gossip Menu
    /// </summary>
    public sealed class GossipFrame
    {
        private static volatile GossipFrame _instance;
        private static readonly object lockObject = new object();
        private static volatile bool _isOpen;
        private static volatile bool _abort;


        private readonly List<GossipOption> _Options = new List<GossipOption>();
        private readonly List<QuestOption> _Quests = new List<QuestOption>();

        private GossipFrame()
        {
            _Options.Clear();
            _Quests.Clear();

            var currentItem = (IntPtr) 0xBBBE90;
            while ((int) currentItem < 0xBC3F50)
            {
                if ((currentItem + 0x800).ReadAs<int>() == -1) break;
                var optionText = currentItem.ReadString();
                var optionType = (currentItem + 0x808).ReadAs<int>();

                _Options.Add(new GossipOptionInterface(optionText,
                    (Enums.GossipTypes) optionType));
                currentItem = IntPtr.Add(currentItem, 0x80C);
            }

            var QuestBaseId = 0x00BB74C0;
            var QuestTypeId = 0x00BB74C8;
            var currentIndex = 0;
            while (ObjectManager.Instance.Player.GossipNpcGuid != 0)
            {
                var tmpQuestId = (QuestBaseId + currentIndex * 4).ReadAs<int>();
                var tmpQuestType = (QuestTypeId + currentIndex * 4).ReadAs<int>();
                if (tmpQuestId == 0) break;
                if (tmpQuestType != 3 && tmpQuestType != 4 && tmpQuestType != 5) break;


                var tmpQuestName = (QuestBaseId + currentIndex * 4 + 0xC).ReadString();
                var tmpState = (Enums.QuestGossipState) tmpQuestType;

                _Quests.Add(new QuestOptionInterface(tmpQuestId, tmpQuestName, tmpState));

                currentIndex += 0x83;
                if (currentIndex >= 0x1060) break;
            }
        }

        /// <summary>
        ///     Access to the current Gossip-Frame-Object
        /// </summary>
        /// <value>
        ///     The frame.
        /// </value>
        public static GossipFrame Instance => _instance;

        /// <summary>
        ///     Tells if there is an open Gossip-Frame right now
        /// </summary>
        /// <value>
        /// </value>
        public static bool IsOpen => _isOpen;

        /// <summary>
        ///     The text of the Gossip-Frame (example: Greetings Location)
        /// </summary>
        /// <value>
        ///     The text.
        /// </value>
        public string Text => 0x0BBB678.ReadString();

        /// <summary>
        ///     The GUID of the NPC the current Gossip-Frame belongs to
        /// </summary>
        /// <value>
        ///     The NPC unique identifier.
        /// </value>
        public ulong NpcGuid => ObjectManager.Instance.Player.GossipNpcGuid;

        /// <summary>
        ///     A list of all non-quest options the current Gossip-Frame offers
        /// </summary>
        /// <value>
        ///     The options.
        /// </value>
        public IReadOnlyList<GossipOption> Options => _Options;

        /// <summary>
        ///     A list of all quest options the current Gossip-Frame offers
        /// </summary>
        /// <value>
        ///     The quests.
        /// </value>
        public IReadOnlyList<QuestOption> Quests => _Quests;

        internal static void Create()
        {
            lock (lockObject)
            {
                _isOpen = false;
                _abort = false;

                var tmp = new GossipFrame();
                if (_abort || ObjectManager.Instance.Player.GossipNpcGuid == 0) return;

                _instance = tmp;
                _isOpen = true;
            }
        }

        internal static void Destroy()
        {
            _abort = true;
            lock (lockObject)
            {
                _isOpen = false;
            }
        }

        /// <summary>
        ///     Selects the gossip option at the specified index
        /// </summary>
        /// <param name="parOptionIndex">Index of the Gossip-Option to select</param>
        public void SelectGossipOption(int parOptionIndex)
        {
            Lua.Instance.Execute("SelectGossipOption(" + parOptionIndex + ")");
        }

        /// <summary>
        ///     Selects the first Gossip-Option that matches the specified type
        /// </summary>
        /// <param name="parType">Type of the wanted Gossip-Option</param>
        public void SelectFirstGossipOfType(Enums.GossipTypes parType)
        {
            for (var i = 0; i < Options.Count; i++)
            {
                if (Options[i].Type != parType) continue;
                Lua.Instance.Execute("SelectGossipOption(" + (i + 1) + ")");
                return;
            }
        }

        /// <summary>
        ///     Accepts the quest with the specified id
        /// </summary>
        /// <param name="parId">The Id.</param>
        public void AcceptQuest(int parId)
        {
            foreach (var q in _Quests)
                if (parId == q.Id)
                    Functions.AcceptQuest(NpcGuid, parId);
        }

        /// <summary>
        ///     Completes the quest with the specified id
        /// </summary>
        /// <param name="parId">The Id.</param>
        public void CompleteQuest(int parId)
        {
            foreach (var q in _Quests)
                if (parId == q.Id)
                    Functions.CompleteQuest(NpcGuid, parId);
        }

        private class GossipOptionInterface : GossipOption
        {
            internal GossipOptionInterface(string parText, Enums.GossipTypes parType) : base(parText, parType)
            {
            }
        }

        private class QuestOptionInterface : QuestOption
        {
            internal QuestOptionInterface(int parId, string parText, Enums.QuestGossipState parState)
                : base(parId, parText, parState)
            {
            }
        }
    }
}