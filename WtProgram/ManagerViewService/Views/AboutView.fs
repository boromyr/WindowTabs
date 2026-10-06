namespace Bemo
open System.Diagnostics
open System.Drawing
open System.Windows.Forms
open Bemo.Win32.Forms

type AboutView() =
    let openLink (url:string) =
        try Process.Start(ProcessStartInfo(url, UseShellExecute = true)).ignore
        with _ -> ()

    let linkCard title (description:string) url =
        FluentUI.settingCard title (Some description) (FluentUI.button None (FluentUI.text "Open") (fun () -> openLink url))

    let panel =
        FluentUI.page (FluentUI.text "About") [
            FluentUI.settingCard "WindowTabs" (Some(FluentUI.text "AboutDescription")) (Label())
            FluentUI.sectionHeader (FluentUI.text "AboutLinks")
            linkCard "GitHub" "github.com/boromyr/WindowTabs" "https://github.com/boromyr/WindowTabs"
            linkCard (FluentUI.text "AboutOriginal") "Maurice Flanagan - github.com/mauricef/WindowTabs" "https://github.com/mauricef/WindowTabs"
            linkCard (FluentUI.text "AboutFork") "leafOfTree - github.com/leafOfTree/WindowTabs" "https://github.com/leafOfTree/WindowTabs"
        ]

    interface ISettingsView with
        member x.key = SettingsViewType.AboutSettings
        member x.title = FluentUI.text "About"
        member x.control = panel
