using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Unity.Netcode.FastBufferWriter;
using static Unity.Netcode.NetworkManager;

namespace LethalPlaytime
{
    public class MissDelightAI : EnemyAI
    {
        public AISearchRoutine searchForPlayers;

        public bool deadlyWeapon = false;

        public float minStopInterval = 0.15f;

        public float currentStopInterval = 0f;

        public float timeSinceLookedAt = 10f;

        public float reservedAgentMovementSpeed = 2.5f; //Base starting speed

        public float reservedAgentAnimationSpeed = 1.0f;

        public float agentSpeedIncrement = 0.75f;

        public float agentAnimationSpeedIncrement = 0.25f;

        public float timeToLaugh = 12f;

        public double timeExisting = 0;

        public double timeToIncrement = 55;

        public int timeToLaughMax = 15;

        public int timeToLaughMin = 8;

        public int swingCount = 0;

        public int maxSwingSpeedBoost = 5;

        public bool canSendLookCheck = true;

        public bool canBeFrozen = true;

        public bool isFrozen = false;

        public bool doorTrigger = false;

        public SphereCollider weaponCollider;

        private System.Random rngGenerator;

        public AudioSource weaponAudioPoint;

        public AudioClip[] weaponImpactClips;

        public AudioClip[] footstepClips;

        public AudioClip[] weaponSwingVoiceClips;

        public AudioClip[] weaponSwingClip;

        public AudioClip[] laughClips;

        public AudioClip[] growlClips;

        public AudioClip[] doorSmashClips;

        //Unfreeze update:
        private readonly float maxStareMeter = 20;

        public float stareMeter = 0;

        private readonly float stareMeterReductionMultiplier = 2f;

        public bool overrideFreeze = false;

        private readonly float stareMeterMinRngThreshold = 6.5f;
        private readonly float stareMeterMaxRngThreshold = 25f;

        private enum BehaviorState
        {
            Searching,
            Chasing,
            Swinging,
            Doorbusting
        }

        public override void Start()
        {
            base.Start();
            rngGenerator = new System.Random(StartOfRound.Instance.randomMapSeed + 1337);
        }
        public override void DoAIInterval()
        {
            base.DoAIInterval(); //Required
                                 //Don't perform anything if everyone is dead already.
            if (StartOfRound.Instance.allPlayersDead)
            {
                return;
            }
            //Change ownership if needed.
            if (!base.IsServer)
            {
                ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
            }
            switch (currentBehaviourStateIndex)
            {
                //Searching State
                case (int)BehaviorState.Searching:
                    for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                    {
                        if (StartOfRound.Instance.allPlayerScripts[i].isPlayerDead || !StartOfRound.Instance.allPlayerScripts[i].isInsideFactory)
                        {
                            continue;
                        }
                        if (PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) && !Physics.Linecast(base.transform.position + Vector3.up * 0.5f, StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault) && Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position) < 30f)
                        {
                            TargetClosestPlayer();
                            movingTowardsTargetPlayer = true;
                            SwitchToBehaviourState(1);
                            return;
                        }
                    }
                    if (!searchForPlayers.inProgress)
                    {
                        movingTowardsTargetPlayer = false;
                        StartSearch(base.transform.position, searchForPlayers);
                    }
                    break;
                //Targeted player State
                case (int)BehaviorState.Chasing:
                    if (searchForPlayers.inProgress)
                    {
                        StopSearch(searchForPlayers);
                    }
                    if (base.targetPlayer != null)
                    {
                        float distanceToTarget = Vector3.Distance(base.transform.position, targetPlayer.transform.position);
                        if (targetPlayer != null) 
                        {
                            if (IsOwner)
                            {
                                gameObject.GetComponent<NavMeshAgent>().angularSpeed = 900;

                                if (!isFrozen && ((distanceToTarget < 1.85f &&
                                    Vector3.Angle(base.transform.forward, (targetPlayer.transform.position - base.transform.position).normalized) < 9f)
                                    || distanceToTarget < 0.85f)) //If close enough and facing the player enough.
                                {
                                    gameObject.GetComponent<NavMeshAgent>().velocity = Vector3.zero;
                                    movingTowardsTargetPlayer = false;
                                    DoAnimationClientRpc("enterSwing");
                                    return;
                                }
                                else if (distanceToTarget < 2f)
                                {
                                    gameObject.GetComponent<NavMeshAgent>().angularSpeed = 4000;
                                }
                                else
                                {
                                    gameObject.GetComponent<NavMeshAgent>().angularSpeed = 900;
                                }
                            }
                        }

                    }
                    if (targetPlayer != null && !targetPlayer.isPlayerDead)
                    {
                        if (GetClosestPlayer() != targetPlayer)
                        {
                            Debug.Log("Retargeted closer player.");
                            TargetClosestPlayer(30f, true, 270);
                        }
                        if (!movingTowardsTargetPlayer)
                        {
                            movingTowardsTargetPlayer = true;
                        }
                    }
                    else
                    {
                        this.targetPlayer = null;
                        SwitchToBehaviourState(0);
                    }
                    break;
                    //Swinging
                case 2:
                    break;
                case 3:

                    break;
            }
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();  //Required
            if (IsHost && isFrozen && !overrideFreeze)
            {
                if (stareMeter < stareMeterMaxRngThreshold)
                {
                    stareMeter += Time.deltaTime;
                }
                if (stareMeter > stareMeterMinRngThreshold)
                {
                    //Attempt to unbreak freeze.
                    float adjustedFreezeUnbreakChanceFrame = ((stareMeter - stareMeterMinRngThreshold)/(stareMeterMaxRngThreshold - stareMeterMinRngThreshold)) * Time.deltaTime;
                    float randomValue = (float)rngGenerator.NextDouble();
                    if (randomValue <= adjustedFreezeUnbreakChanceFrame)
                    {
                        //Force Unfreeze
                        overrideFreeze = true;
                        TryFreezeDelight(false);
                        PerformAnimationSwitchWithImplications("flickerLights");
                    }
                }
            }
            else
            {
                if (stareMeter > 0)
                {
                    stareMeter -= (Time.deltaTime * (stareMeterReductionMultiplier + (swingCount * 0.2f)));
                }
                if (stareMeter <= 0 && overrideFreeze)
                {
                    overrideFreeze = false;
                }
            }
            if (swingCount < maxSwingSpeedBoost)
            {
                timeExisting += Time.deltaTime;
                if (timeExisting >= timeToIncrement && (IsHost || IsServer))
                {
                    DoAnimationClientRpc("incrementSwing");
                    timeExisting = 0;
                }
            }
            if (isEnemyDead)
            {
                return;
            }
            timeSinceLookedAt += Time.deltaTime;
            if (currentStopInterval > 0)
            {
                currentStopInterval -= Time.deltaTime;
                if (currentStopInterval <= 0) 
                {
                    canSendLookCheck = true;
                }
            }
            if (currentBehaviourStateIndex == 1 || (currentBehaviourStateIndex == 2 && isFrozen))
            {
                timeToLaugh -= Time.deltaTime;
                if (timeToLaugh <= 0)
                {
                    PlayRandomLaughSound();
                    timeToLaugh = rngGenerator.Next(timeToLaughMin, timeToLaughMax);
                }
            }
            if (canBeFrozen && (currentBehaviourStateIndex == 1 || currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 0) && !overrideFreeze)
            {
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                {
                    if (canSendLookCheck)
                    {
                        if (PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) && StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(base.transform.position + Vector3.up * 1.6f, 68f) && Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, eye.position) > 0.3f)
                        {
                            //Debug.Log("Trying to call freeze delight RCP");
                            canSendLookCheck = false;
                            currentStopInterval = minStopInterval;
                            FreezeDelightClientRcp("true");
                        }
                    }
                }
            }
            if (IsOwner && timeSinceLookedAt > 0.25)
            {
                FreezeDelightClientRcp("false");
            }
            if (currentBehaviourStateIndex == 2)
            {
                if (deadlyWeapon && !isFrozen)
                {
                    for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                    {
                        if (!StartOfRound.Instance.allPlayerScripts[i].isInsideFactory || StartOfRound.Instance.allPlayerScripts[i].isPlayerDead)
                        {
                            continue;
                        }
                        BoxCollider playerHitBox = StartOfRound.Instance.allPlayerScripts[i].gameObject.GetComponent<BoxCollider>();
                        Collider[] hitColliders = Physics.OverlapBox(
                            playerHitBox.bounds.center,
                            playerHitBox.bounds.extents,
                            playerHitBox.transform.rotation);
                        foreach (Collider collider in hitColliders)
                        {
                            if (collider == weaponCollider)
                            {
                                StartOfRound.Instance.allPlayerScripts[i].DamagePlayer(200, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, false, new Vector3(0, -600, 0));
                                Debug.Log("Miss delight hit " + StartOfRound.Instance.allPlayerScripts[i].name);
                            }
                        }
                    }
                }
                return;
            }
        }

        public void HandleDoorTrigger()
        {
            if (!doorTrigger)
            {
                if (currentBehaviourStateIndex == 0 || currentBehaviourStateIndex == 1)
                {
                    DoAnimationClientRpc("enterDoor");
                }
            }
        }

        private void PlayRandomWeaponSwing()
        {
            if (weaponSwingClip != null && creatureVoice != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, weaponSwingClip);
            }
            
        }

        private void PlayRandomWeaponSwingVoiceSound()
        {
            if (weaponSwingVoiceClips != null && creatureVoice != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, weaponSwingVoiceClips);
                if (doorTrigger && currentBehaviourStateIndex != 2)
                {
                    PlayDoorSmashOpenSound();
                }
            }
        }

        private void PlayDoorSmashOpenSound()
        {
            if (doorSmashClips != null && creatureVoice != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, doorSmashClips);
            }
        }

        private void PlayRandomWeaponImpactSound()
        {
            if (weaponImpactClips != null && weaponAudioPoint != null)
            {
                RoundManager.PlayRandomClip(weaponAudioPoint, weaponImpactClips);
                ShakeScreenNearbyPlayer(GetDistanceFromPlayer());
            }
        }

        private void ShakeScreenNearbyPlayer(float distance)
        {
            if (distance < 10)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
        }

        private float GetDistanceFromPlayer()
        {
            return Vector3.Distance(this.transform.position, GameNetworkManager.Instance.localPlayerController.transform.position);
        }

        private void PlayRandomFootStepSound()
        {
            if (footstepClips != null && creatureSFX != null)
            {
                RoundManager.PlayRandomClip(creatureSFX, footstepClips);
            }
        }

        private void PlayRandomLaughSound()
        {
            if (!isFrozen)
            {
                if (laughClips != null && creatureSFX != null)
                {
                    RoundManager.PlayRandomClip(creatureSFX, laughClips);
                }
            }
            else
            {
                int chosen = rngGenerator.Next(2);
                if (chosen == 0)
                {
                    RoundManager.PlayRandomClip(creatureSFX, laughClips);
                }
                else if (chosen == 1)
                {
                    RoundManager.PlayRandomClip(creatureSFX, growlClips);
                }
            }
        }

        private void PlayRandomGrowlSound()
        {
            if (growlClips != null && creatureSFX != null)
            {
                RoundManager.PlayRandomClip(creatureSFX, growlClips);
            }
        }

        [ClientRpc]
        private void DoAnimationClientRpc(string animationName)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams rpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(1947720646u, rpcParams, 0);
                bool flag = animationName != null;
                bufferWriter.WriteValueSafe(flag, default);
                if (flag)
                {
                    bufferWriter.WriteValueSafe(animationName, false);
                }
                __endSendClientRpc(ref bufferWriter, 1947720646u, rpcParams, 0);
            }
            if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient || networkManager.IsHost))
            {
                PerformAnimationSwitchWithImplications(animationName);
            }
        }


        [ClientRpc]
        private void ChangeWeaponDeadlyStateClientRpc(string valueToSetAsString)
        {
            {
                NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
                if (networkManager == null || !networkManager.IsListening)
                {
                    return;
                }
                if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
                {
                    ClientRpcParams rpcParams = default(ClientRpcParams);
                    FastBufferWriter bufferWriter = __beginSendClientRpc(0947720646u, rpcParams, 0);
                    bool flag = valueToSetAsString != null;
                    bufferWriter.WriteValueSafe(flag, default);
                    if (flag)
                    {
                        bufferWriter.WriteValueSafe(valueToSetAsString, false);
                    }
                    __endSendClientRpc(ref bufferWriter, 0947720646u, rpcParams, 0);
                }
                if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient || networkManager.IsHost))
                {
                    bool valueAsBool = bool.Parse(valueToSetAsString);
                    ChangeWeaponDeadlyState(valueAsBool);
                }
            }
        }

        [ClientRpc]
        private void FreezeDelightClientRcp(string freezeStateString)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams rpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(2947720646u, rpcParams, 0);
                bool flag = freezeStateString != null;
                bufferWriter.WriteValueSafe(flag, default);
                if (flag)
                {
                    bufferWriter.WriteValueSafe(freezeStateString, false);
                }
                __endSendClientRpc(ref bufferWriter, 2947720646u, rpcParams, 0);
            }
            if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient || networkManager.IsHost))
            {
                TryFreezeDelight(Boolean.Parse(freezeStateString));
            }
        }

        private void ChangeWeaponDeadlyState(bool valueAsBool)
        {
            this.deadlyWeapon = valueAsBool;
        }

        private void ScareClosePlayer(float distance, float fearLevel)
        {
            float distanceToTarget = Vector3.Distance(base.transform.position, GameNetworkManager.Instance.localPlayerController.transform.position);
            if (distanceToTarget < distance)
            {
                GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(fearLevel);
            }
        }

        private void PerformAnimationSwitchWithImplications(string animationName)
        {
            switch (animationName)
            {
                case "enterSwing":
                    creatureAnimator.SetTrigger(animationName);
                    SwitchToBehaviourState(2);
                    reservedAgentMovementSpeed = agent.speed;
                    agent.speed = 0;
                    ScareClosePlayer(2.5f, 0.7f);
                    break;
                case "exitSwing":
                    creatureAnimator.SetTrigger(animationName);
                    SwitchToBehaviourState(1);
                    if (swingCount < maxSwingSpeedBoost)
                    {
                        swingCount++;
                    }
                    agent.speed = 2.5f + (swingCount * agentSpeedIncrement);
                    creatureAnimator.speed = 1.0f + (swingCount * agentAnimationSpeedIncrement);
                    break;
                case "enterDoor":
                    SwitchToBehaviourState(3);
                    creatureAnimator.SetTrigger(animationName);
                    openDoorSpeedMultiplier = 3;
                    agent.speed = 0;
                    creatureAnimator.speed = 1.0f + (swingCount * agentAnimationSpeedIncrement);
                    if (isFrozen)
                    {
                        isFrozen = false;
                    }
                    canBeFrozen = false;
                    break;
                case "exitDoor":
                    agent.speed = 1.0f + (swingCount * agentSpeedIncrement);
                    creatureAnimator.SetTrigger(animationName);
                    openDoorSpeedMultiplier = 1;
                    SwitchToBehaviourState(1);
                    canBeFrozen = true;
                    doorTrigger = false;
                    break;
                case "incrementSwing":
                    if (swingCount < maxSwingSpeedBoost) 
                    {
                        swingCount += 1;
                        if (!isFrozen && agent.speed != 0)
                        {
                            agent.speed = 2.5f + (swingCount * agentSpeedIncrement);
                            creatureAnimator.speed = 1.0f + (swingCount * agentAnimationSpeedIncrement);
                        }
                    }
                    break;
                case "flickerLights":
                    fastFlickerLights();
                    break;
            }
        }

        private void fastFlickerLights()
        {
            if (Vector3.Distance(transform.position, StartOfRound.Instance.localPlayerController.transform.position) < 20f)
            {
                RoundManager.Instance.FlickerLights(flickerFlashlights: true, disableFlashlights: false);
                GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.3f);
            }
        }

        private void TryFreezeDelight(bool freezeRequest)
        {
            if (freezeRequest && isFrozen)
            {
                timeSinceLookedAt = 0;
                return;
            }
            else if (freezeRequest && !isFrozen && !agent.isOnOffMeshLink)
            {
                timeSinceLookedAt = 0;
                isFrozen = true;
                reservedAgentMovementSpeed = agent.speed;
                reservedAgentAnimationSpeed = creatureAnimator.speed;
                agent.speed = 0;
                creatureAnimator.speed = 0;
                
                return;
            }
            else if (!freezeRequest && isFrozen)
            {
                agent.speed = reservedAgentMovementSpeed;
                creatureAnimator.speed = reservedAgentAnimationSpeed; //Was 1.0 originally
                isFrozen = false;
            }
        }

        //Set animation on Miss Delight
        private static void __rpc_handler_1947720646(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager != null && networkManager.IsListening)
            {
                bool flag = default(bool);
                reader.ReadValueSafe(out flag, default);
                string animationName = null;
                if (flag)
                {
                    reader.ReadValueSafe(out animationName, false);
                }
                target.__rpc_exec_stage = (__RpcExecStage)2;
                ((MissDelightAI)target).DoAnimationClientRpc(animationName);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }
        //Miss Delight Weapon swing
        private static void __rpc_handler_weaponswing_0947720646(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager != null && networkManager.IsListening)
            {
                bool flag = default(bool);
                reader.ReadValueSafe(out flag, default);
                string valueAsString = null;
                if (flag)
                {
                    reader.ReadValueSafe(out valueAsString, false);
                }
                target.__rpc_exec_stage = (__RpcExecStage)2;
                ((MissDelightAI)target).ChangeWeaponDeadlyStateClientRpc(valueAsString);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }

        //Freezing Miss Delight
        private static void __rpc_handler_freezeDelight_2947720646(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager != null && networkManager.IsListening)
            {
                bool flag = default(bool);
                reader.ReadValueSafe(out flag, default);
                string valueAsString = null;
                if (flag)
                {
                    reader.ReadValueSafe(out valueAsString, false);
                }
                target.__rpc_exec_stage = (__RpcExecStage)2;
                ((MissDelightAI)target).FreezeDelightClientRcp(valueAsString);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }

        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_MissDelight()
        {
            __rpc_func_table.Add(1947720646, new RpcReceiveHandler(__rpc_handler_1947720646)); //SetAnimation
            __rpc_func_table.Add(0947720646, new RpcReceiveHandler(__rpc_handler_weaponswing_0947720646)); //Weaponswing synchronized change state to deadly for all.
            __rpc_func_table.Add(2947720646, new RpcReceiveHandler(__rpc_handler_freezeDelight_2947720646)); //Freeze MissDelight
        }
    }
}
