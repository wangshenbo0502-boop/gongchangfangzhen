using UnityEngine;
using UnityEngine.AI;

namespace WorkshopDemo
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WorkshopInspector : MonoBehaviour
    {
        public WorkshopSimulation simulation;
        public LineRenderer route;
        public Transform leftLeg;
        public Transform rightLeg;
        public Transform leftArm;
        public Transform rightArm;
        public NavMeshAgent Agent { get; private set; }
        public WorkshopAlarm TargetAlarm { get; private set; }
        public bool IsBusy => TargetAlarm != null;
        public bool HasArrived { get; private set; }
        public float RemainingDistance => Agent && Agent.isOnNavMesh && IsBusy ? Agent.remainingDistance : 0;
        Vector3 spawn;
        Quaternion spawnRotation;
        float lineTimer;

        void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            if (route) route.positionCount = 0;
        }

        public bool Dispatch(WorkshopAlarm alarm)
        {
            if (IsBusy || !Agent.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(alarm.device.inspectionPoint, out var hit, 1, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            TargetAlarm = alarm;
            HasArrived = false;
            Agent.isStopped = false;
            Agent.SetPath(path);
            DrawRoute();
            return true;
        }

        void Update()
        {
            if (!Agent || !Agent.isOnNavMesh) return;
            bool walking = IsBusy && !HasArrived && Agent.velocity.sqrMagnitude > .02f;
            float swing = walking ? Mathf.Sin(Time.time * 9) * 25 : 0;
            if (leftLeg) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            if (rightLeg) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            if (leftArm) leftArm.localRotation = Quaternion.Euler(-swing * .6f, 0, 0);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(swing * .6f, 0, 0);
            if (!IsBusy || HasArrived) return;
            lineTimer += Time.deltaTime;
            if (lineTimer > .15f) { lineTimer = 0; DrawRoute(); }
            if (!Agent.pathPending && Agent.pathStatus == NavMeshPathStatus.PathComplete &&
                Vector3.Distance(transform.position, Agent.destination) <= Agent.stoppingDistance + .12f)
            {
                HasArrived = true;
                Agent.isStopped = true;
                route.positionCount = 0;
                transform.rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(TargetAlarm.device.transform.position - transform.position, Vector3.up));
                simulation.InspectorArrived();
            }
        }

        void DrawRoute()
        {
            var corners = Agent.path.corners;
            route.positionCount = corners.Length + (corners.Length > 0 ? 1 : 0);
            if (corners.Length == 0) return;
            route.SetPosition(0, transform.position + Vector3.up * .08f);
            for (int i = 0; i < corners.Length; i++)
                route.SetPosition(i + 1, corners[i] + Vector3.up * .08f);
        }

        public void Complete()
        {
            TargetAlarm = null;
            HasArrived = false;
            route.positionCount = 0;
        }

        public void ResetInspector()
        {
            if (Agent && Agent.isOnNavMesh)
            {
                Agent.isStopped = true;
                Agent.ResetPath();
                Agent.Warp(spawn);
            }
            transform.SetPositionAndRotation(spawn, spawnRotation);
            Complete();
        }
    }
}
