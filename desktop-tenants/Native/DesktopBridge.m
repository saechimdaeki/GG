// DesktopBridge: turns the Unity player window into a transparent, click-through
// overlay on connected monitors and reports other apps' window rects. Build with tools/build-plugin.sh.
// Every coordinate handed to Unity is in points with a top-left origin at the TARGET
// monitor's top-left corner, so the game never needs to know where that monitor sits.
#import <Cocoa/Cocoa.h>
#import <QuartzCore/QuartzCore.h>

static NSNumber *targetID;
static NSWindow *primary;
static NSMutableArray<NSWindow *> *overlays;
static NSMutableArray<NSWindow *> *renderWindows;
static NSMutableArray<NSNumber *> *displays;
static NSMutableIndexSet *inactiveOutputs;
static BOOL externalOnly;
static BOOL clickThrough = YES;

static NSWindow *Win(void) {
    NSWindow *w = NSApp.mainWindow ?: NSApp.keyWindow;
    return w ?: NSApp.windows.firstObject;
}

static NSNumber *DisplayID(NSScreen *screen) {
    return screen.deviceDescription[@"NSScreenNumber"];
}

static BOOL IsBuiltIn(NSScreen *screen) {
    return CGDisplayIsBuiltin(DisplayID(screen).unsignedIntValue) != 0;
}

static NSScreen *FirstExternal(NSArray<NSScreen *> *screens) {
    for (NSScreen *screen in screens)
        if (!IsBuiltIn(screen)) return screen;
    return nil;
}

static NSScreen *Target(void) {
    NSArray<NSScreen *> *screens = NSScreen.screens;
    for (NSScreen *screen in screens)
        if ([DisplayID(screen) isEqual:targetID]) return screen;
    return (externalOnly ? FirstExternal(screens) : nil) ?: screens.firstObject;
}

/// Global top-left-origin space (CoreGraphics) → target-screen-local offset.
static CGPoint Origin(void) {
    NSRect f = Target().frame, main = NSScreen.screens.firstObject.frame;
    return CGPointMake(f.origin.x, main.size.height - (f.origin.y + f.size.height));
}

static void ClearLayers(NSView *v) {
    v.wantsLayer = YES;
    v.layer.opaque = NO;
    v.layer.backgroundColor = NSColor.clearColor.CGColor;
    for (NSView *s in v.subviews) ClearLayers(s);
}

/// Number of connected monitors.
int DB_ScreenCount(void) { return (int)NSScreen.screens.count; }

/// "1: Built-in Retina Display 1512x982 @0,0 (main)" lines, for the player log.
const char *DB_ScreenList(void) {
    NSMutableString *s = [NSMutableString string];
    NSArray<NSScreen *> *all = NSScreen.screens;
    for (NSUInteger i = 0; i < all.count; i++) {
        NSRect f = all[i].frame;
        NSString *name = @"display";
        if (@available(macOS 10.15, *)) name = all[i].localizedName;
        [s appendFormat:@"%lu: %@ %.0fx%.0f @%.0f,%.0f%@\n", (unsigned long)i + 1, name,
            f.size.width, f.size.height, f.origin.x, f.origin.y, i == 0 ? @" (main)" : @""];
    }
    return strdup(s.UTF8String);
}

static void Configure(NSWindow *window, NSScreen *screen) {
    window.styleMask = NSWindowStyleMaskBorderless | (window.styleMask & NSWindowStyleMaskNonactivatingPanel);
    window.opaque = NO;
    window.hasShadow = NO;
    window.backgroundColor = NSColor.clearColor;
    window.level = NSStatusWindowLevel;
    window.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces |
                                NSWindowCollectionBehaviorStationary |
                                NSWindowCollectionBehaviorIgnoresCycle |
                                NSWindowCollectionBehaviorFullScreenAuxiliary;
    window.ignoresMouseEvents = clickThrough;
    [window setFrame:screen.frame display:YES];
    ClearLayers(window.contentView);
    [window orderFrontRegardless];
}

int DB_MakeOverlay(int screen, int external) {
    if (primary) return 1;
    NSWindow *window = Win();
    NSArray<NSScreen *> *all = NSScreen.screens;
    if (!window || all.count == 0) return 0;
    NSScreen *selectedExternal = external ? FirstExternal(all) : nil;
    externalOnly = selectedExternal != nil;
    NSScreen *target = selectedExternal ?: all[MAX(0, MIN(screen, (int)all.count - 1))];
    targetID = DisplayID(target);
    primary = window;
    overlays = [NSMutableArray arrayWithObject:window];
    renderWindows = [NSMutableArray arrayWithObject:window];
    displays = [NSMutableArray arrayWithObject:DisplayID(target)];
    inactiveOutputs = [NSMutableIndexSet indexSet];
    Configure(window, target);
    return 1;
}

/// Registers the new window made by Unity's most recent Display.Activate.
int DB_BindDisplay(void) {
    for (NSWindow *window in [NSApp.windows copy]) {
        NSScreen *screen = window.screen;
        if (!screen || [renderWindows containsObject:window] || [overlays containsObject:window]) continue;
        if (!window.visible || window.frame.size.width < 400 || window.frame.size.height < 300) continue;
        if ([DisplayID(screen) isEqual:DisplayID(Target())] || (externalOnly && IsBuiltIn(screen))) {
            // Output zero already renders the home screen. Do not render a duplicate
            // there or create an overlay on the excluded built-in screen.
            [inactiveOutputs addIndex:overlays.count];
            [renderWindows addObject:window];
            [overlays addObject:window];
            [displays addObject:DisplayID(screen)];
            [window orderOut:nil];
            return 1;
        }
        // Unity opens secondary outputs in its own display spaces. Rehost the
        // Metal view in a regular overlay panel so it follows the desktop space.
        NSView *renderView = window.contentView;
        if (!renderView) continue;
        NSPanel *panel = [[NSPanel alloc] initWithContentRect:screen.frame
            styleMask:NSWindowStyleMaskBorderless | NSWindowStyleMaskNonactivatingPanel
            backing:NSBackingStoreBuffered defer:NO];
        panel.hidesOnDeactivate = NO;
        window.contentView = nil;
        panel.contentView = renderView;
        [window orderOut:nil];
        [renderWindows addObject:window];
        [overlays addObject:panel];
        [displays addObject:DisplayID(screen)];
        Configure(panel, screen);
        return 1;
    }
    return 0;
}

void DB_SetClickThrough(int on) {
    clickThrough = on != 0;
    for (NSWindow *window in overlays) window.ignoresMouseEvents = clickThrough;
}

void DB_Refresh(void) {
    for (NSUInteger i = 0; i < overlays.count; i++) {
        NSWindow *window = overlays[i];
        if ([inactiveOutputs containsIndex:i]) { if (window.visible) [window orderOut:nil]; continue; }
        window.opaque = NO;
        window.backgroundColor = NSColor.clearColor;
        ClearLayers(window.contentView);
    }
}

/// One native screen for each registered camera, in primary-screen-local coordinates.
int DB_DisplayRect(int index, float *out5) {
    if (index < 0 || (NSUInteger)index >= displays.count || [inactiveOutputs containsIndex:index]) return 0;
    NSNumber *identifier = displays[index];
    NSWindow *window = overlays[index];
    if (index == 0) identifier = DisplayID(Target());
    for (NSScreen *screen in NSScreen.screens) {
        if (![DisplayID(screen) isEqual:identifier]) continue;
        NSRect frame = screen.frame;
        NSRect origin = Target().frame;
        out5[0] = NSMinX(frame) - NSMinX(origin);
        out5[1] = NSMaxY(origin) - NSMaxY(frame);
        out5[2] = NSWidth(frame);
        out5[3] = NSHeight(frame);
        out5[4] = NSMaxY(origin) - NSMinY(screen.visibleFrame);
        if (!NSEqualRects(window.frame, frame)) [window setFrame:frame display:YES];
        if (!window.visible) [window orderFrontRegardless];
        return 1;
    }
    [window orderOut:nil];
    return 0;
}

/// Target screen size and its usable area (excludes menu bar and Dock), screen-local, top-left origin.
void DB_Screen(float *out6) {
    NSRect f = Target().frame, v = Target().visibleFrame;
    out6[0] = f.size.width;
    out6[1] = f.size.height;
    out6[2] = v.origin.x - f.origin.x;
    out6[3] = (f.origin.y + f.size.height) - (v.origin.y + v.size.height); // top of usable area
    out6[4] = v.size.width;
    out6[5] = v.size.height;
}

/// Mouse in target-screen points (top-left origin; may be outside it) and pressed-button bitmask.
void DB_Mouse(float *out3) {
    NSPoint p = NSEvent.mouseLocation;
    NSRect f = Target().frame;
    out3[0] = p.x - f.origin.x;
    out3[1] = (f.origin.y + f.size.height) - p.y;
    out3[2] = (float)NSEvent.pressedMouseButtons;
}

/// Fills up to `max` windows front-to-back as [id, x, y, w, h] in target-screen points. Returns count.
/// Only normal app windows (layer 0) of other processes; window names are never read,
/// so no Screen Recording permission is needed.
int DB_Windows(float *out, int max) {
    CFArrayRef list = CGWindowListCopyWindowInfo(
        kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements, kCGNullWindowID);
    if (!list) return 0;
    CGPoint o = Origin();
    int me = NSProcessInfo.processInfo.processIdentifier, n = 0;
    for (NSDictionary *d in (__bridge NSArray *)list) {
        if (n >= max) break;
        if ([d[(id)kCGWindowLayer] intValue] != 0) continue;
        if ([d[(id)kCGWindowOwnerPID] intValue] == me) continue;
        if ([d[(id)kCGWindowAlpha] floatValue] < 0.1f) continue;
        CGRect r;
        if (!CGRectMakeWithDictionaryRepresentation((__bridge CFDictionaryRef)d[(id)kCGWindowBounds], &r)) continue;
        if (r.size.width < 80 || r.size.height < 40) continue;
        float *e = out + n++ * 5;
        e[0] = [d[(id)kCGWindowNumber] floatValue];
        e[1] = r.origin.x - o.x; e[2] = r.origin.y - o.y; e[3] = r.size.width; e[4] = r.size.height;
    }
    CFRelease(list);
    return n;
}

#import <mach/mach.h>
#import <sys/sysctl.h>

/// System load for the status board: [cpu %, memory used GB, memory total GB].
/// CPU is the busy share of all cores since the previous call.
void DB_Stats(float *out3) {
    static unsigned long long lastBusy, lastTotal;
    host_cpu_load_info_data_t cpu;
    mach_msg_type_number_t n = HOST_CPU_LOAD_INFO_COUNT;
    if (host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, (host_info_t)&cpu, &n) == KERN_SUCCESS) {
        unsigned long long busy = (unsigned long long)cpu.cpu_ticks[CPU_STATE_USER] + cpu.cpu_ticks[CPU_STATE_SYSTEM] + cpu.cpu_ticks[CPU_STATE_NICE];
        unsigned long long total = busy + cpu.cpu_ticks[CPU_STATE_IDLE];
        out3[0] = total > lastTotal ? 100.0f * (busy - lastBusy) / (total - lastTotal) : 0;
        lastBusy = busy; lastTotal = total;
    }
    vm_statistics64_data_t vm;
    n = HOST_VM_INFO64_COUNT;
    vm_size_t page = 0;
    host_page_size(mach_host_self(), &page);
    if (host_statistics64(mach_host_self(), HOST_VM_INFO64, (host_info64_t)&vm, &n) == KERN_SUCCESS) {
        double used = ((double)vm.active_count + vm.wire_count + vm.compressor_page_count) * page;
        out3[1] = (float)(used / 1073741824.0);
    }
    unsigned long long mem = 0;
    size_t len = sizeof(mem);
    sysctlbyname("hw.memsize", &mem, &len, NULL, 0);
    out3[2] = (float)(mem / 1073741824.0);
}
