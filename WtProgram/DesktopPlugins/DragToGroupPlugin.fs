namespace Bemo
open System
open System.Drawing
open System.Windows.Forms
open Bemo.Win32.Forms

/// Translucent highlight over the window a dragged window will be grouped with.
/// It never takes focus or mouse input, and sits right above that window in the z-order,
/// so the window being dragged stays in front of it.
// (no "as this": Form calls CreateParams while it is being constructed)
type private GroupTargetHighlight() =
    inherit Form()

    override this.ShowWithoutActivation = true

    override this.CreateParams =
        let cp = base.CreateParams
        cp.ExStyle <- cp.ExStyle ||| WindowsExtendedStyles.WS_EX_TOOLWINDOW ||| WindowsExtendedStyles.WS_EX_NOACTIVATE ||| WindowsExtendedStyles.WS_EX_TRANSPARENT
        cp

    override this.OnHandleCreated(e) =
        base.OnHandleCreated(e)
        // rounded like the windows it covers (Windows 11)
        let DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND = 33, 2
        let mutable preference = DWMWCP_ROUND
        DwmApi.DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, &preference, sizeof<int>) |> ignore

    member this.showOver(target:IntPtr, bounds:Rectangle) =
        // SetWindowPos puts a window below the one given, so give the window right above the target
        let above = WinUserApi.GetWindow(target, GetWindowConstants.GW_HWNDPREV)
        let flags = SetWindowPosFlags.SWP_NOACTIVATE ||| SetWindowPosFlags.SWP_SHOWWINDOW
        let flags = if above = this.Handle then flags ||| SetWindowPosFlags.SWP_NOZORDER else flags
        WinUserApi.SetWindowPos(this.Handle, above, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags) |> ignore

    member this.hide() =
        WinUserApi.ShowWindow(this.Handle, ShowWindowCommands.SW_HIDE) |> ignore

type private WindowDrag = {
    hwnd : IntPtr
    // a tab dragged out of its tab strip, rather than a window moved by its title bar
    isTab : bool
    startSize : Sz
    // maximized and snapped windows change size when dragged, without being resized
    restoresOnMove : bool
    mutable isResize : bool
    // the group under the cursor (none for a window without tabs) and its top window
    mutable target : (IGroup option * IntPtr) option
    }

/// Groups windows the way Groupy does: drag a window by its title bar, or one of its tabs,
/// while holding a key (Ctrl by default) and release it over another window to put the two
/// in the same group.
/// Works for any program; windows of programs without tabs get them only while grouped.
type DragToGroupPlugin() =
    let os = OS()
    let invoker = InvokerService.invoker
    let timer = new Timer(Interval = 30)
    let highlight = lazy (
        let highlight = new GroupTargetHighlight()
        highlight.FormBorderStyle <- FormBorderStyle.None
        highlight.ShowInTaskbar <- false
        highlight.StartPosition <- FormStartPosition.Manual
        highlight.BackColor <- FluentTheme.Accent
        highlight.Opacity <- 0.35
        highlight)
    let mutable drag : WindowDrag option = None
    let mutable moveSizeHook : IDisposable option = None

    let modifierKey() =
        match Services.settings.getValue("dragToGroupKey").cast<string>() with
        | "Ctrl" -> Some(VirtualKeyCodes.VK_CONTROL)
        | "Shift" -> Some(VirtualKeyCodes.VK_SHIFT)
        | "Alt" -> Some(VirtualKeyCodes.VK_MENU)
        | _ -> None

    let isModifierDown() =
        modifierKey() |> Option.exists Win32Helper.IsKeyPressed

    let groupOf hwnd =
        Services.desktop.groups.tryFind(fun g -> g.windows.contains((=) hwnd))

    /// The window under the cursor that the dragged window would be grouped with: its group
    /// (if it has one) and the group's top window. The dragged window's own group is skipped,
    /// and the tab strip of a group counts as part of the group, except for a dragged tab:
    /// dropping that on a tab strip inserts it there as usual.
    let findTarget (dragged:IntPtr) (isTab:bool) =
        let pt = Cursor.Position.Pt
        let groups = Services.desktop.groups
        let source = groupOf dragged
        let isDragged hwnd =
            hwnd = dragged ||
            source |> Option.exists (fun s -> hwnd = s.hwnd || s.windows.contains((=) hwnd))
        let hwnds = Win32Helper.GetWindowsInZOrder()
        let overlay = if highlight.IsValueCreated then highlight.Value.Handle else IntPtr.Zero
        let rec scan i (stripGroup:IGroup option) =
            if i >= hwnds.Length then None
            else
                let hwnd = hwnds.[i]
                let window = os.windowFromHwnd(hwnd)
                let next() = scan (i + 1) stripGroup
                if hwnd = overlay || isDragged hwnd then next()
                elif window.isVisible.not || window.isMinimized || Win32Helper.IsCloaked(hwnd) then next()
                else
                    match stripGroup with
                    | Some(group) when group.windows.contains((=) hwnd) -> Some(Some(group), hwnd)
                    | _ ->
                        let isUnderCursor() = window.bounds.containsPoint(pt) && Win32Helper.GetVisibleBounds(hwnd).Rect.containsPoint(pt)
                        match groups.tryFind(fun g -> g.hwnd = hwnd) with
                        | Some(group) ->
                            // the group's top window comes after its tab strip
                            if isUnderCursor() then
                                if isTab then None else scan (i + 1) (Some(group))
                            else next()
                        | None ->
                            // click-through overlays and our own windows don't hide what is below them
                            if window.pid.isCurrentProcess || window.hasStyleEx WindowsExtendedStyles.WS_EX_TRANSPARENT then next()
                            elif isUnderCursor() then
                                match groupOf hwnd with
                                | Some(group) -> Some(Some(group), hwnd)
                                | None when Services.filter.isAppWindow(hwnd) -> Some(None, hwnd)
                                | None -> None
                            else next()
        scan 0 None

    let hideHighlight() =
        if highlight.IsValueCreated then highlight.Value.hide()

    let update() =
        match drag with
        | Some(info) ->
            let window = os.windowFromHwnd(info.hwnd)
            if info.isTab.not && info.restoresOnMove.not && window.isWindow && window.size.record <> info.startSize.record then
                info.isResize <- true
            let target =
                if info.isResize || window.isWindow.not || isModifierDown().not then None
                else findTarget info.hwnd info.isTab
            info.target <- target
            match target with
            | Some(_, targetHwnd) -> highlight.Value.showOver(targetHwnd, Win32Helper.GetVisibleBounds(targetHwnd))
            | None -> hideHighlight()
        | None -> ()

    /// Moves the dragged window, with the rest of its group, into the target's group (made
    /// for the target if it has none). The dragged window goes last so it is the selected tab.
    let groupWith (dragged:IntPtr) (target:IGroup option) (targetHwnd:IntPtr) =
        let source = groupOf dragged
        let moving =
            match source with
            | Some(source) -> source.windows.where((<>) dragged).append dragged
            | None -> List2([dragged])
        let markManual hwnd =
            if Services.filter.isTabbableWindow(hwnd).not then
                Services.filter.setManuallyGrouped hwnd true
        let addToTarget() =
            let target =
                match target with
                | Some(target) -> target
                | None ->
                    markManual targetHwnd
                    let group = Services.desktop.createGroup(Services.settings.getValue("combineIconsInTaskbar").cast<bool>())
                    group.addWindow(targetHwnd, false)
                    group
            moving.iter <| fun hwnd ->
                markManual hwnd
                target.addWindow(hwnd, false)
            Services.program.resumeTabMonitoring()
        // keep the windows from being put in a group of their own while they move
        Services.program.suspendTabMonitoring()
        match source with
        | Some(source) ->
            // remove them first, so the source group doesn't restore their caption after the
            // target group has hidden it
            let sourceInfo = source.cast<GroupInfo>()
            sourceInfo.invokeGroup <| fun() ->
                moving.iter sourceInfo.group.removeWindow
                invoker.asyncInvoke addToTarget
        | None -> addToTarget()

    let onMoveSizeStart hwnd =
        if modifierKey().IsSome && ((groupOf hwnd).IsSome || Services.filter.isAppWindow(hwnd)) then
            let window = os.windowFromHwnd(hwnd)
            let placement = window.placement
            drag <- Some({
                hwnd = hwnd
                isTab = false
                startSize = window.size
                restoresOnMove =
                    window.isMaximized ||
                    placement.rcNormalPosition.size.record <> window.size.record
                isResize = false
                target = None
                })
            timer.Start()
            update()

    let onTabDragChanged (tab:IntPtr option) =
        match tab with
        | Some(hwnd) when modifierKey().IsSome && drag.IsNone ->
            drag <- Some({
                hwnd = hwnd
                isTab = true
                startSize = Sz()
                restoresOnMove = true
                isResize = false
                target = None
                })
            timer.Start()
        | None when drag |> Option.exists (fun info -> info.isTab) ->
            timer.Stop()
            hideHighlight()
            drag <- None
        | _ -> ()

    /// A tab let go outside the tab strips: group it if the highlight was showing.
    let onTabDropped hwnd =
        match drag with
        | Some({ isTab = true; target = Some(target, targetHwnd) } as info) when info.hwnd = hwnd ->
            hideHighlight()
            groupWith hwnd target targetHwnd
            true
        | _ -> false

    let onMoveSizeEnd hwnd =
        match drag with
        | Some(info) when info.hwnd = hwnd && info.isTab.not ->
            timer.Stop()
            hideHighlight()
            drag <- None
            // group if the highlight was showing when the window was released
            match info.target with
            | Some(target, targetHwnd) when info.isResize.not ->
                let alreadyTogether =
                    match target, groupOf hwnd with
                    | Some(target), Some(source) -> target.hwnd = source.hwnd
                    | _ -> false
                if alreadyTogether.not then groupWith hwnd target targetHwnd
            | _ -> ()
        | _ -> ()

    do
        timer.Tick.Add <| fun _ -> update()

    interface IPlugin with
        member x.init() =
            Services.desktop.tabDragChanged.Add onTabDragChanged
            Services.desktop.setTabDropHandler onTabDropped
            moveSizeHook <- Some(os.setWinEventHook(WinEvent.EVENT_SYSTEM_MOVESIZESTART, WinEvent.EVENT_SYSTEM_MOVESIZEEND, (fun _ evt hwnd _ _ _ _ ->
                match enum<WinEvent>(evt) with
                | WinEvent.EVENT_SYSTEM_MOVESIZESTART -> onMoveSizeStart hwnd
                | WinEvent.EVENT_SYSTEM_MOVESIZEEND -> onMoveSizeEnd hwnd
                | _ -> ()), 0, 0))

    interface IDisposable with
        member x.Dispose() =
            moveSizeHook |> Option.iter (fun hook -> hook.Dispose())
            timer.Dispose()
            if highlight.IsValueCreated then highlight.Value.Dispose()
