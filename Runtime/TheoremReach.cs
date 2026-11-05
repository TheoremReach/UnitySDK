using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// TheoremReach's rewarded surveys, through its native Android and iOS SDKs.
/// </summary>
/// <remarks>
/// Call <see cref="Initialize"/> once, as early as you can, then <see cref="SetListener"/> with the name of the
/// GameObject whose OnReward, OnRewardCenterOpened, OnRewardCenterClosed and TheoremReachSurveyAvailable methods hear
/// the SDK. Make the settings after Initialize, which resets some of them. Outside an Android or iOS player, the editor
/// included, every call does nothing, so a game can make them on any platform.
/// </remarks>
public class TheoremReach : MonoBehaviour
{
    /// <summary>This package's version.</summary>
    public const string Version = "4.0.0";

    /// <summary>
    /// Starts the SDK for this user. The user ID comes back to you in the server side reward callback.
    /// </summary>
    public static void Initialize(string apiKey, string userId)
    {
        RequireText(apiKey, nameof(apiKey));
        RequireText(userId, nameof(userId));
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        // The rewards waiting for the listener were the last user's.
        if (currentUserId != null && currentUserId != userId) TheoremReachCallbacks.DropWaitingRewards();
        currentUserId = userId;
        // The SDKs send every callback to the package's own GameObject, which passes them on to the game's listener.
        TheoremReachCallbacks.EnsureExists();
#endif
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setListener(TheoremReachCallbacks.ObjectName);
        TheoremReach_initialize(apiKey, userId);
#elif UNITY_ANDROID && !UNITY_EDITOR
        // Unity's APIs answer only on its own thread, not on Android's main thread where the SDK runs.
        string title = Application.productName;
        AndroidJavaObject activity = CurrentActivity();
        OnAndroidMainThread(sdk =>
        {
            sdk.Call("setUnityGame", true);
            sdk.Call("setUnityObjectName", TheoremReachCallbacks.ObjectName);
            SdkClass().CallStatic<AndroidJavaObject>("initWithApiKeyAndUserIdAndActivityContext", apiKey, userId,
                activity).Dispose();
            // Android's start titles the reward center TheoremReach. A game's is its product name, as in 3.x.
            sdk.Call("setNavigationBarText", title);
        });
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>
    /// The name of the GameObject whose OnReward(string quantity), OnRewardCenterOpened(), OnRewardCenterClosed() and
    /// TheoremReachSurveyAvailable(string available) methods hear the SDK. It's found by name for each callback. A
    /// reward waits while no active GameObject has that name; the other callbacks go only to one that's there.
    /// </summary>
    public static void SetListener(string gameObjectName)
    {
        RequireText(gameObjectName, nameof(gameObjectName));
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        TheoremReachCallbacks.SetListener(gameObjectName);
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>Opens the reward center, where the user takes surveys for your currency.</summary>
    public static void ShowRewardCenter()
    {
        ShowRewardCenter(null);
    }

    /// <summary>Opens the reward center at a placement: its currency and exchange rate, from your dashboard.</summary>
    public static void ShowRewardCenter(string placementId)
    {
        // Every show names its placement, empty for none: each SDK clears the last one only when its reward center
        // closes, so one that never closed would hand its placement to the next show.
        string placement = placementId ?? "";
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_showRewardCenter(placement);
#elif UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject activity = CurrentActivity();
        OnAndroidMainThread(sdk => sdk.Call("showUnityRewardCenter", activity, placement));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>Whether a survey is available for this user now. False outside an Android or iOS player.</summary>
    public static bool IsSurveyAvailable()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return TheoremReach_isSurveyAvailable() == 1;
#elif UNITY_ANDROID && !UNITY_EDITOR
        return SdkClass().CallStatic<bool>("unityIsSurveyAvailable");
#else
        NotInThisPlayer();
        return false;
#endif
    }

    /// <summary>The reward center's title. Set it after Initialize.</summary>
    public static void SetNavigationBarText(string text)
    {
        RequireNotNull(text, nameof(text));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setNavigationBarText(text);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setNavigationBarText", text));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>The title bar's color, as a hex string such as #17b4b3. Set it after Initialize.</summary>
    public static void SetNavigationBarColor(string hexColor)
    {
        RequireText(hexColor, nameof(hexColor));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setNavigationBarColor(hexColor);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setNavigationBarColor", hexColor));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>The title's color, as a hex string such as #FFFFFF. Set it after Initialize.</summary>
    public static void SetNavigationBarTextColor(string hexColor)
    {
        RequireText(hexColor, nameof(hexColor));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setNavigationBarTextColor(hexColor);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setNavigationBarTextColor", hexColor));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>
    /// The color of the thin loading bar at the top of the reward center, as a hex string. #FC178D by default. Set it
    /// after Initialize.
    /// </summary>
    public static void SetLoadingBarColor(string hexColor)
    {
        RequireText(hexColor, nameof(hexColor));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setLoadingBarColor(hexColor);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setLoadingBarColor", hexColor));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>
    /// For a game that measures installs with a mobile measurement partner (MMP): the partner, such as apptrove, and
    /// the install ID it gave this install. Set it after Initialize, before the reward center opens.
    /// </summary>
    public static void SetMmpInstallId(string provider, string installId)
    {
        RequireText(provider, nameof(provider));
        RequireText(installId, nameof(installId));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setMmpInstallId(provider, installId);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setMmpInstallId", provider, installId));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>Tells the Android SDK the game is back in front: call it from OnApplicationFocus(true).</summary>
    public static void OnResume()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject activity = CurrentActivity();
        OnAndroidMainThread(sdk => sdk.Call("onResume", activity));
#endif
    }

    /// <summary>Tells the Android SDK the game has gone to the background: call it from OnApplicationPause(true).</summary>
    public static void OnPause()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("onPause"));
#endif
    }

    /// <summary>Opens the moment survey the SDK has ready, if Moments are on.</summary>
    public static void ShowMomentSurvey()
    {
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_showMomentSurvey();
#elif UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject activity = CurrentActivity();
        OnAndroidMainThread(sdk => sdk.Call("showUnityMomentSurvey", activity));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>Turns Moments, single surveys offered between levels, on or off.</summary>
    public static void EnableMoments(bool enabled)
    {
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_enableMoments(enabled ? 1 : 0);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("enableMoments", enabled));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>Whether the user has answered TheoremReach's profile questions. False outside an Android or iOS player.</summary>
    public static bool IsProfiled()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return TheoremReach_isProfiled() == 1;
#elif UNITY_ANDROID && !UNITY_EDITOR
        return SdkClass().CallStatic<bool>("unityIsProfiled");
#else
        NotInThisPlayer();
        return false;
#endif
    }

    /// <summary>Resets the user's profile, for testing. Don't leave it set when your game goes live.</summary>
    public static void ResetProfiler(bool resetProfiler)
    {
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_resetProfiler(resetProfiler ? 1 : 0);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("resetProfiler", resetProfiler));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>The URL of an image for the reward center's close button.</summary>
    public static void SetOverrideCloseButtonURL(string overrideCloseButtonURL)
    {
        RequireText(overrideCloseButtonURL, nameof(overrideCloseButtonURL));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setOverrideCloseButtonURL(overrideCloseButtonURL);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setOverrideCloseButtonURL", overrideCloseButtonURL));
#else
        NotInThisPlayer();
#endif
    }

    /// <summary>The URL of an image for the reward center's refresh button.</summary>
    public static void SetOverrideRefreshButtonURL(string overrideRefreshButtonURL)
    {
        RequireText(overrideRefreshButtonURL, nameof(overrideRefreshButtonURL));
#if UNITY_IOS && !UNITY_EDITOR
        TheoremReach_setOverrideRefreshButtonURL(overrideRefreshButtonURL);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OnAndroidMainThread(sdk => sdk.Call("setOverrideRefreshButtonURL", overrideRefreshButtonURL));
#else
        NotInThisPlayer();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // As in 3.x, a TheoremReach component on a GameObject passes the game's pauses and resumes to the Android SDK.
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) OnPause();
        else OnResume();
    }

    void OnApplicationFocus(bool focusStatus)
    {
        if (focusStatus) OnResume();
        else OnPause();
    }
#endif

    // The native SDKs crash on a null string, so a bad argument throws here, where the game's own call is on the stack.
    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("TheoremReach: " + name + " must be a non-empty string.", name);
        }
    }

    private static void RequireNotNull(string value, string name)
    {
        if (value == null)
        {
            throw new ArgumentNullException(name, "TheoremReach: " + name + " must be a string.");
        }
    }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
    private static string currentUserId;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string AndroidSdkClass = "theoremreach.com.theoremreach.TheoremReach";

    private static AndroidJavaObject activity;
    private static AndroidJavaClass sdkClass;
    private static AndroidJavaObject sdkInstance;

    private static AndroidJavaClass SdkClass()
    {
        if (sdkClass == null)
        {
            sdkClass = new AndroidJavaClass(AndroidSdkClass);
        }
        return sdkClass;
    }

    private static AndroidJavaObject CurrentActivity()
    {
        if (activity == null)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
        return activity;
    }

    // The SDK runs on Android's main thread, where it starts its activity and calls its listeners. Unity's scripts run
    // on a thread of their own: under Unity 6's GameActivity, a native one with no Java looper. An exception there is
    // logged rather than ending the game.
    private static void OnAndroidMainThread(Action<AndroidJavaObject> call)
    {
        if (sdkInstance == null)
        {
            sdkInstance = SdkClass().CallStatic<AndroidJavaObject>("getInstance");
        }
        AndroidJavaObject sdk = sdkInstance;
        CurrentActivity().Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            try
            {
                call(sdk);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }));
    }
#elif UNITY_IOS && !UNITY_EDITOR
    // Prefixed, since every plug-in's C functions share the app's one namespace.
    [DllImport("__Internal")] private static extern void TheoremReach_initialize(string apiKey, string userId);
    [DllImport("__Internal")] private static extern void TheoremReach_setListener(string gameObjectName);
    [DllImport("__Internal")] private static extern void TheoremReach_showRewardCenter(string placementId);
    [DllImport("__Internal")] private static extern int TheoremReach_isSurveyAvailable();
    [DllImport("__Internal")] private static extern void TheoremReach_setNavigationBarText(string text);
    [DllImport("__Internal")] private static extern void TheoremReach_setNavigationBarColor(string hexColor);
    [DllImport("__Internal")] private static extern void TheoremReach_setNavigationBarTextColor(string hexColor);
    [DllImport("__Internal")] private static extern void TheoremReach_setLoadingBarColor(string hexColor);
    [DllImport("__Internal")] private static extern void TheoremReach_setMmpInstallId(string provider, string installId);
    [DllImport("__Internal")] private static extern void TheoremReach_showMomentSurvey();
    [DllImport("__Internal")] private static extern void TheoremReach_enableMoments(int enabled);
    [DllImport("__Internal")] private static extern int TheoremReach_isProfiled();
    [DllImport("__Internal")] private static extern void TheoremReach_resetProfiler(int reset);
    [DllImport("__Internal")] private static extern void TheoremReach_setOverrideCloseButtonURL(string url);
    [DllImport("__Internal")] private static extern void TheoremReach_setOverrideRefreshButtonURL(string url);
#else
    private static bool toldNotInThisPlayer;

    // Once per session, so a game that calls TheoremReach every frame doesn't fill the console.
    private static void NotInThisPlayer()
    {
        if (toldNotInThisPlayer) return;
        toldNotInThisPlayer = true;
        Debug.Log("TheoremReach runs in Android and iOS players: its calls do nothing here.");
    }
#endif
}
