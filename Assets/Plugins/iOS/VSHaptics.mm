// Void Stunts — Taptic Engine bridge (called from C# Haptics.Play)
#import <UIKit/UIKit.h>

extern "C" void VS_Haptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind) {
            case 0: { UISelectionFeedbackGenerator *g = [UISelectionFeedbackGenerator new]; [g selectionChanged]; break; }
            case 1: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [g impactOccurred]; break; }
            case 2: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium]; [g impactOccurred]; break; }
            case 3: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy]; [g impactOccurred]; break; }
            case 4: { UINotificationFeedbackGenerator *g = [UINotificationFeedbackGenerator new]; [g notificationOccurred:UINotificationFeedbackTypeSuccess]; break; }
            default: { UINotificationFeedbackGenerator *g = [UINotificationFeedbackGenerator new]; [g notificationOccurred:UINotificationFeedbackTypeError]; break; }
        }
    });
}
