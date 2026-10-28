using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;

namespace WorkshopDemo.Editor
{
    [InitializeOnLoad]
    public static class WorkshopVerification
    {
        static int stage = -1;
        static double deadline;
        static WorkshopSimulation simulation;
        static Vector3 packagePosition;
        static Quaternion rotorRotation;

        static WorkshopVerification()
        {
            EditorApplication.update += Tick;
            if (SessionState.GetBool("Workshop.Verify", false))
            {
                stage = 0;
                deadline = EditorApplication.timeSinceStartup + 90;
            }
        }

        public static void Begin()
        {
            File.WriteAllText("Evidence/runtime-verification.txt", "Started " + DateTime.Now.ToString("s") + "\n");
            stage = 0;
            SessionState.SetBool("Workshop.Verify", true);
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.isPlaying = true;
        }

        static void Check(bool condition, string message)
        {
            File.AppendAllText("Evidence/runtime-verification.txt", (condition ? "PASS " : "FAIL ") + message + "\n");
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Tick()
        {
            if (stage < 0 || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Verification stage " + stage);
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (stage == 0)
                {
                    simulation = UnityEngine.Object.FindFirstObjectByType<WorkshopSimulation>();
                    if (!simulation || simulation.Elapsed < 2) return;
                    Check(simulation.devices.Length == 8 && simulation.RunningCount == 8, "Eight devices running");
                    foreach (var d in simulation.devices)
                    {
                        simulation.Select(d);
                        Check(simulation.Selected == d && d.selectionRing.activeSelf, "Select " + d.deviceId);
                        Check(NavMesh.SamplePosition(d.inspectionPoint, out var hit, 1, NavMesh.AllAreas) &&
                            simulation.inspector.Agent.CalculatePath(hit.position, new NavMeshPath()), "Inspection point " + d.deviceId);
                    }
                    Check(simulation.devices[0].movingPackages?.Length > 0, "Downloaded conveyor packages");
                    var fan = simulation.devices.First(d => d.kind == DeviceKind.Fan);
                    Check(fan.rotor, "Downloaded fan rotor");
                    packagePosition = simulation.devices[0].movingPackages[0].position;
                    rotorRotation = fan.rotor.rotation;
                    simulation.cameraRig.Overview();
                    deadline = EditorApplication.timeSinceStartup + 10;
                    stage = 1;
                }
                else if (stage == 1)
                {
                    if (simulation.Elapsed < 4) return;
                    Check(Vector3.Distance(packagePosition, simulation.devices[0].movingPackages[0].position) > .01f, "Conveyor animation");
                    Check(Quaternion.Angle(rotorRotation, simulation.devices.First(d => d.kind == DeviceKind.Fan).rotor.rotation) > .1f, "Fan animation");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/demo-overview.png"));
                    simulation.TriggerFault();
                    simulation.TriggerFault();
                    Check(simulation.OpenAlarmCount == 1 && simulation.RunningCount == 7, "Fault and duplicate protection");
                    simulation.Resolve();
                    Check(simulation.Records.Count == 0, "Cannot resolve before arrival");
                    simulation.Dispatch();
                    simulation.Dispatch();
                    Check(simulation.inspector.IsBusy && simulation.ActiveAlarm.progress == "巡检途中", "Navigation dispatched once");
                    deadline = EditorApplication.timeSinceStartup + 90;
                    stage = 2;
                }
                else if (stage == 2)
                {
                    if (!simulation.inspector.HasArrived) return;
                    Check(simulation.CanResolve && simulation.ActiveAlarm.progress == "待处理", "NavMesh arrival");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/demo-fault.png"));
                    simulation.Resolve();
                    simulation.Resolve();
                    Check(simulation.Records.Count == 1 && simulation.OpenAlarmCount == 0 && simulation.RunningCount == 8, "Resolve, recover, one record");
                    simulation.ResetDemo();
                    simulation.ResetDemo();
                    Check(simulation.Alarms.Count == 0 && simulation.Records.Count == 0 &&
                        !simulation.inspector.IsBusy && simulation.RunningCount == 8, "Two consecutive resets");
                    simulation.TriggerFault();
                    simulation.Dispatch();
                    Check(simulation.inspector.IsBusy, "Dispatch again after reset");
                    deadline = EditorApplication.timeSinceStartup + 90;
                    stage = 3;
                }
                else if (stage == 3 && simulation.inspector.HasArrived)
                {
                    simulation.Resolve();
                    Check(simulation.Records.Count == 1, "Second full cycle");
                    simulation.ResetDemo();
                    File.AppendAllText("Evidence/runtime-verification.txt", "COMPLETE\n");
                    stage = -1;
                    SessionState.SetBool("Workshop.Verify", false);
                }
            }
            catch (Exception e)
            {
                File.AppendAllText("Evidence/runtime-verification.txt", "FAILED " + e + "\n");
                stage = -1;
                SessionState.SetBool("Workshop.Verify", false);
                Debug.LogException(e);
            }
        }

        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Build/WorkshopDemo");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { WorkshopFoundationBuilder.DemoScene },
                locationPathName = "Build/WorkshopDemo/WorkshopDemo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            File.WriteAllText("Evidence/player-build.txt", result.summary.result +
                "\nErrors: " + result.summary.totalErrors + "\nSize: " + result.summary.totalSize);
        }
    }
}
