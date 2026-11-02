#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>
#import "UI/UnityScene.h"
#import "UnityAppController.h"

// Keeps the branded launch storyboard on screen until Unity renders its own (identical) splash,
// so there is no black frame and no Unity logo between the system launch and the game.
static UIWindow *pkLaunchWindow;

@interface PKLaunchScene : UnityScene { UIWindow *_pkWindow; }
@property(nonatomic, strong) UIWindow *window;
@end

@implementation PKLaunchScene
@synthesize window = _pkWindow;
- (void)scene:(UIScene *)scene willConnectToSession:(UISceneSession *)session options:(UISceneConnectionOptions *)options {
    [super scene:scene willConnectToSession:session options:options];
    if (![scene isKindOfClass:UIWindowScene.class]) return;
    if (!self.window) {
        self.window = [[UIWindow alloc] initWithWindowScene:(UIWindowScene *)scene];
        self.window.rootViewController = [[UIStoryboard storyboardWithName:@"PKLaunchScreen" bundle:NSBundle.mainBundle] instantiateInitialViewController];
    }
    pkLaunchWindow = self.window;
    pkLaunchWindow.windowLevel = UIWindowLevelNormal + 1;
    pkLaunchWindow.userInteractionEnabled = NO;
    [pkLaunchWindow makeKeyAndVisible];
    [pkLaunchWindow layoutIfNeeded];
    [CATransaction flush];
}
@end

extern "C" void PKHideLaunchScreen() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!pkLaunchWindow) return;
        id delegate = pkLaunchWindow.windowScene.delegate;
        UIWindow *launch = pkLaunchWindow;
        pkLaunchWindow = nil;
        [UIView animateWithDuration:0.25 animations:^{ launch.alpha = 0; } completion:^(BOOL finished) {
            launch.hidden = YES;
            launch.rootViewController = nil;
            if ([delegate isKindOfClass:PKLaunchScene.class]) ((PKLaunchScene *)delegate).window = GetAppController().window;
            [GetAppController().window makeKeyAndVisible];
        }];
    });
}
