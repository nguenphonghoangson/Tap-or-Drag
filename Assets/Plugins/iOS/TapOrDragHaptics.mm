// Haptic feedback for Tap Or Drag (called from Assets/_Game/Scripts/Audio/Haptics.cs through DllImport("__Internal")).
// Unity runs scripts on the main thread on iOS, so the UIKit feedback generators can be driven directly.
// Generators are created once and kept prepared so the Taptic Engine responds without latency.

#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator* impactGenerators[5];
static UISelectionFeedbackGenerator* selectionGenerator;
static UINotificationFeedbackGenerator* notificationGenerator;

static UIImpactFeedbackStyle ImpactStyle(int style)
{
    switch (style)
    {
        case 0: return UIImpactFeedbackStyleLight;
        case 1: return UIImpactFeedbackStyleMedium;
        case 2: return UIImpactFeedbackStyleHeavy;
        case 3: return UIImpactFeedbackStyleSoft;
        default: return UIImpactFeedbackStyleRigid;
    }
}

static UIImpactFeedbackGenerator* ImpactGenerator(int style)
{
    if (style < 0 || style > 4) style = 1;
    if (impactGenerators[style] == nil)
        impactGenerators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:ImpactStyle(style)];
    return impactGenerators[style];
}

extern "C"
{
    void TapOrDrag_HapticsPrepare()
    {
        for (int i = 0; i < 5; i++) [ImpactGenerator(i) prepare];
        if (selectionGenerator == nil) selectionGenerator = [[UISelectionFeedbackGenerator alloc] init];
        if (notificationGenerator == nil) notificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
        [selectionGenerator prepare];
        [notificationGenerator prepare];
    }

    // style: 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid. intensity: 0..1.
    void TapOrDrag_HapticImpact(int style, float intensity)
    {
        UIImpactFeedbackGenerator* generator = ImpactGenerator(style);
        [generator impactOccurredWithIntensity:(CGFloat)fmaxf(0.f, fminf(1.f, intensity))];
        [generator prepare];
    }

    void TapOrDrag_HapticSelection()
    {
        if (selectionGenerator == nil) selectionGenerator = [[UISelectionFeedbackGenerator alloc] init];
        [selectionGenerator selectionChanged];
        [selectionGenerator prepare];
    }

    // type: 0 success, 1 warning, 2 error.
    void TapOrDrag_HapticNotification(int type)
    {
        if (notificationGenerator == nil) notificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
        UINotificationFeedbackType feedback = type == 0 ? UINotificationFeedbackTypeSuccess
            : type == 1 ? UINotificationFeedbackTypeWarning : UINotificationFeedbackTypeError;
        [notificationGenerator notificationOccurred:feedback];
        [notificationGenerator prepare];
    }
}
