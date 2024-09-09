using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.UIElements;
using static LethalPlaytime.DogdayAI;
using static Unity.Netcode.NetworkManager;

namespace LethalPlaytime
{
    public class BoxyBooAI : EnemyAI
    {
        private System.Random rng;
        private AISearchRoutine boxySearchRoutine;
        private enum BoxyStates { BasicSearch, AdvancedSearch, BasicChase, AdvancedChase, Box, Jumpscare, Leap, Armgrab, Retreat};
        private enum CrankStates { WindUp, Static, NormalSpin, FastSpin }

        private CrankStates crankState;
        public Transform crank;
        public InteractTrigger interactTrigger;

        //Animation state
        public bool box = false;
        public bool partial = true;
        public bool full = false;
        public bool attacking = false;
        public bool leaping = false;
        public bool grabbing = false;
        public bool jumpscaring = false;

        //Energy
        public static readonly int maxEnergy = 26;
        public static readonly int thresholdEnergy = 21;
        public float energy = 0;
        public bool reversing = false;
        private static readonly int maxCrankBackwardsTime = 3;
        private float crankBackwardsTime = 0;

        //Ability cooldowns
        //FIX ME
        public static readonly float maxLeapCooldown = 8;
        public static readonly float maxGrabCooldown = 10;
        public static readonly float startLeapCooldown = 2000;
        public static readonly float startGrabCooldown = 2000;
        public float leapCooldown = 5;
        public float grabCooldown = 6;

        //Walking/Idle animations.
        public static readonly float maxTimeSinceMoving = 0.15f;
        public float timeSinceMoving = 0;


        //Leap Stuff
        public int numJumps = 1;
        public Transform leftsideCheck;
        public Transform rightsideCheck;
        Vector3 landingPosition;
        private static float peakJumpHeight = 0.5f;
        private static float jumpSpeed = 5f;
        public float timeSinceOnNavMesh = 0;
        private Vector3 reservedAgentSpeed;

        //Jump animation only
        bool wasOnOffMeshLink = false;

        //Audio Sources
        public AudioSource crankAudio;
        public AudioSource hitConnectAudio;
        public AudioSource grabAudioPoint;
        public AudioSource musicAudio;
        public AudioSource creakAudio;
        public AudioSource jumpAudio;
        public AudioSource retractAudio;

        //Audioclips
        public AudioClip[] walkSounds;
        public AudioClip[] staticSounds;
        public AudioClip[] attackSounds;
        public AudioClip[] hitConnectSounds;
        public AudioClip[] grabLaunchSounds;
        public AudioClip[] grabReadySounds;
        public AudioClip[] crankSounds;
        public AudioClip[] musicTrackSounds;
        public AudioClip[] boxLandSounds;
        public AudioClip[] leapSounds;
        public AudioClip[] landSounds;
        public AudioClip[] creakSounds;
        public AudioClip[] popSounds;
        public AudioClip[] jumpscareSounds;
        public AudioClip[] partialRetractSounds;
        public AudioClip[] fullRetractSounds;

        //Soundvariables
        private float maxTimeBetweenCranks = 0.75f;
        public float timeBetweenCranks = 0;

        //Creaking
        private static readonly float maxCreakTime = 6;
        private static readonly float minCreekTime = 3;
        public float creakTime = 5;

        //Attack area
        public BoxCollider attackArea;
        public static readonly float maxAttackCooldown = 1.4f;
        public float attackCooldown = 0;

        //GrabStuff
        public int numGrabs = 1;
        private int angleOffsetGrab = 45;
        private static readonly float maxTimeAimingGrab = 0.6f;  //TO-DO lower
        public float timeAimingGrab = 0;

        //Jumpscare Stuff
        public bool grabbedPlayer = false;
        public bool checkingArmCollision = false;
        public Transform jumpscareAttachPoint;
        private ulong jumpscareClientID;
        public BoxCollider jumpscareCollision;
        public Coroutine jumpscare;

        //Retreat stuff
        Vector3 retreatPosition;
        private readonly float maxRetreatTime = 25f;
        public float retreatTime = 0;
        public float trackedDamage = 0;
        public bool shouldEnterRetreat = false;
        public readonly float maxTrackedDamaged = 8f;

        //Crank setup?
        private bool setupFinished = false;


        public override void Start()
        {
            base.Start();
            rng = new System.Random(StartOfRound.Instance.randomMapSeed);
            boxySearchRoutine = new AISearchRoutine();
            crankState = CrankStates.Static;
            //Set initial music track to cranking
            if (musicAudio != null && musicTrackSounds != null)
            {
                musicAudio.clip = musicTrackSounds[0];
            }
            if (!base.IsServer)
            {
                ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
            }
            retreatPosition = transform.position;
            debugEnemyAI = false; //REMOVE ME
        }

        public void SetupStartTrigger()
        {
            if (debugEnemyAI && IsClient)
            {
                Debug.Log("SetupStartTrigger called on client.");
            }
            if (interactTrigger.onInteract != null)
            {
                interactTrigger.onInteract.AddListener(CrankBackwards);
            }
            else
            {
                interactTrigger.onInteract = new InteractEvent();
                interactTrigger.onInteract.AddListener(CrankBackwards);
            }
        }

        public override void DoAIInterval()
        {
            if (isEnemyDead) { return; }
            if (StartOfRound.Instance.allPlayersDead || isEnemyDead)
            {
                return;
            }
            base.DoAIInterval();
            if (timeSinceSpawn < 2 && timeSinceSpawn > 1 && !setupFinished)
            {
                setupFinished = true;
                if (debugEnemyAI) { Debug.Log("Setting up interact trigger on clients."); }
                BoxyBooSendStringClientRcp("SetupClientInteractTrigger");
            }
            PlayerControllerB potentialTargetPlayer;
            switch (currentBehaviourStateIndex) 
            {
                case (int)BoxyStates.BasicSearch:
                    if (energy <= 0)
                    {
                        SwitchToBehaviourState((int)BoxyStates.Box);
                        BoxyBooSendStringClientRcp("SwitchToBox");
                        StopSearch(boxySearchRoutine, false);
                        return;
                    }
                    potentialTargetPlayer = CheckLineOfSightForClosestPlayer(110, 25);
                    if (potentialTargetPlayer != null && potentialTargetPlayer.isInsideFactory && !potentialTargetPlayer.isPlayerDead && !potentialTargetPlayer.inAnimationWithEnemy)
                    {
                        targetPlayer = potentialTargetPlayer;
                        SwitchToBehaviourState((int)BoxyStates.BasicChase);
                        BoxyBooSendStringClientRcp("SwitchToBasicChase");
                        return;
                    }
                    if (!boxySearchRoutine.inProgress)
                    {
                        StartSearch(transform.position, boxySearchRoutine);
                    }
                    break;
                case (int)BoxyStates.AdvancedSearch:
                    if (energy <= 0)
                    {
                        SwitchToBehaviourState((int)BoxyStates.Box);
                        BoxyBooSendStringClientRcp("SwitchToBox");
                        StopSearch(boxySearchRoutine, false);
                        return;
                    }
                    potentialTargetPlayer = CheckLineOfSightForClosestPlayer(180, 35);
                    if (potentialTargetPlayer != null && potentialTargetPlayer.isInsideFactory && !potentialTargetPlayer.isPlayerDead && !potentialTargetPlayer.inAnimationWithEnemy)
                    {
                        targetPlayer = potentialTargetPlayer;
                        SwitchToBehaviourState((int)BoxyStates.AdvancedChase);
                        BoxyBooSendStringClientRcp("SwitchToAdvancedChase");
                        return;
                    }
                    if (!boxySearchRoutine.inProgress)
                    {
                        StartSearch(transform.position, boxySearchRoutine);
                    }
                    break;
                case (int)BoxyStates.BasicChase:
                    if (boxySearchRoutine.inProgress)
                    {
                        StopSearch(boxySearchRoutine);
                    }
                    if (energy <= 0)
                    {
                        SwitchToBehaviourState((int)BoxyStates.Box);
                        BoxyBooSendStringClientRcp("SwitchToBox");
                        return;
                    }
                    if (targetPlayer != null && !targetPlayer.isPlayerDead && targetPlayer.isInsideFactory && !targetPlayer.inAnimationWithEnemy)
                    {
                        float distanceToCurrentTargetPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
                        potentialTargetPlayer = CheckLineOfSightForClosestPlayer(150, 25);
                        if (distanceToCurrentTargetPlayer < 1.5f && !attacking && attackCooldown <= 0) //ATTACK
                        {
                            AttackRandom();
                            return;
                        }
                        if (potentialTargetPlayer != targetPlayer && potentialTargetPlayer != null && Vector3.Distance(transform.position, potentialTargetPlayer.transform.position) < distanceToCurrentTargetPlayer)
                        {
                            TargetClosestPlayer(5, true, 150);
                            targetPlayer = potentialTargetPlayer;
                        }
                        SetDestinationToPosition(targetPlayer.transform.position);
                    }
                    else
                    {
                        potentialTargetPlayer = CheckLineOfSightForClosestPlayer(150, 25);
                        if (potentialTargetPlayer != null)
                        {
                            targetPlayer = potentialTargetPlayer;
                        }
                        else
                        {
                            if (energy > 2)
                            {
                                targetPlayer = null;
                                SwitchToBehaviourState((int)BoxyStates.BasicSearch);
                                BoxyBooSendStringClientRcp("SwitchToBasicSearch");
                            }
                            else
                            {
                                targetPlayer = null;
                                SwitchToBehaviourState((int)BoxyStates.Box);
                                BoxyBooSendStringClientRcp("SwitchToBox");
                            }
                        }
                    }
                    break;
                case (int)BoxyStates.AdvancedChase:
                    if (boxySearchRoutine.inProgress)
                    {
                        StopSearch(boxySearchRoutine);
                    }
                    //Energy check
                    if (energy <= 0)
                    {
                        SwitchToBehaviourState((int)BoxyStates.Box);
                        BoxyBooSendStringClientRcp("SwitchToBox");
                        return;
                    }
                    if (targetPlayer != null && !targetPlayer.isPlayerDead && targetPlayer.isInsideFactory && !targetPlayer.inAnimationWithEnemy)
                    {
                        float distanceToCurrentTargetPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
                        potentialTargetPlayer = CheckLineOfSightForClosestPlayer(360, 25);
                        //Switch targets if there is a better target or needed.
                        if (distanceToCurrentTargetPlayer < 1.5f && !attacking && attackCooldown <= 0) //ATTACK
                        {
                            AttackRandom();
                            return;
                        }
                        if (potentialTargetPlayer != targetPlayer && potentialTargetPlayer != null && Vector3.Distance(transform.position, potentialTargetPlayer.transform.position) < distanceToCurrentTargetPlayer)
                        {
                            TargetClosestPlayer(5, false);
                            targetPlayer = potentialTargetPlayer;
                        }
                        //Perform abilities
                        if (energy > 2)
                        {
                            if (grabCooldown <= 0 && distanceToCurrentTargetPlayer < 3.25 && distanceToCurrentTargetPlayer > 0.6 && !agent.isOnOffMeshLink && (Math.Abs(transform.position.y - targetPlayer.transform.position.y)) < 1.25) 
                            {
                                if (!Physics.Linecast(eye.transform.position, targetPlayer.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                                {
                                    SwitchToBehaviourState((int)BoxyStates.Armgrab);
                                    BoxyBooSendStringClientRcp("SwitchToGrab");
                                    return;
                                }
                            }
                            if (leapCooldown <= 0  && distanceToCurrentTargetPlayer < 10 && distanceToCurrentTargetPlayer > 5.5 && !agent.isOnOffMeshLink && (Math.Abs(transform.position.y - targetPlayer.transform.position.y)) < 0.7)
                            {
                                Vector3 direction = (targetPlayer.transform.position - transform.position).normalized;
                                Vector3 closerPosition = targetPlayer.transform.position - (direction * 0.5f);

                                if (NavMesh.SamplePosition(closerPosition, out _, 0.6f, NavMesh.AllAreas))
                                {
                                    Vector3 navMeshPosition = GetRandomNavMeshPositionInRadiusSpherical(closerPosition, 1, 0.02f, 100);
                                    Debug.DrawLine(transform.position, navMeshPosition, Color.magenta, 4); // Remove me
                                    landingPosition = navMeshPosition;
                                    BoxyBooSendStringClientRcp("Vector3: " + landingPosition.x + "," + landingPosition.y + "," + landingPosition.z);
                                    bool leftCheck = Physics.Linecast(leftsideCheck.position, targetPlayer.playerEye.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                                    bool rightCheck = Physics.Linecast(rightsideCheck.position, targetPlayer.playerEye.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault);

                                    if (debugEnemyAI)
                                    {
                                        Debug.Log("Leftcheck: " + leftCheck);
                                        Debug.Log("Rightcheck: " + rightCheck);
                                    }
                                    if (leftCheck && rightCheck)
                                    {
                                        landingPosition = navMeshPosition;
                                        SwitchToBehaviourState((int)BoxyStates.Leap);
                                        BoxyBooSendStringClientRcp("SwitchToLeap");
                                        return;
                                    }
                                }
                                //Vector3 navMeshPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(closerPosition, 1, navHit);
                            }
                        }
                        SetDestinationToPosition(targetPlayer.transform.position);
                    }
                    else
                    {
                        potentialTargetPlayer = CheckLineOfSightForClosestPlayer(360, 25);
                        if (potentialTargetPlayer != null)
                        {
                            targetPlayer = potentialTargetPlayer;
                        }
                        else
                        {
                            if (energy > 2)
                            {
                                targetPlayer = null;
                                SwitchToBehaviourState((int)BoxyStates.AdvancedSearch);
                                BoxyBooSendStringClientRcp("SwitchToAdvancedSearch");
                            }
                            else
                            {
                                targetPlayer = null;
                                SwitchToBehaviourState((int)BoxyStates.Box);
                                BoxyBooSendStringClientRcp("SwitchToBox");
                            }
                        }
                    }
                    break;
                case (int)BoxyStates.Box:
                    potentialTargetPlayer = CheckLineOfSightForPlayer(270, 7);
                    if (energy >= maxEnergy)
                    {
                        SwitchToBehaviourState((int)BoxyStates.AdvancedSearch);
                        BoxyBooSendStringClientRcp("SwitchToAdvancedSearch");
                        return;
                    }
                    else if (energy >= thresholdEnergy && potentialTargetPlayer != null && Vector3.Distance(transform.position, potentialTargetPlayer.transform.position) < 7)
                    {
                        SwitchToBehaviourState((int)BoxyStates.BasicSearch);
                        BoxyBooSendStringClientRcp("SwitchToBasicSearch");
                        return;
                    }

                    break;
                case (int)BoxyStates.Jumpscare:
/*                    if (!jumpscaring)
                    {
                        Debug.Log("Boxy: Switching State Jumpscare -> Box");
                        targetPlayer = null;
                        SwitchToBehaviourState((int)BoxyStates.Box);
                        BoxyBooSendStringClientRcp("SwitchToBox");
                    }*/
                    break;
                case (int)BoxyStates.Leap:
/*                    if (numJumps < 1)
                    {
                        FindAndSetValidNavMeshPosition();
                    }*/
                    break;
                case (int)BoxyStates.Armgrab:
                    if (numGrabs <= 0 && !jumpscaring)
                    {
                        SwitchToBehaviourState((int)BoxyStates.AdvancedSearch); //REMOVE ME
                        BoxyBooSendStringClientRcp("SwitchToAdvancedSearch");
                        return;
                    }
                    break;
                case (int)BoxyStates.Retreat:
                    if (Vector3.Distance(transform.position, retreatPosition) < 2.5f || retreatTime > maxRetreatTime)
                    {
                        retreatTime = 0;
                        targetPlayer = null;
                        SwitchToBehaviourState((int)BoxyStates.Box);
                        BoxyBooSendStringClientRcp("SwitchToBox");
                        return;
                    }
                    SetDestinationToPosition(retreatPosition);
                    break;
            }
        }

        public override void Update()
        {
            if (isEnemyDead) { return; }
            base.Update();
            UpdateOffmeshJumpState();
            UpdateCrank(crankState, reversing);
            UpdateEnergy((BoxyStates)currentBehaviourStateIndex);
            UpdateCooldowns();
            UpdateStateDependent();
            UpdateCreak();


            if (IsHost || IsServer)
            {
                UpdateRetreatCheck();
                UpdateWalking();
                UpdateNavMesh();
                CheckArmCollision();
            }
        }

        private void UpdateRetreatCheck()
        {
            if (!attacking && shouldEnterRetreat && (currentBehaviourStateIndex == (int)BoxyStates.BasicSearch || currentBehaviourStateIndex == (int)BoxyStates.AdvancedSearch || currentBehaviourStateIndex == (int)BoxyStates.BasicChase || currentBehaviourStateIndex == (int)BoxyStates.AdvancedChase))
            {
                shouldEnterRetreat = false;
                trackedDamage = 0;
                SwitchToBehaviourState((int)BoxyStates.Retreat);
                BoxyBooSendStringClientRcp("SwitchToRetreat");
            }
        }

        private void UpdateOffmeshJumpState()
        {
            if (agent.isOnOffMeshLink && !wasOnOffMeshLink)
            {
                BoxyBooSendStringClientRcp("PlayJumpAnimation");
            }
            wasOnOffMeshLink = agent.isOnOffMeshLink;
        }

        private void UpdateCreak()
        {
            if (currentBehaviourStateIndex < (int)BoxyStates.Box) 
            {
                creakTime -= Time.deltaTime;
                if (creakTime <= 0) 
                {
                    creakTime = (float)(rng.NextDouble() * (maxCreakTime - minCreekTime)) + minCreekTime; //4 is max - min + 1
                    PlayRandomCreakSound();
                }
            }
        }

        private void UpdateStateDependent()
        {
            if (attacking && targetPlayer != null) //Rotate towards player while swinging
            {
                Vector3 diff = targetPlayer.transform.position - this.transform.position;
                diff.y = 0;
                Quaternion targetLook = Quaternion.LookRotation(diff);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetLook, Time.deltaTime * 10);
            }
            switch (currentBehaviourStateIndex)
            {
                case (int)BoxyStates.BasicSearch:
                    break;
                case (int)BoxyStates.Box:
                    timeBetweenCranks += Time.deltaTime;
                    if (timeBetweenCranks > maxTimeBetweenCranks)
                    {
                        timeBetweenCranks = 0;
                        PlayRandomCrankSound();
                    }
                    break;
                case (int)BoxyStates.Armgrab:
                    PlayerControllerB closestPlayer = CheckLineOfSightForClosestPlayer(360, 7);
                    if (closestPlayer != null && closestPlayer.isInsideFactory && !closestPlayer.isPlayerDead)
                    {
                        Vector3 diff = closestPlayer.transform.position - this.transform.position;
                        diff.y = 0;
                        Quaternion targetLook = Quaternion.LookRotation(diff);
                        Quaternion offset = Quaternion.Euler(0, 7, 0);
                        Quaternion targetLookWithOffset = targetLook * offset;
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetLookWithOffset, Time.deltaTime * 20f);
                    }
                    if (timeAimingGrab > -0.5)
                    {
                        timeAimingGrab += Time.deltaTime;
                        if (timeAimingGrab > maxTimeAimingGrab)
                        {
                            timeAimingGrab = -1;
                            BoxyBooSendStringClientRcp("FinishArmGrab");  //Setting the value that lets us escape winding up.
                        }
                    }
                    break;
                case (int)BoxyStates.Retreat:
                    retreatTime += Time.deltaTime;
                    break;
            }
        }

        private IEnumerator Leap()
        {
            Vector3 startingPosition = transform.position;
            float distanceToDestination = Vector3.Distance(startingPosition, landingPosition);
            float timeTaken = 0;
            while (distanceToDestination > 0.2f && timeTaken < 0.75)
            {
                //Move towards the landing position by predetermined amount jumpspeed
                //transform.position = Vector3.MoveTowards(transform.position, landingPosition, Time.deltaTime * jumpSpeed * 4);
                transform.position = Vector3.Lerp(transform.position, landingPosition, Time.deltaTime * jumpSpeed);
                Vector3 diff = targetPlayer.transform.position - this.transform.position;
                diff.y = 0;
                Quaternion targetLook = Quaternion.LookRotation(diff);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetLook, Time.deltaTime * 10);
                distanceToDestination = Vector3.Distance(transform.position, landingPosition);
                timeTaken += Time.deltaTime;
                yield return null;
            }
            if (distanceToDestination <= 0.15f) {
                if (!agent.isOnNavMesh)
                {
                    FindAndSetValidNavMeshPosition();
                }
                
                SwitchToBehaviourState((int)BoxyStates.AdvancedChase);
                BoxyBooSendStringClientRcp("SwitchToAdvancedChase");
                if (targetPlayer != null && targetPlayer.isInsideFactory && !targetPlayer.isPlayerDead)
                {
                    SetDestinationToPosition(targetPlayer.transform.position, true);
                }
                //I would like to set the agent on the navmesh at his landing location here.
            }
            else
            {
                if (!agent.isOnNavMesh)
                {
                    FindAndSetValidNavMeshPosition();
                }
                
                SwitchToBehaviourState((int)BoxyStates.AdvancedChase);
                BoxyBooSendStringClientRcp("SwitchToAdvancedChase");
                if (targetPlayer != null && targetPlayer.isInsideFactory && !targetPlayer.isPlayerDead)
                {
                    SetDestinationToPosition(targetPlayer.transform.position, true);
                }
            }
        }

        public void FindAndSetValidNavMeshPosition(float searchRadius = 0.1f, int maxAttempts = 500)
        {
            NavMeshHit navHit;
            Vector3 startPosition = transform.position;
            int attempt = 0;

            while (attempt < maxAttempts && !agent.isOnNavMesh)
            {
                //Generate a random point within a spherical volume around the current position
                Vector3 randomPos = startPosition + UnityEngine.Random.insideUnitSphere * searchRadius;

                //Check if the random position is valid on the NavMesh
                if (NavMesh.SamplePosition(randomPos, out navHit, searchRadius, NavMesh.AllAreas))
                {
                    //Warp the agent to the valid NavMesh position
                    agent.Warp(navHit.position);
                    return; 
                }

                attempt++;
                searchRadius += 0.03f;
            }

            Debug.LogWarning("Failed to find a valid NavMesh position after maximum attempts.");
        }

        private Vector3 GetRandomNavMeshPositionInRadiusSpherical(Vector3 pos, float initialRadius = 10f, float radiusIncrement = 5f, int maxAttempts = 10)
        {
            NavMeshHit navHit;
            float currentRadius = initialRadius;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 randomPos = pos + UnityEngine.Random.insideUnitSphere * currentRadius;
                if (NavMesh.SamplePosition(randomPos, out navHit, currentRadius, NavMesh.AllAreas))
                {
                    Debug.DrawRay(navHit.position + Vector3.forward * 0.01f, Vector3.up * 2f, Color.blue);
                    return navHit.position;
                }

                //Increase radius and retry
                currentRadius += radiusIncrement;
            }

            //If no valid position found after maxAttempts, return original position or handle as needed
            Debug.DrawRay(pos + Vector3.forward * 0.01f, Vector3.up * 2f, Color.yellow);
            return pos;
        }

        private void SwitchToAdvancedSearch()
        {
            if (musicAudio.clip != musicTrackSounds[1])
            {
                musicAudio.clip = musicTrackSounds[1];
                musicAudio.Play();
            }
            numJumps = 1;
            box = false;
            partial = false;
            full = true;
            UpdateAnimationState();
            agent.speed = 5;
            crankState = CrankStates.FastSpin;
        }


        private void SwitchToAdvancedChase()
        {
            if (musicAudio.clip != musicTrackSounds[1])
            {
                musicAudio.clip = musicTrackSounds[1];
                musicAudio.Play();
            }
            box = false;
            partial = false;
            full = true;
            UpdateAnimationState();
            agent.speed = 6.5f;
            attacking = false;
            attackCooldown = maxAttackCooldown * 1.5f;
            crankState = CrankStates.FastSpin;
        }

        private void SwitchToBasicSearch()
        {
            if (musicAudio.clip != musicTrackSounds[0])
            {
                musicAudio.clip = musicTrackSounds[0];
                musicAudio.Play();
            }
            box = false;
            partial = false;
            full = true;
            UpdateAnimationState();
            agent.speed = 4;
            crankState = CrankStates.NormalSpin;
        }

        private void SwitchToBasicChase()
        {
            {
                if (musicAudio.clip != musicTrackSounds[0])
                {
                    musicAudio.clip = musicTrackSounds[0];
                    musicAudio.Play();
                }
                attacking = false;
                attackCooldown = maxAttackCooldown * 2;
                box = false;
                partial = false;
                full = true;
                UpdateAnimationState();
                agent.speed = 6;
                crankState = CrankStates.NormalSpin;
            }
        }

        private void SwitchToLeap()
        {
            {
                numJumps = 0;
                box = false;
                partial = false;
                full = true;
                leapCooldown = maxLeapCooldown;
                UpdateAnimationState();
                agent.speed = 0;
                agent.velocity = Vector3.zero;
                creatureAnimator.SetTrigger("JumpHop");
                PlayRandomLeapSound();
                //StartCoroutine(Leap());
                if (IsHost)
                {
                    StartCoroutine(Leap());
                }
            }
        }

        private void SwitchToGrab()
        {
            numGrabs = 1;
                box = false;
                partial = false;
                full = true;
                UpdateAnimationState();
                agent.speed = 0;
                agent.velocity = Vector3.zero;
            timeAimingGrab = 0;
                crankState = CrankStates.FastSpin;
            creatureAnimator.SetTrigger("ArmGrab");
            creatureAnimator.SetBool("Winding", true);
            PlayRandomGrabReadySound();
            grabCooldown = maxGrabCooldown;


        }

        private void SwitchToRetreat()
        {
            if (boxySearchRoutine.inProgress)
            {
                StopSearch(boxySearchRoutine);
            }
            retreatPosition = ChooseFarthestNodeFromPosition(transform.position).position;
            energy = 0;
            agent.speed = 0;
            agent.velocity = Vector3.zero;
            StartCoroutine(SetSpeedAfterDelay(1.2f, 5.5f));
        }

        private IEnumerator SetSpeedAfterDelay(float delay, float speed)
        {
            yield return new WaitForSeconds(delay);
            agent.speed = speed;
        }

        private void SwitchToBox()
        {
            musicAudio.clip = null;
            box = true;
            partial = false;
            full = false;
            UpdateAnimationState();
            agent.speed = 0;
            agent.velocity = Vector3.zero;
            crankState = CrankStates.WindUp;
        }

        private void SwitchToFull()
        {
            box = false;
            partial = false;
            full = true;
            UpdateAnimationState();
            agent.speed = 4.5f;
        }

        private void SwitchToPartial()
        {
            musicAudio.clip = null;
            box = false;
            partial = true;
            full = false;
            UpdateAnimationState();
            crankState = CrankStates.Static;
        }

        private void UpdateAnimationState()
        {
            creatureAnimator.SetBool("box", box);
            creatureAnimator.SetBool("partial", partial);
            creatureAnimator.SetBool("full", full);
        }

        private void UpdateCooldowns()
        {
            if (leapCooldown > 0)
            {
                leapCooldown -= Time.deltaTime;
            }
            if (grabCooldown > 0)
            {
                grabCooldown -= Time.deltaTime;
            }
            if (attackCooldown > 0)
            {
                attackCooldown -= Time.deltaTime;
            }
        }

        private void UpdateWalking()
        {
            timeSinceMoving += Time.deltaTime;
            if (agent.velocity.magnitude < 0.1)
            {
                if (timeSinceMoving > maxTimeSinceMoving && creatureAnimator.GetBool("walking"))
                {
                    BoxyBooSendStringClientRcp("Idle");
                    creatureAnimator.SetBool("walking", false);
                }
            }
            else 
            {
                if (!creatureAnimator.GetBool("walking"))
                {
                    BoxyBooSendStringClientRcp("Walking");
                    creatureAnimator.SetBool("walking", true);
                }
                timeSinceMoving = 0;
            }
        }

        private void UpdateEnergy(BoxyStates boxyState)
        {
            switch (boxyState)
            {
                case BoxyStates.BasicChase:
                    energy -= Time.deltaTime * 0.5f;
                    break;
                case BoxyStates.AdvancedChase:
                    energy -= Time.deltaTime;
                    break;
                case BoxyStates.BasicSearch:
                    energy -= Time.deltaTime * 0.3333f;
                    break;
                case BoxyStates.AdvancedSearch:
                    energy -= Time.deltaTime * 0.75f;
                    break;
                case BoxyStates.Box:
                    energy += Time.deltaTime;
                    break;
                default:
                    break;
            }
        }

        private void UpdateCrank(CrankStates crankState, bool reversing = false)
        {
            int spinSpeed = 0;
            switch(crankState)
            {
                case CrankStates.WindUp:
                    spinSpeed = 90;
                    break;
                case CrankStates.Static:
                    spinSpeed = 0;
                    break;
                case CrankStates.NormalSpin:
                    spinSpeed = -270;
                    break;
                case CrankStates.FastSpin:
                    spinSpeed = -540;
                    break;
                default:
                    break;
            }
            if (reversing)
            {
                crankBackwardsTime -= Time.deltaTime;
                if (crankBackwardsTime <= 0)
                {
                    this.reversing = false;
                    return;
                }
                spinSpeed *= -5;
            }
            crank.Rotate(Vector3.right, spinSpeed * Time.deltaTime);
        }

        private void UpdateNavMesh()
        {
            if (agent.isOnNavMesh)
            {
                timeSinceOnNavMesh = 0;
            }
            else
            {
                timeSinceOnNavMesh += Time.deltaTime;
                if (timeSinceOnNavMesh > 5)
                {
                    Debug.Log("Tried to fix Boxy Boo's position.");
                    FindAndSetValidNavMeshPosition();
                }
            }
        }

        public void CrankBackwards(PlayerControllerB player)
        {
            if (debugEnemyAI)
            {
                Debug.Log($"Boxy: Tried to alert host to crank request. Is client?: {IsClient}" );
            }
            BoxyBooSendStringCrankRpc("ClientCrank");
        }

        public void CheckAttackArea()
        {
            if (StartOfRound.Instance.localPlayerController.GetComponent<BoxCollider>().bounds.Intersects(attackArea.bounds))
            {
                PlayRandomHitConnectSound();
                if (!debugEnemyAI)
                {
                    StartOfRound.Instance.localPlayerController.DamagePlayer(35, false, true, CauseOfDeath.Mauling);
                }
            }
            else
            {
                PlayRandomSwingSound();
            }
        }

        private void InterpretRpcString(string informationString)
        {
            if (informationString.Contains("Vector3:"))
            {
                informationString = informationString.Replace("Vector3:", "").Trim();
                string[] informationStringList = informationString.Split(",");
                if (informationStringList.Length < 3) { return; }
                landingPosition = new Vector3(float.Parse(informationStringList[0]), float.Parse(informationStringList[1]), float.Parse(informationStringList[2]));
                return;
            }
            if (informationString.Contains("Jumpscare:"))
            {
                if (debugEnemyAI) { Debug.Log("Boxy: Processing Jumpscare clientID"); }
                informationString = informationString.Replace("Jumpscare:", "").Trim();
                ulong clientID = (ulong)int.Parse(informationString);
                creatureAnimator.SetBool("GrabbedTarget", true);
                jumpscare =  StartCoroutine(GrabAndJumpscare(clientID));
                return;
            }
            if (!IsHost && debugEnemyAI)
            {
                Debug.Log("String received from host: " + informationString);
            }
            switch (informationString)
            {
                case "SwitchToBox":
                    SwitchToBox();
                    break;
                case "SwitchToAdvancedSearch":
                    SwitchToFull();
                    SwitchToAdvancedSearch();
                    break;
                case "SwitchToAdvancedChase":
                    SwitchToFull();
                    SwitchToAdvancedChase();
                    break;
                case "SwitchToBasicSearch":
                    SwitchToFull();
                    SwitchToBasicSearch();
                    break;
                case "SwitchToBasicChase":
                    SwitchToFull();
                    SwitchToBasicChase();
                    break;
                case "SwitchToGrab":
                    SwitchToGrab();
                    break;
                case "SwitchToLeap":
                    SwitchToLeap();
                    break;
                case "Walking":
                    creatureAnimator.SetBool("walking", true);
                    break;
                case "Idle":
                    creatureAnimator.SetBool("walking", false);
                    break;
                case "ClientCrank":
                    if (IsHost) { CrankBackwardsHostProcess(); }
                    break;
                case "HostCrankApprove":
                    reversing = true;
                    crankBackwardsTime += maxCrankBackwardsTime;
                    break;
                case "AttackLeft":
                    AttackLeft();
                    break;
                case "AttackRight":
                    AttackRight();
                    break;
                case "FinishArmGrab":
                    creatureAnimator.SetBool("Winding", false);
                    break;
                case "PlayJumpAnimation":
                    creatureAnimator.SetTrigger("JumpHop");
                    if ((IsHost || IsServer) && currentBehaviourStateIndex == (int)BoxyStates.AdvancedChase)
                    {
                        //Prevents Boxy from doing something while still in landing animation.  All of these cooldowns are handled solely on the host.  We process even host actions at the end of a Rpc call.
                        //For example, the host simply calls the RPC methods to switch states, and will eventually reach here.  This will more closely align the time the host does the thing with when the client does it.
                        leapCooldown += 0.65f;
                        grabCooldown += 0.65f;
                        attackCooldown += 0.65f;
                    }
                    break;
                case "SwitchToRetreat":
                    SwitchToRetreat();
                    SwitchToPartial();
                    break;
                case "SetupClientInteractTrigger":
                    SetupStartTrigger();
                    break;
            }
        }

        private void AttackLeft()
        {
            agent.speed = 0;
            agent.velocity = Vector3.zero;
            creatureAnimator.SetTrigger("AttackLeft");
            agent.gameObject.GetComponent<BoxCollider>().isTrigger = false;
        }

        private void AttackRight()
        {
            agent.speed = 0;
            agent.velocity = Vector3.zero;
            creatureAnimator.SetTrigger("AttackRight");
            agent.gameObject.GetComponent<BoxCollider>().isTrigger = false;
        }

        public void ResetAfterAttack()
        {
            agent.gameObject.GetComponent<BoxCollider>().isTrigger = true;
            attacking = false;
            switch (currentBehaviourStateIndex)
            {
                case (int)BoxyStates.AdvancedChase:
                    agent.speed = 6.5f;
                    break;
                case (int)BoxyStates.AdvancedSearch:
                    agent.speed = 5;
                    break;
                case (int)BoxyStates.BasicChase:
                    agent.speed = 6.0f;
                    break;
                case (int)BoxyStates.BasicSearch:
                    agent.speed = 4;
                    break;
                case (int)BoxyStates.Box:
                    agent.speed = 0f;
                    break;
            }
        }

        private void AttackRandom()
        {
            attacking = true;
            int leftOrRight = rng.Next(2); //0 or 1
            if (leftOrRight == 0)
            {
                BoxyBooSendStringClientRcp("AttackLeft");
            }
            else
            {
                BoxyBooSendStringClientRcp("AttackRight");
            }
        }

        private void CrankBackwardsHostProcess()
        {
            if (debugEnemyAI) { Debug.Log($"CrankBackwardsHostProcess called. Host: {IsHost}, Server: {IsServer}, Energy: {energy}"); }
            if (IsHost || IsServer)
            {
                if (box && energy < 22)
                {
                    energy -= 5;
                    if (energy < 0)
                    {
                        energy = 0;
                    }
                    BoxyBooSendStringClientRcp("HostCrankApprove");
                    Debug.Log("Request to uncrank approved.");
                }
                else
                {
                    Debug.Log("Request to uncrank denied.");
                }
            }
        }

        public void FinishGrab()
        {
            numGrabs = 0;
            if (debugEnemyAI) { Debug.Log("Finished Boxy Grab"); }
        }

        private void CheckArmCollision()
        {
            if (checkingArmCollision && IsHost && jumpscareCollision != null)
            {
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                {
                    if (grabbedPlayer == false && !jumpscaring && StartOfRound.Instance.allPlayerScripts[i].GetComponent<BoxCollider>().bounds.Intersects(jumpscareCollision.bounds))
                    {
                        if (debugEnemyAI) { Debug.Log("Boxy: Arm Collided with player."); }
                        jumpscaring = true;
                        grabbedPlayer = true;
                        checkingArmCollision = false;
                        ulong playerID = StartOfRound.Instance.allPlayerScripts[i].actualClientId;
                        SwitchToBehaviourState((int)BoxyStates.Jumpscare);
                        BoxyBooSendStringClientRcp("Jumpscare:" + playerID.ToString());
                    }
                }
            }
        }

        public void SetArmGrabChech(bool value)
        {
            checkingArmCollision = value;
        }

        public void EndJumpscare()
        {
            if (inSpecialAnimationWithPlayer != null)
            {
                inSpecialAnimationWithPlayer.DropBlood(Vector3.forward);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.left);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.right);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.up);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.back);
                inSpecialAnimationWithPlayer.DropBlood(Vector3.down);
                inSpecialAnimationWithPlayer.DamagePlayer(10000, false, true, CauseOfDeath.Unknown, 7);
                inSpecialAnimationWithPlayer.inSpecialInteractAnimation = false;
                inSpecialAnimationWithPlayer.snapToServerPosition = false;
                inSpecialAnimationWithPlayer.disableLookInput = false;
                inSpecialAnimationWithPlayer = null;
            }
            grabbedPlayer = false;
            jumpscaring = false;
            creatureAnimator.SetBool("GrabbedTarget", false);
            targetPlayer = null;
            inSpecialAnimation = false;
            SwitchToBehaviourState((int)BoxyStates.AdvancedSearch);
            BoxyBooSendStringClientRcp("SwitchToAdvancedSearch");
            if (debugEnemyAI) { Debug.Log("Boxy: Ended Jumpscare."); }
        }

        private IEnumerator GrabAndJumpscare(ulong playerID)
        {
            if (debugEnemyAI) { Debug.Log("Boxy: Beginning Jumpscare"); }
            agent.speed = 0;
            agent.velocity = Vector3.zero;
            inSpecialAnimationWithPlayer = StartOfRound.Instance.allPlayerScripts[playerID];
            targetPlayer = inSpecialAnimationWithPlayer;
            if (inSpecialAnimationWithPlayer != null)
            {
                inSpecialAnimation = true;
                inSpecialAnimationWithPlayer.inSpecialInteractAnimation = true;
                inSpecialAnimationWithPlayer.snapToServerPosition = true;
                inSpecialAnimationWithPlayer.disableLookInput = true;
                inSpecialAnimationWithPlayer.DropAllHeldItems();
            }
            bool inArmGrab = true;
            float timeElapsed = 0;
            while (inArmGrab == true && timeElapsed < 2.0f && inSpecialAnimationWithPlayer != null)
            {
                Vector3 diff2 = this.transform.position - targetPlayer.transform.position;
                diff2.y = 0;
                inSpecialAnimationWithPlayer.transform.position = jumpscareAttachPoint.position + Vector3.down;
                inSpecialAnimationWithPlayer.transform.rotation = Quaternion.LookRotation(diff2);
                inSpecialAnimationWithPlayer.ResetFallGravity();
                if (creatureAnimator.GetAnimatorStateName(0, true) == "Boxy_Jumpscare")
                {
                    inArmGrab = false;
                }
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            bool inJumpscare = true;
            timeElapsed = 0f;
            while (inJumpscare && timeElapsed < 1.7f && inSpecialAnimationWithPlayer != null)
            {
                Vector3 diff2 = this.transform.position - targetPlayer.transform.position;
                diff2.y = 0;
                inSpecialAnimationWithPlayer.transform.position = jumpscareAttachPoint.position + Vector3.down;
                inSpecialAnimationWithPlayer.transform.rotation = Quaternion.LookRotation(diff2);
                inSpecialAnimationWithPlayer.ResetFallGravity();
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            //We end jumpscare with animation events.
        }



        [ClientRpc]
        private void BoxyBooSendStringClientRcp(string informationString)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if ((int)__rpc_exec_stage != 2 && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams rpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(1245740165u, rpcParams, 0);
                bool flag = informationString != null;
                bufferWriter.WriteValueSafe(flag, default);
                if (flag)
                {
                    bufferWriter.WriteValueSafe(informationString, false);
                }
                __endSendClientRpc(ref bufferWriter, 1245740165u, rpcParams, 0);
            }
            if ((int)__rpc_exec_stage == 2 && (networkManager.IsClient || networkManager.IsHost || IsServer))
            {
                if (!IsHost && !IsServer)
                {
                    InterpretRpcString(informationString);
                }
                else //Interpret same string differently on host or server if desired but usually not.
                {
                    InterpretRpcString(informationString);
                }
            }
        }

        //Send String information
        private static void __rpc_handler_1245740165(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
                ((BoxyBooAI)target).BoxyBooSendStringClientRcp(valueAsString);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }


        [ServerRpc(RequireOwnership = false)]
        private void BoxyBooSendStringCrankRpc(string informationString)
        {
            NetworkManager networkManager = ((NetworkBehaviour)this).NetworkManager;
            if (networkManager != null && networkManager.IsListening)
            {
                if ((int)((NetworkBehaviour)this).__rpc_exec_stage != 1 && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams rpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = ((NetworkBehaviour)this).__beginSendServerRpc(1245549521u, rpcParams, (RpcDelivery)0);
                    bool hasString = informationString != null;
                    bufferWriter.WriteValueSafe(hasString);
                    if (hasString)
                    {
                        bufferWriter.WriteValueSafe(informationString);
                    }

                    ((NetworkBehaviour)this).__endSendServerRpc(ref bufferWriter, 1245549521u, rpcParams, (RpcDelivery)0);
                }
                if ((int)((NetworkBehaviour)this).__rpc_exec_stage == 1 && (networkManager.IsServer || networkManager.IsHost))
                {
                    if (debugEnemyAI)
                    {
                        Debug.Log($"[Server] Processing on server with information: {informationString}");
                    }
                    InterpretClientCrank(informationString);
                }
            }
        }

        private void InterpretClientCrank(string informationString)
        {
            if (debugEnemyAI) { Debug.Log($"InterpretClientCrank tried to interpret the crank received.  IsClient? {IsClient}"); }
            switch (informationString)
            {
                case "ClientCrank":
                    if (IsHost) 
                    { 
                        if (debugEnemyAI) { Debug.Log($"InterpretClientCrank tried to interpret the crank received.  IsClient? {IsClient}"); }
                        CrankBackwardsHostProcess(); 
                    }
                    break;
            }
        }

        //Send String information
        private static void __rpc_handler_1245549521(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
                target.__rpc_exec_stage = (__RpcExecStage)1;
                ((BoxyBooAI)target).BoxyBooSendStringCrankRpc(valueAsString);
                target.__rpc_exec_stage = (__RpcExecStage)0;
            }
        }

        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_BoxyBooAI()
        {
            __rpc_func_table.Add(1245740165u, new RpcReceiveHandler(__rpc_handler_1245740165));
            __rpc_func_table.Add(1245549521u, new RpcReceiveHandler(__rpc_handler_1245549521));
        }

        public void PlayRandomWalkSound()
        {
            if (walkSounds != null && creatureSFX != null)
            {
                RoundManager.PlayRandomClip(creatureSFX, walkSounds, true, 3);
            }
        }

        public void PlayRandomSwingSound()
        {
            if (attackSounds != null && hitConnectAudio != null)
            {
                RoundManager.PlayRandomClip(hitConnectAudio, attackSounds, true, 2);
            }
        }

        public void PlayRandomHitConnectSound()
        {
            if (hitConnectSounds != null && hitConnectAudio != null)
            {
                RoundManager.PlayRandomClip(hitConnectAudio, hitConnectSounds, true, 2);
            }
        }

        public void PlayRandomGrabReadySound()
        {
            if (grabReadySounds != null && grabAudioPoint != null)
            {
                RoundManager.PlayRandomClip(grabAudioPoint, grabReadySounds, true, 2);
            }
        }

        public void PlayRandomGrabLaunchSound()
        {
            if (grabLaunchSounds != null && grabAudioPoint != null)
            {
                RoundManager.PlayRandomClip(grabAudioPoint, grabLaunchSounds, true, 3);
            }
        }

        public void PlayRandomBoxLandSound()
        {
            if (boxLandSounds != null && creatureSFX != null)
            {
                RoundManager.PlayRandomClip(creatureSFX, boxLandSounds, true, 1);
            }
        }

        public void PlayRandomLandingSound()
        {
            if (landSounds != null && creatureSFX != null)
            {
                RoundManager.PlayRandomClip(creatureSFX, landSounds, true, 1);
            }
        }

        public void PlayRandomCrankSound()
        {
            if (crankSounds != null && crankAudio != null)
            {
                RoundManager.PlayRandomClip(crankAudio, crankSounds, true, 1);
            }
        }

        public void PlayRandomLeapSound()
        {
            if (leapSounds != null && jumpAudio != null)
            {
                RoundManager.PlayRandomClip(jumpAudio, leapSounds, true, 4);
            }
        }

        public void PlayRandomFullPopSound()
        {
            if (popSounds != null && creatureSFX != null)
            {
                RoundManager.PlayRandomClip(creatureSFX, popSounds, true, 3);
            }
        }

        public void PlayRandomCreakSound()
        {
            if (creakSounds != null && creakAudio != null)
            {
                RoundManager.PlayRandomClip(creakAudio, creakSounds, true, 3f);
            }
        }

        public void PlayRandomJumpscareSound()
        {
            if (jumpscareSounds != null && hitConnectAudio != null)
            {
                RoundManager.PlayRandomClip(hitConnectAudio, jumpscareSounds, true, 3f);
            }
        }

        public void PlayRandomPartialRetractSound()
        {
            if (partialRetractSounds != null && retractAudio != null)
            {
                RoundManager.PlayRandomClip(retractAudio, partialRetractSounds, true, 2.5f);
            }
        }

        public void PlayRandomFullRetractSound()
        {
            if (fullRetractSounds != null && retractAudio != null)
            {
                RoundManager.PlayRandomClip(retractAudio, fullRetractSounds, true, 2.5f);
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (currentBehaviourStateIndex == (int)BoxyStates.Retreat || currentBehaviourStateIndex == (int)BoxyStates.Box)
            {
                return;
            }
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            trackedDamage += force;
            if (IsHost && trackedDamage > maxTrackedDamaged)
            {
                shouldEnterRetreat = true;
            }
        }

    }
}
