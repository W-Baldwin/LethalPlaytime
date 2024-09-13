using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using BepInEx.Configuration;
using LethalLib;
using LethalConfig.ConfigItems.Options;
using LethalConfig.ConfigItems;
using LethalConfig;

namespace LethalPlaytime
{
    public class PlaytimeConfig
    {
        private static AssetBundle assetBundle;

        internal enum RarityAddTypes { All, Modded, Vanilla, List };

        private static LethalLib.Modules.Levels.LevelTypes chosenRegistrationMethod = LethalLib.Modules.Levels.LevelTypes.All;

        private static RarityAddTypes defaultCreatureRegistrationMethod = RarityAddTypes.All;

        private static int missDelightRarity;
        private static int huggyWuggyRarity;
        private static int dogdayMonsterRarity;
        private static int boxyBooMonsterRarity;

        public static void ConfigureAndRegisterAssets(LethalPlaytime Instance)
        {
            LoadAssets();
            CreateConfigEntries(Instance);
            ConfigureAndRegisterMissDelight();
            ConfigureAndRegisterHuggyWuggy();
            ConfigureAndRegisterDogday();
            ConfigureAndRegisterBoxyBoo();
        }

        internal static void CreateConfigEntries(LethalPlaytime Instance)
        {
            //Enumerated way to register creatures.
            var configEntryForScrapMethod = Instance.Config.Bind("Creatures", "Registration Method", RarityAddTypes.All
                , "The method to add creatures to the level. \n Default = All \n Vanilla \n Modded \n List (Not yet implemented for creatures, defaults to All)");
            var enumCreatureRegistration = new EnumDropDownConfigItem<RarityAddTypes>(configEntryForScrapMethod);
            switch (configEntryForScrapMethod.Value)
            {
                case RarityAddTypes.All:
                    chosenRegistrationMethod = LethalLib.Modules.Levels.LevelTypes.All;
                    break;
                case RarityAddTypes.Vanilla:
                    chosenRegistrationMethod = LethalLib.Modules.Levels.LevelTypes.Vanilla;
                    break;
                case RarityAddTypes.Modded:
                    chosenRegistrationMethod = LethalLib.Modules.Levels.LevelTypes.Modded;
                    break;
                case RarityAddTypes.List:
                    chosenRegistrationMethod = LethalLib.Modules.Levels.LevelTypes.All;
                    break;
            }

            var rarityEntryMissDelight = Instance.CreateIntSliderConfig("Miss Delight", 50, "Adjust how often you see the enemy Miss Delight.", 0, 100, "Creatures");
            missDelightRarity = rarityEntryMissDelight.Value;
            var rarityEntryHuggyWuggy = Instance.CreateIntSliderConfig("Huggy Wuggy", 40, "Adjust how often you see Huggy Wuggy.", 0, 100, "Creatures");
            huggyWuggyRarity = rarityEntryHuggyWuggy.Value;
            var rarityEntryDogdayMonster = Instance.CreateIntSliderConfig("Monster Dogday", 40, "Adjust how often you see Dogday.", 0, 100, "Creatures");
            dogdayMonsterRarity = rarityEntryDogdayMonster.Value;
            var rarityEntryBoxyBoo = Instance.CreateIntSliderConfig("Boxy Boo", 35, "Adjust how often you see Boxy Boo.", 0, 100, "Creatures");
            boxyBooMonsterRarity = rarityEntryBoxyBoo.Value;
        }

        private static void ConfigureAndRegisterMissDelight()
        {
            EnemyType missDelight = assetBundle.LoadAsset<EnemyType>("Miss Delight");

            //Configure Miss Delight
            MissDelightAI missDelightAIOriginal = missDelight.enemyPrefab.AddComponent<MissDelightAI>();
            missDelightAIOriginal.creatureVoice = missDelight.enemyPrefab.GetComponent<AudioSource>();
            missDelightAIOriginal.creatureSFX = missDelight.enemyPrefab.GetComponent<AudioSource>();
            missDelightAIOriginal.weaponAudioPoint = FindDeepChild(missDelight.enemyPrefab.transform, "DelightWeaponAudio").GetComponent<AudioSource>();
            missDelightAIOriginal.agent = missDelight.enemyPrefab.GetComponent<NavMeshAgent>();
            missDelightAIOriginal.creatureAnimator = missDelight.enemyPrefab.GetComponent<Animator>();
            missDelightAIOriginal.syncMovementSpeed = 0.1f;
            missDelightAIOriginal.exitVentAnimationTime = 1;
            missDelightAIOriginal.eye = missDelight.enemyPrefab.transform.Find("Eye");
            missDelightAIOriginal.enemyType = missDelight;
            missDelightAIOriginal.updatePositionThreshold = 0.1f;
            missDelightAIOriginal.enemyBehaviourStates = new EnemyBehaviourState[4];
            missDelightAIOriginal.AIIntervalTime = 0.08f;
            missDelightAIOriginal.weaponCollider = FindDeepChild(missDelight.enemyPrefab.transform, "JNT_R_Hand_IK").GetComponent<SphereCollider>();

            //Fix reference for EnemyAICollisionDetect to allow for opening/closing door functions.
            missDelight.enemyPrefab.transform.Find("Teacher").GetComponent<EnemyAICollisionDetect>().mainScript = missDelightAIOriginal;


            //Add and load aduio clips.
            missDelightAIOriginal.footstepClips = new AudioClip[7];
            missDelightAIOriginal.footstepClips[0] = assetBundle.LoadAsset<AudioClip>("DelightFootstep1");
            missDelightAIOriginal.footstepClips[1] = assetBundle.LoadAsset<AudioClip>("DelightFootstep2");
            missDelightAIOriginal.footstepClips[2] = assetBundle.LoadAsset<AudioClip>("DelightFootstep3");
            missDelightAIOriginal.footstepClips[3] = assetBundle.LoadAsset<AudioClip>("DelightFootstep4");
            missDelightAIOriginal.footstepClips[4] = assetBundle.LoadAsset<AudioClip>("DelightFootstep5");
            missDelightAIOriginal.footstepClips[5] = assetBundle.LoadAsset<AudioClip>("DelightFootstep6");
            missDelightAIOriginal.footstepClips[6] = assetBundle.LoadAsset<AudioClip>("DelightFootstep7");

            missDelightAIOriginal.weaponSwingClip = new AudioClip[1];
            missDelightAIOriginal.weaponSwingClip[0] = assetBundle.LoadAsset<AudioClip>("DelightWeaponSwing1");

            missDelightAIOriginal.weaponSwingVoiceClips = new AudioClip[4];
            missDelightAIOriginal.weaponSwingVoiceClips[0] = assetBundle.LoadAsset<AudioClip>("DelightSwingVoice1");
            missDelightAIOriginal.weaponSwingVoiceClips[1] = assetBundle.LoadAsset<AudioClip>("DelightSwingVoice2");
            missDelightAIOriginal.weaponSwingVoiceClips[2] = assetBundle.LoadAsset<AudioClip>("DelightSwingVoice3");
            missDelightAIOriginal.weaponSwingVoiceClips[3] = assetBundle.LoadAsset<AudioClip>("DelightSwingVoice4");

            missDelightAIOriginal.weaponImpactClips = new AudioClip[2];
            missDelightAIOriginal.weaponImpactClips[0] = assetBundle.LoadAsset<AudioClip>("DelightWeaponImpact1");
            missDelightAIOriginal.weaponImpactClips[1] = assetBundle.LoadAsset<AudioClip>("DelightWeaponImpact2");

            missDelightAIOriginal.laughClips = new AudioClip[4];
            missDelightAIOriginal.laughClips[0] = assetBundle.LoadAsset<AudioClip>("DelightLaugh1");
            missDelightAIOriginal.laughClips[1] = assetBundle.LoadAsset<AudioClip>("DelightLaugh2");
            missDelightAIOriginal.laughClips[2] = assetBundle.LoadAsset<AudioClip>("DelightLaugh3");
            missDelightAIOriginal.laughClips[3] = assetBundle.LoadAsset<AudioClip>("DelightLaugh4");

            missDelightAIOriginal.doorSmashClips = new AudioClip[1];
            missDelightAIOriginal.doorSmashClips[0] = assetBundle.LoadAsset<AudioClip>("DelightDoorKick 1");

            missDelightAIOriginal.growlClips = new AudioClip[4];
            missDelightAIOriginal.growlClips[0] = assetBundle.LoadAsset<AudioClip>("DelightBreathe1");
            missDelightAIOriginal.growlClips[1] = assetBundle.LoadAsset<AudioClip>("DelightBreathe2");
            missDelightAIOriginal.growlClips[2] = assetBundle.LoadAsset<AudioClip>("DelightBreathe3");
            missDelightAIOriginal.growlClips[3] = assetBundle.LoadAsset<AudioClip>("DelightBreathe4");

            //Register Miss Delight
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(missDelight.enemyPrefab);
            TerminalNode missDelightTerminalNode = assetBundle.LoadAsset<TerminalNode>("Miss Delight Terminal Node");
            TerminalKeyword missDelightTerminalKeyword = assetBundle.LoadAsset<TerminalKeyword>("Miss Delight Terminal Keyword");
            LethalLib.Modules.Enemies.RegisterEnemy(missDelight, missDelightRarity, chosenRegistrationMethod, missDelightTerminalNode, missDelightTerminalKeyword);
        }

        private static void ConfigureAndRegisterHuggyWuggy()
        {
            
            EnemyType huggyWuggy = assetBundle.LoadAsset<EnemyType>("Huggy Wuggy");
            HuggyAI huggyWuggyAIScript = huggyWuggy.enemyPrefab.AddComponent<HuggyAI>();
            huggyWuggyAIScript.creatureVoice = huggyWuggy.enemyPrefab.GetComponent<AudioSource>();
            huggyWuggyAIScript.creatureSFX = huggyWuggy.enemyPrefab.GetComponent<AudioSource>();
            huggyWuggyAIScript.voiceAudio = huggyWuggy.enemyPrefab.transform.Find("VoiceAudio").GetComponent<AudioSource>();
            huggyWuggyAIScript.voiceFarAudio = huggyWuggy.enemyPrefab.transform.Find("VoiceFarAudio").GetComponent<AudioSource>();
            huggyWuggyAIScript.slashAudio = huggyWuggy.enemyPrefab.transform.Find("SlashAudio").GetComponent<AudioSource>();
            huggyWuggyAIScript.enemyBehaviourStates = new EnemyBehaviourState[6];
            huggyWuggyAIScript.AIIntervalTime = 0.08f;
            huggyWuggyAIScript.syncMovementSpeed = 0.1f;
            huggyWuggyAIScript.updatePositionThreshold = 0.12f;
            huggyWuggyAIScript.exitVentAnimationTime = 1;
            huggyWuggyAIScript.enemyType = huggyWuggy;
            huggyWuggyAIScript.eye = huggyWuggy.enemyPrefab.transform.Find("Eye");
            huggyWuggyAIScript.agent = huggyWuggy.enemyPrefab.GetComponent<NavMeshAgent>();
            huggyWuggyAIScript.creatureAnimator = huggyWuggy.enemyPrefab.GetComponent<Animator>();
            huggyWuggyAIScript.jumpScarePoint = FindDeepChild(huggyWuggy.enemyPrefab.transform, "JumpScarePoint");
            huggyWuggyAIScript.agent.baseOffset = 0f;

            //Collision
            huggyWuggy.enemyPrefab.transform.Find("HuggyWuggyModelContainer").GetComponent<EnemyAICollisionDetect>().mainScript = huggyWuggyAIScript;
            huggyWuggyAIScript.weaponSwingCheckArea = huggyWuggy.enemyPrefab.transform.Find("AttackCollisionArea").GetComponent<BoxCollider>();
            huggyWuggyAIScript.sitCollisionArea = huggyWuggy.enemyPrefab.transform.Find("SitCollision").GetComponent<BoxCollider>();

            TerminalNode huggyWuggyTerminalNode = assetBundle.LoadAsset<TerminalNode>("Huggy Wuggy Terminal Node");
            TerminalKeyword huggyWuggyTerminalKeyword = assetBundle.LoadAsset<TerminalKeyword>("Huggy Wuggy Terminal Keyword");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(huggyWuggy.enemyPrefab);
            LethalLib.Modules.Enemies.RegisterEnemy(huggyWuggy, huggyWuggyRarity, chosenRegistrationMethod, huggyWuggyTerminalNode, huggyWuggyTerminalKeyword);

            //Audio configuration for Huggy
            //Custom audio sources already configured above.
            //Footstep Clips
            huggyWuggyAIScript.footstepClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggyStomp1"),
            assetBundle.LoadAsset<AudioClip>("HuggyStomp2"),
            assetBundle.LoadAsset<AudioClip>("HuggyStomp3")
            };

            //Attack swing clips
            huggyWuggyAIScript.attackSwingClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggyAttack1"),
            assetBundle.LoadAsset<AudioClip>("HuggyAttack2"),
            assetBundle.LoadAsset<AudioClip>("HuggyAttack3"),
            assetBundle.LoadAsset<AudioClip>("HuggyAttack4")
            };
            //Attacking voice clips.
            huggyWuggyAIScript.attackVoiceClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggyAttackVoice1"),
            assetBundle.LoadAsset<AudioClip>("HuggyAttackVoice2"),
            assetBundle.LoadAsset<AudioClip>("HuggyAttackVoice3")
            };
            //Crouch clip
            huggyWuggyAIScript.crouchClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggyCrouch1"),
            };
            //Sit down clip
            huggyWuggyAIScript.sitDownClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggySitDown"),
            };
            //Sit up clip
            huggyWuggyAIScript.sitUpClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggySitUp"),
            };
            //Roar up close clips
            huggyWuggyAIScript.roarClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggyRoar1"),
            assetBundle.LoadAsset<AudioClip>("HuggyRoar2"),
            assetBundle.LoadAsset<AudioClip>("HuggyRoar3")
            };
            //Roar far clips
            huggyWuggyAIScript.roarFarClips = new AudioClip[] {
            assetBundle.LoadAsset<AudioClip>("HuggyRoar1Far"),
            assetBundle.LoadAsset<AudioClip>("HuggyRoar2Far"),
            assetBundle.LoadAsset<AudioClip>("HuggyRoar3Far")
            };
            //Slash clip
            huggyWuggyAIScript.slashClips = new AudioClip[]
            {
                assetBundle.LoadAsset<AudioClip>("HuggySlash")
            };
            //Jumpscare
            huggyWuggyAIScript.jumpscareClips = new AudioClip[1];
            huggyWuggyAIScript.jumpscareClips[0] = assetBundle.LoadAsset<AudioClip>("Huggie_Jumpscare");
        }

        private static void ConfigureAndRegisterDogday()
        {
            EnemyType dogdayMonster = assetBundle.LoadAsset<EnemyType>("Dogday Monster");
            DogdayAI dogdayAIScript = dogdayMonster.enemyPrefab.AddComponent<DogdayAI>();
            dogdayAIScript.creatureVoice = dogdayMonster.enemyPrefab.GetComponent<AudioSource>();
            dogdayAIScript.creatureSFX = dogdayMonster.enemyPrefab.GetComponent<AudioSource>();
            dogdayAIScript.enemyBehaviourStates = new EnemyBehaviourState[4];
            dogdayAIScript.AIIntervalTime = 0.1f;
            dogdayAIScript.syncMovementSpeed = 0.1f;
            dogdayAIScript.updatePositionThreshold = 0.12f;
            dogdayAIScript.exitVentAnimationTime = 1;
            dogdayAIScript.enemyType = dogdayMonster;
            dogdayAIScript.eye = dogdayMonster.enemyPrefab.transform.Find("Eye");
            dogdayAIScript.agent = dogdayMonster.enemyPrefab.GetComponent<NavMeshAgent>();
            dogdayAIScript.creatureAnimator = dogdayMonster.enemyPrefab.transform.Find("DogdayModelContainer").GetComponent<Animator>();
            dogdayAIScript.enemyHP = 6;

            //Collision and animation events.
            dogdayMonster.enemyPrefab.transform.Find("DogdayCollision").GetComponent<EnemyAICollisionDetect>().mainScript = dogdayAIScript;
            DogdayAnimationEvents dogdayAnimationEvents = dogdayMonster.enemyPrefab.transform.Find("DogdayModelContainer").gameObject.AddComponent<DogdayAnimationEvents>();
            dogdayAnimationEvents.scriptReference = dogdayAIScript;
            dogdayAIScript.attackArea = dogdayMonster.enemyPrefab.transform.Find("AttackArea").GetComponent<BoxCollider>();
            //Audio Source
            dogdayAIScript.walkAudio = dogdayMonster.enemyPrefab.transform.Find("FootStepAudio").GetComponent<AudioSource>();
            dogdayAIScript.staticSoundSource = dogdayMonster.enemyPrefab.transform.Find("StaticAudio").GetComponent<AudioSource>();
            dogdayAIScript.hitConnectAudio = dogdayMonster.enemyPrefab.transform.Find("HitAudio").GetComponent<AudioSource>();

            dogdayAIScript.dogdayModel = dogdayMonster.enemyPrefab.transform.Find("DogdayModelContainer");


            //Walk Audio
            dogdayAIScript.walkSounds = new AudioClip[] {
                assetBundle.LoadAsset<AudioClip>("HuggyStomp1"),
                assetBundle.LoadAsset<AudioClip>("HuggyStomp2"),
                assetBundle.LoadAsset<AudioClip>("HuggyStomp3")
            };

            dogdayAIScript.gurrgleSounds = new AudioClip[] {
                assetBundle.LoadAsset<AudioClip>("DogdayMonsterGurgle1"),
                assetBundle.LoadAsset<AudioClip>("DogdayMonsterGurgle2"),
                assetBundle.LoadAsset<AudioClip>("DogdayMonsterGurgle3"),
                assetBundle.LoadAsset<AudioClip>("DogdayMonsterGurgle4"),
                assetBundle.LoadAsset<AudioClip>("DogdayMonsterGurgle5")
            };

            //Audio death
            dogdayAIScript.dieSFX = assetBundle.LoadAsset<AudioClip>("DogdayDie1");

            //Hit Sounds
            dogdayAIScript.hitSounds = new AudioClip[] {
                assetBundle.LoadAsset<AudioClip>("DogdayHit1"),
                assetBundle.LoadAsset<AudioClip>("DogdayHit2"),
                assetBundle.LoadAsset<AudioClip>("DogdayHit3")
            };

            //Attack Sounds
            dogdayAIScript.attackSounds = new AudioClip[] {
                assetBundle.LoadAsset<AudioClip>("DogdayAttack1"),
                assetBundle.LoadAsset<AudioClip>("DogdayAttack2"),
                assetBundle.LoadAsset<AudioClip>("DogdayAttack3")
            };

            //Hit Connect Sounds
            dogdayAIScript.hitConnectSounds = new AudioClip[] {
                assetBundle.LoadAsset<AudioClip>("DogdayHitConnect1"),
                assetBundle.LoadAsset<AudioClip>("DogdayHitConnect2"),
                assetBundle.LoadAsset<AudioClip>("DogdayHitConnect3"),
                assetBundle.LoadAsset<AudioClip>("DogdayHitConnect4")
            };

            TerminalNode dogdayMonsterTerminalNode = assetBundle.LoadAsset<TerminalNode>("Dogday Monster Terminal Node");
            TerminalKeyword dogdayMonsterTerminalKeyword = assetBundle.LoadAsset<TerminalKeyword>("Dogday Monster Terminal Keyword");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(dogdayMonster.enemyPrefab);
            LethalLib.Modules.Enemies.RegisterEnemy(dogdayMonster, dogdayMonsterRarity, chosenRegistrationMethod, dogdayMonsterTerminalNode, dogdayMonsterTerminalKeyword);
        }

        private static void ConfigureAndRegisterBoxyBoo()
        {
            EnemyType boxyBoo = assetBundle.LoadAsset<EnemyType>("Boxy Boo");
            BoxyBooAI boxyBooAIScript = boxyBoo.enemyPrefab.AddComponent<BoxyBooAI>();
            boxyBooAIScript.creatureVoice = boxyBoo.enemyPrefab.GetComponent<AudioSource>();
            boxyBooAIScript.creatureSFX = boxyBoo.enemyPrefab.GetComponent<AudioSource>();
            boxyBooAIScript.enemyBehaviourStates = new EnemyBehaviourState[9];
            boxyBooAIScript.AIIntervalTime = 0.1f;
            boxyBooAIScript.syncMovementSpeed = 0.2f;
            boxyBooAIScript.updatePositionThreshold = 0.2f;
            boxyBooAIScript.exitVentAnimationTime = 1;
            boxyBooAIScript.enemyType = boxyBoo;
            boxyBooAIScript.eye = boxyBoo.enemyPrefab.transform.Find("Eye");
            boxyBooAIScript.agent = boxyBoo.enemyPrefab.GetComponent<NavMeshAgent>();
            boxyBooAIScript.creatureAnimator = boxyBoo.enemyPrefab.transform.Find("BoxyBoo Model").GetComponent<Animator>();
            BoxyBooAnimationEvents boxyAnimEvents = boxyBooAIScript.creatureAnimator.gameObject.AddComponent<BoxyBooAnimationEvents>();
            boxyAnimEvents.scriptReference = boxyBooAIScript;
            
            boxyBooAIScript.crank = FindDeepChild(boxyBoo.enemyPrefab.transform, "box_handle_JNT");
            boxyBooAIScript.leftsideCheck = FindDeepChild(boxyBoo.enemyPrefab.transform, "BoxcheckLeft").transform;
            boxyBooAIScript.rightsideCheck = FindDeepChild(boxyBoo.enemyPrefab.transform, "BoxcheckRight").transform;
            boxyBooAIScript.agent.baseOffset = -0.3f;
            boxyBoo.enemyPrefab.transform.Find("BoxyCollision").GetComponent<EnemyAICollisionDetect>().mainScript = boxyBooAIScript;

            //Handle interaction trigger.
            boxyBooAIScript.interactTrigger = boxyBooAIScript.crank.Find("Handle_Trigger").GetComponent<InteractTrigger>();
            boxyBooAIScript.interactTrigger.playersManager = StartOfRound.Instance;
            boxyBooAIScript.interactTrigger.onInteract.AddListener(boxyBooAIScript.CrankBackwards);

            //Attack COllision
            boxyBooAIScript.attackArea = FindDeepChild(boxyBoo.enemyPrefab.transform, "AttackArea").GetComponent<BoxCollider>();

            //Box Collision
            boxyBooAIScript.boxCollision = FindDeepChild(boxyBoo.enemyPrefab.transform, "cog_JNT").GetComponent<BoxCollider>();

            //JumpscarePoint
            boxyBooAIScript.jumpscareAttachPoint = FindDeepChild(boxyBoo.enemyPrefab.transform, "JumpscarePoint");
            boxyBooAIScript.jumpscareCollision = boxyBooAIScript.jumpscareAttachPoint.GetComponent<BoxCollider>();

            //Audio Sources
            boxyBooAIScript.musicAudio = FindDeepChild(boxyBoo.enemyPrefab.transform, "MusicAudio").GetComponent<AudioSource>();
            boxyBooAIScript.grabAudioPoint = FindDeepChild(boxyBoo.enemyPrefab.transform, "GrabAudio").GetComponent<AudioSource>();
            boxyBooAIScript.hitConnectAudio = FindDeepChild(boxyBoo.enemyPrefab.transform, "HitConnectAudio").GetComponent<AudioSource>();
            boxyBooAIScript.crankAudio = FindDeepChild(boxyBoo.enemyPrefab.transform, "CrankAudio").GetComponent<AudioSource>();
            boxyBooAIScript.creakAudio = FindDeepChild(boxyBoo.enemyPrefab.transform, "CreakAudio").GetComponent<AudioSource>();
            boxyBooAIScript.jumpAudio = FindDeepChild(boxyBoo.enemyPrefab.transform, "JumpAudio").GetComponent<AudioSource>();
            boxyBooAIScript.retractAudio = FindDeepChild(boxyBoo.enemyPrefab.transform, "RetractAudio").GetComponent<AudioSource>();

            //Audio Clips
            //Walking
            boxyBooAIScript.walkSounds = new AudioClip[3];
            boxyBooAIScript.walkSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyStepHard1");
            boxyBooAIScript.walkSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxyStepHard2");
            boxyBooAIScript.walkSounds[2] = assetBundle.LoadAsset<AudioClip>("BoxyStepHard3");

            //Attacking/swing
            boxyBooAIScript.attackSounds = new AudioClip[2];
            boxyBooAIScript.attackSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxySwing1");
            boxyBooAIScript.attackSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxySwing2");

            //Hit connection
            boxyBooAIScript.hitConnectSounds = new AudioClip[2];
            boxyBooAIScript.hitConnectSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyHitConnect1");
            boxyBooAIScript.hitConnectSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxyHitConnect2");

            //Grab Launch
            boxyBooAIScript.grabLaunchSounds = new AudioClip[4];
            boxyBooAIScript.grabLaunchSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyGrabLaunch1");
            boxyBooAIScript.grabLaunchSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxyGrabLaunch2");
            boxyBooAIScript.grabLaunchSounds[2] = assetBundle.LoadAsset<AudioClip>("BoxyGrabLaunch3");
            boxyBooAIScript.grabLaunchSounds[3] = assetBundle.LoadAsset<AudioClip>("BoxyGrabLaunch4");

            //Grab Ready
            boxyBooAIScript.grabReadySounds = new AudioClip[1];
            boxyBooAIScript.grabReadySounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyGrabReady");

            //Crank
            boxyBooAIScript.crankSounds = new AudioClip[6];
            boxyBooAIScript.crankSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyCrank1");
            boxyBooAIScript.crankSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxyCrank2");
            boxyBooAIScript.crankSounds[2] = assetBundle.LoadAsset<AudioClip>("BoxyCrank3");
            boxyBooAIScript.crankSounds[3] = assetBundle.LoadAsset<AudioClip>("BoxyCrank4");
            boxyBooAIScript.crankSounds[4] = assetBundle.LoadAsset<AudioClip>("BoxyCrank5");
            boxyBooAIScript.crankSounds[5] = assetBundle.LoadAsset<AudioClip>("BoxyCrank6");

            //BoxLanding
            boxyBooAIScript.boxLandSounds = new AudioClip[1];
            boxyBooAIScript.boxLandSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyCrateLand");

            //Jump
            boxyBooAIScript.leapSounds = new AudioClip[1];
            boxyBooAIScript.leapSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyLeap");

            //Land
            boxyBooAIScript.landSounds = new AudioClip[1];
            boxyBooAIScript.leapSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyMetal1");

            //Music
            boxyBooAIScript.musicTrackSounds = new AudioClip[2];
            boxyBooAIScript.musicTrackSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxySongSlow");
            boxyBooAIScript.musicTrackSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxySongFast");

            //Creak
            boxyBooAIScript.creakSounds = new AudioClip[9];
            boxyBooAIScript.creakSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyCreak1");
            boxyBooAIScript.creakSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxyCreak2");
            boxyBooAIScript.creakSounds[2] = assetBundle.LoadAsset<AudioClip>("BoxyCreak3");
            boxyBooAIScript.creakSounds[3] = assetBundle.LoadAsset<AudioClip>("BoxyCreak4");
            boxyBooAIScript.creakSounds[4] = assetBundle.LoadAsset<AudioClip>("BoxyCreak5");
            boxyBooAIScript.creakSounds[5] = assetBundle.LoadAsset<AudioClip>("BoxyCreak6");
            boxyBooAIScript.creakSounds[6] = assetBundle.LoadAsset<AudioClip>("BoxyCreak7");
            boxyBooAIScript.creakSounds[7] = assetBundle.LoadAsset<AudioClip>("BoxyCreak8");
            boxyBooAIScript.creakSounds[8] = assetBundle.LoadAsset<AudioClip>("BoxyCreak9");

            //PopOpen
            boxyBooAIScript.popSounds = new AudioClip[1];
            boxyBooAIScript.popSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyFullPop1");

            //Jumpsacre
            boxyBooAIScript.jumpscareSounds = new AudioClip[1];
            boxyBooAIScript.jumpscareSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyJumpscare");

            //Partial Retract Sounds
            boxyBooAIScript.partialRetractSounds = new AudioClip[1];
            boxyBooAIScript.partialRetractSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyRetractPartial");

            //Full Retract sound
            boxyBooAIScript.fullRetractSounds = new AudioClip[1];
            boxyBooAIScript.fullRetractSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyRetractFull");

            //Hit Connect Slash
            boxyBooAIScript.hitConnectSlashSounds = new AudioClip[3];
            boxyBooAIScript.hitConnectSlashSounds[0] = assetBundle.LoadAsset<AudioClip>("BoxyHitConnectSlash1");
            boxyBooAIScript.hitConnectSlashSounds[1] = assetBundle.LoadAsset<AudioClip>("BoxyHitConnectSlash2");
            boxyBooAIScript.hitConnectSlashSounds[2] = assetBundle.LoadAsset<AudioClip>("BoxyHitConnectSlash3");

            TerminalNode boxyBooTerminalNode = assetBundle.LoadAsset<TerminalNode>("Boxy Terminal Node");
            TerminalKeyword boxyBooTerminalKeyword = assetBundle.LoadAsset<TerminalKeyword>("Boxy Terminal Keyword");
            LethalLib.Modules.Utilities.FixMixerGroups(boxyBoo.enemyPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(boxyBoo.enemyPrefab);
            LethalLib.Modules.Enemies.RegisterEnemy(boxyBoo, boxyBooMonsterRarity, chosenRegistrationMethod, boxyBooTerminalNode, boxyBooTerminalKeyword);
        }

        private static void LoadAssets()
        {
            string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lethalplaytime");
            assetBundle = AssetBundle.LoadFromFile(text);
        }

        public static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;
                Transform result = FindDeepChild(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
