using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BelobogOptimization
{
    public sealed class BelobogTraversalVerification : MonoBehaviour
    {
        public bool runInEditor;
        public Vector3 mapOffset;
        [Serializable] public class Check { public string name; public bool passed; public float travelled, fallDistance, finalY; public string detail; }
        [Serializable] public class Report { public List<Check> checks=new(); public bool passed; }
        IEnumerator Start()
        {
            bool standalone=Environment.GetCommandLineArgs().Contains("--belobog-traversal");
            if(!standalone&&!runInEditor)yield break;
            Application.runInBackground=true;
            yield return new WaitForSeconds(2);
            var p=FindFirstObjectByType<PlayerController>();var rb=p.GetComponent<Rigidbody>();
            var ground=p.GetComponent<GroundCheck>();
            var report=new Report();
            // Drive the existing SetMove API at fixed updates, isolating movement from physical keyboard input.
            p.enabled=false;
            var starts=new[]{new Vector3(-10,1,0),new Vector3(70,1,5),new Vector3(-10,1,20),new Vector3(-10,3,32),new Vector3(-40,21,-90),new Vector3(-67,1,-3)};
            var dirs=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.back,Vector3.forward,Vector3.forward};
            var names=new[]{"PlazaSprintAndSeams","StreetRun","StairsUp","StairsDown","RoofLandingAndWalk","NarrowPassage"};
            for(int i=0;i<starts.Length;i++)
            {
                var at=mapOffset+starts[i];
                var hits=Physics.RaycastAll(at+Vector3.up*2,Vector3.down,6,1<<7,QueryTriggerInteraction.Ignore);
                if(hits.Length==0){report.checks.Add(new Check{name=names[i],passed=false,detail="No walkable support near start"});continue;}
                var hit=hits.OrderBy(h=>h.distance).First();
                rb.position=new Vector3(at.x,hit.point.y+.08f,at.z);rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                yield return new WaitForSeconds(1);
                var begin=rb.position;float minY=begin.y;int groundedFrames=0,total=0;
                float duration=i==2||i==3?4:3;
                for(float t=0;t<duration;t+=Time.fixedDeltaTime)
                {
                    p.SetMove(dirs[i],i==0?5:2.5f);
                    yield return new WaitForFixedUpdate();
                    minY=Mathf.Min(minY,rb.position.y);if(ground.isGrounded())groundedFrames++;total++;
                }
                rb.linearVelocity=Vector3.zero;
                float travelled=Vector3.Dot(rb.position-begin,dirs[i]);
                report.checks.Add(new Check{name=names[i],travelled=travelled,fallDistance=begin.y-minY,finalY=rb.position.y,
                    passed=travelled>duration*(i==0?5:2.5f)*.65f&&begin.y-minY<3&&groundedFrames>total*.6f,
                    detail="Grounded fixed frames "+groundedFrames+"/"+total});
            }
            // Verify jump and landing on the real plaza using the configured impulse and Rigidbody.
            rb.position=mapOffset+new Vector3(-10,.2f,5);rb.linearVelocity=Vector3.zero;
            yield return new WaitForSeconds(1);float baseY=rb.position.y,maxY=baseY;
            rb.AddForce(Vector3.up*p.playerMovementConfigSO.JumpForce,ForceMode.Impulse);
            for(float t=0;t<3;t+=Time.fixedDeltaTime){yield return new WaitForFixedUpdate();maxY=Mathf.Max(maxY,rb.position.y);}
            report.checks.Add(new Check{name="JumpAndLanding",passed=maxY-baseY>.15f&&Mathf.Abs(rb.position.y-baseY)<.15f&&ground.isGrounded(),finalY=rb.position.y,detail="Jump height "+(maxY-baseY)});
            // Approach a measured wall with the real player body; do not accept tunnelling as movement.
            rb.position=mapOffset+new Vector3(60,.15f,5);rb.linearVelocity=Vector3.zero;
            yield return new WaitForSeconds(1);var wallStart=rb.position;
            bool wallFound=Physics.SphereCast(wallStart+Vector3.up*.8f,.28f,Vector3.forward,out var wallHit,12,(1<<3)|(1<<7),QueryTriggerInteraction.Ignore);
            for(float t=0;t<3;t+=Time.fixedDeltaTime){p.SetMove(Vector3.forward,5);yield return new WaitForFixedUpdate();}
            float wallTravel=Vector3.Dot(rb.position-wallStart,Vector3.forward);
            report.checks.Add(new Check{name="WallBlocksSprint",passed=wallFound&&wallTravel<=wallHit.distance+.5f&&wallTravel>1,travelled=wallTravel,detail="Measured wall distance "+wallHit.distance});
            // Exit the measured recessed wall opening before turning; side walls are only 1.5m away.
            rb.linearVelocity=Vector3.zero;
            for(float t=0;t<1.5f;t+=Time.fixedDeltaTime){p.SetMove(Vector3.back,2.5f);yield return new WaitForFixedUpdate();}
            var cornerStart=rb.position;
            for(float t=0;t<2;t+=Time.fixedDeltaTime){p.SetMove(Vector3.right,2.5f);yield return new WaitForFixedUpdate();}
            float cornerTravel=Vector3.Dot(rb.position-cornerStart,Vector3.right);
            report.checks.Add(new Check{name="WallCornerTurn",passed=cornerTravel>2.5f&&ground.isGrounded()&&Mathf.Abs(rb.position.y-cornerStart.y)<1,travelled=cornerTravel,finalY=rb.position.y});
            rb.linearVelocity=Vector3.zero;rb.position=new Vector3(.69f,.15f,0);
            yield return new WaitForSeconds(2);
            report.checks.Add(new Check{name="SpawnStability",passed=ground.isGrounded()&&Mathf.Abs(rb.position.y)<.2f,finalY=rb.position.y});
            report.passed=report.checks.All(c=>c.passed);
            string output=Path.Combine(Application.persistentDataPath,"BelobogTraversal.json");
            var args=Environment.GetCommandLineArgs();int ri=Array.IndexOf(args,"--report-dir");if(ri>=0&&ri+1<args.Length)output=Path.Combine(args[ri+1],"BelobogTraversal.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log("BELOBOG_TRAVERSAL "+JsonUtility.ToJson(report));
            if(standalone)Application.Quit();
        }
    }
}
