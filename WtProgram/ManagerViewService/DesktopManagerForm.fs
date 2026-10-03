namespace Bemo
open System
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32.Forms
open System.Resources
open System.Reflection

/// The settings window, laid out like the Windows 11 Settings app:
/// navigation on the left, the selected page on the right.
///
/// Creating native windows is the slow part of showing a page, so pages are built once and then
/// only shown or hidden, the ones not opened yet are prepared in the background after the window
/// appears, and closing the window just hides it.
type DesktopManagerForm() =
    // Segoe Fluent Icons glyph, title key and constructor of each page
    let pageInfo = List2([
        ("", "Programs", SettingsViewType.ProgramSettings, fun () -> ProgramView() :> ISettingsView)
        ("", "Appearance", SettingsViewType.AppearanceSettings, fun () -> AppearanceView() :> ISettingsView)
        ("", "Behavior", SettingsViewType.HotKeySettings, fun () -> HotKeyView() :> ISettingsView)
        ("", "Workspace", SettingsViewType.LayoutSettings, fun () -> WorkspaceView() :> ISettingsView)
        ("", "Diagnostics", SettingsViewType.DiagnosticsSettings, fun () -> DiagnosticsView() :> ISettingsView)
        ])

    let views : ISettingsView option array = Array.create pageInfo.length None
    let pages : Control option array = Array.create pageInfo.length None

    let content =
        let panel = Panel()
        panel.Dock <- DockStyle.Fill
        panel.BackColor <- FluentTheme.Background
        panel

    let createPage index =
        match pages.[index] with
        | Some(page) -> page
        | None ->
            let (_, _, _, create) = pageInfo.at(index)
            let view = create()
            let page = view.control
            page.Dock <- DockStyle.Fill
            page.Visible <- false
            content.Controls.Add(page)
            // lay the page out while it has no window handles: moving controls is cheap then,
            // and they get created directly at their final position
            page.Bounds <- content.ClientRectangle
            page.PerformLayout()
            views.[index] <- Some(view)
            pages.[index] <- Some(page)
            page

    let showPage index =
        let page = createPage index
        page.Visible <- true
        page.BringToFront()
        pages |> Array.iteri (fun i other ->
            if i <> index then other |> Option.iter (fun other -> other.Visible <- false))

    /// Builds the pages not opened yet, one per timer tick so the window stays responsive.
    /// Each is shown once behind the current page, which creates its windows, then hidden again.
    let prepareRemainingPages() =
        let timer = new Timer()
        timer.Interval <- 50
        timer.Tick.Add <| fun _ ->
            match pages |> Array.tryFindIndex Option.isNone with
            | Some(index) ->
                let page = createPage index
                page.SendToBack()
                page.Visible <- true
                page.Visible <- false
            | None ->
                timer.Stop()
                timer.Dispose()
        timer.Start()

    let nav =
        let nav = FluentNavList()
        nav.Dock <- DockStyle.Fill
        nav.Font <- FluentTheme.Body
        pageInfo.iter <| fun (glyph, titleKey, _, _) -> nav.AddItem(glyph, FluentUI.text titleKey)
        nav.SelectedIndexChanged.Add <| fun _ -> showPage nav.SelectedIndex
        nav

    let sidebar =
        let panel = Panel()
        panel.Dock <- DockStyle.Left
        panel.Width <- Dpi.px 280
        panel.BackColor <- FluentTheme.Background
        panel.Padding <- Padding(Dpi.px 12, Dpi.px 16, Dpi.px 8, Dpi.px 16)

        let name = FluentUI.label "WindowTabs"
        name.Font <- FluentTheme.Subtitle
        name.AutoSize <- false
        name.Dock <- DockStyle.Top
        name.Height <- Dpi.px 52
        name.Padding <- Padding(Dpi.px 16, 0, 0, Dpi.px 12)
        name.TextAlign <- ContentAlignment.MiddleLeft

        panel.Controls.Add(nav)
        panel.Controls.Add(name)
        panel

    let form =
        let form = new FluentForm()
        form.SuspendLayout()
        form.Controls.Add(content)
        form.Controls.Add(sidebar)
        form.StartPosition <- FormStartPosition.CenterScreen
        form.ClientSize <- Size(Dpi.px 1000, Dpi.px 700)
        form.MinimumSize <- Size(Dpi.px 760, Dpi.px 520)
        form.Text <- FluentUI.text "SettingsTitle"
        form.Icon <- Services.openIcon("Bemo.ico")
        form.ResumeLayout()
        // keep the window (and its pages) for the next time it is opened
        // (a WM_CLOSE sent by another program arrives with CloseReason.None, so only let the
        // window really close when WindowTabs or Windows is shutting down)
        form.FormClosing.Add <| fun e ->
            match e.CloseReason with
            | CloseReason.ApplicationExitCall
            | CloseReason.WindowsShutDown
            | CloseReason.TaskManagerClosing -> ()
            | _ ->
                e.Cancel <- true
                form.Hide()
        form.Shown.Add <| fun _ -> prepareRemainingPages()
        form

    member this.show() =
        if nav.SelectedIndex < 0 then nav.SelectedIndex <- 0
        if form.Visible.not then
            // reopening a hidden window: the list of programs may have changed meanwhile
            if form.IsHandleCreated then
              views |> Array.iter (function
                | Some(:? ProgramView as programs) -> programs.refresh()
                | _ -> ())
            form.Show()
        form.BringToForeground()

    member this.isDisposed = form.IsDisposed

    member this.showView(view) =
        nav.SelectedIndex <- pageInfo.findIndex(fun (_, _, key, _) -> key = view)
        this.show()
