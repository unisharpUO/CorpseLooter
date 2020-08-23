using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ScriptSDK;
using StealthAPI;
using ScriptSDK.Attributes;
using ScriptSDK.Engines;
using ScriptSDK.Items;
using ScriptSDK.Mobiles;
using XScript.Attributes;
using XScript.Enumerations;
using XScript.Extensions;
using XScript.Interfaces;
using XScript.Items;
using ScriptSDK.Data;
using ScriptSDK.Configuration;
using XScript.Scripts.unisharpUO;

namespace CorpseLooter
{
    class Program
    {
        #region Vars
        static Mobile CurrentTarget = null;
        static PlayerMobile Self = PlayerMobile.GetPlayer();
        static bool Looting, ChaseMonster, LootingItems, DontMove;
        static Mobile Ari = new Mobile(new Serial(40387283));
        static Item LootBag;
        static List<Jewelry> JewelryList = new List<Jewelry>();
        static List<Armor> ArmorList = new List<Armor>();
        static List<Weapon> WeaponsList = new List<Weapon>();
        static List<Shield> ShieldList = new List<Shield>();
        static List<Item> MissingItems = new List<Item>();
        static uint BokutosFound;
        static bool Confidence = false;
        #endregion

        #region Objects
        [QueryType(typeof(BaseArmor), typeof(BaseJewel), typeof(BaseWeapon))]
        public class BaseEquipment : Item
        {
            public BaseEquipment(Serial serial)
            : base(serial)
            {
            }
        }

        [QuerySearch(new ushort[] { 0x10B })]
        public class Trog : Item
        {
            public Trog(Serial serial)
            : base(serial)
            {
            }
        }
        [QuerySearch(new ushort[] { 0x18 })]
        public class Liche : Item
        {
            public Liche(Serial serial)
            : base(serial)
            {
            }
        }

        [QuerySearch(new ushort[] { 0x09B })]
        public class UnfrozenMummy : Item
        {
            public UnfrozenMummy(Serial serial)
            : base(serial)
            {
            }
        }
        

        [QuerySearch(new ushort[] { 0x0F7 })]
        public class FanDancer : Item
        {
            public FanDancer(Serial serial)
            : base(serial)
            {
            }
        }

        [QueryType(typeof(BaseEquipment))]
        public class Loot : Item
        {
            private string _reason;
            public string Reason
            {
                get { return _reason; }
                set { _reason = value; }
            }

            public Loot(Serial serial)
                : base(serial)
            {
            }

            public Loot(Serial serial, string Reason)
            : base(serial)
            {
                this.Reason = Reason;
            }
        }
        #endregion

        /// <summary>
        /// Method for posting messages to the console
        /// with a timestamp
        /// </summary>
        /// <param name="message">string: message to send to the console</param>
        /// <param name="args">(optional)object[]: additional arguments</param>
        public static void ConsoleMessage(string message, params object[] args)
        {
            Console.Write("[{0}-{1}] ", Self.Name, DateTime.Now.ToString("hh:mm:ss"));
            Console.WriteLine(message, args);
        }

        /// <summary>
        /// Method for posting messages to the console
        /// with a timestamp and optional color
        /// </summary>
        /// <param name="message">string: message to send to the console</param>
        /// <param name="color">(optional)enum ConsoleColor: the color you want the message to be</param>
        /// <param name="args">(optional)object[]: additional arguments</param>
        public static void ConsoleMessage(string message, ConsoleColor color = ConsoleColor.White, params object[] args)
        {
            Console.Write("[{0}-{1}] ", Self.Name, DateTime.Now.ToString("hh:mm:ss"));
            Console.ForegroundColor = color;
            Console.WriteLine(message, args);
            Console.ResetColor();
        }

        /// <summary>
        /// Method for re
        /// </summary>
        /// <param name="TimeoutMS"></param>
        /// <returns></returns>
        public static Item RequestTarget(uint TimeoutMS = 0)
        {
            Stealth.Client.ClientRequestObjectTarget();
            Stopwatch timer = new Stopwatch();


            timer.Start();
            while (Stealth.Client.ClientTargetResponsePresent() == false)
            {
                if (TimeoutMS != 0 && timer.ElapsedMilliseconds >= TimeoutMS)
                    return default(Item);
            }

            return new Item(new Serial(Stealth.Client.ClientTargetResponse().ID));
        }

        static void Main(string[] args)
        {
            ObjectOptions.ToolTipDelay = 500;

            Self.Backpack.DoubleClick();

            Stealth.Client.Wait(1000);

            Stealth.Client.AddItemToContainer += OnAdd;
            Stealth.Client.ClilocSpeech += OnClilocSpeech;
            Stealth.Client.Speech += OnSpeech;

            ConsoleMessage("Target loot bag");

            LootBag = RequestTarget();
            LootBag.DoubleClick();

            Stealth.Client.Wait(1000);

            List<Item> _backpackItems = new List<Item>();
            List<uint> _findList = new List<uint>();

            Stealth.Client.FindTypeEx(0xFFFF, 0xFFFF, Self.Backpack.Serial.Value, true);
            if (!(Stealth.Client.GetFindCount() == 0))
                _findList = Stealth.Client.GetFindList();

            foreach (uint _item in _findList)
            {
                Stealth.Client.Ignore(_item);
                Scanner.Ignore(_item);
            }
            
            ConsoleMessage("starting loot routine...", ConsoleColor.DarkYellow);
            Looting = true;
            LootingItems = false;

            Scanner.Initialize();

            Scanner.Range = 5;
            Scanner.VerticalRange = 5;

            Stealth.Client.SetFindDistance(5);
            Stealth.Client.SetFindVertical(5);
            Stealth.Client.SetMoveThroughNPC(0);

            var _virtuehelper = VirtueHelper.GetVirtues();
            var _targethelper = TargetHelper.GetTarget();

            while (Stealth.Client.GetConnectedStatus())
            {
                try
                {
                    #region Combat
                    Stealth.Client.ClearBadLocationList();

                    var _monster = Item.Find(typeof(FanDancer), 0x0, false).OrderBy(x => x.Distance).ToList();

                    //ConsoleMessage("{0},{1}", Self.Location.X, Self.Location.Y);

                    _virtuehelper.Request();

                    /*
                    If current target is dead, find a new target
                    if new target is 100% hp, honor target
                    else follow current target until it's dead if we're not looting

                    */

                    if (CurrentTarget == null || CurrentTarget.Dead || !CurrentTarget.Valid)
                    {
                        if (_monster.Any())
                        {
                            CurrentTarget = _monster.First().Cast<Mobile>();
                            ConsoleMessage("Current target set to: {0}", ConsoleColor.Magenta, CurrentTarget.Serial.Value.ToString());

                            Stealth.Client.UseVirtue(Virtue.Honor);
                            Stealth.Client.WaitTargetObject(CurrentTarget.Serial.Value);

                            Self.Attack(CurrentTarget.Serial);

                        }
                    }

                    //entrance room
                    /*if (((ushort)CurrentTarget.Location.X >= 79 && (ushort)CurrentTarget.Location.X <= 97)
                        && ((ushort)CurrentTarget.Location.Y >= 326 && (ushort)CurrentTarget.Location.Y <= 344))*/


                    //bloody room
                    /*if (((ushort)CurrentTarget.Location.X >= 104 && (ushort)CurrentTarget.Location.X <= 115)
                        && ((ushort)CurrentTarget.Location.Y >= 640 && (ushort)CurrentTarget.Location.Y <= 660))*/


                    if (((ushort)CurrentTarget.Location.X >= 79 && (ushort)CurrentTarget.Location.X <= 97)
                        && ((ushort)CurrentTarget.Location.Y >= 326 && (ushort)CurrentTarget.Location.Y <= 344))
                    {
                        Self.Movement.newMoveXY((ushort)CurrentTarget.Location.X, (ushort)CurrentTarget.Location.Y, true, 0, true);
                        Self.Attack(CurrentTarget.Serial);
                    }

                    /* For leafblade
                    if (Self.Mana > 35 && Self.HealthPercent > 70)
                        Self.UseSecondaryAbility();
                    else if (Self.Mana > 35 && Self.HealthPercent < 70)
                        Self.UsePrimaryAbility();
                        */

                    if (Self.Mana > 35)
                        Self.UsePrimaryAbility();

                    //if (Self.HealthPercent < 50)
                    //Self.Cast("Evasion");
                    //if (Self.HealthPercent < 80 && !Confidence)                    
                    //Self.Cast("Confidence");

                    #endregion

                    #region Loot
                    var _corpses = Item.Find(typeof(Corpse), 0x0, false).OrderBy(x => x.Distance).ToList();

                    if (_corpses.Count > 0)
                    {
                        foreach (Corpse _corpse in _corpses)
                        {
                            if (_corpse.Distance < 3)
                            {
                                LootCorpse(_corpse);
                                Scanner.Ignore(_corpse.Serial);
                            }
                            else
                            {/*
                            if (((ushort)_corpse.Location.X >= 79 && (ushort)_corpse.Location.X <= 97)
                                && ((ushort)_corpse.Location.Y >= 326 && (ushort)_corpse.Location.Y <= 344))*/
                                //Bloody room
                                /*if (((ushort)_corpse.Location.X >= 104 && (ushort)_corpse.Location.X <= 115)
                                    && ((ushort)_corpse.Location.Y >= 640 && (ushort)_corpse.Location.Y <= 660))*/
                                if (((ushort)_corpse.Location.X >= 79 && (ushort)_corpse.Location.X <= 97)
                                    && ((ushort)_corpse.Location.Y >= 326 && (ushort)_corpse.Location.Y <= 344))
                                {
                                    Self.Movement.MoveXYZ((ushort)_corpse.Location.X, (ushort)_corpse.Location.Y, (sbyte)_corpse.Location.Z, 1, 1, true);
                                    LootCorpse(_corpse);
                                    Scanner.Ignore(_corpse.Serial);
                                }
                            }
                        }
                    }
                    #endregion

                    Thread.Sleep(2000);
                }
                catch (Exception ex)
                {
                    ConsoleMessage("Error in main routine: {0}", ex.StackTrace);
                }

            }

        }

        #region Event Listeners
        private static void OnAdd(object sender, AddItemToContainerEventArgs e)
        {
            try
            {
                var _incomingContainer = new Serial((uint)e.ContainerId);
                if (_incomingContainer.Value == LootBag.Serial.Value)
                    ConsoleMessage("Looted: {0}", ConsoleColor.Green, e.ItemId.ToString());
            }
            catch (Exception ex)
            {
                ConsoleMessage("Error during OnAdd: {0}", ex.StackTrace);
            }
            
        }

        private static void OnSpeech(object sender, SpeechEventArgs e)
        {
            try
            {
                if (e.Text.Contains("severely damaged"))
                ConsoleMessage("Equipment Damaged", ConsoleColor.Magenta);
            ConsoleMessage("Speech caught: {0}", ConsoleColor.Cyan, e.Text);
            }
            catch (Exception ex)
            {
                ConsoleMessage("Error during OnSpeech: {0}", ex.StackTrace);
            }

        }

        private static void OnClilocSpeech(object sender, ClilocSpeechEventArgs e)
        {
            try
            {
                if (e.Text.Contains("severely damaged"))
                ConsoleMessage("Equipment Damaged", ConsoleColor.Magenta);
            else if (e.Text.Contains("exude confidence"))
                Confidence = true;
            else if (e.Text.Contains("confidence wanes"))
                Confidence = false;
            }
            catch (Exception ex)
            {
                ConsoleMessage("Error during OnClilocSpeech: {0}", ex.StackTrace);
            }

        }
        #endregion

        #region Loot Methods
        public static void LootCorpse(Corpse _corpse)
        {
            try
            {
                List<Loot> _toLootList = new List<Loot>();

                Stopwatch _lootTimer = new Stopwatch();
                var _corpseID = _corpse.Serial.Value;

                _corpse.DoubleClick();

                Thread.Sleep(1500);
                _lootTimer.Start();

                //ConsoleMessage("Starting Search");

                List<Item> _container = new List<Item>();
                List<uint> _findList = new List<uint>();

                Stealth.Client.FindTypeEx(0xFFFF, 0xFFFF, _corpseID, true);
                if (!(Stealth.Client.GetFindCount() == 0))
                    _findList = Stealth.Client.GetFindList();

                foreach (uint _item in _findList)
                {
                    _container.Add(new Item(new Serial(_item)));
                }


                if (_container.Count == 0)
                    return;

                List<uint> _firstList = new List<uint>();

                foreach (Item _item in _container)
                {
                    _firstList.Add(_item.Serial.Value);
                }


                List<Item> _resultsList = new List<Item>();
                List<uint> _secondList = new List<uint>();

                List<string> _types = new List<string>();
                _types.Add("Armor");
                _types.Add("Jewel");
                _types.Add("Weapon");
                _types.Add("Shield");

                LocateGear.Find(_corpse, _types);

                foreach (BaseArmor _armor in LocateGear.ArmorList)
                {
                    _secondList.Add(_armor.Serial.Value);
                    _armor.UpdateLocalizedProperties();
                    if (!_armor.Antique && !_armor.Cursed)
                        ArmorList.Add(new Armor(_armor, _corpseID));
                }
                foreach (BaseJewel _jewel in LocateGear.JewelList)
                {
                    _secondList.Add(_jewel.Serial.Value);
                    _jewel.UpdateLocalizedProperties();
                    JewelryList.Add(new Jewelry(_jewel, _corpseID));
                }
                foreach (BaseWeapon _weapon in LocateGear.WeaponList)
                {
                    _secondList.Add(_weapon.Serial.Value);
                    _weapon.UpdateLocalizedProperties();
                    if (!_weapon.Antique && !_weapon.Cursed)
                        WeaponsList.Add(new Weapon(_weapon, _corpseID));
                }
                foreach (BaseShield _shield in LocateGear.ShieldList)
                {
                    _secondList.Add(_shield.Serial.Value);
                    _shield.UpdateLocalizedProperties();
                    if (!_shield.Antique)
                        ShieldList.Add(new Shield(_shield, _corpseID));
                }

                List<uint> _missingItems = _firstList.Except(_secondList).ToList();

                foreach (Armor _armor in ArmorList)
                {
                    if (_armor.TotalStat >= 20 && _armor.LMC > 4)
                        _toLootList.Add(new Loot(new Serial(_armor.ID), "20 Stats and LMC"));
                    else if (_armor.TotalStat >= 20 && _armor.LRC > 4)
                        _toLootList.Add(new Loot(new Serial(_armor.ID), "20 Stats and LRC"));
                }

                foreach (Jewelry _jewel in JewelryList)
                {
                    if (_jewel.EnhancePotions >= 25 && _jewel.DCI >= 15 && _jewel.HCI >= 15 && _jewel.FCR >= 3)
                        _toLootList.Add(new Loot(new Serial(_jewel.ID), "EP/DCI Jewel"));
                }

                foreach (Weapon _weapon in WeaponsList)
                {
                    if (_weapon.Splintering >= 20)
                        _toLootList.Add(new Loot(new Serial(_weapon.ID), "Splintering Weapon"));
                    if (_weapon.ItemName.Contains("Bokuto") || _weapon.ItemName.Contains("bokuto"))
                    {
                        ConsoleMessage("Parsed bokuto...", ConsoleColor.Blue);
                        if (_weapon.Splintering >= 5)
                            ConsoleMessage("It had splintering...", ConsoleColor.Yellow);
                    }

                    if (_weapon.ItemName.Contains("Yumi") || _weapon.ItemName.Contains("yumi"))
                        if (_weapon.HitSpell.Contains("Lightning") || _weapon.HitSpell.Contains("lightning"))
                            _toLootList.Add(new Loot(new Serial(_weapon.ID), "Yumi with hit lightning"));



                    //add check for splintering anything bokuto
                }

                //ConsoleMessage("Search complete");

                _lootTimer.Stop();
                ConsoleMessage("Search took {0}, {1} items.", ConsoleColor.Green, _lootTimer.Elapsed, _findList.Count());
                //ConsoleMessage("Old search took {0}", ConsoleColor.Cyan, _lootTimer.Elapsed);

                if (_toLootList.Count() > 0)
                    LootItems(_toLootList);

                ArmorList.Clear();
                WeaponsList.Clear();
                JewelryList.Clear();
                ShieldList.Clear();
            }
            catch (Exception ex)
            {
                ConsoleMessage("Error during Looting: {0}", ex.StackTrace);
            }
        }

        public static void LootItems(List<Loot> _toLootList)
        {
            Thread.Sleep(1500);

            LootingItems = true;
            PlayerMobile _player = PlayerMobile.GetPlayer();

            Stealth.Client.CancelTarget();

            foreach (Loot _loot in _toLootList)
            {
                ConsoleMessage("Looting Item: " + _loot.Serial.Value.ToString() + ' ' + _loot.Reason, ConsoleColor.DarkGreen);

                //_loot.Grab();
                var _lootBag = new Container(LootBag.Serial);
                _loot.MoveItem(_lootBag);

                Thread.Sleep(1500);

                InsureItem(_loot);
                
                Scanner.Ignore(_loot.Serial);

                Thread.Sleep(1500);
            }
            _toLootList.Clear();

            LootingItems = false;
        }

        public static byte GetInsureNumber()
        {
            //List<string> _stringList = new List<string>();
            string _string;
            _string = Stealth.Client.GetContextMenu();
            string[] _stringArray = _string.Split('|');

            for (int i = 0; i <= (_stringArray.Count()) - 1; i++)
            {
                ConsoleMessage(i.ToString());

                ConsoleMessage(_stringArray[i].ToString());

                if (_stringArray[i] == "418")
                {
                    ConsoleMessage(i.ToString());
                    return (byte)i;
                }
            }

            return 0;
        }

        public static void InsureItem(Loot _loot)
        {
            Stealth.Client.RequestContextMenu(Self.Serial.Value);
            Stealth.Client.SetContextMenuHook(Self.Serial.Value, 9);

            Thread.Sleep(1500);

            var _helper = TargetHelper.GetTarget();

            _helper.AutoTargetTo(_loot.Serial);

            Thread.Sleep(500);

            Stealth.Client.CancelMenu();
            Stealth.Client.CancelTarget();

            ConsoleMessage("Insured Item: {0}", _loot.Serial.Value);
        }
        #endregion

        public static List<ushort> GetTypes(Type T)
        {
            try
            {
                var ca = T.GetCustomAttributes(false);
                var tlist = new List<ushort>();

                if (ca != null)
                {

                    foreach (var a in ca)
                    {
                        if (a is QuerySearchAttribute)
                        {
                            var x = (QuerySearchAttribute)a;
                            tlist.AddRange(x.Graphics);
                        }
                        else if (a is QueryTypeAttribute)
                        {
                            var x = (QueryTypeAttribute)a;
                            foreach (var e in x.Types)
                                tlist.AddRange(GetTypes(e));
                        }
                    }
                }

                return tlist.Distinct().ToList();
            }
            catch (Exception ex)
            {
                ConsoleMessage("Error during GetTypes: {0}", ex.StackTrace);
                var _blankList = new List<ushort>();
                return _blankList;
            }
        }
    }
}
