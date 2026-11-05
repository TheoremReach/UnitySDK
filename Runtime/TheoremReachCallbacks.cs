using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

// Hears the native SDKs, which send every callback by name to this GameObject, kept across scenes, and passes each one
// on to the game's listener GameObject, which TheoremReach.SetListener names.
//
// The SDKs confirm a reward with TheoremReach as soon as they've sent it, and Unity drops a message to a GameObject that
// isn't there. So a reward waits here until a listener with an OnReward method is in the scene: one whose scene hasn't
// loaded yet or has unloaded, or one SetListener hasn't named yet. The wait is in memory, so a reward still waiting when
// the game ends is lost. A waiting reward is only ever the current user's: starting the SDK for another user drops it.
// The other callbacks aren't held: a listener that isn't there misses them.
[AddComponentMenu("")]
[Preserve]
internal sealed class TheoremReachCallbacks : MonoBehaviour
{
    internal const string ObjectName = "TheoremReachCallbacks";

    private const BindingFlags AnyInstanceMethod =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static TheoremReachCallbacks instance;
    private static string listenerName;
    private static readonly Queue<string> waitingRewards = new Queue<string>();
    // Whether a component type has a method SendMessage would call, by name: it looks through the base types too.
    private static readonly Dictionary<(Type, string), bool> receives = new Dictionary<(Type, string), bool>();

    internal static void EnsureExists()
    {
        if (instance != null) return;
        GameObject callbacks = new GameObject(ObjectName);
        DontDestroyOnLoad(callbacks);
        instance = callbacks.AddComponent<TheoremReachCallbacks>();
    }

    internal static void SetListener(string gameObjectName)
    {
        listenerName = gameObjectName;
        DeliverWaitingRewards();
    }

    internal static void DropWaitingRewards()
    {
        waitingRewards.Clear();
    }

    // The native SDKs call these by name. Unity's managed code stripping can't see such calls, so they're preserved.
    [Preserve]
    private void OnReward(string quantity)
    {
        waitingRewards.Enqueue(quantity);
        DeliverWaitingRewards();
    }

    [Preserve] private void OnRewardCenterOpened(string message) => Forward("OnRewardCenterOpened", message);
    [Preserve] private void OnRewardCenterClosed(string message) => Forward("OnRewardCenterClosed", message);
    [Preserve] private void TheoremReachSurveyAvailable(string available) => Forward("TheoremReachSurveyAvailable", available);
    [Preserve] private void OnMomentSurveyOpened(string message) => Forward("OnMomentSurveyOpened", message);
    [Preserve] private void OnMomentSurveyClosed(string message) => Forward("OnMomentSurveyClosed", message);
    [Preserve] private void OnMomentSurveyReceived(string length) => Forward("OnMomentSurveyReceived", length);
    [Preserve] private void OnMomentSurveyCompleted(string message) => Forward("OnMomentSurveyCompleted", message);
    [Preserve] private void OnMomentSurveyNotEligible(string message) => Forward("OnMomentSurveyNotEligible", message);

    // A listener's scene can load at any frame.
    private void Update()
    {
        if (waitingRewards.Count > 0) DeliverWaitingRewards();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Each reward leaves the queue as it's sent, so a listener that throws leaves the rest for the next frame.
    private static void DeliverWaitingRewards()
    {
        while (waitingRewards.Count > 0)
        {
            GameObject listener = Listener("OnReward");
            if (listener == null) return;
            listener.SendMessage("OnReward", waitingRewards.Dequeue(), SendMessageOptions.DontRequireReceiver);
        }
    }

    private static void Forward(string method, string message)
    {
        GameObject listener = Listener(method);
        if (listener != null) listener.SendMessage(method, message, SendMessageOptions.DontRequireReceiver);
    }

    // The listener, if it's in the scene with a component that has the method: SendMessage would drop the call
    // otherwise.
    private static GameObject Listener(string method)
    {
        if (string.IsNullOrEmpty(listenerName)) return null;
        GameObject listener = GameObject.Find(listenerName);
        // This object is never the game's listener: a reward would come straight back to it.
        if (listener == null || (instance != null && listener == instance.gameObject)) return null;
        foreach (MonoBehaviour component in listener.GetComponents<MonoBehaviour>())
        {
            // A missing script leaves a null component.
            if (component != null && Receives(component.GetType(), method)) return listener;
        }
        return null;
    }

    private static bool Receives(Type type, string method)
    {
        if (receives.TryGetValue((type, method), out bool found)) return found;
        found = false;
        for (Type t = type; t != null && t != typeof(MonoBehaviour) && !found; t = t.BaseType)
        {
            foreach (MethodInfo candidate in t.GetMethods(AnyInstanceMethod))
            {
                if (candidate.Name == method && candidate.GetParameters().Length <= 1)
                {
                    found = true;
                    break;
                }
            }
        }
        receives[(type, method)] = found;
        return found;
    }
}
