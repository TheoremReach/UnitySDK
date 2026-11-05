using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TheoremReachSDK.Tests
{
    // The package's receiver, driven as the native SDKs drive it: each callback is a message, by name, to its GameObject.
    // A project runs these with the package in its manifest's "testables".
    public class TheoremReachCallbacksTests
    {
        private const string ListenerName = "TheoremReachTestListener";
        // Past the receiver's once-a-second look for a waiting reward's listener, with room for a slow frame.
        private const float WellPastALook = 1.6f;

        private GameObject receiver;
        private readonly List<GameObject> listeners = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            TheoremReachCallbacks.EnsureExists();
            receiver = GameObject.Find(TheoremReachCallbacks.ObjectName);
            TheoremReachCallbacks.DropWaitingRewards();
            TheoremReachCallbacks.SetListener(ListenerName);
            // Ends any open the last test left, with no listener there to hear it.
            FromSdk("OnRewardCenterClosed");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject listener in listeners) Object.DestroyImmediate(listener);
            listeners.Clear();
            // The next test's EnsureExists makes another.
            Object.DestroyImmediate(receiver);
        }

        [Test]
        public void ARewardGoesStraightToAListenerThatsThere()
        {
            RewardListener listener = AddListener<RewardListener>();
            FromSdk("OnReward", "100");
            Assert.That(listener.Heard, Is.EqualTo(new[] { "OnReward 100" }));
        }

        [UnityTest]
        public IEnumerator RewardsWaitInOrderForTheirListenerToComeIntoTheScene()
        {
            FromSdk("OnReward", "100");
            FromSdk("OnReward", "260");
            yield return null;
            RewardListener listener = AddListener<RewardListener>();
            yield return Until(() => listener.Heard.Count == 2, WellPastALook);
            Assert.That(listener.Heard, Is.EqualTo(new[] { "OnReward 100", "OnReward 260" }));
        }

        [UnityTest]
        public IEnumerator AWaitingRewardLooksForItsListenerOnceASecondNotEveryFrame()
        {
            FromSdk("OnReward", "100");
            // This frame's look finds no listener.
            yield return null;
            RewardListener listener = AddListener<RewardListener>();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(listener.Heard, Is.Empty, "it looked again before the second was up");
            yield return Until(() => listener.Heard.Count > 0, WellPastALook);
            Assert.That(listener.Heard, Is.EqualTo(new[] { "OnReward 100" }));
        }

        [UnityTest]
        public IEnumerator SetListenerHandsAWaitingRewardOverOnTheNextFrameNotInsideTheCall()
        {
            FromSdk("OnReward", "100");
            yield return null;
            RewardListener listener = AddListener<RewardListener>();
            // As the listener's Awake would, before the game's other components are ready.
            TheoremReachCallbacks.SetListener(ListenerName);
            Assert.That(listener.Heard, Is.Empty, "it was handed over inside SetListener");
            yield return null;
            Assert.That(listener.Heard, Is.EqualTo(new[] { "OnReward 100" }));
        }

        [UnityTest]
        public IEnumerator AListenerWithNoOnRewardDoesntKeepARewardWaiting()
        {
            // A game that credits rewards on its server, and listens only for the reward center.
            OpenCloseListener openClose = AddListener<OpenCloseListener>();
            FromSdk("OnReward", "100");
            RewardListener addedLater = openClose.gameObject.AddComponent<RewardListener>();
            yield return new WaitForSecondsRealtime(WellPastALook);
            Assert.That(addedLater.Heard, Is.Empty);
        }

        [Test]
        public void TheGameHearsOneOpenForEachClose()
        {
            RewardListener listener = AddListener<RewardListener>();
            // As Android's SDK reports a show while the reward center is open: it replaces the open one, and only the
            // newer one reports its close.
            FromSdk("OnRewardCenterOpened");
            FromSdk("OnRewardCenterOpened");
            FromSdk("OnRewardCenterClosed");
            FromSdk("OnRewardCenterOpened");
            FromSdk("OnRewardCenterClosed");
            Assert.That(listener.Heard, Is.EqualTo(new[]
            {
                "OnRewardCenterOpened", "OnRewardCenterClosed", "OnRewardCenterOpened", "OnRewardCenterClosed",
            }));
        }

        [UnityTest]
        public IEnumerator OnlyRewardsWaitForTheListener()
        {
            FromSdk("OnRewardCenterOpened");
            RewardListener listener = AddListener<RewardListener>();
            yield return new WaitForSecondsRealtime(WellPastALook);
            Assert.That(listener.Heard, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DroppedRewardsNeverReachTheListener()
        {
            FromSdk("OnReward", "100");
            // As a start for another user does.
            TheoremReachCallbacks.DropWaitingRewards();
            RewardListener listener = AddListener<RewardListener>();
            yield return new WaitForSecondsRealtime(WellPastALook);
            Assert.That(listener.Heard, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TheReceiverIsNeverItsOwnListener()
        {
            TheoremReachCallbacks.SetListener(TheoremReachCallbacks.ObjectName);
            FromSdk("OnReward", "100");
            yield return null;
            RewardListener listener = AddListener<RewardListener>();
            TheoremReachCallbacks.SetListener(ListenerName);
            yield return Until(() => listener.Heard.Count > 0, WellPastALook);
            Assert.That(listener.Heard, Is.EqualTo(new[] { "OnReward 100" }));
        }

        // A message to the receiver by name, as the SDKs send one. It requires a receiver, so a callback the receiver
        // doesn't have fails the test with Unity's error.
        private void FromSdk(string method, string message = "")
        {
            receiver.SendMessage(method, message, SendMessageOptions.RequireReceiver);
        }

        private T AddListener<T>() where T : MonoBehaviour
        {
            GameObject listener = new GameObject(ListenerName);
            listeners.Add(listener);
            return listener.AddComponent<T>();
        }

        private static IEnumerator Until(Func<bool> done, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (!done() && Time.unscaledTime < end) yield return null;
        }
    }

    public class RewardListener : MonoBehaviour
    {
        public readonly List<string> Heard = new List<string>();

        private void OnReward(string quantity) => Heard.Add("OnReward " + quantity);
        private void OnRewardCenterOpened() => Heard.Add("OnRewardCenterOpened");
        private void OnRewardCenterClosed() => Heard.Add("OnRewardCenterClosed");
    }

    public class OpenCloseListener : MonoBehaviour
    {
        public readonly List<string> Heard = new List<string>();

        private void OnRewardCenterOpened() => Heard.Add("OnRewardCenterOpened");
        private void OnRewardCenterClosed() => Heard.Add("OnRewardCenterClosed");
    }
}
