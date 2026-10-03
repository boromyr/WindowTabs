namespace Bemo
open System
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32.Forms
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Resources
open System.Reflection

type DiagnosticsView() as this =
    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());

    let textBox =
        let tb = TextBox()
        tb.ReadOnly <- true
        tb.Multiline <- true
        tb.ScrollBars <- ScrollBars.Both
        tb.WordWrap <- false
        tb.BorderStyle <- BorderStyle.None
        tb.BackColor <- FluentTheme.Card
        tb.ForeColor <- FluentTheme.Text
        tb.Font <- FluentTheme.Mono
        FluentTheme.UseDarkScrollBars(tb)
        tb

    let copyToClipboard() =
        textBox.SelectAll()
        textBox.Refresh()
        textBox.Copy()
        MessageBox.Show(FluentUI.text "CopiedToClipboardMessage", FluentUI.text "CopiedToClipboard").ignore

    let copySettingsFile() =
        let fileName = "WindowTabsSettings.txt"
        let settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowTabs")
        let settingsFile = Path.Combine(settingsFolder, fileName)
        let targetFile = Path.Combine(".", fileName)
        try
            File.Copy(settingsFile, targetFile, false)
            MessageBox.Show(FluentUI.text "SettingsFileCopiedMessage", FluentUI.text "Copied").ignore
        with ex ->
            MessageBox.Show(FluentUI.text "CopyFailedMessage" + " " + ex.Message, FluentUI.text "CopyFailed").ignore

    let toolBar =
        FluentUI.row [
            FluentUI.button (Some "") (FluentUI.text "Scan") (fun () -> this.doRefresh())
            FluentUI.button (Some "") (FluentUI.text "CopyToClipboard") copyToClipboard
            FluentUI.button (Some "") (FluentUI.text "CopySettingsFile") copySettingsFile
        ]

    let panel = FluentUI.fillPage (resources.GetString "Diagnostics") [toolBar] textBox

    member this.doRefresh() =
        let os = OS()
        let windows = os.windowsInZorder
        let diagnosticsJson = JObject()
        let windowObjs = windows.map <| fun window ->
            let windowObj = JObject()
            windowObj.setIntPtr("hwnd", window.hwnd)
            windowObj.setIntPtr("style", window.style)
            windowObj.setIntPtr("styleEx", window.styleEx)
            windowObj.setIntPtr("hwndParent", window.parent.hwnd)
            windowObj.setBool("isVisible", window.isVisible)
            windowObj.setBool("isTopMost", window.isTopMost)
            windowObj.setString("title", window.text)
            windowObj.setInt32("pid", window.pid.pid)
            windowObj
        let pids = List2.distinct (windows.map(fun w -> w.pid.pid))
        let processObjs = pids.map <| fun pid ->
            let pid = Pid(pid)
            let processObj = JObject()
            processObj.setInt32("pid", pid.pid)
            processObj.setBool("canQueryProcess", pid.canQueryProcess)
            processObj.setString("path", pid.processPath)
            processObj

        diagnosticsJson.setObjectArray("processes", processObjs)
        diagnosticsJson.setObjectArray("windows", windowObjs)
 
        textBox.Text <- diagnosticsJson.ToString()

    interface ISettingsView with
        member x.key = SettingsViewType.DiagnosticsSettings
        member x.title = resources.GetString "Diagnostics"
        member x.control = panel


