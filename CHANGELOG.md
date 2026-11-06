# Changelog

## 4.0.0

- A Unity Package Manager package, added by git URL, in place of the `.unitypackage`. Upgrades replace the whole
  package, so no old file stays behind. A script finds the files older packages left under `Assets` and offers to
  remove them.
- On TheoremReach's Android SDK 3.7.1, from Maven Central, and iOS SDK 3.6.2, which the package bundles.
- Unity 6000.0.85f2 or later, with Unity's External Dependency Manager in place of Google's EDM4U. One Maven entry
  replaces the hand-kept list of Android libraries. Unity 2022.3 and earlier can't dex the libraries the Android SDK
  depends on, so a game on one keeps 3.7.0.
- New: `SetLoadingBarColor` and `SetMmpInstallId`.
- A reward that comes while the listener GameObject isn't in the scene waits for it, instead of being lost: the SDKs
  confirm a reward once they've sent it. They now send every callback to the package's own GameObject, kept across
  scenes, which passes each one on to the listener `SetListener` names. A start for another user drops a waiting
  reward.
- No second `OnRewardCenterOpened` comes before the first one's `OnRewardCenterClosed`, as one did on Android for a
  show while the reward center was open. On iOS, `Initialize` while one is open sends its close, which the SDK's start
  would lose, and so does the next show or `Initialize` after UIKit took one away without its own close.
- Every call exists on every platform and does nothing outside an Android or iOS player. A null argument, or an empty
  one where the SDKs need text, throws an `ArgumentException`; a null placement is no placement.
- iOS: the reward center opens from Unity's own view controller, one at a time, and the status bar over it follows its
  title. The framework embeds from its import settings, with no post-build script, so it's no longer embedded twice.
  The bridge's C functions are prefixed, so they can't collide with another plug-in's.
- Every show passes its placement, empty for none, so a reward center that never closed can't hand its placement to the
  next.
- The demo scene, its script and Unity IAP's `BillingMode.json` no longer ship in the package.

## 3.7.0

Fix Android dependency resolution by migrating to EDM4U; update iOS SDK to xcframework format

## 3.6.3

Miscellaneous updates

## 3.6.2

Fixed Android webview flickering issue

## 3.6.1

Removed fixed screen orientation setting for Android

## 3.6.0

Added support for Android 16 edge-to-edge

## 3.5.10

Added Android Photo Picker for image upload

## 3.5.9

image upload support

## 3.5.8

Update bottom bar color in Android

## 3.5.7

Remove bitcode

## 3.5.6

Updated version tracking and default colors

## 3.5.5

Fix close and refresh buttons for iPhone 8 and earlier

## 3.5.4

Better third party login support

## 3.5.3

Fix issue with redirecting to offers

## 3.5.2

Update iOS sdk to fix visual issue in landscape

## 3.5.1

Update Android and iOS sdks. No functional changes

## 3.5.0

- Fix mobile network issue with android sdk

## 3.4.7

- Update Android and iOS SDK versions

## 3.4.6

- Remove android 'READ_PHONE_STATE' permission requirement

## 3.4.5

- Fix iOS code signing issue

## 3.4.4

- Update Android sdk version

## 3.4.2

- Added support for calling ShowRewardCenter with a placement id: `void ShowRewardCenter(string placementId);`

## 3.4.1

- Critical iOS update to decrease LC_VERSION_MIN_IPHONEOS back to 13.6

## 3.4.0

- iOS 14 release preparations
