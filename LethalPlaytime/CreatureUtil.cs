using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LethalPlaytime
{
    internal static class CreatureUtil
    {
        /*
         * Given a creature's AI script, it will adjust the angle the creature is at based on the slope of the angle they are standing on.
         * All it will do is set a new target rotation that the creature will interpolate between every update when adjustment is needed.
         */
        public static void AdjustCreatureRotationWithSlope(EnemyAI monster)
        {
            switch (monster) 
            {
                case DogdayAI dogdayAI:
/*                    MakeForwardAngleAdjustment(dogdayAI, dogdayAI.targetForwardAngle);*/
                    break;
                case HuggyAI huggyAI: //TO-DO IF WORKS
                    break;
                case MissDelightAI missDelightAI: //TO-DO IF WORKS
                    break;
            }
            
        }

        private static void MakeForwardAngleAdjustment(EnemyAI monster, float targetForwardAngle)
        {
            float angleDifference = Mathf.DeltaAngle(monster.transform.rotation.eulerAngles.x, targetForwardAngle);
            if (angleDifference < 1)
            {
                return;
            }
            Quaternion targetRotation = Quaternion.Euler(targetForwardAngle, monster.transform.rotation.eulerAngles.y, 0);
            monster.transform.rotation = Quaternion.Slerp(monster.transform.rotation, targetRotation, Time.deltaTime * 2f);
        }

        public static void CalculateForwardTargetRotation(EnemyAI monster)
        {
            RaycastHit hit;
            
            if (Physics.Raycast(new Vector3(monster.transform.position.x, monster.transform.position.y + 0.5f, monster.transform.position.z), Vector3.down, out hit))
            {
                Vector3 surfaceNormal = hit.normal;
                float slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);
                float targetAngle = Mathf.Clamp(slopeAngle, -15f, 15f); // Limit the angle to 15 degrees

                switch (monster)
                {
                    case DogdayAI dogdayAI:
/*                        dogdayAI.targetForwardAngle = targetAngle;*/
                        break;
                }
            }
        }
    }
}
