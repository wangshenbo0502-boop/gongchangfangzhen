using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

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
        public bool ManualControl { get; private set; }
        public float RemainingDistance => Agent && Agent.isOnNavMesh && IsBusy ? Agent.remainingDistance : 0;
        Vector3 spawn;
        Quaternion spawnRotation;
        float lineTimer;
        DistributionCenter.wheelRotation wheels;

        void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            wheels = GetComponentInChildren<DistributionCenter.wheelRotation>(true);
            if (route) route.positionCount = 0;
        }

        void Start()
        {
            // The scene's NavMeshSurface registers its data before Start.
            Agent.enabled = true;
            if (!Agent.isOnNavMesh) Debug.LogError("Inspection robot has no walkable NavMesh.", this);
        }

        public bool Dispatch(WorkshopAlarm alarm)
        {
            if (IsBusy || !Agent.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(alarm.device.inspectionPoint, out var hit, 1, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            ManualControl = false;
            Agent.updateRotation = true;
            TargetAlarm = alarm;
            HasArrived = false;
            Agent.isStopped = false;
            Agent.SetPath(path);
            DrawRoute();
            return true;
        }

        public void SetManualControl(bool manual)
        {
            if (ManualControl == manual || !Agent || !Agent.isOnNavMesh) return;
            if (manual)
            {
                Agent.isStopped = true;
                Agent.ResetPath();
                Agent.velocity = Vector3.zero;
                Agent.updateRotation = false;
                route.positionCount = 0;
            }
            else if (IsBusy && !HasArrived)
            {
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(TargetAlarm.device.inspectionPoint, out var hit, 1, NavMesh.AllAreas) ||
                    !Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    simulation.Notify("无法恢复巡检路线，请驶回通道后重试");
                    return;
                }
                Agent.updateRotation = true;
                Agent.isStopped = false;
                Agent.SetPath(path);
            }
            else Agent.updateRotation = true;
            ManualControl = manual;
            if (IsBusy) TargetAlarm.progress = HasArrived ? "待处理" : manual ? "手动接管" : "巡检途中";
            simulation.Notify(manual ? "已切换手动驾驶" : IsBusy ? "自动巡检已恢复" : "已切换自动巡检");
        }

        void Update()
        {
            if (!Agent || !Agent.isOnNavMesh) return;
            float move = 0, turn = 0;
            if (ManualControl)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    move = (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0);
                    turn = (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0);
                }
                transform.Rotate(Vector3.up, turn * 90 * Time.deltaTime);
                if (move != 0) Agent.Move(transform.forward * (move * 3.5f * Time.deltaTime));
                if (IsBusy)
                {
                    bool near = Vector3.Distance(transform.position, TargetAlarm.device.inspectionPoint) <= Agent.stoppingDistance + .22f;
                    if (HasArrived && !near)
                    {
                        HasArrived = false;
                        TargetAlarm.progress = "手动接管";
                        simulation.Notify("已离开巡检点");
                    }
                    else if (!HasArrived && near)
                    {
                        HasArrived = true;
                        simulation.InspectorArrived();
                    }
                }
            }
            if (wheels)
            {
                wheels.moveDirection = ManualControl ? move :
                    Agent.velocity.sqrMagnitude > .01f ? 1 : 0;
                wheels.turnDirection = turn;
            }
            bool walking = IsBusy && !HasArrived && Agent.velocity.sqrMagnitude > .02f;
            float swing = walking ? Mathf.Sin(Time.time * 9) * 25 : 0;
            if (leftLeg) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            if (rightLeg) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            if (leftArm) leftArm.localRotation = Quaternion.Euler(-swing * .6f, 0, 0);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(swing * .6f, 0, 0);
            if (!IsBusy || HasArrived || ManualControl) return;
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
            if (Agent && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); }
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
            ManualControl = false;
            if (Agent) Agent.updateRotation = true;
            Complete();
        }
    }
}
