# TheoremReach Unity SDK

Monetize your players through rewarded surveys. This package wraps TheoremReach's native SDKs: Android 3.7.1 and iOS
3.6.2. The integration guide is at [theoremreach.com/docs/unity](https://theoremreach.com/docs/unity).

## Requirements

- Unity 6000.0.85f2 or later. On iOS, Unity's Objective-C Xcode project: the Swift project type that Unity 6.5 added as
  an option isn't supported yet.
- Android: a Target API Level of 35 or later (Google sign-in uses Credential Manager, which needs compileSdk 35), and a
  Minimum API Level of 23 or later.
- iOS 14 or later.
- Git, which Unity's Package Manager uses to fetch a package from a git URL.

The package depends on Unity's External Dependency Manager (`com.unity.external-dependency-manager`), which the Package
Manager installs with it. Unity 2022.3 and earlier can't build the Android SDK's libraries: their Android Gradle plugin
(7.4.2 in 2022.3) can't dex the Kotlin 2 code in some of them. A game that stays on one can keep the 3.7.0
`.unitypackage`
([theoremreach-3.7.0.unitypackage](https://github.com/TheoremReach/UnitySDK/raw/1fb3e741362f7b594cda50db9c92e6568aa507a9/theoremreach-3.7.0.unitypackage)).
If 3.7.0's Android build fails on duplicate Kotlin classes (`kotlin-stdlib` against `-jdk7` and `-jdk8`), add Kotlin's BOM
to the `dependencies` of `Assets/Plugins/Android/mainTemplate.gradle`, above the External Dependency Manager's
lines: `implementation platform('org.jetbrains.kotlin:kotlin-bom:1.8.22')`. The Package Manager doesn't stop an older
Unity from adding the package by git URL, so there it installs and then fails the Android build in D8
(`:launcher:mergeExtDexRelease`).

## Install

In Unity, open **Window > Package Manager**, choose **+ > Install package from git URL...**, enter the URL, and
select **Install**:

```text
https://github.com/TheoremReach/UnitySDK.git#4.0.0
```

Or add it to `Packages/manifest.json`:

```json
"com.theoremreach.sdk": "https://github.com/TheoremReach/UnitySDK.git#4.0.0"
```

To upgrade, change the tag. The Package Manager replaces the whole package, so no old file stays behind.

### Upgrading from a .unitypackage (3.7.0 and earlier)

Move the game to Unity 6000.0.85f2 or later first, if it's on an older Unity, with iOS Build Support: 3.7.0's
post-build script needs it until step 1 deletes it. Then:

1. **Before you add the package,** delete the old TheoremReach files under `Assets`: `Assets/Plugins/TheoremReach.cs`,
   `Assets/Plugins/iOS/TheoremReachWrapper.h` and `.mm`, `Assets/Plugins/iOS/SDK`, and `Assets/TheoremReach`. Left in,
   the old `TheoremReach` class collides with the package's, and the editor stops at compile errors.
2. If you added Google's EDM4U only for TheoremReach, remove it (`Assets/ExternalDependencyManager`) before you add the
   package: Unity's External Dependency Manager replaces it, and while both are installed, Unity asks which to use.
   Gradle templates from an older Unity fail Unity 6's Android build, so delete `mainTemplate.gradle`,
   `settingsTemplate.gradle` and `gradleTemplate.properties` in `Assets/Plugins/Android` if EDM4U wrote them; the
   External Dependency Manager writes Unity 6's. A template the game changed itself takes those changes again in Unity
   6's, as Unity's upgrade guide says.
3. Add the package. When it first loads, it looks for anything else the old packages left, such as the `theoremreach`
   and library AARs in `Assets/Plugins/Android`, by the GUIDs those packages gave them, and offers to move it to the
   trash. **Tools > TheoremReach > Remove Old TheoremReach Files** offers again.

The API is 3.7.0's, plus `SetLoadingBarColor` and `SetMmpInstallId`. Three things changed:

- Every call now exists on every platform, and does nothing outside an Android or iOS player, the editor included. So
  `#if UNITY_ANDROID || UNITY_IOS` around the calls is no longer needed.
- A call throws an `ArgumentException` for a null argument, or an empty one where the SDKs need text: an API key, a
  user ID, a GameObject name, a color, an MMP or its install ID, or a URL. 3.7.0 passed them on to the native SDKs. A
  null placement is no placement, as in `ShowRewardCenter()`.
- The native SDKs send their callbacks to the package's own GameObject, `TheoremReachCallbacks`, kept across scenes,
  which passes them on to your listener. A reward that comes while your listener isn't in the scene waits for it,
  where 3.7.0 lost it. `Initialize` makes that GameObject, so call it on Unity's main thread, and keep the GameObject:
  a game that destroys everything kept across scenes, to restart, calls `Initialize` again afterwards.

## Usage

```csharp
using UnityEngine;

public class Surveys : MonoBehaviour
{
    void Start()
    {
        TheoremReach.Initialize("YOUR_API_KEY", "YOUR_USER_ID");
        // Settings go after Initialize, which resets some of them.
        TheoremReach.SetNavigationBarText("Earn rewards");
        TheoremReach.SetListener(gameObject.name);
    }

    // From your surveys button.
    public void ShowSurveys()
    {
        TheoremReach.ShowRewardCenter();
    }

    // Android: tell the SDK when the game pauses and resumes.
    void OnApplicationPause(bool paused)
    {
        if (paused) TheoremReach.OnPause();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) TheoremReach.OnResume();
    }

    // The SDK's callbacks, sent to the GameObject SetListener names.
    void OnReward(string quantity)
    {
        // Credit quantity of your currency to the player.
    }

    void OnRewardCenterOpened() { }

    void OnRewardCenterClosed() { }

    void TheoremReachSurveyAvailable(string available)
    {
        // "true" or "false"
    }
}
```

Each callback goes to the GameObject `SetListener` names, found by that name when the callback comes. A reward waits
while no active GameObject has that name, so one that arrives while the listener's scene is loading isn't lost. The
other callbacks go only to a listener that's there. The wait is in memory: a reward still waiting when the game ends
is lost, and starting the SDK for another user drops it.

### Calls

| Call | What it does |
| --- | --- |
| `Initialize(apiKey, userId)` | Starts the SDK for this user. The user ID comes back in the server side reward callback. |
| `SetListener(gameObjectName)` | The GameObject that gets the callbacks. |
| `ShowRewardCenter()`, `ShowRewardCenter(placementId)` | Opens the reward center, at a placement when you pass one. |
| `IsSurveyAvailable()` | Whether a survey is available now. |
| `SetNavigationBarText(text)` | The reward center's title. |
| `SetNavigationBarColor(hexColor)` | The title bar's color, such as `#17b4b3`. |
| `SetNavigationBarTextColor(hexColor)` | The title's color. |
| `SetLoadingBarColor(hexColor)` | The thin loading bar's color, `#FC178D` by default. |
| `SetMmpInstallId(provider, installId)` | For a game that measures installs with a mobile measurement partner (MMP): the partner, such as `apptrove`, and its install ID. Call it before the reward center opens. |
| `OnResume()`, `OnPause()` | Android: the game came back to the front, or went to the background. |

Make the settings after `Initialize`: starting the SDK resets some of them (the colors on iOS, the title on Android).
The Moments calls of 3.x (`ShowMomentSurvey`, `EnableMoments`) and `IsProfiled`, `ResetProfiler`,
`SetOverrideCloseButtonURL` and `SetOverrideRefreshButtonURL` are kept.

### Callbacks

| Method on the listener GameObject | When |
| --- | --- |
| `OnReward(string quantity)` | The player earned `quantity` of your currency, in your app's units. |
| `OnRewardCenterOpened()` | The reward center opened. |
| `OnRewardCenterClosed()` | The reward center closed. |
| `TheoremReachSurveyAvailable(string available)` | A survey became available (`"true"`) or not (`"false"`). |

No second `OnRewardCenterOpened` comes before the first one's `OnRewardCenterClosed`, so a game can pause for the reward
center and resume at its close. On iOS the close comes at another moment in two cases: `Initialize` while the reward
center is open reports it then, though the reward center stays on screen until the player closes it; and one that UIKit
took away without its own close (when another plug-in dismisses the view it opened over, or everything over Unity's)
reports it at the next show or `Initialize`. Like every callback but a reward, each goes only to a listener that's there
when it comes.

For security, we recommend crediting rewards from the server side callback; if you use both, credit each reward once.

## Android

The External Dependency Manager adds `com.theoremreach:theoremreach:3.7.1` from Maven Central to the Gradle build, with
the libraries it names. It writes the dependency and its repositories into the Gradle templates in
`Assets/Plugins/Android` (`mainTemplate.gradle`, `settingsTemplate.gradle` and `gradleTemplate.properties`), copying in
Unity's own when the project has none, so keep those files in your project. No AAR goes into `Assets`.

## iOS

The package bundles `TheoremReachSDK.xcframework` (3.6.2), which its import settings embed in the app. The reward center
opens from Unity's own view controller, and the status bar over it follows its title.

## Other platforms

[iOS](https://theoremreach.com/docs/ios) · [Android](https://theoremreach.com/docs/android) ·
[React Native](https://theoremreach.com/docs/react_native) · [Web](https://theoremreach.com/docs/web)

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

The TheoremReach SDK is licensed under the
[TheoremReach Publisher Terms and Conditions](https://info.theoremreach.com/terms-of-service/); see
[LICENSE.md](LICENSE.md).
