namespace Bemo
open System
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32.Forms
open System.Resources
open System.Reflection

type ProgramView() as this =
    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());

    let invoker = InvokerService.invoker

    let status =
        let label = FluentUI.secondaryLabel (FluentUI.text "Ready")
        label.Anchor <- AnchorStyles.Left
        label.Margin <- Padding(Dpi.px 4, Dpi.px 8, 0, 0)
        label

    let list =
        let list = FluentUI.stack []
        list.Margin <- Padding(0, Dpi.px 12, 0, 0)
        list

    let programIcon (procPath:string) =
        let procIcon = Win32Helper.GetFileIcon(procPath)
        let icon = Ico.fromHandle(procIcon).def(SystemIcons.Application)
        try icon.ToBitmap() :> Image with _ -> SystemIcons.Application.ToBitmap() :> Image

    let programCard (procPath:string) (windowCount:int) (icon:Image) =
        let description = if windowCount = 1 then FluentUI.text "OneWindow" else String.Format(FluentUI.text "WindowCount", windowCount)
        // both check boxes in one window: window creation is the expensive part of building the list
        let checks = new FluentCheckGroup()
        checks.Font <- FluentTheme.Body
        checks.AutoSize <- true
        checks.AddItem(resources.GetString "enableTabs", Services.filter.getIsTabbingEnabledForProcess procPath)
        checks.AddItem(resources.GetString "enableAutoGrouping", Services.program.getAutoGroupingEnabled procPath)
        checks.CheckedChanged.Add <| fun index ->
            let value = checks.IsChecked(index)
            if index = 0 then Services.filter.setIsTabbingEnabledForProcess procPath value
            else Services.program.setAutoGroupingEnabled procPath value
        FluentUI.settingCardWithIcon (Some icon) (Path.GetFileName(procPath)) (Some description) checks
    let refreshButton = FluentUI.button (Some "") (resources.GetString("Refresh")) (fun () -> this.populateNodes())

    let page =
        FluentUI.page (resources.GetString "Programs") [
            FluentUI.row [refreshButton; status]
            list
        ]

    do
        this.populateNodes()
        Services.settings.notifyValue "enableTabbingByDefault" <| fun(_) ->
            this.populateNodes()

    member this.refresh() = this.populateNodes()

    member private this.populateNodes() =
        refreshButton.Enabled <- false
        status.Text <- FluentUI.text "Scanning"
        ThreadHelper.queueBackground <| fun() ->
            let os = OS()
            let procs = Services.program.appWindows.fold (Map2()) <| fun procs hwnd ->
                let window = os.windowFromHwnd(hwnd)
                let procPath = window.pid.processPath
                procs.add procPath (procs.tryFind(procPath).def(0) + 1)
            let programs = procs.items.sortBy(fun (procPath, _) -> Path.GetFileName(procPath).ToLowerInvariant())
            let programs = programs.map(fun (procPath, count) -> procPath, count, programIcon procPath)

            invoker.asyncInvoke <| fun() ->
                list.SuspendLayout()
                let old = list.Controls |> Seq.cast<Control> |> List.ofSeq
                list.Controls.Clear()
                old |> List.iter (fun control -> control.Dispose())
                programs.iter <| fun (procPath, count, icon) ->
                    list.Controls.Add(programCard procPath count icon)
                list.ResumeLayout()
                status.Text <- FluentUI.text "Ready"
                refreshButton.Enabled <- true

    interface ISettingsView with
        member x.key = SettingsViewType.ProgramSettings
        member x.title = resources.GetString "Programs"
        member x.control = page
