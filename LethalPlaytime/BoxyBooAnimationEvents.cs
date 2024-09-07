using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LethalPlaytime
{
    public class BoxyBooAnimationEvents : MonoBehaviour
        
    {
        public BoxyBooAI scriptReference;

        public void PlayRandomWalkSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomWalkSound();
            }
        }

        public void PlayRandomLandingSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomLandingSound();
            }
        }

        public void PlayRandomBoxLandSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomBoxLandSound();
            }
        }

        public void CheckAttackArea()
        {
            if (scriptReference != null)
            {
                scriptReference.CheckAttackArea();
            }
        }

        public void PlayRandomGrabLaunchSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomGrabLaunchSound();
            }
        }

        public void ResetAfterAttack()
        {
            if (scriptReference != null)
            {
                scriptReference.ResetAfterAttack();
            }
        }

        public void PlayRandomFullPopSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomFullPopSound();
            }
        }

        public void PlayRandomGrabReadySound()
        {
            if(scriptReference != null)
            {
                scriptReference.PlayRandomGrabReadySound();
            }
        }

        public void PlayRandomJumpscareSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomJumpscareSound();
            }
        }

        public void FinishGrab()
        {
            if (scriptReference != null)
            {
                scriptReference.FinishGrab();
            }
        }
        public void EndJumpscare()
        {
            if (scriptReference != null)
            {
                scriptReference.EndJumpscare();
            }
        }

        public void SetArmGrabCheck(int value)
        {
            if (scriptReference != null)
            {
                if (value == 2)
                {
                    scriptReference.SetArmGrabChech(false);
                }
                else
                {
                    scriptReference.SetArmGrabChech(true);
                }
                
            }
        }
    }
}
