using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LethalPlaytime
{
    [HarmonyPatch(typeof(DoorLock))]
    [HarmonyPatch("OnTriggerStay")]
    internal class DoorLockMissDelight
    {
        private static Dictionary<DoorLock, bool> interactionTable = new Dictionary<DoorLock, bool>();

        [HarmonyPostfix]
        private static void OnTriggerStay(DoorLock __instance, Collider other)
        {
            if (!interactionTable.TryGetValue(__instance, out bool inInteraction))
            {
                inInteraction = false;
                interactionTable[__instance] = inInteraction;
            }
            if (__instance.isLocked || __instance.isDoorOpened || !__instance.IsServer || interactionTable[__instance] == true)
            {
                return;
            }

            GameObject parent = other.gameObject.transform.parent.gameObject;
            
            if (parent != null)
            {
                if (__instance.gameObject.transform.parent.transform.parent.transform.parent == null)
                {
                    return;
                }
                MissDelightAI enemyScript = parent.GetComponent<MissDelightAI>();
                if (enemyScript != null && !enemyScript.doorTrigger)
                {
                    if (enemyScript.targetPlayer != null)
                    {
                        //If delight is closer to player than door, she should ignore the door.
                        float distToDoor = Vector3.Distance(__instance.gameObject.transform.parent.transform.position, parent.transform.position);
                        float distToPlayer = Vector3.Distance(parent.transform.position, enemyScript.targetPlayer.transform.position);
                        if (distToPlayer < distToDoor)
                        {
                            return;
                        }
                        //If the door is not in between Miss Delight and the player, it should be ignored.
                        if (!IsBetween(parent.transform, enemyScript.targetPlayer.transform, __instance.gameObject.transform.parent.transform))
                        {
                            return;
                        }
                    }
                    //If the door swings open towards Miss Delight, it makes no sense to kick it open.
                    if (Vector3.Dot(__instance.gameObject.transform.parent.transform.parent.transform.parent.transform.forward, __instance.gameObject.transform.parent.transform.parent.transform.parent.transform.position - parent.transform.position) > 0)
                    {
                        return;
                    }
                    //Prevents additional Miss Delights to kick open the door at the same time.
                    interactionTable[__instance] = true;
                    enemyScript.HandleDoorTrigger();
                    enemyScript.doorTrigger = true;
                    // make delight face the door.
                    Vector3 diff = __instance.gameObject.transform.parent.transform.parent.transform.parent.transform.position - parent.gameObject.transform.position;
                    diff.y = 0;
                    parent.transform.rotation = Quaternion.LookRotation(diff);
                    //Door can be kicked open again after a reset.
                    __instance.StartCoroutine(ResetInteractionStateAfterDelay(__instance ,1.5f));
                }
            }
        }

        private static IEnumerator ResetInteractionStateAfterDelay(DoorLock instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            interactionTable[instance] = false;
        }

        private static bool IsBetween(Transform me, Transform target, Transform obstacle)
        {
            Vector3 toTarget = target.position - me.position;
            Vector3 toObstacle = obstacle.position - me.position;

            return Vector3.Dot(toTarget.normalized, toObstacle.normalized) > 0 && toObstacle.magnitude < toTarget.magnitude;
        }
    }
}