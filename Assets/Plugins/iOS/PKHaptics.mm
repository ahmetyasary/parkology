#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

// Authored transient patterns for Parkology. Kind numbers match the Haptic enum in Haptics.cs.
static CHHapticEngine *pkEngine;
static NSMutableDictionary<NSNumber *, id<CHHapticPatternPlayer>> *pkPlayers;
static UISelectionFeedbackGenerator *pkSelection;
static UIImpactFeedbackGenerator *pkLight, *pkHeavy;
static UINotificationFeedbackGenerator *pkNotify;
static BOOL pkEnabled = YES, pkSupported, pkRunning;

static CHHapticEvent *PKTap(double time, float intensity, float sharpness) {
    return [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient parameters:@[
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharpness]
    ] relativeTime:time];
}

static CHHapticEvent *PKBuzz(double time, double duration, float intensity, float sharpness) {
    return [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous parameters:@[
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharpness]
    ] relativeTime:time duration:duration];
}

static NSArray<CHHapticEvent *> *PKPattern(int kind) {
    switch (kind) {
        case 0: return @[PKTap(0, .45, .55)];                                                     // tap
        case 1: return @[PKTap(0, .22, .35)];                                                     // hop
        case 2: return @[PKTap(0, .62, .95), PKTap(.03, .3, .8)];                                 // lego snap
        case 3: return @[PKTap(0, .5, .6), PKTap(.08, .65, .75), PKTap(.17, .85, .9)];            // train full
        case 4: return @[PKTap(0, .6, .5), PKTap(.12, .7, .6), PKTap(.24, .8, .7), PKBuzz(.36, .25, .55, .3)]; // win
        case 5: return @[PKBuzz(0, .18, .7, .15), PKTap(.26, .8, .2), PKTap(.42, .6, .15)];      // fail
        case 6: return @[PKTap(0, 1, .25), PKBuzz(.02, .12, .6, .1)];                            // hammer
        case 7: return @[PKTap(0, .55, .2), PKTap(.1, .45, .2)];                                  // warning
        case 8: return @[PKTap(0, .3, 1), PKTap(.05, .35, 1), PKTap(.1, .4, 1)];                  // reveal sparkle
        default: return @[PKTap(0, .3, .3)];                                                      // soft
    }
}

static void PKPrepare() {
    if (!pkSelection) {
        pkSelection = [UISelectionFeedbackGenerator new];
        pkLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        pkHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        pkNotify = [UINotificationFeedbackGenerator new];
        pkSupported = [CHHapticEngine capabilitiesForHardware].supportsHaptics;
    }
    if (!pkSupported || pkEngine) return;
    NSError *error = nil;
    pkEngine = [[CHHapticEngine alloc] initAndReturnError:&error];
    if (!pkEngine) { NSLog(@"[PKHaptics] UIKit fallback: %@", error); return; }
    pkEngine.playsHapticsOnly = YES;
    pkEngine.autoShutdownEnabled = YES;
    pkPlayers = [NSMutableDictionary new];
    pkEngine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) { pkRunning = NO; };
    pkEngine.resetHandler = ^{ dispatch_async(dispatch_get_main_queue(), ^{
        [pkPlayers removeAllObjects];
        pkRunning = pkEnabled && [pkEngine startAndReturnError:nil];
    }); };
}

extern "C" {
void PKHapticsEnable(int enabled) {
    pkEnabled = enabled != 0;
    PKPrepare();
    if (pkEnabled) { [pkSelection prepare]; if (pkEngine && !pkRunning) pkRunning = [pkEngine startAndReturnError:nil]; }
    else if (pkEngine) { pkRunning = NO; [pkEngine stopWithCompletionHandler:nil]; }
}

void PKHapticsPlay(int kind) {
    if (!pkEnabled) return;
    PKPrepare();
    NSError *error = nil;
    if (pkEngine && (pkRunning || (pkRunning = [pkEngine startAndReturnError:&error]))) {
        id<CHHapticPatternPlayer> player = pkPlayers[@(kind)];
        if (!player) {
            CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:PKPattern(kind) parameters:@[] error:&error];
            if (pattern) player = [pkEngine createPlayerWithPattern:pattern error:&error];
            if (player) pkPlayers[@(kind)] = player;
        }
        if (player && [player startAtTime:CHHapticTimeImmediate error:&error]) return;
        pkRunning = NO;
    }
    switch (kind) {
        case 0: [pkSelection selectionChanged]; break;
        case 3: case 4: case 8: [pkNotify notificationOccurred:UINotificationFeedbackTypeSuccess]; break;
        case 5: [pkNotify notificationOccurred:UINotificationFeedbackTypeError]; break;
        case 7: [pkNotify notificationOccurred:UINotificationFeedbackTypeWarning]; break;
        case 6: [pkHeavy impactOccurred]; break;
        default: [pkLight impactOccurredWithIntensity:kind == 2 ? 0.9 : 0.5]; break;
    }
}
}
