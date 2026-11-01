using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace DistributionCenter
{
    [System.Serializable]
    public class TargetGroup
    {
        public GameObject[] targets;
    }

    public class RandomAgentMovement : MonoBehaviour
    {
        public NavMeshAgent[] navAgentArr;
        public TargetGroup[] agentTargetsArr;
        public bool[] agentEnabledArr;

        public Button startStopBTN;
        public Text startStopBTNText;

        public float minWaitTime = 1f;
        public float maxWaitTime = 3f;
        public float slowDownDistance = 2f;
        public float minSpeedFactor = 0.2f;

        [Header("Button Animation")]
        public float buttonAnimSpeed = 2f;

        private Dictionary<NavMeshAgent, Queue<int>> agentRecentTargets = new Dictionary<NavMeshAgent, Queue<int>>();
        // Retain full speeds across Play-mode script reloads, even while an agent is slowing down.
        [SerializeField, HideInInspector] private float[] agentMaxSpeeds;
        private Dictionary<NavMeshAgent, Coroutine> movementCoroutines = new Dictionary<NavMeshAgent, Coroutine>();

        private bool isAgent0Running = false;
        public bool IsAutoRunning => isAgent0Running;

        void OnEnable()
        {
            if (agentMaxSpeeds == null || agentMaxSpeeds.Length != navAgentArr.Length)
            {
                agentMaxSpeeds = new float[navAgentArr.Length];
                for (int i = 0; i < navAgentArr.Length; i++)
                    if (navAgentArr[i]) agentMaxSpeeds[i] = navAgentArr[i].speed;
            }
            if (startStopBTN) startStopBTN.onClick.AddListener(ToggleAgent0);
            UpdateButtonUI();
        }

        void OnDisable()
        {
            if (startStopBTN) startStopBTN.onClick.RemoveListener(ToggleAgent0);
            StopAllCoroutines();
            movementCoroutines.Clear();
        }

        void Start()
        {
            for (int i = 0; i < navAgentArr.Length; i++)
            {
                var agent = navAgentArr[i];

                if (agentEnabledArr.Length <= i || !agentEnabledArr[i])
                {
                    if (agent != null)
                        agent.gameObject.SetActive(false);
                    continue;
                }

                if (!agent) continue;
                agent.autoBraking = false;
                agentRecentTargets[agent] = new Queue<int>();

                if (i != 0)
                    AssignRandomTarget(agent, i);
            }
        }

        void Update()
        {
            for (int i = 0; i < navAgentArr.Length; i++)
            {
                if (agentEnabledArr.Length <= i || !agentEnabledArr[i])
                    continue;

                var agent = navAgentArr[i];
                if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
                    continue;

                if (i == 0 && !isAgent0Running)
                    continue;

                if (agent.pathPending)
                    continue;

                float remainingDistance = agent.remainingDistance;

                if (remainingDistance <= slowDownDistance)
                {
                    float speedFactor = Mathf.Lerp(minSpeedFactor, 1f, remainingDistance / slowDownDistance);
                    agent.speed = agentMaxSpeeds[i] * speedFactor;
                }
                else
                {
                    agent.speed = agentMaxSpeeds[i];
                }

                if (remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f))
                {
                    if (!movementCoroutines.ContainsKey(agent) || movementCoroutines[agent] == null)
                    {
                        movementCoroutines[agent] = StartCoroutine(WaitAndAssignTarget(agent, i));
                    }
                }
            }
        }

        IEnumerator WaitAndAssignTarget(NavMeshAgent agent, int agentIndex)
        {
            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitTime);
            AssignRandomTarget(agent, agentIndex);
            movementCoroutines[agent] = null;
        }

        public void AssignRandomTarget(NavMeshAgent agent, int agentIndex)
        {
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            if (agentTargetsArr == null || agentIndex >= agentTargetsArr.Length)
                return;

            GameObject[] agentTargets = agentTargetsArr[agentIndex].targets;
            if (agentTargets == null || agentTargets.Length == 0)
                return;

            if (!agentRecentTargets.ContainsKey(agent))
                agentRecentTargets[agent] = new Queue<int>();

            Queue<int> recentTargets = agentRecentTargets[agent];
            int newIndex;

            do
            {
                newIndex = Random.Range(0, agentTargets.Length);
            }
            while (recentTargets.Contains(newIndex) && recentTargets.Count < agentTargets.Length);

            recentTargets.Enqueue(newIndex);
            if (recentTargets.Count > 2)
                recentTargets.Dequeue();

            agent.SetDestination(agentTargets[newIndex].transform.position);
        }

        public void ForceAssignNewTarget(NavMeshAgent agent, int agentIndex)
        {
            if (movementCoroutines.ContainsKey(agent) && movementCoroutines[agent] != null)
            {
                StopCoroutine(movementCoroutines[agent]);
                movementCoroutines[agent] = null;
            }

            AssignRandomTarget(agent, agentIndex);
        }

        void ToggleAgent0()
        {
            if (agentEnabledArr.Length == 0 || !agentEnabledArr[0])
                return;

            var agent = navAgentArr[0];
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            isAgent0Running = !isAgent0Running;

            if (!isAgent0Running)
            {
                agent.ResetPath();
                if (movementCoroutines.ContainsKey(agent) && movementCoroutines[agent] != null)
                {
                    StopCoroutine(movementCoroutines[agent]);
                    movementCoroutines[agent] = null;
                }

            }
            else
            {
                AssignRandomTarget(agent, 0);
            }

            UpdateButtonUI();
        }

        void UpdateButtonUI()
        {
            if (startStopBTNText != null)
                startStopBTNText.text = isAgent0Running ? "停止自动运行" : "启动自动运行";

            if (startStopBTN != null)
            {
                ColorBlock cb = startStopBTN.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color32(255, 244, 211, 255);
                cb.pressedColor = new Color32(205, 192, 153, 255);
                cb.selectedColor = Color.white;
                startStopBTN.colors = cb;
            }
        }
    }
}
