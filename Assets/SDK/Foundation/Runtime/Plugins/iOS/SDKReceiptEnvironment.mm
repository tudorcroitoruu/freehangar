#import <Foundation/Foundation.h>

extern "C" bool SDKIsSandboxReceipt()
{
    NSURL *receiptURL = NSBundle.mainBundle.appStoreReceiptURL;
    if (receiptURL == nil) return false;
    return [receiptURL.lastPathComponent isEqualToString:@"sandboxReceipt"];
}
