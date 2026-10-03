namespace Bemo
open System
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32.Forms
open System.Resources
open System.Reflection

module DarkTheme =
    let isDark() =
        try
            match Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", box 1) with
            | :? int as v -> v = 0
            | _ -> false
        with _ -> false
    let back = Color.FromArgb(32, 32, 32)
    let inputBack = Color.FromArgb(45, 45, 45)
    let fore = Color.FromArgb(230, 230, 230)

    let rec apply (c:Control) =
        match c with
        | :? TextBox | :? NumericUpDown | :? ComboBox -> c.BackColor <- inputBack
        | :? Button as b ->
            b.BackColor <- inputBack
            b.FlatStyle <- FlatStyle.Flat
        | _ -> c.BackColor <- back
        c.ForeColor <- fore
        match c with
        | :? ToolStrip as ts ->
            ts.RenderMode <- ToolStripRenderMode.System
            ts.BackColor <- back
            ts.ForeColor <- fore
            for item in ts.Items do
                item.ForeColor <- fore
        | _ -> ()
        for child in c.Controls do apply child

type DesktopManagerForm() =
    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());
    let title = sprintf "WindowTabs Settings (version %s)"  (Services.program.version)
    let tabs = List2([
        ProgramView() :> ISettingsView
        AppearanceView() :> ISettingsView
        HotKeyView() :> ISettingsView
        WorkspaceView() :> ISettingsView
        DiagnosticsView() :> ISettingsView
        ])
    let tabControl : TabControl = {
        new TabControl() with
            override this.OnKeyDown(e:KeyEventArgs) =
                if (e.KeyData = (Keys.Control ||| Keys.PageDown) ||
                    e.KeyData = (Keys.Control  ||| Keys.PageUp)) then
                    ()
                else
                    base.OnKeyDown(e)
        }
    let font = Font(resources.GetString("Font"), 10f)

    let form = 
        let form = Form()
        tabs.iter <| fun view ->
            let page = TabPage(view.title)
            let control = view.control
            control.Dock <- DockStyle.Fill
            page.Controls.Add(control)
            page.Dock <- DockStyle.Fill
            page.Font <- font
            tabControl.TabPages.Add(page)
            page.BackColor <- Color.White
        tabControl.Dock <- DockStyle.Fill
        form.Controls.Add(tabControl)
        form.FormBorderStyle <- FormBorderStyle.SizableToolWindow
        form.StartPosition <- FormStartPosition.CenterScreen
        form.Size <- Size(800, 600)
        form.Text <- title
        form.Icon <- Services.openIcon("Bemo.ico")
        form.Font <- font
        form.BackColor <- Color.White
        // The layout uses 96-DPI pixel sizes while fonts already follow the display DPI,
        // so scale the geometry to match (TableLayoutPanels scale their absolute rows too).
        form.Scale(SizeF(float32(Dpi.scale()), float32(Dpi.scale())))
        if DarkTheme.isDark() then
            DarkTheme.apply form
        form

    member this.show() =
        form.Show()
        form.Activate()

    member this.showView(view) =
        let tabIndex = tabs.findIndex(fun tab -> tab.key = view)
        tabControl.SelectedIndex <- tabIndex
        form.Show()
        form.Activate()
        
