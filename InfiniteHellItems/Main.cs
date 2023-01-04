using System;
using System.Collections.Generic;
using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;


namespace InfiniteHellItems
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]

    public class InfiniteHellItems : BaseUnityPlugin {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Recoherent";
        public const string PluginName = "InfiniteHellItems";
        public const string PluginVersion = "0.0.1";

        //base defs of each item
        private static ItemDef annealingCrucible;
        private static ItemDef oldWarStimpack;

        //rng definition for the crucible
        private Xoroshiro128Plus crucibleRng;


        public void Awake() {

            //make these mfin items Real
            annealingCrucible = ScriptableObject.CreateInstance<ItemDef>();
            oldWarStimpack = ScriptableObject.CreateInstance<ItemDef>();


            //define a list that contains all item definitions and their shorthand name
            var itemRegistry = new Dictionary<ItemDef, String> {
                [annealingCrucible] = "CRUCIBLE",
                [oldWarStimpack] = "STIMPACK"
            };


            //iterate the language values for each item
            foreach(KeyValuePair<ItemDef, String> itemSelected in itemRegistry) {
                itemSelected.Key.name = "INFINITEHELL_" + itemSelected.Value + "_NAME";
                itemSelected.Key.nameToken = "INFINITEHELL_" + itemSelected.Value + "_NAME";
                itemSelected.Key.pickupToken = "INFINITEHELL_" + itemSelected.Value + "_PICKUP";
                itemSelected.Key.descriptionToken = "INFINITEHELL_" + itemSelected.Value + "_DESC";
                itemSelected.Key.loreToken = "INFINITEHELL_" + itemSelected.Value + "_LORE";
            };

            //define tiers because apparently that shit is weird
            /*
            #pragma warning disable Publicizer001
            annealingCrucible._itemTierDef = Addressables.LoadAssetAsync<ItemTierDef>("RoR2/Base/Common/LunarDef.asset").WaitForCompletion();
            oldWarStimpack._itemTierDef = Addressables.LoadAssetAsync<ItemTierDef>("RoR2/Base/Common/VoidTier2Def.asset").WaitForCompletion();
            #pragma warning restore Publicizer001
            */

            //annealing crucible
            annealingCrucible.deprecatedTier = ItemTier.Lunar;
            annealingCrucible.canRemove = false;
            annealingCrucible.hidden = false;

            annealingCrucible.pickupIconSprite = Resources.Load<Sprite>("Textures/MiscIcons/texMysteryIcon");
            annealingCrucible.pickupModelPrefab = Resources.Load<GameObject>("Prefabs/PickupModels/PickupMystery");

            LanguageAPI.Add(annealingCrucible.nameToken, "Annealing Crucible");
            LanguageAPI.Add(annealingCrucible.pickupToken, "Downgrades your items at the start of each stage.");
            LanguageAPI.Add(annealingCrucible.descriptionToken, "<style=cIsHealth>Downgrades 1</style> <style=cStack><i>(+1 per stack)</i></style> random items to items of the next <style=cIsHealth>lower rarity</style> at the <style=cIsUtility>start of each stage</style>. Gain 1 <style=cIsUtility>bonus item</style> in each downgraded stack.");
            LanguageAPI.Add(annealingCrucible.loreToken, "<style=cMono>========================================" +
            "\n====   MyBabel Machine Translator   ====" +
            "\n====     [Version 12.45.1.009 ]   ======" +
            "\n========================================" +
            "\nTraining… <100000000 cycles>" +
            "\nTraining… <100000000 cycles>" +
            "\nTraining… <33125760 cycles>" +
            "\nPaused…"+
            "\nDisplay partial result? Y/N" +
            "\nY" +
            "\n========================================</style>" +
            "\nThe [Weapon] is appraised. Sturdy, powerful, in good condition. Date of creation approximately [?] ago. Suitable." +
            "\n\nWe place [Weapon] in the crucible. Heat on." +
            "\n\n[Weapon] begins to melt. Bonds break, history burned. [Imperfections] are skimmed off and discarded." +
            "\n\nResulting matter is appraised. Sturdy, malleable, ductile, springy. Suitable." +
            "\n\nMatter is cast into [Weapons]." +
            "\n\nThe [Weapons] are appraised. Flimsy, brittle. Suitable." +
            "\n\n\n<style=cMono>================================" +
            "\nContinue training? Y/N" +
            "\nY</style>"
            );


            //old war stimpack
            oldWarStimpack.deprecatedTier = ItemTier.VoidTier2;
            oldWarStimpack.canRemove = false;
            oldWarStimpack.hidden = false;

            oldWarStimpack.pickupIconSprite = Resources.Load<Sprite>("Textures/MiscIcons/texMysteryIcon");
            oldWarStimpack.pickupModelPrefab = Resources.Load<GameObject>("Prefabs/PickupModels/PickupMystery");

            LanguageAPI.Add(oldWarStimpack.nameToken, "Old War Stimpack");
            LanguageAPI.Add(oldWarStimpack.pickupToken, "Increase stats at low health. <style=cIsVoid>Corrupts all Old War Stealthkits</style>.");
            LanguageAPI.Add(oldWarStimpack.descriptionToken, "Falling below <style=cIsHealth>25% health</style> increases <style=cIsDamage>attack speed</style>, <style=cIsUtility>movement speed</style>, and <style=cIsDamage>critical chance</style> by <style=cIsDamage>15%</style> <style=cStack><i>(+15% per stack)</i></style>. <style=cIsVoid>Corrupts all Old War Stealthkits</style>.");
            LanguageAPI.Add(oldWarStimpack.loreToken, "\"If there's any one rule I learned out in the field, it's that the only time drugs get bad is if you ever stop takin' the damn things.\"" +
            "\n\n-Signal echoes, UES Contact Light"
            );


            //i ain't doing display right now
            var displayRules = new ItemDisplayRuleDict(null);

            foreach(KeyValuePair<ItemDef, String> itemSelected in itemRegistry) {
                ItemAPI.Add(new CustomItem(itemSelected.Key, displayRules));
            };


            //define hooks
            //Stage.onServerStageBegin += Stage_onServerStageBegin;
            On.RoR2.CharacterMaster.OnServerStageBegin += CharacterMaster_OnServerStageBegin;
        }

        private void TryCrucibleDegrades(CharacterMaster self) {
            //current just debug function, gives you another when you change stages
            /*
            if (self.inventory && self.inventory.GetItemCount(annealingCrucible) > 0) {
                self.inventory.GiveItem(annealingCrucible, 1);
                CharacterMasterNotificationQueue.SendTransformNotification(self, annealingCrucible.itemIndex, annealingCrucible.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);

                //plays a noise!!
                GameObject gameObject = self.bodyInstanceObject;
                if ((bool)gameObject)
                {
                    Util.PlaySound("Play_item_proc_extraLife", gameObject);
                }
            }
            */

            //the actual function!

            if (crucibleRng == null) //ensures that it has a new seed every run
		    {   
			    crucibleRng = new Xoroshiro128Plus(Run.instance.seed);
		    }   

            //quantity of item
            int itemCount = self.inventory.GetItemCount(annealingCrucible);

            //available items to choose from when downgrading
            List<PickupIndex> tier1List = new List<PickupIndex>(Run.instance.availableTier1DropList);
            List<PickupIndex> tier2List = new List<PickupIndex>(Run.instance.availableTier2DropList);
            //your own items
            List<ItemIndex> ownItems = new List<ItemIndex>(self.inventory.itemAcquisitionOrder);

            int downgradeBonus = 1; //how many extra items a stack should gain when broken down. probably broken as shit

            int maxDowngrade = itemCount * 1; //amount of stacks to downgrade
            int downgradeSuccesses = 0; //how many times things have successfully downgraded
            int downgradeIndex = 0; //how many times things have attempted to downgrade

            while (downgradeSuccesses < maxDowngrade && downgradeIndex < ownItems.Count) {
                ItemDef startingItemDef = ItemCatalog.GetItemDef(ownItems[downgradeIndex]); //gets next item to downgrade
                ItemDef postDowngrade = null; //item that will be downgraded into
                List<PickupIndex> targetList = null; //list of potential downgrade targets

                switch (startingItemDef.tier) { //picks lower tier. targetlist is null if the current item is t1/lunar/whatever
                    case ItemTier.Tier2:
                        targetList = tier1List;
                        break;
                    case ItemTier.Tier3:
                        targetList = tier2List;
                        break;
                }

                if (targetList != null && targetList.Count > 0) { //only runs if can actually downgrade
                    Util.ShuffleList(targetList, crucibleRng);
                    targetList.Sort(CompareTags); //essentially this grabs whatever the first item of the correct matching tag is and moves it to the top of the array
                    postDowngrade = ItemCatalog.GetItemDef(targetList[0].itemIndex); //and then grabs it for conversion
                }

                if (postDowngrade != null) { //only runs if an item is actually found for downgrading
                    if (self.inventory.GetItemCount(postDowngrade) == 0) { //adds to your item array so that items can be downgraded twice if there is no other option
                        ownItems.Add(postDowngrade.itemIndex);
                    }
                    downgradeSuccesses++;
                    int downgradeQuantity = self.inventory.GetItemCount(startingItemDef.itemIndex) + downgradeBonus; //amount of items in the downgraded stack
                    self.inventory.RemoveItem(startingItemDef.itemIndex, downgradeQuantity); //removes the whole stack
                    self.inventory.GiveItem(postDowngrade.itemIndex, downgradeQuantity); //gives them all back as the lower rarity item
                    CharacterMasterNotificationQueue.SendTransformNotification(self, startingItemDef.itemIndex, postDowngrade.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                }

                downgradeIndex++;

                //i ain't gonna sugarcoat it this is just copied from benthic
                //moves items with a tag matching the starting item up in the array
                int CompareTags(PickupIndex lhs, PickupIndex rhs) {
                    int num4 = 0;
                    int num5 = 0;
                    ItemDef itemDef2 = ItemCatalog.GetItemDef(lhs.itemIndex);
                    ItemDef itemDef3 = ItemCatalog.GetItemDef(rhs.itemIndex);
                    if (startingItemDef.ContainsTag(ItemTag.Damage))
                    {
                        if (itemDef2.ContainsTag(ItemTag.Damage))
                        {
                            num4 = 1;
                        }
                        if (itemDef3.ContainsTag(ItemTag.Damage))
                        {
                            num5 = 1;
                        }
                    }
                    if (startingItemDef.ContainsTag(ItemTag.Healing))
                    {
                        if (itemDef2.ContainsTag(ItemTag.Healing))
                        {
                            num4 = 1;
                        }
                        if (itemDef3.ContainsTag(ItemTag.Healing))
                        {
                            num5 = 1;
                        }
                    }
                    if (startingItemDef.ContainsTag(ItemTag.Utility))
                    {
                        if (itemDef2.ContainsTag(ItemTag.Utility))
                        {
                            num4 = 1;
                        }
                        if (itemDef3.ContainsTag(ItemTag.Utility))
                        {
                            num5 = 1;
                        }
                    }
                    return num5 - num4;
                }

            }

            if (downgradeSuccesses > 0) { //play a fun noise if the process did anything
                GameObject gameObject = self.bodyInstanceObject;
                if ((bool)gameObject)
                {
                    Util.PlaySound("Play_item_proc_extraLife", gameObject);
                }
            }
        }

        private void CharacterMaster_OnServerStageBegin(On.RoR2.CharacterMaster.orig_OnServerStageBegin orig, CharacterMaster self, Stage stage) {
            //annealing crucible checks stuff when entering stage so check stuff when entering stage. i love. Commence
            orig(self, stage);

            TryCrucibleDegrades(self);
            
        }

        private void Update() {
            //debug to give the items
            if (Input.GetKeyDown(KeyCode.Y)) {
                //pc controller > first character in list > charactermaster > inventory > give
                var theGuy = PlayerCharacterMasterController.instances[0].master;
                theGuy.inventory.GiveItem(annealingCrucible, 1);
                CharacterMasterNotificationQueue.SendTransformNotification(theGuy, annealingCrucible.itemIndex, annealingCrucible.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                //plays a noise!!
                GameObject gameObject = theGuy.bodyInstanceObject;
                if ((bool)gameObject)
                {
                    Util.PlaySound("Play_item_proc_extraLife", gameObject);
                }
            }
        }
    }
}
