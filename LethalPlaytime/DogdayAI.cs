using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Netcode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using static Unity.Netcode.NetworkManager;

namespace LethalPlaytime
{
    public class DogdayAI : EnemyAI
    {
        public Transform dogdayModel;

        private readonly float debugSpeedCheck = 6.0f;
        private float debugSpeed = 6.0f;

        //Speeds and animation speed.
        private readonly float animationSpeedNormal = 1f;
        private readonly float animationSpeedChase = 1.7f;
        private readonly float agentSpeedNormal = 2.5f;
        private readonly float agentSpeedChase = 5.5f;

        private System.Random rng;

        public AISearchRoutine dogdaySearch;
        public PlayerControllerB playerLooking = null;
        public bool attacking = false;
        public bool dying = false;
        public bool changeInAnimationStateNeeded = false;
        public enum dogdayStates {Searching, Chasing, Enrage, HuntingSound};

        //Audio detection variables.
        private readonly float hearNoiseCooldown = 0.1f;
        private float currentHearNoiseCooldown = 0;
        private Vector3 lastHeardAudioPosition;

        public readonly float timeBetweenAttacks = 1.4f;
        public float attackCooldown = 0;
        public BoxCollider attackArea;
        private bool pastAttackDamageArea = false;

        //AudioChaseStateVariables
        private readonly float maxHuntAudioTime = 45f;
        private float huntAudioTime = 0f;

        //Gurgle
        private readonly float minGurgleCooldown = 3.0f;
        private readonly float maxnGurgleCooldown = 5.0f;
        private float gurgleCooldown = 5;

        //Audio Sources
        public AudioSource walkAudio;
        public AudioSource staticSoundSource;
        public AudioSource hitConnectAudio;

        //Audio sounds
        public AudioClip lastGurgle;
        public AudioClip[] gurrgleSounds;
        public AudioClip[] deathSounds;
        public AudioClip[] walkSounds;
        public AudioClip[] staticSounds;
        public AudioClip[] attackSounds;
        public AudioClip[] hitSounds;
        public AudioClip[] hitConnectSounds;

        public override void Start()
        {
            base.Start();
            rng = new System.Random(StartOfRound.Instance.randomMapSeed);
        }
        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (StartOfRound.Instance.allPlayersDead || isEnemyDead)
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
                case (int)dogdayStates.Searching:
                    PlayerControllerB potentialTargetPlayer = CheckLineOfSightForClosestPlayer(90, 23);
                    if (potentialTargetPlayer != null && potentialTargetPlayer.isInsideFactory && !potentialTargetPlayer.isPlayerDead && !potentialTargetPlayer.inAnimationWithEnemy)
                    {
                        targetPlayer = potentialTargetPlayer;
                        SwitchToBehaviourState((int)dogdayStates.Chasing);
                        DogdaySendStringClientRcp("SwitchToChasing");
                        return;
                    }
                    if (!dogdaySearch.inProgress)
                    {
                        StartSearch(transform.position, dogdaySearch);
                    }
                    break;
                case (int)dogdayStates.Chasing:
                    if (dogdaySearch.inProgress)
                    {
                        StopSearch(dogdaySearch);
                    }
                    if (targetPlayer != null && !targetPlayer.isPlayerDead && targetPlayer.isInsideFactory && !targetPlayer.inAnimationWithEnemy)
                    {
                        float distanceToCurrentTargetPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
                        PlayerControllerB newPotentialTarget = CheckLineOfSightForClosestPlayer(150, 23);
                        if (newPotentialTarget != targetPlayer && newPotentialTarget != null && Vector3.Distance(transform.position, newPotentialTarget.transform.position) < distanceToCurrentTargetPlayer)
                        {
                            TargetClosestPlayer(5, false);
                            targetPlayer = newPotentialTarget;
                        }
                        SetDestinationToPosition(targetPlayer.transform.position);
                        float distanceToFinalTargetPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
                        if (distanceToFinalTargetPlayer < 1.35f && !attacking && attackCooldown <= 0)
                        {
                            DogdaySendStringClientRcp("Attacking");
                        }
                        bool lineOfSightToFinalTarget = !Physics.Linecast(eye.transform.position, targetPlayer.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                        if (lineOfSightToFinalTarget)
                        {
                            if (distanceToFinalTargetPlayer > 26)
                            {
                                targetPlayer = null;
                            }
                        }
                        else if (distanceToFinalTargetPlayer > 17)
                        {
                            targetPlayer = null;
                        }
                    }
                    else //No valid target player.
                    {
                        PlayerControllerB newPotentialTarget = CheckLineOfSightForClosestPlayer(180, 23);
                        if (newPotentialTarget != null)
                        {
                            targetPlayer = newPotentialTarget;
                        }
                        else
                        {
                            targetPlayer = null;
                            SwitchToBehaviourState((int)dogdayStates.Searching);
                            DogdaySendStringClientRcp("SwitchToSearching");
                        }
                    }
                    

                    break;
                case (int)dogdayStates.Enrage:

                    break;
                case (int)dogdayStates.HuntingSound:
                    if (dogdaySearch.inProgress)
                    {
                        StopSearch(dogdaySearch);
                    }
                    if (destination != lastHeardAudioPosition)
                    {
                        SetDestinationToPosition(lastHeardAudioPosition, true);
                    }
                    if (CheckLineOfSightForPlayer(120, 22) != null)
                    {
                        SwitchToBehaviourState((int)dogdayStates.Chasing);
                        DogdaySendStringClientRcp("SwitchToChasing");
                    }
                    if (Vector3.Distance(transform.position, lastHeardAudioPosition) < 1.7)
                    {
                        SwitchToBehaviourState((int)(dogdayStates.Searching));
                        DogdaySendStringClientRcp("SwitchToSearching");
                    }
                    break;
            }
        }
        public override void Update()
        {
            base.Update();
            if (isEnemyDead)
            {
                return;
            }
            if (!dying && gurgleCooldown > 0)
            {
                gurgleCooldown -= Time.deltaTime;
                if (gurgleCooldown <= 0)
                {
                    gurgleCooldown = ((float)rng.NextDouble() * 2.0f) + minGurgleCooldown;
                    PlayRandomGurgleSound();
                }
            }
            /*            if (debugSpeed > 0)
                        {
                            debugSpeed -= Time.deltaTime;
                            if (debugSpeed <= 0)
                            {
                                debugSpeed = debugSpeedCheck;
                                Debug.Log("Dogday agent speed: " + agent.speed + "\nDogday animator speed: " + creatureAnimator.speed);
                            }
                        }*/
            if (changeInAnimationStateNeeded)
            {
                UpdateAnimationState();
            }
            if (currentHearNoiseCooldown > 0)
            {
                currentHearNoiseCooldown -= Time.deltaTime;
            }
            if (attackCooldown > 0)
            {
                attackCooldown -= Time.deltaTime;
            }
            if (targetPlayer != null && attacking && targetPlayer.isInsideFactory && !targetPlayer.isPlayerDead)
            {
                Vector3 diff = targetPlayer.transform.position - this.transform.position;
                diff.y = 0;
                Quaternion targetLook = Quaternion.LookRotation(diff);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetLook, Time.deltaTime * 3f);
                float yDiff = transform.position.y - targetPlayer.transform.position.y;
                float verticalAngle = 10;
                if (yDiff > 0.1)
                {
                    verticalAngle = -25f;
                }
                else if (yDiff < 0)
                {
                    verticalAngle = 25f;
                }
                //verticalAngle = targetPlayer.transform.position.y > transform.position.y ? 25f : -25f;

                // Create the pitch adjustment quaternion
                Quaternion pitchAdjustment = Quaternion.Euler(verticalAngle, dogdayModel.localRotation.eulerAngles.y, dogdayModel.localRotation.eulerAngles.z);

                // Apply the pitch adjustment to the model
                dogdayModel.localRotation = Quaternion.Slerp(dogdayModel.localRotation, pitchAdjustment, Time.deltaTime * 9f);
            }
            else if (dogdayModel.localRotation != Quaternion.identity)
            {
                dogdayModel.localRotation = Quaternion.Slerp(dogdayModel.localRotation, Quaternion.identity, Time.deltaTime * 9f);
            }
            if (currentBehaviourStateIndex == (int)dogdayStates.HuntingSound)
            {
                huntAudioTime += Time.deltaTime;
                if (huntAudioTime > maxHuntAudioTime)
                {
                    huntAudioTime = 0;
                    SwitchToBehaviourState((int)dogdayStates.Searching);
                    DogdaySendStringClientRcp("SwitchToSearching");
                }
            }
        }

        public void ResetAfterAttack()
        {
            attacking = false;
            
            if (currentBehaviourStateIndex == (int)dogdayStates.Chasing)
            {
                creatureAnimator.speed = animationSpeedChase;
                agent.speed = agentSpeedChase;
            }
            else
            {
                agent.speed = agentSpeedNormal;
                creatureAnimator.speed = animationSpeedNormal;
            }
            
            
        }

        private void UpdateAnimationState()
        {
            if (IsHost || IsServer)
            {
                //Logic to send state to playrs
            }
        }

        private PlayerControllerB GetClosestPlayerFixed(Vector3 toThisPosition)
        {
            PlayerControllerB closestPlayer = null;
            float distanceOfClosestPlayerSoFar = 10000f;
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].isPlayerDead || !StartOfRound.Instance.allPlayerScripts[i].isInsideFactory || StartOfRound.Instance.allPlayerScripts[i].inSpecialInteractAnimation)
                {
                    continue;
                }
                float playerDistanceToPosition = Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].transform.position, toThisPosition);
                if (playerDistanceToPosition < distanceOfClosestPlayerSoFar)
                {
                    closestPlayer = StartOfRound.Instance.allPlayerScripts[i];
                    distanceOfClosestPlayerSoFar = playerDistanceToPosition;
                }
            }
            return closestPlayer;
        }

        private bool GetAnyPlayerLooking(Vector3 atPosition, int range = 60)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].isPlayerDead || !StartOfRound.Instance.allPlayerScripts[i].isInsideFactory || StartOfRound.Instance.allPlayerScripts[i].inSpecialInteractAnimation)
                {
                    continue;
                }
                if (StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(atPosition, 90, range))
                {
                    playerLooking = StartOfRound.Instance.allPlayerScripts[i];
                    return true;
                }
            }
            playerLooking = null;
            return false;
        }



        public override void ReceiveLoudNoiseBlast(Vector3 position, float angle)
        {
            base.ReceiveLoudNoiseBlast(position, angle);
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot = 0, int noiseID = 0)
        {
            if (!IsOwner)
            {
                return;
            }
            base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
            PlayerControllerB closestPlayerToSound = GetClosestPlayerFixed(noisePosition);
            if (closestPlayerToSound == null || Vector3.Distance(closestPlayerToSound.transform.position, noisePosition) > 12)
            {
                //Debug.Log("Sound Failed PlayerDistance Check.");
                return;
            }
            float distanceToSound = Vector3.Distance(transform.position, noisePosition);
            if (distanceToSound > 12 || distanceToSound < 1)
            {
                //Debug.Log("Sound Failed DistanceToSound Check.");
                return;
            }
            if (currentBehaviourStateIndex != (int)dogdayStates.Searching && currentBehaviourStateIndex != (int)dogdayStates.HuntingSound)
            {
                return;
            }
            if (stunNormalizedTimer > 0f || currentHearNoiseCooldown > 0f || timesNoisePlayedInOneSpot > 15)
            {
                return;
            }
            currentHearNoiseCooldown = 0.25f;
            //Debug.Log($"Dogday '{base.gameObject.name}': Heard noise! Distance: {distanceToSound} meters");
            float num2 = 18f * noiseLoudness;
            if (Physics.Linecast(base.transform.position, noisePosition, 256))
            {
                noiseLoudness /= 2f;
            }
            if (noiseLoudness < 0.25f)
            {
                return;
            }
            lastHeardAudioPosition = noisePosition;
            if (currentBehaviourStateIndex != (int)dogdayStates.HuntingSound)
            {
                SwitchToBehaviourState((int)dogdayStates.HuntingSound);  
            }
            SetDestinationToPosition(lastHeardAudioPosition, true);
        }

        public void PlayRandomGurgleSound()
        {
            if (creatureSFX != null && gurrgleSounds != null)
            {
                AudioClip[] chosenClip = { gurrgleSounds[rng.Next(gurrgleSounds.Length)] };
                while (chosenClip[0] == lastGurgle) 
                {
                    chosenClip[0] = gurrgleSounds[rng.Next(gurrgleSounds.Length)];
                }
                lastGurgle = chosenClip[0];
                RoundManager.PlayRandomClip(creatureSFX, chosenClip, true, 3);
            }
        }

        public void PlayRandomDeathSound()
        {
            if (creatureVoice != null && deathSounds != null)
            {
                creatureVoice.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(creatureVoice, deathSounds, true, 1.5f);
            }
        }

        public void PlayRandomWalkSound()
        {
            if (walkSounds != null && walkAudio != null)
            {
                walkAudio.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(walkAudio, walkSounds, true, 1);
            }
        }

        public void PlayRandomHitSound()
        {
            if (hitSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, hitSounds, true, 1);
            }
        }

        public void PlayRandomAttackSound()
        {
            if (attackSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, attackSounds, true, 1);
            }
        }

        public void PlayRandomHitConnectSound()
        {
            Debug.Log("Played Hit Connection Sound");
            if (hitConnectSounds != null && hitConnectAudio != null)
            {
                RoundManager.PlayRandomClip(hitConnectAudio, hitConnectSounds, true, 3);
            }
        }

        public void ResetFlinchLayer()
        {
            Debug.Log("Reset Flinch Layer");
            creatureAnimator.SetLayerWeight(1, 0);
            creatureAnimator.SetLayerWeight(0, 1f);
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (isEnemyDead)
            {
                return;
            }
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            enemyHP -= force;
            if ((IsOwner || IsServer || IsClient) && enemyHP <= 0)
            {
                changeInAnimationStateNeeded = true;
                KillEnemyOnOwnerClient();
                staticSoundSource.Stop();
                //DogdaySendStringClientRcp("Dying");
                return;
            }
            DogdaySendStringClientRcp("Flinchy");
            if (IsClient)
            {
                //Debug.Log("Dogday Should Flinch: " + isEnemyDead);
                if (!isEnemyDead)
                {
                    creatureAnimator.SetLayerWeight(1, 1f);
                    creatureAnimator.SetLayerWeight(0, 0f);
                    creatureAnimator.SetTrigger("Flinch");
                    if (attackArea != null && attacking && !pastAttackDamageArea)
                    {
                        creatureAnimator.Play("DogdayMonsterAttack", 0, 0f);
                        //Debug.Log("ResetAttack");
                    }
                }
                else
                {
                    creatureAnimator.SetLayerWeight(1, 0);
                }
            }
        }

        public override void KillEnemy(bool destroy = false)
        {
            staticSoundSource.Stop();
            base.KillEnemy(destroy);
        }

        public void CheckAttackCollision()
        {
            gameObject.GetComponent<BoxCollider>().isTrigger = true;
            if (attackArea != null && !StartOfRound.Instance.localPlayerController.isPlayerDead)
            {
                if (StartOfRound.Instance.localPlayerController.GetComponent<BoxCollider>().bounds.Intersects(attackArea.bounds))
                {
                    PlayRandomHitConnectSound();
                    StartOfRound.Instance.localPlayerController.DamagePlayer(25, false, true, CauseOfDeath.Mauling);
                    StartOfRound.Instance.localPlayerController.JumpToFearLevel(0.8f, false);
                    StartOfRound.Instance.localPlayerController.DropBlood();
                    
                }
            }
        }

        private void InterpretRpcString(string rpcString)
        {
            switch(rpcString)
            {
                case "SwitchToChasing":
                    agent.speed = agentSpeedChase;
                    creatureAnimator.speed = animationSpeedChase;
                    break;
                case "SwitchToSearching":
                    creatureAnimator.speed = animationSpeedNormal;
                    agent.speed = agentSpeedNormal;
                    break;
                case "Attacking":
                    attacking = true;
                    attackCooldown = timeBetweenAttacks;
                    agent.speed = 0;
                    creatureAnimator.speed = animationSpeedNormal;
                    creatureAnimator.SetTrigger("Attacking");
                    break;
                case "Flinchy":
                    //Debug.Log("Dogday Should Flinch: " + isEnemyDead);
                    if (!isEnemyDead)
                    {
                        creatureAnimator.SetLayerWeight(1, 1f);
                        creatureAnimator.SetLayerWeight(0, 0f);
                        creatureAnimator.SetTrigger("Flinch");
                        if (attackArea != null && attacking && !pastAttackDamageArea)
                        {
                            creatureAnimator.Play("DogdayMonsterAttack", 0, 0f);
                            //Debug.Log("ResetAttack");
                        }
                    }
                    else
                    {
                        creatureAnimator.SetLayerWeight(1, 0);
                    }
                    break;
                case "Dying":
                    Debug.Log("Killed Dogday :(");
                    KillEnemy();
                    staticSoundSource.Stop();
                    break;
            }
        }

        [ClientRpc]
        private void DogdaySendStringClientRcp(string informationString)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams rpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(4193916205u, rpcParams, 0);
                bool flag = informationString != null;
                bufferWriter.WriteValueSafe(flag, default);
                if (flag)
                {
                    bufferWriter.WriteValueSafe(informationString, false);
                }
                __endSendClientRpc(ref bufferWriter, 4193916205u, rpcParams, 0);
            }
            if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient || networkManager.IsHost))
            {
                InterpretRpcString(informationString);
                if (!IsServer && !IsHost)
                {
                    
                }
            }
        }

        //Send String information
        private static void __rpc_handler_4193916205(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
                ((DogdayAI)target).DogdaySendStringClientRcp(valueAsString);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }

        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_DogdayAI()
        {
            __rpc_func_table.Add(4193916205, new RpcReceiveHandler(__rpc_handler_4193916205)); //Sendinfo
        }
    }
}
