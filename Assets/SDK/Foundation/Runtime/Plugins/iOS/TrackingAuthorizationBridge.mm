#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <UIKit/UIKit.h>

extern "C" int TrackingAuthorizationGetStatus()
{
    if (@available(iOS 14.0, *))
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    return -1;
}

extern "C" int TrackingAuthorizationIsApplicationActive()
{
    return [UIApplication sharedApplication].applicationState == UIApplicationStateActive ? 1 : 0;
}
