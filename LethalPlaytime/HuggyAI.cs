using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using static Unity.Netcode.NetworkManager;

namespace LethalPlaytime
{
    public class HuggyAI : EnemyAI
    {
        //States
        private enum HuggyStates {Wandering, Creeping, RegularChase, SitRecovery, Enrage, Retreat};
        private HuggyStates beforeRetreatState = HuggyStates.Creeping;

        //Standard
        private System.Random rng;
        private AISearchRoutine huggySearch;

        //Stamina
        private readonly float maxStamina = 20;
        public float currentStamina = 20;

        //Wandering time.
        private readonly float maxWanderingTime = 19;
        public float currentWanderingTime = 0;

        //Frustration meter.
        private readonly float maxFrustration = 3.5f;
        public float currentFrustration = 4;

        //Saved positions.
        Vector3 idealHidingSpotPosition;
        Vector3 idealRetreatSpotPosition;
        Vector3 mainEntrancePosition;
        Vector3 hidingSpotPosition;

        private readonly float chasingSpeed;
        private readonly float retreatingSpeed;


        //Animation state internal tracking, applied on update to character.
        //If there was a change in state, we need to update.
        private bool changeInAnimationStateNeeded = false;
        public bool sitting;
        public bool standing;
        public bool crouching;
        public bool forward;
        public bool running;
        public bool charging;
        public bool attacking;
        public bool jumpscaring;

        //Hiding Variables
        public bool atHidingPosition = false;
        public bool hidingPositionFound = false;
        public bool hidingPositionNoPlayers = true;
        public PlayerControllerB playerLooking = null;

        //Retreat Variables
        public float retreatTime = 12;
        public float currentRetreatTime = 0;
        public bool validRetreatSpot = false;

        //Jumpscare Things
        public bool canJumpscare = false;
        Coroutine jumpscareAnimation;
        Coroutine waitingRoutine;
        public Transform jumpScarePoint;

        //Animation state control and communication.
        public string[] statesArray;
        public string nameOfLastAnimation;

        //Custom Audio Sources
        public AudioSource voiceAudio;
        public AudioSource voiceFarAudio;
        public AudioSource slashAudio;

        //AudioClip arrays
        public AudioClip[] attackSwingClips;
        public AudioClip[] attackVoiceClips;
        public AudioClip[] roarClips;
        public AudioClip[] roarFarClips;
        public AudioClip[] footstepClips;
        public AudioClip[] crouchClips;
        public AudioClip[] sitDownClips;
        public AudioClip[] sitUpClips;
        public AudioClip[] jumpscareClips;
        public AudioClip[] slashClips;

        //Attack Collider Box
        public BoxCollider weaponSwingCheckArea;

        //Sitting Collision Box
        public BoxCollider sitCollisionArea;


        public override void Start()
        {
            base.Start();
            idealRetreatSpotPosition = transform.position;
            idealHidingSpotPosition = transform.position;
            mainEntrancePosition = RoundManager.FindMainEntrancePosition();
            rng = new System.Random(StartOfRound.Instance.randomMapSeed + 5);
            huggySearch = new AISearchRoutine();
            sitting = false;
            standing = true;
            crouching = false;
            forward = true;
            running = false;
            attacking = false;
            charging = false;
            jumpscaring = false;
            if (!IsHost || !IsServer)
            {
                ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
            }
            
            if (IsHost || IsServer)
            {
                nameOfLastAnimation = creatureAnimator.GetCurrentStateName(0);
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            switch (currentBehaviourStateIndex)
            {
                case (int)HuggyStates.Wandering:
                    if (!huggySearch.inProgress)
                    {
                        forward = true;
                        changeInAnimationStateNeeded = true;
                        StartSearch(transform.position, huggySearch);
                    }
                    if (currentWanderingTime > maxWanderingTime)
                    {
                        SwitchToCreeping();
                        SwitchToBehaviourState((int)HuggyStates.Creeping);
                        return;
                    }
                    PlayerControllerB potentialTarget = CheckLineOfSightForClosestPlayer(150, 23);
                    if (potentialTarget != null)
                    {
                        ScareClientAmount(potentialTarget.actualClientId, 0.65f);
                        SwitchToRegularChasing();
                        SwitchToBehaviourState((int)HuggyStates.RegularChase);
                        return;
                    }
                    break;
                case (int)HuggyStates.Creeping:
                    if (!atHidingPosition)
                    {
                        SetDestinationToPosition(hidingSpotPosition, true);
                    }
                    else //At hiding spot
                    {
                        PlayerControllerB closestPlayer = GetClosestPlayerFixed(transform.position);
                        if (closestPlayer != null)
                        {
                            Vector3 diff = closestPlayer.transform.position - this.transform.position;
                            diff.y = 0;
                            transform.rotation = Quaternion.LookRotation(diff);
                        }
                    }
                    if (!atHidingPosition && Vector3.Distance(transform.position, hidingSpotPosition) < 1f)
                    {
                        atHidingPosition = true;
                        forward = false;
                        changeInAnimationStateNeeded = true;
                        agent.velocity = Vector3.zero;
                        agent.avoidancePriority = 51;
                        PlayerControllerB closestPlayer = GetClosestPlayerFixed(transform.position);
                        if (closestPlayer != null)
                        {
                            Vector3 diff = closestPlayer.transform.position - this.transform.position;
                            diff.y = 0;
                            transform.rotation = Quaternion.LookRotation(diff);
                        }
                    }
                    RevalidateHidingSpot();
                    break;
                case (int)HuggyStates.RegularChase:
                    if (currentStamina <= 0)
                    {
                        SwitchToSitting();
                        SwitchToBehaviourState((int)HuggyStates.SitRecovery);
                        return;
                    }
                    if (targetPlayer != null && targetPlayer.isInsideFactory && !targetPlayer.isPlayerDead && !targetPlayer.inSpecialInteractAnimation)
                    {
                        float distanceToCurrentTargetPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
                        if (distanceToCurrentTargetPlayer > 25)
                        {
                            targetPlayer = null;
                        }
                        else
                        {
                            SetDestinationToPosition(targetPlayer.transform.position, true);
                        }
                    }

                    PlayerControllerB closestVisiblePlayer = CheckLineOfSightForClosestPlayer(270, 23);
                    if (closestVisiblePlayer != null && !closestVisiblePlayer.isPlayerDead && closestVisiblePlayer.isInsideFactory && currentStamina > 0 && targetPlayer != closestVisiblePlayer) 
                    {
                        targetPlayer = closestVisiblePlayer;
                        ScareClientAmount(targetPlayer.actualClientId, 0.4f);
                        SetDestinationToPosition(targetPlayer.transform.position, true);
                        return; 
                    }
                    else if (currentStamina > 0 && targetPlayer == null)
                    {
                        if (closestVisiblePlayer != null)
                        {
                            TargetClosestPlayer(0);
                        }
                        else
                        {
                            running = false;
                            changeInAnimationStateNeeded = true;
                            SwitchToBehaviourState((int)HuggyStates.Wandering);
                        }
                    }
                    break;
                case (int)HuggyStates.SitRecovery:
                    if (moveTowardsDestination || movingTowardsTargetPlayer)
                    {
                        movingTowardsTargetPlayer = false;
                        moveTowardsDestination = false;
                        targetPlayer = null;
                        destination = transform.position;
                        forward = false;
                    }
                    if (currentStamina >= maxStamina)
                    {
                        sitting = false;
                        standing = true;
                        forward = true;
                        changeInAnimationStateNeeded = true;
                        if (currentFrustration >= maxFrustration)
                        {
                            SwitchToEnrage();
                            SwitchToBehaviourState((int)HuggyStates.Enrage);
                        }
                        else
                        {
                            SwitchToBehaviourState((int)HuggyStates.Wandering);
                        }
                        
                    }
                    break;
                case (int)HuggyStates.Enrage:
                    if (currentStamina <= 0)
                    {
                        if (huggySearch.inProgress)
                        {
                            StopSearch(huggySearch);
                        }
                        SwitchToWandering();
                        SwitchToBehaviourState((int)HuggyStates.Wandering);
                    }
                    if (!charging)
                    {
                        if (!huggySearch.inProgress)
                        {
                            StartSearch(transform.position, huggySearch);
                        }
                        PlayerControllerB potentialChargeTarget = CheckLineOfSightForClosestPlayer(150, 23);
                        if (potentialChargeTarget != null && !potentialChargeTarget.isPlayerDead && potentialChargeTarget.isInsideFactory && !potentialChargeTarget.inSpecialInteractAnimation)
                        {
                            ScareClientAmount(potentialChargeTarget.actualClientId, 0.75f);
                            StopSearch(huggySearch);
                            SwitchToCharging();
                            TargetClosestPlayer(0, true, 360);
                            return;
                        }
                    }
                    else //charging
                    {
                        if (targetPlayer != null && !targetPlayer.inSpecialInteractAnimation && !targetPlayer.isPlayerDead && targetPlayer.isInsideFactory)
                        {
                            if (Vector3.Distance(targetPlayer.transform.position, transform.position) > 27)
                            {
                                PlayerControllerB potentialChargeTarget = CheckLineOfSightForClosestPlayer(360, 24);
                                if (potentialChargeTarget != null && !potentialChargeTarget.isPlayerDead && potentialChargeTarget.isInsideFactory && !potentialChargeTarget.inSpecialInteractAnimation)
                                {
                                    TargetClosestPlayer(0, true, 360);
                                    return;
                                }
                                charging = false;
                                return;
                            }
                            SetDestinationToPosition(targetPlayer.transform.position, true);
                            if (Vector3.Distance(targetPlayer.transform.position, this.transform.position) < 1.4f && canJumpscare)
                            {
                                ScareClientAmount(targetPlayer.actualClientId, 1.0f);
                                SwitchToJumpscare(targetPlayer.actualClientId);
                            }
                        }
                        else
                        {
                            PlayerControllerB potentialChargeTarget = CheckLineOfSightForClosestPlayer(360, 24);
                            if (potentialChargeTarget != null)
                            {
                                TargetClosestPlayer(0, true, 360);
                                return;
                            }
                            else
                            {
                                charging = false;
                            }
                        }
                    }
                    break;
                case (int)HuggyStates.Retreat:
                    if (currentRetreatTime >= retreatTime)
                    {
                        validRetreatSpot = false;
                        if (currentFrustration > maxFrustration)
                        {
                            SwitchToEnrage();
                            SwitchToBehaviourState((int)HuggyStates.Enrage);
                            break;
                        }
                        if (previousBehaviourStateIndex == (int)HuggyStates.Creeping)
                        {
                            SwitchToWandering();
                            SwitchToBehaviourState((int)HuggyStates.Wandering);
                        }
                        else if (previousBehaviourStateIndex == (int)HuggyStates.RegularChase)
                        {
                            SwitchToRegularChasing();
                            SwitchToBehaviourState((int)HuggyStates.RegularChase);
                        }
                        break;
                    }
                    else if (!validRetreatSpot)
                    {
                        idealRetreatSpotPosition = ChooseFarthestNodeFromPosition(GetClosestPlayerFixed(transform.position).transform.position).position;
                        SetDestinationToPosition(ChooseFarthestNodeFromPosition(GetClosestPlayerFixed(transform.position).transform.position).position , true);
                        currentFrustration += 0.65f;
                        validRetreatSpot = true;
                    }
                    else
                    {
                        if (path1.status == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                        {
                            validRetreatSpot = false;
                        }
                    }
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (targetPlayer != null && attacking && targetPlayer.isInsideFactory && !targetPlayer.isPlayerDead)
            {
                Vector3 diff = targetPlayer.transform.position - this.transform.position;
                diff.y = 0;
                Quaternion targetLook = Quaternion.LookRotation(diff);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetLook, Time.deltaTime * 3f);
            }
            if (changeInAnimationStateNeeded)
            {
                UpdateAnimationStates();
            }
            switch (currentBehaviourStateIndex)
            {
                case (int)HuggyStates.Wandering:
                    currentWanderingTime += Time.deltaTime;
                    if (currentStamina < maxStamina)
                    {
                        currentStamina += Time.deltaTime * 0.5f;
                    }
                    if (!forward && IsHost)
                    {
                        forward = true;
                        changeInAnimationStateNeeded = true;
                    }
                    break;
                case (int)HuggyStates.Creeping:
                    break;
                case (int)HuggyStates.RegularChase:
                    currentStamina -= Time.deltaTime;
                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(this.transform.position, targetPlayer.transform.position) < 1.5 && !attacking)
                        {
                            agent.velocity = Vector3.zero;
                            agent.speed = 0;
                            Vector3 diff = targetPlayer.transform.position - this.transform.position;
                            diff.y = 0;
                            transform.rotation = Quaternion.LookRotation(diff);
                            int randomAttackAnimation = rng.Next(2);
                            gameObject.GetComponent<BoxCollider>().isTrigger = false;
                            if (randomAttackAnimation == 0)
                            {
                                HuggySendStringClientRcp("Attacking");
                                creatureAnimator.SetTrigger("Attacking");
                                attacking = true;
                            }
                            else if (randomAttackAnimation == 1)
                            {
                                HuggySendStringClientRcp("AttackingMirror");
                                creatureAnimator.SetTrigger("AttackingMirror");
                                attacking = true;
                            }
                            ScareClientAmount(targetPlayer.actualClientId, 0.4f); //First test
                        }
                    }
                    break;
                case (int)HuggyStates.SitRecovery:
                    if (currentStamina < maxStamina)
                    {
                        currentStamina += Time.deltaTime;
                    }
                    break;
                case (int)HuggyStates.Enrage:
                    if (!inSpecialAnimation)
                    {
                        currentStamina -= Time.deltaTime/3; //Effectively 60s (20s - deltaTime/3)
                    }
                    break;
                case (int)HuggyStates.Retreat:
                    if (currentRetreatTime < retreatTime)
                    {
                        currentRetreatTime += Time.deltaTime;
                    }
                    break;
            }
        }

        private void CheckAttackCollision()
        {
            gameObject.GetComponent<BoxCollider>().isTrigger = true;
            if (weaponSwingCheckArea != null && !StartOfRound.Instance.localPlayerController.isPlayerDead)
            {
                if (StartOfRound.Instance.localPlayerController.GetComponent<BoxCollider>().bounds.Intersects(weaponSwingCheckArea.bounds))
                {
                    PlayRandomSlashSound();
                    StartOfRound.Instance.localPlayerController.DamagePlayer(28, false, true, CauseOfDeath.Mauling);
                    StartOfRound.Instance.localPlayerController.DropBlood();
                }
            }
        }

        private void PlayHuggyRoar()
        {
            float distanceFromHuggy = Vector3.Distance(this.transform.position, StartOfRound.Instance.localPlayerController.transform.position);
            if (distanceFromHuggy < 5)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
                StartOfRound.Instance.localPlayerController.JumpToFearLevel(1.0f);
            }
            else if (distanceFromHuggy < 11)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                StartOfRound.Instance.localPlayerController.JumpToFearLevel(0.7f);
            }
            else if (distanceFromHuggy < 25)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
                StartOfRound.Instance.localPlayerController.JumpToFearLevel(0.35f);
            }
        }

        private void PlayRandomFootstepSound()
        {
            if (footstepClips != null && creatureSFX != null)
            {
                creatureSFX.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(creatureSFX, footstepClips, true, 0.7f);
            }

        }

        private void PlayRandomAttackVoiceSound()
        {
            if (attackVoiceClips != null && voiceAudio != null)
            {
                voiceAudio.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(voiceAudio, attackVoiceClips, true, 3);
            }

        }

        private void PlayRandomAttackSound()
        {
            if (attackSwingClips != null && creatureSFX != null)
            {
                creatureSFX.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(creatureSFX, attackSwingClips, true, 2);
            }
        }

        private void PlayRandomRoarSound()
        {
            if (roarClips != null && voiceAudio != null)
            {
                voiceAudio.pitch = (float)(1 - rng.NextDouble() / 14);
                int played = RoundManager.PlayRandomClip(voiceAudio, roarClips);
                if (roarFarClips != null && voiceFarAudio != null)
                {
                    RoundManager.PlayRandomClip(voiceFarAudio, roarFarClips, true, 4);
                }
            }
        }

        private void PlayRandomSitDownSound()
        {
            if (sitDownClips != null && creatureSFX != null)
            {
                creatureSFX.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(creatureSFX, sitDownClips);
            }
        }

        private void PlayRandomSitUpSound()
        {
            if (sitUpClips != null && creatureSFX != null)
            {
                creatureSFX.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(creatureSFX, sitUpClips);
            }
        }
        private void PlayRandomCrouchSound()
        {
            if (crouchClips != null && creatureSFX != null)
            {
                creatureSFX.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(creatureSFX, crouchClips);
            }
        }

        private void PlayRandomSlashSound()
        {
            if (slashClips != null && slashAudio != null)
            {
                slashAudio.pitch = (float)(1 - rng.NextDouble() / 14);
                RoundManager.PlayRandomClip(slashAudio, slashClips);
            }
        }

        private void SetSitCollision(int value)
        {
            sitCollisionArea.isTrigger = (value == 0);
        }

        private void ResetAfterStanding()
        {
            switch (currentBehaviourStateIndex) 
            {
                case (int)HuggyStates.Enrage:
                    agent.speed = 6.5f;
                    break;
                case (int)HuggyStates.RegularChase:
                    agent.speed = 4.5f;
                    break;
                default:
                    agent.speed = 3;
                    break;
            }
        }

        private void ResetAfterAttacking()
        {
            attacking = false;
            agent.speed = 4.5f;
        }

        private void SetStatsAfterRoaring()
        {
            agent.speed = 6.5f;
            openDoorSpeedMultiplier = 100;
            canJumpscare = true;
        }

        private void ResetAfterJumpscare()
        {
            canJumpscare = false;
            openDoorSpeedMultiplier = 1;
            inSpecialAnimation = false;
            agent.speed = 3.0f;
            currentFrustration = 0;
            standing = true;
            forward = true;
            running = false;
            charging = false;
            attacking = false;
            sitting = false;
            crouching = false;
            changeInAnimationStateNeeded = true;
        }

        private void SwitchToWandering()
        {
            crouching = false;
            forward = false;
            running = false;
            sitting = false;
            standing = true;
            charging = false;
            agent.speed = 3f;
            changeInAnimationStateNeeded = true;
        }

        private void SwitchToCreeping()
        {
            currentWanderingTime = 0;
            atHidingPosition = false;
            hidingPositionFound = false;
            hidingPositionNoPlayers = true;
            StopSearch(huggySearch);
            FindHidingSpot();
            crouching = true;
            forward = true;
            running = false;
            sitting = false;
            standing = false;
            changeInAnimationStateNeeded = true;
            agent.speed = 3;

        }

        private void SwitchToRegularChasing()
        {
            crouching = false;
            standing = true;
            forward = true;
            running = true;
            atHidingPosition = false;
            changeInAnimationStateNeeded = true;
            agent.speed = 4.5f;
            currentWanderingTime = 0;
            TargetClosestPlayer();
            StopSearch(huggySearch);
        }

        private void SwitchToSitting()
        {
            targetPlayer = null;
            standing = false;
            forward = false;
            running = false;
            sitting = true;
            crouching = false;
            changeInAnimationStateNeeded = true;
            currentFrustration += 1;
            agent.velocity = Vector3.zero;
            agent.speed = 0;
            if (huggySearch.inProgress) { StopSearch(huggySearch); }
        }

        private void SwitchToCharging()
        {
            HuggySendStringClientRcp("Charge");
            creatureAnimator.SetTrigger("Charge");
            agent.speed = 0;
            forward = true;
            running = false;
            charging = true;
            crouching = false;
            sitting = false;
            changeInAnimationStateNeeded = true;
        }

        private void SwitchToRetreating(float retreatTime)
        {
            this.retreatTime = retreatTime;
            currentRetreatTime = 0f;
            agent.speed = 5.0f;
            standing = true;
            crouching = false;
            forward = true;
            running = true;
            changeInAnimationStateNeeded = true;
        }

        private void SwitchToEnrage()
        {
            sitting = false;
            standing = true;
            forward = true;
            currentFrustration = 0;
            running = true;
            agent.velocity = Vector3.zero;
            currentStamina = maxStamina;
            if (currentBehaviourStateIndex == (int)HuggyStates.SitRecovery) { agent.speed = 0; }
            else { agent.speed = 6.5f; }
            changeInAnimationStateNeeded = true;
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

        private void FindHidingSpot()
        {   
            PlayerControllerB closestPlayer = GetClosestPlayerFixed(transform.position);
            if (closestPlayer != null && (!hidingPositionFound || hidingPositionNoPlayers))
            {
                Vector3 cloestValidPlayerTransform = closestPlayer.transform.position;
                //Vector3 potentialHidingSpotNodeTransform = ChooseClosestNodeToPosition(cloestValidPlayerTransform, true, 2).position;
                Vector3 potentialHidingSpotNodeTransform = GetHidingSpotNode(cloestValidPlayerTransform, hidingSpotPosition).position;
                hidingSpotPosition = potentialHidingSpotNodeTransform;
                hidingPositionFound = true;
                hidingPositionNoPlayers = false;
                atHidingPosition = false;
                forward = true;
                changeInAnimationStateNeeded = true;
            }
            else if (!hidingPositionFound) 
            {
                forward = true;
                changeInAnimationStateNeeded = true;
                hidingSpotPosition = idealHidingSpotPosition;
                hidingPositionFound = true;
                hidingPositionNoPlayers = true;
                atHidingPosition = false;
            }
        }

        private Transform GetHidingSpotNode(Vector3 closestPlayerPosition, Vector3 previousHidingSpot)
        {
            Transform closestNodeToPlayer = ChooseClosestNodeToPosition(closestPlayerPosition, true);
            int offsetSearchWidth = 4;
            int indexOfClosestNode = allAINodes.IndexOf(closestNodeToPlayer.gameObject);
            int currentSearchIndex = indexOfClosestNode - offsetSearchWidth; //Starting here
            int endIndex = indexOfClosestNode + offsetSearchWidth;
            while (currentSearchIndex <= endIndex)
            {
                Transform currentNode;
                //search negative of current search width offset IF its an index within allAINodes
                
                if (currentSearchIndex >= 0 && currentSearchIndex < allAINodes.Length)
                {
                    currentNode = allAINodes[currentSearchIndex].transform;
                    float distanceFromHuggyToNode = Vector3.Distance(transform.position, currentNode.position);
                    float distanceFromHuggyToPlayer = Vector3.Distance(transform.position, closestNodeToPlayer.position);
                    bool withinYRangeCheck = (Math.Abs(currentNode.position.y - closestPlayerPosition.y) < 3);
                    bool betweenHuggyAndPlayer = distanceFromHuggyToNode < distanceFromHuggyToPlayer;
                    bool nobodyLooking = !GetAnyPlayerLooking(currentNode.position, 5);
                    //DebugEnemy = true; //REMOVE ME
                    if (DebugEnemy)
                    {
                        if (withinYRangeCheck)
                            Debug.Log("Passed Y range check.");
                        else Debug.Log("Failed Y range check.");
                        if (betweenHuggyAndPlayer)
                        {
                            Debug.Log("Passed between check.");
                        }
                        else Debug.Log("Failed between check.");
                        if (nobodyLooking)
                        {
                            Debug.Log("Passed looking check.");
                        }
                        else Debug.Log("Failed looking check.");
                    }
                    if (nobodyLooking && betweenHuggyAndPlayer && withinYRangeCheck)
                    {
                        if (Vector3.Distance(currentNode.position, closestPlayerPosition) < 20) {
                            hidingPositionFound = true;
                            return currentNode;
                        }
                    }
                }
                currentSearchIndex++;
            }
            //Debug.Log("Search failed.");
            return transform; //Return our current position as a failsafe just in case.  This search will happen once every 0.2 seconds that it fails so the players will move and a new chance
            //To succeed will eventually arise.
        }

        private void RevalidateHidingSpot() //Performs logic to revalidate our hiding spot, or to enter a different state if a player gets too close to us while we are approaching it or even at it.
        {
            GetAnyPlayerLooking(transform.position, 20);
            PlayerControllerB closestPlayer = GetClosestPlayerFixed(transform.position);
            if (playerLooking != null)
            {
                float distanceToLookingPlayer = Vector3.Distance(playerLooking.transform.position, transform.position);
                hidingPositionFound = false;
                hidingPositionNoPlayers = true;
                agent.avoidancePriority = 49;
                if (distanceToLookingPlayer < 7f)
                {
                    //If the player looks at us while hiding while being close, begin a chase.
                    ScareClientAmount(playerLooking.actualClientId, 0.5f);
                    SwitchToRegularChasing();
                    SwitchToBehaviourState((int)HuggyStates.RegularChase);
                }
                else
                {
                    //If the player look at us but is pretty far away, do a short 8 second retreat before trying to find a better hiding spot.
                    ScareClientAmount(playerLooking.actualClientId, 0.35f);
                    SwitchToRetreating(8f);
                    SwitchToBehaviourState((int)(HuggyStates.Retreat));
                }
                return;
            }

            else
            {
                //Always check if we need to find a newer hiding spot, will be a simple boolean check if we already found one.
                FindHidingSpot();
            }
            //If any player looks at the hiding spot from too close of a distance, find a newer and better hiding spot.
            GetAnyPlayerLooking(hidingSpotPosition, 3);
            if (playerLooking != null)
            {
                if (DebugEnemy)
                {
                    Debug.Log("INVALID HIDING SPOT: Player looked at it too closely.");
                }
                hidingPositionFound = false;
            }
            //If no player is near the hiding spot, its not a good hiding spot.
            if (closestPlayer != null && hidingSpotPosition != null)
            {
                float distanceToClosestPlayer = Vector3.Distance(closestPlayer.transform.position, hidingSpotPosition);
                if (DebugEnemy)
                {
                    Debug.Log("Distance to player from found hiding position: " + distanceToClosestPlayer);
                }
                
                if (distanceToClosestPlayer > 20)
                {
                    if (DebugEnemy)
                    {
                        Debug.Log("INVALID HIDING SPOT: Distance from target player too far.");
                    }
                    hidingPositionFound = false;
                }
            }
        }
        /*
                public override void OnCollideWithPlayer(Collider other)
                {
                    if (currentBehaviourStateIndex != (int)(HuggyStates.Enrage) || inSpecialAnimation)
                    {
                        return;
                    }
                    base.OnCollideWithPlayer(other);
                    PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
                    if (playerControllerB != null)
                    {
                        if (IsHost || IsServer)
                        {
                            HuggySendStringClientRcp("Jumpscare:" + playerControllerB.actualClientId);
                            SwitchToJumpscare(playerControllerB.actualClientId);
                        }              
                    }
                }*/

        private void SwitchToJumpscare(ulong playerID)
        {
            if (IsHost || IsServer)
            {
                HuggySendStringClientRcp("Jumpscare");
                HuggySendStringClientRcp("Jumpscare:" + playerID.ToString());
                creatureAnimator.SetTrigger("Jumpscare");
            }
            RoundManager.PlayRandomClip(creatureSFX, jumpscareClips, default, 1.5f);
            agent.speed = 0;
            currentFrustration = 0;
            agent.velocity = Vector3.zero;
            charging = false;
            changeInAnimationStateNeeded = true;
            inSpecialAnimationWithPlayer = StartOfRound.Instance.allPlayerScripts[playerID];
            targetPlayer = inSpecialAnimationWithPlayer;
            if (inSpecialAnimationWithPlayer != null)
            {
                inSpecialAnimation = true;
                inSpecialAnimationWithPlayer.inSpecialInteractAnimation = true;
                inSpecialAnimationWithPlayer.snapToServerPosition = true;
                inSpecialAnimationWithPlayer.disableLookInput = true;
                inSpecialAnimationWithPlayer.DropAllHeldItems();
                //Adjust where huggy is looking.
                Vector3 diff = targetPlayer.transform.position - this.transform.position;
                diff.y = 0;
                transform.rotation = Quaternion.LookRotation(diff);
                //Adjust where the player is looking.
                Vector3 diff2 = this.transform.position - targetPlayer.transform.position;
                diff2.y = 0;
                inSpecialAnimationWithPlayer.transform.rotation = Quaternion.LookRotation(diff);
                jumpscareAnimation = StartCoroutine(JumpScareAnimation());
            }
            else
            {
                //Exit jumpscare animation early, unique circumstance where the player doesn't exist for whatever reason anymore.  Maybe they died or maybe they disconnected, etc.
            }

        }

        private IEnumerator JumpScareAnimation()
        {
            float elapsedTime = 0f;
            while (elapsedTime < 1f) {
                Vector3 diff2 = this.transform.position - targetPlayer.transform.position;
                diff2.y = 0;
                inSpecialAnimationWithPlayer.transform.position = jumpScarePoint.position;
                inSpecialAnimationWithPlayer.transform.rotation = Quaternion.LookRotation(diff2);
                inSpecialAnimationWithPlayer.ResetFallGravity();
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            EndJumpscareAnimation();
        }

        private IEnumerator WaitAfterJumpscare()
        {
            yield return new WaitForSeconds(3.0f);
            ResetAfterJumpscare();
            SwitchToBehaviourState((int)HuggyStates.Wandering);
        }
        private void EndJumpscareAnimation()
        {
            if (inSpecialAnimationWithPlayer)
            {
                forward = false;
                inSpecialAnimationWithPlayer.DropBlood(Vector3.forward);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.left);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.right);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.up);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.back);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.down);
                //inSpecialAnimationWithPlayer.KillPlayer(Vector3.zero, true, CauseOfDeath.Strangulation, 1);
                inSpecialAnimationWithPlayer.DamagePlayer(1000, false, true, CauseOfDeath.Strangulation, 1);
                inSpecialAnimationWithPlayer.inSpecialInteractAnimation = false;
                inSpecialAnimationWithPlayer.snapToServerPosition = false;
                inSpecialAnimationWithPlayer.disableLookInput = false;
                inSpecialAnimationWithPlayer = null;
                changeInAnimationStateNeeded = true;
            }
            waitingRoutine = StartCoroutine(WaitAfterJumpscare());         
        }

        private void formStateArray()
        {
            statesArray = new string[] {sitting.ToString(), standing.ToString(), crouching.ToString(), forward.ToString(), running.ToString(), charging.ToString(), attacking.ToString(), jumpscaring.ToString() };
        }

        [ClientRpc]
        private void UpdateAnimationState(string[] states)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams rpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(2947720591u, rpcParams, 0);
                bool flag = states != null;
                bufferWriter.WriteValueSafe(flag, default);
                if (flag)
                {
                    int length = states.Length;
                    bufferWriter.WriteValueSafe(length, default);  // Write the length of the array
                    for (int i = 0; i < states.Length; i++)
                    {
                        bufferWriter.WriteValueSafe(states[i], default);  // Write each string value
                    }
                }
                __endSendClientRpc(ref bufferWriter, 2947720591u, rpcParams, 0);
            }
            if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient/* || networkManager.IsHost*/))
            {
                if (!IsHost && !IsServer)
                {
                    sitting = bool.Parse(states[0]); standing = bool.Parse(states[1]); crouching = bool.Parse(states[2]); forward = bool.Parse(states[3]); running = bool.Parse(states[4]);
                    UpdateAnimationStates();
                    charging = bool.Parse(states[5]);
                    attacking = bool.Parse(states[6]);
                    jumpscaring = bool.Parse(states[7]);
                }
            }
        }

        private void UpdateAnimationStates()
        {
            if (IsHost || IsServer)
            {
                formStateArray();
                UpdateAnimationState(statesArray);
            }
            creatureAnimator.SetBool("Sitting", sitting);
            creatureAnimator.SetBool("Standing", standing);
            creatureAnimator.SetBool("Crouching", crouching);
            creatureAnimator.SetBool("Forward", forward);
            creatureAnimator.SetBool("Running", running);
            changeInAnimationStateNeeded = false;
        }

        private void ProcessScareClientAmount(ulong clientID, float amount)
        {
            if (StartOfRound.Instance.localPlayerController.actualClientId == clientID)
            {
                StartOfRound.Instance.localPlayerController.JumpToFearLevel(amount);
            }
        }

        private void ScareClientAmount(ulong clientID, float amount)
        {
            string toSendOff = "Scare:" + clientID.ToString() + "," + amount.ToString();
            HuggySendStringClientRcp(toSendOff);
        }

        private static void __rpc_handler_2947720591(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager != null && networkManager.IsListening)
            {
                bool flag;
                reader.ReadValueSafe(out flag, default);
                string[] states = null;
                if (flag)
                {
                    int length;
                    reader.ReadValueSafe(out length, default);  // Read the length of the array
                    states = new string[length];
                    for (int i = 0; i < length; i++)
                    {
                        reader.ReadValueSafe(out states[i], default);  // Read each string value
                    }
                }
                target.__rpc_exec_stage = (__RpcExecStage)2;
                ((HuggyAI)target).UpdateAnimationState(states);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }

        [ClientRpc]
        private void HuggySendStringClientRcp(string informationString)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams rpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(1345740394u, rpcParams, 0);
                bool flag = informationString != null;
                bufferWriter.WriteValueSafe(flag, default);
                if (flag)
                {
                    bufferWriter.WriteValueSafe(informationString, false);
                }
                __endSendClientRpc(ref bufferWriter, 1345740394u, rpcParams, 0);
            }
            if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient/* || networkManager.IsHost*/))
            {
                if (!IsServer && !IsHost)
                {
                    InterpretRpcString(informationString);
                }
            }
        }

        private void InterpretRpcString(string informationString)
        {
            if (informationString.Contains("Jumpscare:")) {
                string remainingString = informationString.Replace("Jumpscare:", "");
                ulong clientID = (ulong)int.Parse(remainingString);
                SwitchToJumpscare(clientID);
            }
            if (informationString.Contains("Scare:"))
            {
                string[] remainingStringScared = informationString.Replace("Scare:", "").Split(",");
                if (remainingStringScared.Length == 2)
                {
                    ulong clientIDScared = (ulong)int.Parse(remainingStringScared[0]);
                    ProcessScareClientAmount(clientIDScared, (float)double.Parse(remainingStringScared[1]));
                }
            }
            switch (informationString)
            {
                case "Attacking":
                    gameObject.GetComponent<BoxCollider>().isTrigger = false;
                    creatureAnimator.SetTrigger(informationString);
                    attacking = true;
                    break;
                case "AttackingMirror":
                    gameObject.GetComponent<BoxCollider>().isTrigger = false;
                    creatureAnimator.SetTrigger(informationString);
                    attacking = true;
                    break;
                case "Jumpscare":
                    forward = true;
                    creatureAnimator.SetTrigger(informationString);
                    break;
                case "Charge":
                    creatureAnimator.SetTrigger(informationString);
                    break;
                
            }
        }

        //Send String information
        private static void __rpc_handler_1345740394(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
                ((HuggyAI)target).HuggySendStringClientRcp(valueAsString);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }

        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_HuggyAI()
        {
            __rpc_func_table.Add(2947720591, new RpcReceiveHandler(__rpc_handler_2947720591)); //SetAnimationVariables
            __rpc_func_table.Add(1345740394, new RpcReceiveHandler(__rpc_handler_1345740394)); //AnimationTriggers
        }
    }
}
