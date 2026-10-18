#import <AudioToolbox/AudioToolbox.h>
#import <CoreHaptics/CoreHaptics.h>
#import <UIKit/UIKit.h>

static CHHapticEngine *SDKHapticsEngine;
static id<CHHapticPatternPlayer> SDKHapticsPlayer;
static NSUInteger SDKHapticsEngineGeneration;

static void SDKHapticsRunOnMain(dispatch_block_t action)
{
    if ([NSThread isMainThread]) action();
    else dispatch_async(dispatch_get_main_queue(), action);
}

static BOOL SDKHapticsHardwareSupported()
{
    if (@available(iOS 13.0, *))
        return [CHHapticEngine capabilitiesForHardware].supportsHaptics;
    return NO;
}

// Core Haptics may stop the engine asynchronously (including its idle auto-shutdown).
// Never keep a player beyond the lifetime of the playback/engine that created it.
static void SDKHapticsDiscardPlayer()
{
    SDKHapticsPlayer = nil;
}

static BOOL SDKHapticsStartEngine()
{
    if (!SDKHapticsHardwareSupported()) return NO;

    if (SDKHapticsEngine == nil)
    {
        NSError *error = nil;
        SDKHapticsEngine = [[CHHapticEngine alloc] initAndReturnError:&error];
        if (SDKHapticsEngine == nil || error != nil)
        {
            SDKHapticsEngine = nil;
            SDKHapticsDiscardPlayer();
            return NO;
        }
        // The app explicitly stops the engine on pause/scene transitions. Keeping
        // auto-shutdown disabled avoids an idle shutdown racing a player command.
        SDKHapticsEngine.autoShutdownEnabled = NO;
        NSUInteger engineGeneration = ++SDKHapticsEngineGeneration;
        SDKHapticsEngine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (SDKHapticsEngineGeneration != engineGeneration) return;
                SDKHapticsDiscardPlayer();
                SDKHapticsEngine = nil;
                SDKHapticsEngineGeneration++;
            });
        };
        SDKHapticsEngine.resetHandler = ^{
            dispatch_async(dispatch_get_main_queue(), ^{
                if (SDKHapticsEngineGeneration != engineGeneration) return;
                SDKHapticsDiscardPlayer();
                NSError *restartError = nil;
                if (![SDKHapticsEngine startAndReturnError:&restartError] || restartError != nil)
                {
                    SDKHapticsEngine = nil;
                    SDKHapticsEngineGeneration++;
                }
            });
        };
    }

    NSError *error = nil;
    BOOL started = [SDKHapticsEngine startAndReturnError:&error];
    if (!started || error != nil)
    {
        SDKHapticsDiscardPlayer();
        SDKHapticsEngine = nil;
        SDKHapticsEngineGeneration++;
    }
    return started && error == nil;
}

static CHHapticEvent *SDKHapticsTransient(float time, float intensity, float sharpness)
{
    NSArray<CHHapticEventParameter *> *parameters = @[
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharpness]
    ];
    return [[CHHapticEvent alloc]
        initWithEventType:CHHapticEventTypeHapticTransient
        parameters:parameters
        relativeTime:time];
}

static CHHapticEvent *SDKHapticsContinuousEvent(float duration, float intensity, float sharpness)
{
    NSArray<CHHapticEventParameter *> *parameters = @[
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharpness]
    ];
    return [[CHHapticEvent alloc]
        initWithEventType:CHHapticEventTypeHapticContinuous
        parameters:parameters
        relativeTime:0
        duration:duration];
}

static void SDKHapticsPlayEvents(NSArray<CHHapticEvent *> *events)
{
    if (!SDKHapticsStartEngine())
    {
        AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
        return;
    }

    // Do not call stopAtTime:error: on the previous player. Core Haptics can stop
    // its engine between callbacks, and messaging a player from that stopped
    // engine can raise an uncaught internal exception instead of returning NSError.
    SDKHapticsDiscardPlayer();

    NSError *error = nil;
    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:events parameters:@[] error:&error];
    if (pattern == nil || error != nil) return;

    id<CHHapticPatternPlayer> player = [SDKHapticsEngine createPlayerWithPattern:pattern error:&error];
    if (player == nil || error != nil) return;

    SDKHapticsPlayer = player;
    [SDKHapticsEngine notifyWhenPlayersFinished:^CHHapticEngineFinishedAction(NSError *finishedError) {
        dispatch_async(dispatch_get_main_queue(), ^{
            // A newer pattern may already be playing when this callback arrives.
            if (SDKHapticsPlayer == player) SDKHapticsPlayer = nil;
        });
        return CHHapticEngineFinishedActionLeaveEngineRunning;
    }];

    [player startAtTime:CHHapticTimeImmediate error:&error];
    if (error != nil && SDKHapticsPlayer == player) SDKHapticsPlayer = nil;
}

static void SDKHapticsImpact(UIImpactFeedbackStyle style, float intensity)
{
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [generator prepare];
    if (@available(iOS 13.0, *)) [generator impactOccurredWithIntensity:intensity];
    else [generator impactOccurred];
}

static void SDKHapticsNotification(UINotificationFeedbackType type)
{
    UINotificationFeedbackGenerator *generator = [UINotificationFeedbackGenerator new];
    [generator prepare];
    [generator notificationOccurred:type];
}

extern "C" int SDKHapticsSupportsHaptics()
{
    return SDKHapticsHardwareSupported() ? 1 : 0;
}

extern "C" void SDKHapticsPlayContinuous(float duration, float intensity, float sharpness)
{
    SDKHapticsRunOnMain(^{
        float safeDuration = fmaxf(0.05f, fminf(duration, 30.0f));
        float safeIntensity = fmaxf(0.0f, fminf(intensity, 1.0f));
        float safeSharpness = fmaxf(0.0f, fminf(sharpness, 1.0f));
        SDKHapticsPlayEvents(@[SDKHapticsContinuousEvent(safeDuration, safeIntensity, safeSharpness)]);
    });
}

extern "C" void SDKHapticsPlayPreset(int preset)
{
    SDKHapticsRunOnMain(^{
        switch (preset)
        {
            case 0: SDKHapticsImpact(UIImpactFeedbackStyleMedium, 0.85f); break;
            case 1:
            {
                UISelectionFeedbackGenerator *generator = [UISelectionFeedbackGenerator new];
                [generator prepare];
                [generator selectionChanged];
                SDKHapticsImpact(UIImpactFeedbackStyleLight, 0.65f);
                break;
            }
            case 2: SDKHapticsImpact(UIImpactFeedbackStyleSoft, 1.0f); break;
            case 3: SDKHapticsImpact(UIImpactFeedbackStyleLight, 1.0f); break;
            case 4: SDKHapticsImpact(UIImpactFeedbackStyleMedium, 1.0f); break;
            case 5: SDKHapticsImpact(UIImpactFeedbackStyleHeavy, 1.0f); break;
            case 6: SDKHapticsImpact(UIImpactFeedbackStyleSoft, 1.0f); break;
            case 7: SDKHapticsImpact(UIImpactFeedbackStyleRigid, 1.0f); break;
            case 8: SDKHapticsNotification(UINotificationFeedbackTypeSuccess); break;
            case 9: SDKHapticsNotification(UINotificationFeedbackTypeWarning); break;
            case 10: SDKHapticsNotification(UINotificationFeedbackTypeError); break;
            case 11:
                SDKHapticsPlayEvents(@[
                    SDKHapticsContinuousEvent(0.32f, 1.0f, 0.85f),
                    SDKHapticsTransient(0.0f, 1.0f, 1.0f),
                    SDKHapticsTransient(0.09f, 0.75f, 0.65f)
                ]);
                break;
            case 12:
                SDKHapticsPlayEvents(@[
                    SDKHapticsTransient(0.0f, 1.0f, 0.8f),
                    SDKHapticsTransient(0.07f, 0.55f, 0.45f)
                ]);
                break;
            case 13:
                SDKHapticsPlayEvents(@[
                    SDKHapticsTransient(0.0f, 0.45f, 0.65f),
                    SDKHapticsTransient(0.09f, 0.7f, 0.75f),
                    SDKHapticsTransient(0.2f, 1.0f, 0.9f)
                ]);
                break;
            case 14:
                SDKHapticsPlayEvents(@[
                    SDKHapticsTransient(0.0f, 0.8f, 0.4f),
                    SDKHapticsTransient(0.14f, 1.0f, 0.5f)
                ]);
                break;
            case 15: SDKHapticsImpact(UIImpactFeedbackStyleRigid, 0.9f); break;
            case 16: SDKHapticsImpact(UIImpactFeedbackStyleHeavy, 0.85f); break;
            case 17: SDKHapticsPlayEvents(@[SDKHapticsContinuousEvent(0.6f, 0.3f, 0.25f)]); break;
            case 18: SDKHapticsPlayEvents(@[SDKHapticsContinuousEvent(0.8f, 0.6f, 0.5f)]); break;
            case 19: SDKHapticsPlayEvents(@[SDKHapticsContinuousEvent(1.0f, 1.0f, 0.8f)]); break;
            default: break;
        }
    });
}

extern "C" void SDKHapticsStop()
{
    SDKHapticsRunOnMain(^{
        CHHapticEngine *engine = SDKHapticsEngine;
        SDKHapticsDiscardPlayer();
        SDKHapticsEngine = nil;
        SDKHapticsEngineGeneration++;
        if (engine != nil)
        {
            [engine stopWithCompletionHandler:^(NSError *error) {
                if (error != nil)
                    NSLog(@"[SDK Haptics] Engine stop failed: %@", error.localizedDescription);
            }];
        }
    });
}
