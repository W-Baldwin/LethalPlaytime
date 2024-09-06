using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LethalPlaytime
{
    public class DogdayAnimationEvents : MonoBehaviour
        
    {
        public DogdayAI scriptReference;
        public void ResetAfterAttack()
        {
            if (scriptReference != null)
            {
                scriptReference.ResetAfterAttack();
            }
        }

        public void ResetFlinchLayer()
        {
            if (scriptReference != null)
            {
                scriptReference.ResetFlinchLayer();
            }
        }

        public void PlayRandomWalkSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomWalkSound();
            }
        }

        public void CheckAttackCollision()
        {
            if (scriptReference != null)
            {
                scriptReference.CheckAttackCollision();
            }
        }

        public void PlayRandomHitSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomHitSound();
            }
        }

        public void PlayRandomAttackSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomAttackSound();
            }
        }
    }
}
