using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;

// The package's PlayMode tests drive the receiver directly, as the SDKs do.
[assembly: InternalsVisibleTo("TheoremReach.Tests")]

// Hears the native SDKs, which send every callback by name to this GameObject, kept across scenes, and passes each one
// on to the game's listener GameObject, which TheoremReach.SetListener names.
//
// The SDKs confirm a reward with TheoremReach as soon as they've sent it, and Unity drops a message to a GameObject that
// isn't there. So a reward waits here while no active GameObject has the listener's name: one whose scene hasn't loaded
// yet or has unloaded, or one SetListener hasn't named yet. A listener that's there gets it as in 3.x, so one with no
// OnReward method, as in a game that credits rewards on its server, doesn't keep a reward waiting. The wait is in
// memory, so a reward still waiting when the game ends is lost. Starting the SDK for another user drops the rewards
// waiting here, though one the SDK sent before the switch that Unity hasn't delivered yet still comes, as in 3.x.
// The other callbacks aren't held: a listener that isn't there misses them.
[AddComponentMenu("")]
[Preserve]
internal sealed class TheoremReachCallbacks : MonoBehaviour
{
    internal const string ObjectName = "TheoremReachCallbacks";

    // GameObject.Find walks the scene's objects, so a waiting reward looks for the listener once a second, not every
    // frame.
    private const float ListenerCheckSeconds = 1f;

    private static TheoremReachCallbacks instance;
    private static string listenerName;
    private static readonly Queue<string> waitingRewards = new Queue<string>();
    private static float nextListenerCheck;
    // Android's SDK reports an open for every show, but a show while the reward center is open either replaces it,
    // and only the newer one reports its close, or, with the game paused under it, opens nothing. So the game hears one
    // open for each close.
    private static bool rewardCenterOpen;

    internal static void EnsureExists()
    {
        if (instance != null) return;
        GameObject callbacks = new GameObject(ObjectName);
        DontDestroyOnLoad(callbacks);
        instance = callbacks.AddComponent<TheoremReachCallbacks>();
    }

    // Waiting rewards go from the next Update, not inside the game's call, which may come from its listener's Awake
    // before the game's other components are ready.
    internal static void SetListener(string gameObjectName)
    {
        listenerName = gameObjectName;
        nextListenerCheck = 0;
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

    [Preserve]
    private void OnRewardCenterOpened(string message)
    {
        if (rewardCenterOpen) return;
        rewardCenterOpen = true;
        Forward("OnRewardCenterOpened", message);
    }

    [Preserve]
    private void OnRewardCenterClosed(string message)
    {
        rewardCenterOpen = false;
        Forward("OnRewardCenterClosed", message);
    }

    [Preserve] private void TheoremReachSurveyAvailable(string available) => Forward("TheoremReachSurveyAvailable", available);
    [Preserve] private void OnMomentSurveyOpened(string message) => Forward("OnMomentSurveyOpened", message);
    [Preserve] private void OnMomentSurveyClosed(string message) => Forward("OnMomentSurveyClosed", message);
    [Preserve] private void OnMomentSurveyReceived(string length) => Forward("OnMomentSurveyReceived", length);
    [Preserve] private void OnMomentSurveyCompleted(string message) => Forward("OnMomentSurveyCompleted", message);
    [Preserve] private void OnMomentSurveyNotEligible(string message) => Forward("OnMomentSurveyNotEligible", message);

    // A listener's scene can load at any frame.
    private void Update()
    {
        if (waitingRewards.Count == 0 || Time.unscaledTime < nextListenerCheck) return;
        nextListenerCheck = Time.unscaledTime + ListenerCheckSeconds;
        DeliverWaitingRewards();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Each reward leaves the queue as it's sent, so a listener that throws leaves the rest for a later check.
    private static void DeliverWaitingRewards()
    {
        while (waitingRewards.Count > 0)
        {
            GameObject listener = Listener();
            if (listener == null) return;
            listener.SendMessage("OnReward", waitingRewards.Dequeue(), SendMessageOptions.DontRequireReceiver);
        }
    }

    private static void Forward(string method, string message)
    {
        GameObject listener = Listener();
        if (listener != null) listener.SendMessage(method, message, SendMessageOptions.DontRequireReceiver);
    }

    // The active GameObject with the listener's name, if there is one.
    private static GameObject Listener()
    {
        if (string.IsNullOrEmpty(listenerName)) return null;
        GameObject listener = GameObject.Find(listenerName);
        // This object is never the game's listener: a reward would come straight back to it.
        if (listener == null || (instance != null && listener == instance.gameObject)) return null;
        return listener;
    }
}
