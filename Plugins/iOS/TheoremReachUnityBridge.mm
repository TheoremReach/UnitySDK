#import <UIKit/UIKit.h>
#import <TheoremReachSDK/TheoremReach.h>

// Unity's own, from its iOS player (UnityInterface.h).
extern "C" {
void UnitySendMessage(const char *obj, const char *method, const char *msg);
UIViewController *UnityGetGLViewController(void);
}

// Between TheoremReach's iOS SDK and the package's C# API (TheoremReach.cs): the SDK's notifications go through
// UnitySendMessage to the package's own GameObject (TheoremReachCallbacks.cs), which passes them on to the game's
// listener, and the C# calls come in through the C functions at the end. Unity calls those on the main thread, where the
// SDK presents the reward center.
@interface TRUnityBridge : NSObject
@end

// The package's GameObject that hears the SDK, which Initialize names.
static NSString *TRListenerName;
// The game heard the reward center open and hasn't heard it close.
static BOOL TRRewardCenterOpen;
// The game's status bar style while the reward center is open, put back when it closes, and the reward center's that
// the bridge set in its place.
static BOOL TRStatusBarStyleSaved;
static UIStatusBarStyle TRSavedStatusBarStyle;
static UIStatusBarStyle TRRewardCenterStatusBarStyle;

static NSString *TRString(const char *text)
{
    return text ? [NSString stringWithUTF8String:text] : nil;
}

static void TRSend(const char *method, NSString *message)
{
    if (TRListenerName.length == 0) {
        NSLog(@"TheoremReach: %s came before Initialize, so nothing heard it", method);
        return;
    }
    UnitySendMessage(TRListenerName.UTF8String, method, (message ?: @"").UTF8String);
}

#pragma mark - The status bar over the reward center

// The reward center asks for a status bar that follows its title (the SDK's rewardCenterStatusBarStyle: dark under a
// dark title, light under a light one), but a Unity game turns view controller based status bar appearance off, so
// iOS never asks it: the game's own style would sit on the title bar. Such a game gets the reward center's style while
// it's open.
static BOOL TRGameSetsStatusBar(void)
{
    NSNumber *viewControllerBased =
        [[NSBundle mainBundle] objectForInfoDictionaryKey:@"UIViewControllerBasedStatusBarAppearance"];
    return viewControllerBased != nil && !viewControllerBased.boolValue;
}

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
static void TRUseRewardCenterStatusBar(void)
{
    if (TRStatusBarStyleSaved || !TRGameSetsStatusBar()) {
        return;
    }
    UIApplication *application = [UIApplication sharedApplication];
    TRSavedStatusBarStyle = application.statusBarStyle;
    TRRewardCenterStatusBarStyle = [[TheoremReach getInstance] rewardCenterStatusBarStyle];
    TRStatusBarStyleSaved = YES;
    application.statusBarStyle = TRRewardCenterStatusBarStyle;
}

static void TRRestoreStatusBar(void)
{
    if (!TRStatusBarStyleSaved) {
        return;
    }
    TRStatusBarStyleSaved = NO;
    // A style the game set while the reward center was open stands.
    UIApplication *application = [UIApplication sharedApplication];
    if (application.statusBarStyle == TRRewardCenterStatusBarStyle) {
        application.statusBarStyle = TRSavedStatusBarStyle;
    }
}
#pragma clang diagnostic pop

@implementation TRUnityBridge

// Before the app's launch, so no notification goes unheard.
+ (void)load
{
    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    [center addObserver:self selector:@selector(userEarnedReward:) name:@"TheoremReachUserEarnedReward" object:nil];
    [center addObserver:self selector:@selector(rewardCenterOpened:) name:@"TheoremReachRewardCenterOpened" object:nil];
    [center addObserver:self selector:@selector(rewardCenterClosed:) name:@"TheoremReachRewardCenterClosed" object:nil];
    [center addObserver:self selector:@selector(surveyAvailable:) name:@"TheoremReachSurveyAvailable" object:nil];
    [center addObserver:self selector:@selector(momentSurveyOpened:) name:@"MomentSurveyOpened" object:nil];
    [center addObserver:self selector:@selector(momentSurveyClosed:) name:@"MomentSurveyClosed" object:nil];
    [center addObserver:self selector:@selector(momentSurveyReceived:) name:@"MomentSurveyReceived" object:nil];
    [center addObserver:self selector:@selector(momentSurveyCompleted:) name:@"MomentSurveyCompleted" object:nil];
    [center addObserver:self selector:@selector(momentSurveyNotEligible:) name:@"MomentSurveyNotEligible" object:nil];
}

+ (void)userEarnedReward:(NSNotification *)notification
{
    TRSend("OnReward", [notification.userInfo[@"userEarnedReward"] description]);
}

// Posted just before the SDK presents the reward center.
+ (void)rewardCenterOpened:(NSNotification *)notification
{
    TRUseRewardCenterStatusBar();
    TRRewardCenterOpen = YES;
    TRSend("OnRewardCenterOpened", @"");
}

+ (void)rewardCenterClosed:(NSNotification *)notification
{
    TRRestoreStatusBar();
    TRRewardCenterOpen = NO;
    TRSend("OnRewardCenterClosed", @"");
}

+ (void)surveyAvailable:(NSNotification *)notification
{
    TRSend("TheoremReachSurveyAvailable", [notification.userInfo[@"surveyAvailable"] boolValue] ? @"true" : @"false");
}

+ (void)momentSurveyOpened:(NSNotification *)notification
{
    TRSend("OnMomentSurveyOpened", @"");
}

+ (void)momentSurveyClosed:(NSNotification *)notification
{
    TRSend("OnMomentSurveyClosed", @"");
}

+ (void)momentSurveyReceived:(NSNotification *)notification
{
    TRSend("OnMomentSurveyReceived", [notification.userInfo[@"momentSurveyLength"] description]);
}

+ (void)momentSurveyCompleted:(NSNotification *)notification
{
    TRSend("OnMomentSurveyCompleted", @"");
}

+ (void)momentSurveyNotEligible:(NSNotification *)notification
{
    TRSend("OnMomentSurveyNotEligible", @"");
}

@end

extern "C" {

void TheoremReach_initialize(const char *apiKey, const char *userId)
{
    // Starting the SDK forgets an open reward center, which then never reports its close.
    TRRestoreStatusBar();
    [TheoremReach initWithApiKey:TRString(apiKey) userId:TRString(userId)];
}

void TheoremReach_setListener(const char *gameObjectName)
{
    TRListenerName = TRString(gameObjectName);
}

void TheoremReach_showRewardCenter(const char *placementId)
{
    // One reward center at a time: a second would open over the first, and the first's close would then never reach
    // the game.
    if ([TheoremReach getInstance].rewardCenterViewController.presentingViewController != nil) {
        return;
    }

    // Presented from Unity's own view controller. The placement goes with every show, nil when there's none: the SDK
    // clears it only when its reward center closes, so one that never closed would hand its placement to the next.
    NSString *placement = TRString(placementId);
    [TheoremReach showRewardCenter:(placement.length > 0 ? placement : nil)
                fromViewController:UnityGetGLViewController()];

    // The SDK reports the reward center open before presenting it. If UIKit refused the presentation (the presenter
    // was mid-transition, say, or the last reward center was still sliding out), no close will come, so the game's
    // style comes back, and the game hears the close of the open it heard.
    if ([TheoremReach getInstance].rewardCenterViewController.presentingViewController == nil) {
        TRRestoreStatusBar();
        if (TRRewardCenterOpen) {
            TRRewardCenterOpen = NO;
            TRSend("OnRewardCenterClosed", @"");
        }
    }
}

int TheoremReach_isSurveyAvailable(void)
{
    return [TheoremReach getIsSurveyAvailable] ? 1 : 0;
}

void TheoremReach_setNavigationBarText(const char *text)
{
    [[TheoremReach getInstance] setNavigationBarText:TRString(text)];
}

void TheoremReach_setNavigationBarColor(const char *hexColor)
{
    [[TheoremReach getInstance] setNavigationBarColor:TRString(hexColor)];
}

void TheoremReach_setNavigationBarTextColor(const char *hexColor)
{
    [[TheoremReach getInstance] setNavigationBarTextColor:TRString(hexColor)];
}

void TheoremReach_setLoadingBarColor(const char *hexColor)
{
    [[TheoremReach getInstance] setLoadingBarColor:TRString(hexColor)];
}

void TheoremReach_setMmpInstallId(const char *provider, const char *installId)
{
    [[TheoremReach getInstance] setMmpProvider:TRString(provider) installId:TRString(installId)];
}

void TheoremReach_showMomentSurvey(void)
{
    [TheoremReach showMomentSurvey];
}

void TheoremReach_enableMoments(int enabled)
{
    [[TheoremReach getInstance] enableMoments:enabled != 0];
}

int TheoremReach_isProfiled(void)
{
    return [[TheoremReach getInstance] unityIsAppuserProfiled];
}

void TheoremReach_resetProfiler(int reset)
{
    [[TheoremReach getInstance] resetProfiler:reset != 0];
}

void TheoremReach_setOverrideCloseButtonURL(const char *url)
{
    [TheoremReach getInstance].overrideCloseButtonURL = TRString(url);
}

void TheoremReach_setOverrideRefreshButtonURL(const char *url)
{
    [TheoremReach getInstance].overrideRefreshButtonURL = TRString(url);
}

}
