namespace Bemo
open System
open System.Windows.Forms
open System.Reflection
open System.Resources

type NotifyIconPlugin() as this =
    let Cell = CellScope()
    
    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());
    let menuItems = System.Collections.Generic.List<ContextMenuItem>()
    // owner of the native popup menu (it needs a window to receive the menu messages)
    let menuOwner = lazy (
        let window = new NativeWindow()
        window.CreateHandle(CreateParams())
        window)

    let showMenu() =
        let hwnd = menuOwner.Value.Handle
        // the menu closes when it loses focus, so the owner must be the foreground window
        WinUserApi.SetForegroundWindow(hwnd).ignore
        let pt = Cursor.Position
        Win32Menu.show hwnd (Pt(pt.X, pt.Y)) (List2(menuItems))

    member this.icon = Cell.cacheProp this <| fun() ->
        let notifyIcon = new NotifyIcon()
        notifyIcon.Visible <- true
        notifyIcon.Text <- "WindowTabs"
        notifyIcon.Icon <- Services.openIcon("Bemo.ico")
        // the classic native Windows menu instead of a WinForms ContextMenuStrip
        notifyIcon.MouseUp.Add <| fun e ->
            if e.Button = MouseButtons.Right then showMenu()
        notifyIcon.DoubleClick.Add <| fun _ -> Services.managerView.show()
        notifyIcon

    member this.addItem(text:string, handler) =
        menuItems.Add(CmiRegular({ text = text; image = None; flags = List2(); click = handler }))

    member this.onNewVersion() =
        this.icon.ShowBalloonTip(
            1000,
            "A new version is available.",
            "Please visit windowtabs.com to download the latest version.",
            ToolTipIcon.Info
        )


    interface IPlugin with
        member this.init() =
            // the icon is created lazily: touch it so it shows up in the tray
            this.icon.ignore
            this.addItem(resources.GetString("Settings"), fun() -> Services.managerView.show())
            //this.addItem(resources.GetString("Feedback"), Forms.openFeedback) // 404 Not Found.
            menuItems.Add(CmiSeparator)
            this.addItem(resources.GetString("CloseWindowTabs"), fun() -> Services.program.shutdown())
            Services.program.newVersion.Add this.onNewVersion

    interface IDisposable with
        member this.Dispose() = this.icon.Dispose()